"""
Contact Agent schemas.
"""
from __future__ import annotations

from datetime import datetime
from typing import Optional

from pydantic import BaseModel, Field

from app.models.cv_model import OptimizedCV


class ContactInput(BaseModel):
    optimized_cv: OptimizedCV
    recipient_email: str
    cover_letter_hint: Optional[str] = None


class ContactOutput(BaseModel):
    success: bool
    delivery_id: str
    sent_at: datetime = Field(default_factory=datetime.utcnow)
    error_message: Optional[str] = None