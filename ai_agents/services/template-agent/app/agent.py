"""
Template agent — renders CV sections from matched data.
"""
import os
import uuid
from langchain.agents import create_agent
from cvtools.core.llm import get_llm
from cvtools import html_to_pdf, latex_to_pdf, upload_pdf
from app.schemas import TemplateInput, RenderedCV
from cvtools.models.cv_model import CVSection
from app.prompt import LLMPrompt
from app.tools import get_template_code, build_cv_data, parse_sections


def _store_rendered_cv(cv_code: str, template_format: str, template_id: str) -> str:
    """
    Convert the rendered CV to a PDF and persist it to MinIO object storage.

    Uses the existing cvtools converters (WeasyPrint for HTML, pdflatex for
    LaTeX) and the existing upload_pdf helper — MinIO connection details and
    the target bucket all come from environment variables.

    Returns the object URL of the stored PDF.
    """
    if template_format == "latex":
        pdf_path = latex_to_pdf(cv_code)
    else:
        pdf_path = html_to_pdf(cv_code)

    object_name = f"{template_id}-{uuid.uuid4().hex}.pdf"
    return upload_pdf(pdf_path, object_name=object_name)


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
    cv_code = last_message.content if last_message else ""

    sections = parse_sections(cv_code, template_format, input_data.cv_draft.summary)

    # Persist the rendered CV PDF to MinIO and return its object URL.
    pdf_url = _store_rendered_cv(cv_code, template_format, input_data.template_id)

    return RenderedCV(
        cv_code=cv_code,
        template_id=input_data.template_id,
        sections=sections,
        pdf_url=pdf_url,
    )