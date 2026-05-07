from fastapi import APIRouter, status
from app.agent import extract_job_requirements
from app.schemas import ExtractorInput, ExtractorOutput

router = APIRouter()


@router.post("/extract", response_model=ExtractorOutput, status_code=status.HTTP_200_OK)
async def extract(input_data: ExtractorInput) -> ExtractorOutput:
    return await extract_job_requirements(input_data)


@router.get("/health")
async def health():
    return {"status": "ok", "service": "job-extractor"}