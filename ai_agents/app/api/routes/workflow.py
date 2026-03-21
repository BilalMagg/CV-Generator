"""FastAPI router — Workflow Orchestrator (POST /api/v1/workflow/run)"""
from fastapi import APIRouter, HTTPException

from app.models.job_model import JobRequest
from app.workflows.cv_generation import run_cv_generation_workflow

router = APIRouter(prefix="/workflow", tags=["Orchestrator"])


@router.post("/run", summary="Trigger the full 5-step AI CV generation workflow")
async def trigger_workflow(request: JobRequest):
    """
    Orchestrates the entire process from extraction to delivery.
    """
    result = await run_cv_generation_workflow(request)
    if not result.get("success"):
        raise HTTPException(status_code=500, detail=result.get("message"))
    return result
