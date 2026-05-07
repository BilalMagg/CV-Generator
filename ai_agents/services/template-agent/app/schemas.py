"""
Template Agent schemas.
"""
from __future__ import annotations

from typing import List, Optional

from pydantic import BaseModel, Field

from cvtools.models.cv_model import CVDraft, CVSection


# Input/Output schema
class TemplateInput(BaseModel):
    cv_draft: CVDraft
    template_id: str  = Field(default="default")
    target_role: str  = Field(..., description="Job role being targeted — used to tailor section wording")

class RenderedCV(BaseModel):
    cv_code : str | bytes
    template_id: str
    sections : Optional[List[CVSection]]


# class TemplateOutput(BaseModel):
#     sections: List[CVSection] = Field(default_factory=list)
#     template_used: str