"""
BIME router — chat endpoint + conversation history proxy.
History storage lives in the .NET backend; this router just calls it.
"""
import logging
import httpx
from fastapi import APIRouter, HTTPException, Header
from typing import Optional
from shared.config import settings
from agents.bime.schemas import ChatRequest, ChatResponse

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/bime", tags=["bime"])


def _backend(user_id: str) -> httpx.AsyncClient:
    return httpx.AsyncClient(
        base_url=settings.BACKEND_BASE_URL,
        timeout=20.0,
        headers={"X-User-Id": user_id, "Content-Type": "application/json"},
    )


@router.post("/chat", response_model=ChatResponse)
async def chat(request: ChatRequest, x_user_id: Optional[str] = Header(None)):
    user_id = x_user_id
    if not user_id:
        raise HTTPException(status_code=401, detail="X-User-Id header required")

    conversation_id = request.conversation_id

    # Fetch history from .NET backend
    history = []
    if conversation_id:
        async with _backend(user_id) as client:
            resp = await client.get(f"/api/bime/conversations/{conversation_id}/messages")
            if resp.status_code == 200:
                history = resp.json().get("data", [])

    from agents.bime.agent import chat as bime_chat
    result = await bime_chat(
        message=request.message,
        user_id=user_id,
        conversation_id=conversation_id,
        history=history,
        provider=request.provider,
        model=request.model,
    )

    # Save user message + assistant reply to .NET backend
    async with _backend(user_id) as client:
        if not conversation_id:
            # Create conversation
            resp = await client.post("/api/bime/conversations", json={"title": request.message[:60]})
            if resp.status_code in (200, 201):
                conversation_id = resp.json().get("data", {}).get("id", "")
                result["conversation_id"] = conversation_id

        if conversation_id:
            await client.post(f"/api/bime/conversations/{conversation_id}/messages", json={
                "messages": [
                    {"role": "user", "content": request.message},
                    {"role": "assistant", "content": result["reply"]},
                ]
            })

    return ChatResponse(reply=result["reply"], conversation_id=conversation_id or None)


@router.get("/health")
async def health_check():
    return {"status": "ok", "service": "bime"}
