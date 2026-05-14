"""
Template agent — renders CV sections from matched data.
"""
from langchain.agents import create_agent
from cvtools.core.llm import get_llm
from app.schemas import TemplateInput, RenderedCV
from cvtools.models.cv_model import CVSection
from app.prompt import LLMPrompt
from app.tools import get_template_code, build_cv_data, parse_sections


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

    sections = parse_sections(cv_code, template_format, input_data.cv_draft.summary)

    return RenderedCV(
        cv_code=cv_code,
        template_id=input_data.template_id,
        sections=sections,
    )