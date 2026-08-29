from typing import Optional, List
from pydantic import BaseModel


class ApplyPrepBase(BaseModel):
    user_id: str
    job_description: str = ""
    job_role: str = ""
    company_name: str = ""
    required_skills: List[str] = []
    responsibilities: List[str] = []
    language: str = "English"
    provider: Optional[str] = None
    model: Optional[str] = None


class FormResponsesRequest(ApplyPrepBase):
    fields: List[str] = []


class MessageRequest(ApplyPrepBase):
    channel: str = "LinkedIn"
    considerations: str = ""


class FormResponseItem(BaseModel):
    field: str
    answer: str


class FormResponsesResponse(BaseModel):
    responses: List[FormResponseItem]


class MessageResponse(BaseModel):
    message: str
