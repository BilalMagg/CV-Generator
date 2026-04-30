"""
Template agent — logic to create and render CV based on the sections and the data.
"""
from langchain.agents import create_agent
from app.core.llm import get_llm
from app.agents.template_agent.schemas import TemplateInput, RenderedCV
from app.models.cv_model import CVSection
from app.agents.template_agent.prompt import LLMPrompt

async def render_template(input_data: TemplateInput, prompt : LLMPrompt) -> RenderedCV:

    sections = [
        CVSection(section_type="summary", content=f"Experienced {input_data.target_role}", order=0),
        CVSection(section_type="skills", content="Python, FastAPI, Docker", order=1),
    ]
    model = get_llm()
    client_agent = create_agent(model, tools =[], system_prompt=[prompt.system_prompt_generic(input_data.template_type)])
    result = client_agent.invoke(input_data.cv_draft)

    return RenderedCV(
        cv_code = result,
        template_type=input_data.template_id,
        sections=sections,
    )
