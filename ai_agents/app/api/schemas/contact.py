"""HTTP-facing request/response schemas for the Contact / Delivery route."""
from __future__ import annotations

from datetime import datetime
from typing import Optional

from pydantic import BaseModel


class ContactRequest(BaseModel):
    job_id: str
    email: str
    cover_letter_hint: Optional[str] = None


class ContactResponse(BaseModel):
    success: bool
    delivery_id: str
    sent_at: datetime
    error_message: Optional[str] = None
