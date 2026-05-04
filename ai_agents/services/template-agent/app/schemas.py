"""
Template Agent schemas.
"""
from __future__ import annotations

from typing import List, Optional

from pydantic import BaseModel, Field

from app import CVDraft, CVSection

# Input/Output schema
class TemplateInput(BaseModel):
    cv_draft: CVDraft
    template_id: str  = Field(default="default")
    template_type: str  = Field(..., description= "latex | html | pdf")
    target_role: str  = Field(..., description="Job role being targeted — used to tailor section wording")

class RenderedCV(BaseModel):
    cv_code : str | bytes #works for latex, html string and pdf bytes
    template_type: str
    sections : Optional[List[CVSection]] #just in case the optimize needed each section to analyse it better


# class TemplateOutput(BaseModel):
#     sections: List[CVSection] = Field(default_factory=list)
#     template_used: str