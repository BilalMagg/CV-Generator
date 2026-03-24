"""
Template agent — internal I/O schemas.

Takes a CV draft (assembled by the RAG agent) and a template choice,
and renders it into ordered CV sections.
"""
from __future__ import annotations

from typing import List

from pydantic import BaseModel, Field

from app.models.cv_model import CVDraft, CVSection


class TemplateInput(BaseModel):
    cv_draft: CVDraft
    template_id: str  = Field(default="default")
    target_role: str  = Field(..., description="Job role being targeted — used to tailor section wording")


class TemplateOutput(BaseModel):
    sections: List[CVSection] = Field(default_factory=list)
    template_used: str
