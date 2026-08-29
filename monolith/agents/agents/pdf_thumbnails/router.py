import logging
from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import Response
from shared.tools.pdf_utils import render_first_page_thumbnail

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/agents/pdf", tags=["pdf"])


@router.post("/thumbnail")
async def pdf_thumbnail(request: Request):
    payload = await request.body()
    if not payload:
        raise HTTPException(status_code=400, detail="PDF bytes are required")
    if len(payload) > 50 * 1024 * 1024:
        raise HTTPException(status_code=413, detail="PDF too large")
    png = render_first_page_thumbnail(payload)
    if png is None:
        raise HTTPException(
            status_code=422,
            detail="Could not render thumbnail (pymupdf missing or invalid PDF)",
        )
    return Response(content=png, media_type="image/png")


@router.get("/health")
async def health():
    return {"status": "ok", "service": "pdf"}