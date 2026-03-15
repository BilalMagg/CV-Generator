from fastapi import APIRouter, HTTPException
from app.models.job_model import JobRequest, JobRequirements
from app.workflows.cv_generation import run_cv_generation_workflow
from app.agents.job_extractor import extract_job_requirements

router = APIRouter(prefix="/agents", tags=["CV Generation"])

@router.post("/generate-cv")
async def generate_cv(request: JobRequest):
    """
    Triggers the AI CV generation workflow based on a user's profile and a job description.
    """
    result = run_cv_generation_workflow(request)
    if not result.get("success"):
        raise HTTPException(status_code=500, detail=result.get("message"))
    return result

@router.post("/extract-requirements", response_model=JobRequirements)
async def extract_requirements(request: JobRequest):
    """
    Extracts structured job requirements from a job description.
    """
    try:
        return extract_job_requirements(request.job_description)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
