from fastapi import APIRouter, status
from app.agent import optimize_cv
from app.schemas import OptimizerInput, OptimizerOutput

router = APIRouter()


@router.post("/optimize", response_model=OptimizerOutput, status_code=status.HTTP_200_OK)
async def optimize(input_data: OptimizerInput) -> OptimizerOutput:
    return await optimize_cv(input_data)


@router.get("/health")
async def health():
    return {"status": "ok", "service": "cv-optimizer"}