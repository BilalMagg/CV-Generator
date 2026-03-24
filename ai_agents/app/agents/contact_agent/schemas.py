"""
Contact / Delivery agent — internal I/O schemas.

Sends the optimised CV to the candidate's email address.
"""
from __future__ import annotations

from datetime import datetime
from typing import Optional

from pydantic import BaseModel, Field

from app.models.cv_model import OptimizedCV


class ContactInput(BaseModel):
    optimized_cv: OptimizedCV
    recipient_email: str
    cover_letter_hint: Optional[str] = Field(
        default=None,
        description="Optional user-provided direction for the cover letter tone / key points"
    )


class ContactOutput(BaseModel):
    success: bool
    delivery_id: str               = Field(..., description="Unique ID for tracking this delivery")
    sent_at: datetime              = Field(default_factory=datetime.utcnow)
    error_message: Optional[str]   = None
