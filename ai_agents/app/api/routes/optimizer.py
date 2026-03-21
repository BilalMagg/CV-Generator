"""FastAPI router — CV Optimizer  (POST /api/v1/optimizer/optimize)"""
from fastapi import APIRouter, HTTPException

from app.api.schemas.optimizer import OptimizeRequest, OptimizeResponse

router = APIRouter(prefix="/optimizer", tags=["CV Optimizer"])


@router.post("/optimize", response_model=OptimizeResponse, summary="Optimize rendered CV for ATS")
async def optimize_cv(body: OptimizeRequest) -> OptimizeResponse:
    """
    Scores and rewrites the CV sections to maximise ATS pass rate
    against the extracted job requirements.
    """
    raise HTTPException(status_code=501, detail="CV Optimizer agent not yet implemented")
