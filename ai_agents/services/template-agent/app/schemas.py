"""
Template Agent schemas.
"""
from __future__ import annotations

from typing import List

from pydantic import BaseModel, Field

from app.models.cv_model import CVDraft, CVSection


class TemplateInput(BaseModel):
    cv_draft: CVDraft
    template_id: str = Field(default="default")
    target_role: str


class TemplateOutput(BaseModel):
    sections: List[CVSection] = Field(default_factory=list)
    template_used: str