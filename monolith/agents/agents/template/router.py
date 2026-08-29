import logging
from pathlib import PurePosixPath

from fastapi import APIRouter, HTTPException

from agents.template.schemas import (
    LetterRequest, LetterResponse, PdfRequest, PdfResponse,
    TemplateRenderRequest, TemplateRenderResponse, CvSections,
)
from agents.template.cv_tex import build_cv_tex, load_template
from agents.template.render_cv import generate_cv_sections, generate_freeform_sections

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/agents/template", tags=["template"])


@router.post("/letter", response_model=LetterResponse)
async def generate_letter(request: LetterRequest):
    try:
        from agents.template.agent import generate_cover_letter
        letter = await generate_cover_letter(
            user_id=request.user_id,
            job_title=request.job_title,
            company_name=request.company_name,
            job_description=request.job_description,
            hiring_manager_name=request.hiring_manager_name,
            hiring_manager_email=request.hiring_manager_email,
            language=request.language,
            tone=request.tone,
        )
        return LetterResponse(
            letter=letter, letter_type=request.letter_type,
            language=request.language, tone=request.tone,
            job_title=request.job_title, company_name=request.company_name,
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Letter generation failed: {str(e)}")


@router.post("/render", response_model=TemplateRenderResponse)
async def render_cv(request: TemplateRenderRequest):
    """Build structured sections from raw inputs and fill the LaTeX template.

    Returns the .tex source (latex output format) plus the structured sections.
    """
    try:
        uid = request.user_id or (request.cv_draft or {}).get("user_id") or ""
        template_name = request.template_id or request.template_name or "default"
        sections = await generate_cv_sections(
            user_id=uid,
            cv_draft=request.cv_draft,
            target_role=request.target_role,
            language=request.language,
            tone=request.tone,
            provider=request.provider,
            model=request.model,
            template_content=request.template_content,
        )
        template_src = request.template_content or load_template(template_name)
        freeform = await generate_freeform_sections(
            template_content=request.template_content,
            cv_draft=request.cv_draft,
            target_role=request.target_role,
            provider=request.provider,
            model=request.model,
            sections=sections,
        )
        tex = build_cv_tex(template_src, sections, photo_ref=None, freeform=freeform)
        return TemplateRenderResponse(
            cv_code=tex,
            template_id=template_name,
            sections=sections,
        )
    except Exception as e:
        logger.exception("CV render failed")
        raise HTTPException(status_code=500, detail=f"CV render failed: {str(e)}")


@router.post("/pdf", response_model=PdfResponse)
async def render_cv_pdf(request: PdfRequest):
    """Compile a CV to PDF (via dockerized texlive) and store it in MinIO."""
    try:
        if request.cv_data_format in ("json", "sections"):
            sections = CvSections.model_validate_json(request.cv_data)
            photo_ref = "photo" if request.photo_key else None
            template_src = request.template_content or load_template(request.template_name)
            freeform = await generate_freeform_sections(
                template_content=request.template_content,
                cv_draft=request.cv_data,
                target_role=request.target_role if hasattr(request, "target_role") else "",
                provider=request.provider,
                model=request.model,
                sections=sections,
            )
            tex = build_cv_tex(template_src, sections, photo_ref=photo_ref, freeform=freeform)
        else:
            tex = request.cv_data

        from agents.template.cv_tex import _sanitize_unicode
        tex = _sanitize_unicode(tex)

        photo_bytes, photo_ext = None, "jpg"
        if request.photo_key:
            from shared.tools.storage import get_object, PROFILE_IMAGES_BUCKET
            photo_bytes = get_object(PROFILE_IMAGES_BUCKET, request.photo_key)
            suffix = PurePosixPath(request.photo_key).suffix
            photo_ext = suffix.lstrip(".") if suffix else "jpg"
            if photo_bytes is None:
                logger.warning("Photo %s not found; compiling without photo", request.photo_key)

        from shared.tools.latex_compile import compile_latex_to_pdf
        pdf_bytes, error = await _compile_with_timeout(tex, photo_bytes, photo_ext)
        if pdf_bytes is None:
            raise HTTPException(status_code=500, detail=f"PDF generation failed:\n{error}")

        from shared.tools.storage import upload_bytes, CV_ARTIFACTS_BUCKET
        safe_name = PurePosixPath(request.file_name).name or "cv.pdf"
        key = f"{request.user_id}/{safe_name}"
        url = upload_bytes(CV_ARTIFACTS_BUCKET, key, pdf_bytes, content_type="application/pdf")
        return PdfResponse(file_path=url, file_size=len(pdf_bytes), message="PDF generated")
    except HTTPException:
        raise
    except Exception as e:
        logger.exception("CV PDF render failed")
        raise HTTPException(status_code=500, detail=f"CV PDF failed: {str(e)}")


async def _compile_with_timeout(tex: str, photo_bytes=None, photo_ext="jpg", timeout: int = 300):
    """Thin async wrapper over the blocking docker compile."""
    import asyncio
    from shared.tools.latex_compile import compile_latex_to_pdf
    return await asyncio.to_thread(compile_latex_to_pdf, tex, photo_bytes, photo_ext, timeout)


@router.get("/health")
async def health_check():
    from shared.tools.latex_compile import tex_tooling_available
    info = tex_tooling_available()
    return {"status": "ok", "service": "template-agent", **info}