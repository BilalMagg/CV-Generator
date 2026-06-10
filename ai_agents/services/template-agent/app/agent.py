"""
Template agent — renders CV sections from matched data.
"""
import os
import uuid
from langchain.agents import create_agent
from cvtools.core.llm import get_llm
from cvtools import html_to_pdf, latex_to_pdf, upload_pdf, upload_code, extract_cv_code
from app.schemas import TemplateInput, RenderedCV
from cvtools.models.cv_model import CVSection
from app.prompt import LLMPrompt
from app.tools import get_template_code, build_cv_data, parse_sections


# Per-format file extension and MIME type for the stored CV source code.
_CODE_FORMATS = {
    "latex": (".tex", "text/x-tex"),
    "html": (".html", "text/html"),
}


def _store_rendered_cv(cv_code: str, template_format: str, template_id: str) -> tuple[str, str]:
    """
    Persist the rendered CV to MinIO as both a PDF and its source code.

    Uses the existing cvtools converters (WeasyPrint for HTML, pdflatex for
    LaTeX) plus the upload_pdf / upload_code helpers — MinIO connection details
    and the target buckets all come from environment variables. The PDF and the
    source share the same base name so the two artifacts stay correlated.

    Returns a (pdf_url, code_url) tuple of the stored object URLs.
    """
    if template_format == "latex":
        pdf_path = latex_to_pdf(cv_code)
    else:
        pdf_path = html_to_pdf(cv_code)

    base = f"{template_id}-{uuid.uuid4().hex}"
    ext, content_type = _CODE_FORMATS.get(template_format, _CODE_FORMATS["html"])

    pdf_url = upload_pdf(pdf_path, object_name=f"{base}.pdf")
    code_url = upload_code(cv_code, object_name=f"{base}{ext}", content_type=content_type)
    return pdf_url, code_url


async def render_template(input_data: TemplateInput) -> RenderedCV:
    """
    Render CV using the template from MINIO.

    Args:
        input_data: TemplateInput with cv_draft and template_id

    Returns:
        RenderedCV with cv_code, template_id, and sections
    """
    template_code, template_format = get_template_code(input_data.template_id)

    cv_data = build_cv_data(input_data)

    prompt = LLMPrompt.get_prompt(template_format)
    system_prompt = prompt.format(
        target_role=input_data.target_role,
        cv_data=cv_data,
        template_code=template_code,
    )

    provider = os.getenv("LLM_PROVIDER") or "groq"
    model_name = os.getenv("LLM_MODEL") or "llama-3.3-70b-versatile"
    model = get_llm(provider=provider, model=model_name)
    client_agent = create_agent(model, tools=[], system_prompt=system_prompt)

    model_input = {"messages": [
        {"role": "user",
         "content": "Generate a CV using the provided template structure and CV data. "
                   "Return the CV code and also include a JSON section at the end with the sections breakdown."}
    ]}
    result = client_agent.invoke(model_input)
    messages = result.get("messages", [])
    last_message = messages[-1] if messages else None
    raw_output = last_message.content if last_message else ""

    # Parse sections from the RAW output (it still has the trailing JSON block),
    # but compile/store the cleaned, compilable source only.
    sections = parse_sections(raw_output, template_format, input_data.cv_draft.summary)
    clean_code = extract_cv_code(raw_output, template_format)

    # Persist the rendered CV PDF and its source code to MinIO.
    pdf_url, code_url = _store_rendered_cv(clean_code, template_format, input_data.template_id)

    return RenderedCV(
        cv_code=clean_code,
        template_id=input_data.template_id,
        sections=sections,
        pdf_url=pdf_url,
        code_url=code_url,
    )