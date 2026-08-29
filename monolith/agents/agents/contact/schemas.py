from pydantic import BaseModel
from typing import Optional, List


class ContactRequest(BaseModel):
    user_id: str
    company_name: str
    job_title: Optional[str] = None
    job_description: Optional[str] = None
    contact_type: str = "recruiter"
    language: str = "English"
    provider: Optional[str] = None
    model: Optional[str] = None


class ContactEmailResponse(BaseModel):
    subject: str
    body: str
    to_email: Optional[str] = None
    contact_type: str
    language: str


class EmailCheckRequest(BaseModel):
    email_address: str
