"""
Template agent — renders CV sections from matched data.
"""
from app.schemas import TemplateInput, TemplateOutput
from app.models.cv_model import CVSection


async def render_template(input_data: TemplateInput) -> TemplateOutput:
    """
    TODO: Implement markdown rendering logic.
    For now, returns stub sections.
    """
    sections = [
        CVSection(section_type="summary", content=f"Experienced {input_data.target_role}", order=0),
        CVSection(section_type="skills", content="Python, FastAPI, Docker", order=1),
    ]
    return TemplateOutput(
        sections=sections,
        template_used=input_data.template_id
    )