import logging
from fastapi import APIRouter, HTTPException
from agents.apply_prep.schemas import (
    FormResponsesRequest,
    MessageRequest,
    FormResponsesResponse,
    MessageResponse,
)
from agents.apply_prep.agent import generate_form_responses, generate_message

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/agents/apply-prep", tags=["apply-prep"])


@router.post("/form-responses", response_model=FormResponsesResponse)
async def form_responses(request: FormResponsesRequest):
    try:
        return await generate_form_responses(request)
    except Exception as e:
        logger.exception("apply-prep: form response generation failed")
        raise HTTPException(status_code=500, detail=f"Form response generation failed: {str(e)}")


@router.post("/message", response_model=MessageResponse)
async def message(request: MessageRequest):
    try:
        return await generate_message(request)
    except Exception as e:
        logger.exception("apply-prep: message generation failed")
        raise HTTPException(status_code=500, detail=f"Message generation failed: {str(e)}")


@router.get("/health")
async def health():
    return {"status": "ok", "service": "apply-prep"}
