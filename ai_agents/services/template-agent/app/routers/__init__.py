from fastapi import APIRouter, HTTPException, status
from fastapi.responses import Response
from app.agent import render_template
from app.schemas import TemplateInput, RenderedCV
from app.tools import list_templates, get_template_preview

router = APIRouter()

@router.post("/render", response_model=RenderedCV, status_code=status.HTTP_200_OK)
async def render(input_data: TemplateInput) -> RenderedCV:
    return await render_template(input_data)

@router.get("/templates")
async def get_templates():
    return list_templates()

@router.get("/templates/{template_id}/preview")
async def get_preview(template_id: str):
    try:
        data, content_type = get_template_preview(template_id)
        return Response(content=data, media_type=content_type)
    except Exception:
        raise HTTPException(status_code=404, detail="Preview not found")

@router.get("/health")
async def health():
    return {"status": "ok", "service": "template-agent"}