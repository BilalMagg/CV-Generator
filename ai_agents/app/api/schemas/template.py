"""HTTP-facing request/response schemas for the Template route."""
from __future__ import annotations

from typing import List, Optional

from pydantic import BaseModel, Field

from app.models.cv_model import CVDraft, CVSection


class TemplateRequest(BaseModel):
    cv_draft: CVDraft
    template_id: Optional[str] = Field(default="default")


class TemplateResponse(BaseModel):
    sections: List[CVSection]
    template_used: str
