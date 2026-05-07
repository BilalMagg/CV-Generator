"""
Template agent — renders CV sections from matched data.
"""
import json
import re
from langchain.agents import create_agent
from cvtools.core.llm import get_llm
from app.schemas import TemplateInput, RenderedCV
from cvtools.models.cv_model import CVSection
from app.prompt import LLMPrompt
from app.tools import get_template_code


def _build_cv_data(input_data: TemplateInput) -> str:
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


def _parse_sections(cv_output: str, template_format: str, fallback_summary: str = "") -> list[CVSection]:
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


async def render_template(input_data: TemplateInput) -> RenderedCV:
    """
    Render CV using the template from MINIO.

    Args:
        input_data: TemplateInput with cv_draft and template_id

    Returns:
        RenderedCV with cv_code, template_id, and sections
    """
    template_code, template_format = get_template_code(input_data.template_id)

    cv_data = _build_cv_data(input_data)

    prompt = LLMPrompt.get_prompt(template_format)
    system_prompt = prompt.format(
        target_role=input_data.target_role,
        cv_data=cv_data,
        template_code=template_code,
    )

    model = get_llm()
    client_agent = create_agent(model, tools=[], system_prompt=system_prompt)

    model_input = {"messages": [
        {"role": "user",
         "content": "Generate a CV using the provided template structure and CV data. "
                   "Return the CV code and also include a JSON section at the end with the sections breakdown."}
    ]}
    result = client_agent.invoke(model_input)
    messages = result.get("messages", [])
    last_message = messages[-1] if messages else None
    cv_code = last_message.content if last_message else ""

    sections = _parse_sections(cv_code, template_format, input_data.cv_draft.summary)

    return RenderedCV(
        cv_code=cv_code,
        template_id=input_data.template_id,
        sections=sections,
    )