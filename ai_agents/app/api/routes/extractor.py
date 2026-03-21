"""FastAPI router — Job Extractor  (POST /api/v1/extractor/extract)"""
from fastapi import APIRouter, HTTPException

from app.api.schemas.extractor import ExtractRequest, ExtractResponse

router = APIRouter(prefix="/extractor", tags=["Job Extractor"])


@router.post("/extract", response_model=ExtractResponse, summary="Extract structured requirements from a job description")
async def extract_job_requirements(body: ExtractRequest) -> ExtractResponse:
    """
    Calls the LLM extraction agent to parse a raw job posting into
    structured requirements (skills, keywords, seniority, etc.).
    """
    raise HTTPException(status_code=501, detail="Job Extractor agent not yet implemented")
