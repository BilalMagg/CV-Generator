from pydantic import BaseModel, ConfigDict
from typing import Optional
from uuid import UUID, uuid4
from datetime import datetime
from enum import Enum


class MessageRole(str, Enum):
    user = "user"
    assistant = "assistant"


class MessageModel(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID = uuid4()
    created_at: datetime = datetime.now()
    updated_at: datetime = datetime.now()
    msg_index: int = 0
    type: str = "human"
    content: str
    conversation_id: Optional[str] = None
    user_id: Optional[str] = None
    token_count: int = 0
    runnable_id: Optional[str] = None


class ConversationModel(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: str = ""
    created_at: datetime = datetime.now()
    updated_at: datetime = datetime.now()
    title: str = ""
    thread_id: Optional[str] = None


class ChatRequest(BaseModel):
    query: str
    workflow_id: str
    user_id: str
    conversation_id: Optional[str] = None
    provider: Optional[str] = None
    model: Optional[str] = None


class ChatResponse(BaseModel):
    response: str
    conversation_id: str
    messages: list[MessageModel]


class ConversationResponse(BaseModel):
    conversations: list[ConversationModel]
