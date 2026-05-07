from fastapi import APIRouter, File, HTTPException, UploadFile, status

from app.agent import extract_job_requirements
from app.schemas import ExtractorInput, ExtractorOutput

MAX_FILE_SIZE = 10 * 1024 * 1024

router = APIRouter()


@router.post("/extract", response_model=ExtractorOutput, status_code=status.HTTP_200_OK)
async def extract(input_data: ExtractorInput) -> ExtractorOutput:
    return await extract_job_requirements(input_data)


@router.post("/extract-file", response_model=ExtractorOutput, status_code=status.HTTP_200_OK)
async def extract_file(file: UploadFile = File(...)) -> ExtractorOutput:
    content = await file.read()
    if len(content) > MAX_FILE_SIZE:
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail=f"File size exceeds {MAX_FILE_SIZE // (1024*1024)}MB limit",
        )
    input_data = ExtractorInput(
        file_content=content,
        file_name=file.filename or "unknown",
    )
    return await extract_job_requirements(input_data)


@router.get("/health")
async def health():
    return {"status": "ok", "service": "job-extractor"}
