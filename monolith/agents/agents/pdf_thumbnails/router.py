import logging
from typing import Optional
from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import Response
from pydantic import BaseModel
from shared.tools.pdf_utils import render_first_page_thumbnail, render_text_pdf

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/agents/pdf", tags=["pdf"])


class TextPdfRequest(BaseModel):
    text: str
    title: Optional[str] = None


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


@router.get("/capabilities")
async def capabilities():
    """What this sidecar can actually do — drives the frontend's graceful
    degradation (LaTeX banner, .tex-only flow) when no LaTeX engine exists."""
    from shared.tools.latex_compile import tex_tooling_available

    info = tex_tooling_available()
    text_pdf_ok = False
    try:
        from fpdf import FPDF  # noqa: F401 — fpdf2 backs render_text_pdf
        text_pdf_ok = True
    except Exception:
        text_pdf_ok = False
    return {
        "latex_engine_available": bool(info.get("tex_available")),
        "latex_engine": info.get("engine"),
        "latex_engine_reason": info.get("reason"),
        "text_pdf_available": text_pdf_ok,
    }


@router.post("/text")
async def text_pdf(request: TextPdfRequest):
    if not request.text or not request.text.strip():
        raise HTTPException(status_code=400, detail="Text is required")
    if len(request.text) > 200_000:
        raise HTTPException(status_code=413, detail="Text too large (max 200k chars)")
    pdf = render_text_pdf(request.text, request.title or "")
    if pdf is None:
        raise HTTPException(
            status_code=422,
            detail="Could not generate PDF (fpdf2 missing or invalid text)",
        )
    return Response(content=pdf, media_type="application/pdf")