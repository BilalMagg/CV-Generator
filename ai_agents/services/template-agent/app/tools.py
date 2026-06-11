"""
Template Agent tools.
"""
import json
import re
from cvtools import get_template_object, TEMPLATES_BUCKET
from cvtools.models.cv_model import CVSection
from app.schemas import TemplateInput

_PREVIEW_IMAGE_EXTS = frozenset({".png", ".jpg", ".jpeg", ".webp", ".gif"})


def _find_preview_key(template_id: str, all_keys: frozenset) -> str | None:
    """
    Return the MinIO key for this template's preview image, or None.
    Searches the pre-fetched all_keys set — no extra stat_object calls.
    Tolerates any naming the user chose when uploading to MinIO, as long
    as the key starts with the template ID and has an image extension.
    """
    tid_lower = template_id.lower()
    exact = [
        f"{tid_lower}.preview",
        f"{tid_lower}.preview.png",
        f"{tid_lower}.preview.jpg",
        f"{tid_lower}.preview.jpeg",
        f"{tid_lower}.preview.webp",
    ]
    keys_lower = {k.lower(): k for k in all_keys}
    for candidate in exact:
        if candidate in keys_lower:
            return keys_lower[candidate]
    # Fallback: any image object whose name starts with the template id
    for k_lower, k_orig in sorted(keys_lower.items()):
        if k_lower.startswith(tid_lower) and any(k_lower.endswith(ext) for ext in _PREVIEW_IMAGE_EXTS):
            return k_orig
    return None


def _is_template_object(name: str) -> bool:
    """Return True if the MinIO object is a template (JSON), not a preview image."""
    lower = name.lower()
    if lower.endswith(".preview"):
        return False
    for ext in _PREVIEW_IMAGE_EXTS:
        if lower.endswith(ext):
            return False
    return True

#tools for data transformation
def build_cv_data(input_data: TemplateInput) -> str:
    """Build CV data string from cv_draft. because LLMs understand NL better
    so passing this will make the output more accurate than using pydantic 
    object
    """
    return (
        f"Target Role: {input_data.cv_draft.target_role}\n\n"
        f"Summary: {input_data.cv_draft.summary}\n\n"
        f"--- Experience ---\n"
        + "\n".join(
            f"- {exp.title} at {exp.company} ({exp.start_date.strftime('%Y-%m')}"
            + (f" to {exp.end_date.strftime('%Y-%m')}" if exp.end_date else " to Present")
            + f")\n  Description: {exp.description or 'N/A'}"
            for exp in input_data.cv_draft.matched_experiences
        )
        + "\n\n--- Projects ---\n"
        + "\n".join(
            f"- {proj.title} ({proj.status})"
            + (f" | Repo: {proj.repository_url}" if proj.repository_url else "")
            + (f" | Demo: {proj.demo_url}" if proj.demo_url else "")
            + f"\n  Description: {proj.description or 'N/A'}"
            + (f"\n  Achievements: {proj.achievements}" if proj.achievements else "")
            for proj in input_data.cv_draft.matched_projects
        )
        + "\n\n--- Skills ---\n"
        + "\n".join(
            f"- {skill.name} ({skill.level or 'N/A'})"
            + (f" | {skill.years_of_experience}y exp" if skill.years_of_experience else "")
            for skill in input_data.cv_draft.matched_skills
        )
        + "\n\n--- Gap Skills (missing but recommended) ---\n"
        + ", ".join(input_data.cv_draft.gap_skills)
    )


def parse_sections(cv_output: str, template_format: str, fallback_summary: str = "") -> list[CVSection]:
    """
    Parse CV sections from LLM output.
    Looks for JSON in the output or extracts sections based on format.
    """
    sections = []

    json_match = re.search(r'```json\s*(\{[\s\S]*?\})\s*```', cv_output)
    if json_match:
        try:
            data = json.loads(json_match.group(1))
            for idx, (section_type, content) in enumerate(data.get("sections", {}).items()):
                sections.append(CVSection(
                    section_type=section_type,
                    content=content,
                    order=idx
                ))
            return sections
        except json.JSONDecodeError:
            pass

    if template_format == "latex":
        section_patterns = [
            (r'\\section\{Summary\}([\s\S]*?)(?=\\section|$)', "summary"),
            (r'\\section\{Experience\}([\s\S]*?)(?=\\section|$)', "experience"),
            (r'\\section\{Skills\}([\s\S]*?)(?=\\section|$)', "skills"),
            (r'\\section\{Education\}([\s\S]*?)(?=\\section|$)', "education"),
            (r'\\section\{Projects\}([\s\S]*?)(?=\\section|$)', "projects"),
        ]
    else:
        section_patterns = [
            (r'<h2[^>]*>Summary</h2>\s*<p>([\s\S]*?)</p>', "summary"),
            (r'<h2[^>]*>Experience</h2>\s*([\s\S]*?)(?=<h2|$)', "experience"),
            (r'<h2[^>]*>Skills</h2>\s*([\s\S]*?)(?=<h2|$)', "skills"),
            (r'<h2[^>]*>Education</h2>\s*([\s\S]*?)(?=<h2|$)', "education"),
            (r'<h2[^>]*>Projects</h2>\s*([\s\S]*?)(?=<h2|$)', "projects"),
        ]

    for pattern, section_type in section_patterns:
        match = re.search(pattern, cv_output, re.IGNORECASE)
        if match:
            content = match.group(1).strip()
            if content:
                sections.append(CVSection(
                    section_type=section_type,
                    content=content[:500],
                    order=len(sections)
                ))

    if not sections:
        sections.append(CVSection(
            section_type="summary",
            content=fallback_summary[:500],
            order=0
        ))

    return sections



# tools for MinIO communication
def get_template(template_id: str) -> dict:
    """
    Fetch template JSON from MINIO cv-templates bucket.

    Returns:
        dict with keys: id, type, latex_code, html_code
    """
    return get_template_object(template_id, TEMPLATES_BUCKET)


def get_template_type(template_id: str) -> str:
    """
    Get template format type (latex or html).

    Args:
        template_id: The template ID to look up.

    Returns:
        "latex" or "html"
    """
    template = get_template(template_id)
    return template.get("type", "latex") #defualt is latex


def get_template_code(template_id: str) -> tuple[str, str]:
    """
    Get template code and format type.

    Args:
        template_id: The template ID to look up.

    Returns:
        Tuple of (template_code, format_type)
    """
    template = get_template(template_id)
    template_type = template.get("type", "latex")

    if template_type == "latex":
        return template.get("latex_code", ""), "latex"
    elif template_type == "html":
        return template.get("html_code", ""), "html"
    else:
        return template.get("latex_code", ""), "latex"


def list_templates() -> list[dict]:
    """List all templates available in the cv-templates MinIO bucket."""
    from cvtools.core.tools.minio_storage import get_minio_client
    try:
        client = get_minio_client()
        all_objects = list(client.list_objects(TEMPLATES_BUCKET))
        all_keys = frozenset(obj.object_name for obj in all_objects)
        template_ids = [k for k in all_keys if _is_template_object(k)]
        result = []
        for tid in template_ids:
            try:
                tpl = get_template(tid)
                ttype = tpl.get("type", "latex")
            except Exception:
                ttype = "latex"
            name = tid.replace("-", " ").replace("_", " ").title()
            preview_key = _find_preview_key(tid, all_keys)
            preview_url = f"/api/workflows/template/templates/{tid}/preview" if preview_key else None
            result.append({"id": tid, "name": name, "type": ttype, "preview_url": preview_url})
        return result
    except Exception:
        return []


def get_template_preview(template_id: str) -> tuple[bytes, str]:
    """Fetch preview image bytes and content-type from MinIO."""
    from cvtools.core.tools.minio_storage import get_minio_client
    client = get_minio_client()
    all_keys = frozenset(obj.object_name for obj in client.list_objects(TEMPLATES_BUCKET))
    preview_key = _find_preview_key(template_id, all_keys)
    if not preview_key:
        raise FileNotFoundError(f"No preview found for template {template_id!r}")
    response = client.get_object(TEMPLATES_BUCKET, preview_key)
    data = response.read()
    response.close()
    response.release_conn()
    content_type = "image/png" if data[:4] == b'\x89PNG' else "image/jpeg"
    return data, content_type


def seed_previews_from_dir(templates_dir, overwrite: bool = False) -> list[str]:
    """Upload *.preview.png / *.preview.jpg files from templates_dir to MinIO as {stem}.preview objects."""
    import io
    import pathlib
    from cvtools.core.tools.minio_storage import get_minio_client, ensure_bucket
    from minio.error import S3Error
    _PREVIEW_EXTS = {".png", ".jpg", ".jpeg", ".webp"}
    client = get_minio_client()
    ensure_bucket(client, TEMPLATES_BUCKET)
    seeded: list[str] = []
    for path in sorted(pathlib.Path(templates_dir).glob("*")):
        if path.suffix.lower() not in _PREVIEW_EXTS:
            continue
        stem = path.stem  # e.g. "html_basic.preview"
        if not stem.endswith(".preview"):
            continue
        object_key = stem  # MinIO key: "html_basic.preview"
        if not overwrite:
            try:
                client.stat_object(TEMPLATES_BUCKET, object_key)
                continue
            except S3Error:
                pass
        data = path.read_bytes()
        content_type = "image/png" if path.suffix.lower() == ".png" else "image/jpeg"
        client.put_object(
            TEMPLATES_BUCKET, object_key,
            io.BytesIO(data), length=len(data), content_type=content_type,
        )
        seeded.append(object_key)
    return seeded


# i might add more tools that will be linked to the agent directly
# like picking the best template based on the industry