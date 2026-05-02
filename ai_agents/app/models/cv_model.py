"""
CV pipeline output models — produced by the AI agents, not stored in the backend DB.

CVSection    : one rendered section of the final CV.
CVDraft      : assembled draft (RAG search output → template input).
OptimizedCV  : final polished CV ready for delivery.
"""
from __future__ import annotations

from datetime import datetime
from typing import List, Optional

from pydantic import BaseModel, Field

from app.models.user_model import ExperienceResponse, ProjectResponse, SkillResponse


class CVSection(BaseModel):
    """One block of the rendered CV (e.g. 'summary', 'experience', 'skills')."""
    section_type: str   = Field(..., description="e.g. 'summary' | 'experience' | 'projects' | 'skills'")
    content: str        = Field(..., description="Rendered markdown / text content for this section")
    order: int          = Field(..., description="Display order, 0-indexed")


class CVDraft(BaseModel):
    """
    Assembled CV draft — carries references to the backend data objects
    chosen by the RAG search agent.
    """
    target_role: str
    summary: str
    matched_skills: List[SkillResponse]           = Field(default_factory=list)
    matched_experiences: List[ExperienceResponse] = Field(default_factory=list)
    matched_projects: List[ProjectResponse]       = Field(default_factory=list)
    gap_skills: List[str]                         = Field(default_factory=list,
                                                          description="Skills the user lacks but the job requires, it's gonna be shown in a seperate pop up")


# class OptimizedCV(BaseModel):
#     """Final CV — produced by the optimizer agent."""
#     job_id: str
#     final_sections: List[CVSection]         = Field(default_factory=list)
#     ats_score_estimate: int                 = Field(ge=0, le=100, description="Estimated ATS pass rate 0-100")
#     optimization_notes: List[str]           = Field(default_factory=list)
#     pdf_url: Optional[str]                  = None
#     generated_at: datetime                  = Field(default_factory=datetime.utcnow)
