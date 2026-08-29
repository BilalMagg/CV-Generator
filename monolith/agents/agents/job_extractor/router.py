import logging
from fastapi import APIRouter, HTTPException
from agents.job_extractor.schemas import JobRequest, JobExtractionResult
from agents.job_extractor.agent import extract_text_from_url, extract_job_from_text

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/agents/extract", tags=["extract"])


@router.post("/job", response_model=JobExtractionResult)
async def extract_job(request: JobRequest):
    try:
        if request.job_description:
            text = request.job_description
        elif request.url:
            text = extract_text_from_url(request.url)
        elif request.job_offer_id:
            raise HTTPException(
                status_code=422,
                detail="job_offer_id extraction is not supported by the sidecar",
            )
        else:
            raise HTTPException(
                status_code=422,
                detail="Provide job_description, url, or job_offer_id",
            )

        if not text or not text.strip():
            raise HTTPException(status_code=422, detail="Empty job description")

        result = await extract_job_from_text(text, request.language, provider=request.provider, model=request.model)
        result.raw_description = text
        result.source_url = request.url or ""
        return result
    except HTTPException:
        raise
    except Exception as e:
        logger.exception("Job extraction failed")
        raise HTTPException(status_code=500, detail=f"Extraction failed: {str(e)}")


@router.get("/health")
async def health_check():
    return {"status": "ok", "service": "job-extractor"}