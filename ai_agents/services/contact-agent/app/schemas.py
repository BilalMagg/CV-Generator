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

    # ── From CV Optimizer (step 4 of pipeline)
    optimized_cv: OptimizedCV

    # ── From Job Extractor (step 1 of pipeline)
    # Needed by the LLM to generate a tailored subject + body
    job_title: str       = Field(..., description="e.g. 'Data Engineer'")
    company_name: str    = Field(..., description="e.g. 'Acme Corp'")
    job_description: str = Field(..., description="Full job description text")

    recipient_email: str


    cover_letter_hint: Optional[str] = Field(
        default=None,
        description="Optional user-provided direction for the cover letter tone / key points"
    )


class ContactOutput(BaseModel):
    success: bool
    delivery_id: str               = Field(..., description="Unique ID for tracking this delivery")
    sent_at: datetime              = Field(default_factory=datetime.utcnow)
    subject_used: str            = Field(..., description="The generated subject line that was sent")
    error_message: Optional[str]   = None