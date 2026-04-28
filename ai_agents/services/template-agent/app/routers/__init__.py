from fastapi import APIRouter, status
from app.agent import render_template
from app.schemas import TemplateInput, TemplateOutput

router = APIRouter()


@router.post("/render", response_model=TemplateOutput, status_code=status.HTTP_200_OK)
async def render(input_data: TemplateInput) -> TemplateOutput:
    return await render_template(input_data)


@router.get("/health")
async def health():
    return {"status": "ok", "service": "template-agent"}