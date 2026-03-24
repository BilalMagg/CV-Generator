"""FastAPI router — Template Agent  (POST /api/v1/template/render)"""
from fastapi import APIRouter, HTTPException

from app.api.schemas.template import TemplateRequest, TemplateResponse

router = APIRouter(prefix="/template", tags=["CV Template"])


@router.post("/render", response_model=TemplateResponse, summary="Render a CV draft into structured sections")
async def render_template(body: TemplateRequest) -> TemplateResponse:
    """
    Takes a CV draft and template ID, returns ordered CV sections
    ready for the optimizer.
    """
    raise HTTPException(status_code=501, detail="Template agent not yet implemented")
