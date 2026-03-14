from fastapi import APIRouter, HTTPException
from app.models.job_model import JobRequest
from app.workflows.cv_generation import run_cv_generation_workflow

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
