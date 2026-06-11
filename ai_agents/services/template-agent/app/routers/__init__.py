from fastapi import APIRouter, status
from app.agent import render_template
from app.schemas import TemplateInput, RenderedCV
from app.tools import list_templates

router = APIRouter()

@router.post("/render", response_model=RenderedCV, status_code=status.HTTP_200_OK)
async def render(input_data: TemplateInput) -> RenderedCV:
    return await render_template(input_data)

@router.get("/templates")
async def get_templates():
    return list_templates()

@router.get("/health")
async def health():
    return {"status": "ok", "service": "template-agent"}