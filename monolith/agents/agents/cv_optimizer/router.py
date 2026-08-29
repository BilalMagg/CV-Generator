import logging
from fastapi import APIRouter, HTTPException
from agents.cv_optimizer.schemas import ChatRequest, ChatResponse, ConversationResponse
from agents.cv_optimizer.agent import agent
from agents.cv_optimizer.db import list_conversations

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/agents/cv-optimizer", tags=["cv-optimizer"])


@router.post("/chat", response_model=ChatResponse)
async def chat(request: ChatRequest):
    try:
        result = await agent.chat(
            user_id=request.user_id, query=request.query,
            conversation_id=request.conversation_id, workflow_id=request.workflow_id,
            provider=request.provider, model=request.model,
        )
        return ChatResponse(**result)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Chat failed: {str(e)}")


@router.get("/conversations/{user_id}", response_model=ConversationResponse)
async def get_conversations(user_id: str):
    try:
        conversations = list_conversations(user_id)
        return ConversationResponse(conversations=conversations)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to fetch conversations: {str(e)}")


@router.get("/health")
async def health_check():
    return {"status": "ok", "service": "cv-optimizer"}
