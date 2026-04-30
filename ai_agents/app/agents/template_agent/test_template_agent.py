import asyncio
from app.agents.template_agent.schemas import TemplateInput,RenderedCV
from app.agents.template_agent.agent import render_template
from app.agents.template_agent.prompt import LLMPrompt
from app.models.cv_model import CVDraft
from app.models.user_model import ExperienceResponse,ProjectResponse, SkillResponse
from datetime import datetime
from uuid import uuid4

async def test():
  # Minimal test data
  draft = CVDraft(
    target_role="Senior Python Developer",
    summary="5+ years of experience building scalable APIs",
    matched_skills=[],
    matched_experiences=[],
    matched_projects=[],
    gap_skills=[]
    )

  input_data = TemplateInput(
    cv_draft=draft,
    template_id="latex_cv",
    template_type="latex",
    target_role="Senior Python Developer"
  )

  result = await render_template(input_data, LLMPrompt)
  print(result)

  
asyncio.run(test())