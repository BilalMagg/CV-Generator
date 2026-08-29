from pydantic import BaseModel, ConfigDict
from typing import Optional, List
from uuid import UUID


class WorkflowResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    name: str
    description: Optional[str] = None
    status: str
    cvData: Optional[str] = None
    targetJobDescription: Optional[str] = None
    aiSuggestions: Optional[str] = None
    userId: UUID
