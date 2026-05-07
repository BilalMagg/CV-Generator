"""
CV pipeline output models.
"""
from __future__ import annotations

from datetime import datetime
from typing import List, Optional

from pydantic import BaseModel, Field

from cvtools.models.user_model import ExperienceResponse, ProjectResponse, SkillResponse


class CVSection(BaseModel):
    section_type: str
    content: str
    order: int


class CVDraft(BaseModel):
    target_role: str
    summary: str
    matched_skills: List[SkillResponse] = Field(default_factory=list)
    matched_experiences: List[ExperienceResponse] = Field(default_factory=list)
    matched_projects: List[ProjectResponse] = Field(default_factory=list)
    gap_skills: List[str] = Field(default_factory=list)


class OptimizedCV(BaseModel):
    job_id: str
    final_sections: List[CVSection] = Field(default_factory=list)
    ats_score_estimate: int = Field(ge=0, le=100)
    optimization_notes: List[str] = Field(default_factory=list)
    pdf_url: Optional[str] = None
    generated_at: datetime = Field(default_factory=datetime.utcnow)