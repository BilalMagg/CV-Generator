"""FastAPI router — Workflow Orchestrator (POST /api/v1/workflow/run)"""
from fastapi import APIRouter, HTTPException

from app.models.job_model import JobRequest
from app.workflows.cv_generation import run_cv_generation_workflow
from app.workflows.dynamic_orchestrator import run_dynamic_workflow

router = APIRouter(prefix="/workflow", tags=["Orchestrator"])


@router.post("/run", summary="Trigger a workflow (static or dynamic)")
async def trigger_workflow(request: JobRequest):
    """
    Orchestrates the process. If workflow_id is provided, executes
    a dynamic node-based flow from the backend. Otherwise, runs the 
    standard 5-step legacy flow.
    """
    if request.workflow_id:
        # Dynamic execution
        initial_data = {"job_description": request.job_description}
        context = await run_dynamic_workflow(request.workflow_id, request.user_id, initial_data)
        
        # Check if there were errors logged
        # (Simple heuristic for now - a real system might check context.success)
        return {
            "success": True,
            "log": context.execution_log,
            "ats_score": context.optimized_cv.ats_score_estimate if context.optimized_cv else None
        }
    else:
        # Standard static execution
        result = await run_cv_generation_workflow(request)
        if not result.get("success"):
            raise HTTPException(status_code=500, detail=result.get("message"))
        return result
