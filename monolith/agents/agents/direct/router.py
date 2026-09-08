import logging

from fastapi import APIRouter, HTTPException

from agents.direct.schemas import (
    DirectChatRequest,
    DirectChatResponse,
    DirectMessageRequest,
    DirectMessageResponse,
    LinkedInRequest,
    LinkedInResponse,
)
from agents.direct.agent import generate_message, chat, generate_linkedin

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/direct", tags=["direct"])


@router.post("/message", response_model=DirectMessageResponse)
async def direct_message(request: DirectMessageRequest):
    try:
        return await generate_message(request)
    except Exception as e:  # noqa: BLE001
        logger.exception("direct: message generation failed")
        raise HTTPException(status_code=500, detail=f"Message generation failed: {str(e)}")


@router.post("/chat", response_model=DirectChatResponse)
async def direct_chat(request: DirectChatRequest):
    try:
        return await chat(request)
    except Exception as e:  # noqa: BLE001
        logger.exception("direct: chat failed")
        raise HTTPException(status_code=500, detail=f"Chat failed: {str(e)}")


@router.post("/linkedin", response_model=LinkedInResponse)
async def direct_linkedin(request: LinkedInRequest):
    try:
        return await generate_linkedin(request)
    except Exception as e:  # noqa: BLE001
        logger.exception("direct: linkedin content generation failed")
        raise HTTPException(status_code=500, detail=f"LinkedIn content generation failed: {str(e)}")


@router.get("/health")
async def health():
    return {"status": "ok", "service": "direct"}
