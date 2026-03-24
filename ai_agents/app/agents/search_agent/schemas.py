"""
Search (RAG) agent — internal I/O schemas.

Takes the extracted job requirements + the user's backend data
and returns the subset that is most relevant to the job posting.
"""
from __future__ import annotations

from typing import List
from uuid import UUID

from pydantic import BaseModel, Field

from app.models.job_model import JobRequirements
from app.models.user_model import ExperienceResponse, ProjectResponse, SkillResponse


class SearchInput(BaseModel):
    user_id: UUID
    job_requirements: JobRequirements


class SearchOutput(BaseModel):
    matched_skills: List[SkillResponse]           = Field(default_factory=list)
    matched_experiences: List[ExperienceResponse] = Field(default_factory=list)
    matched_projects: List[ProjectResponse]       = Field(default_factory=list)
    gap_skills: List[str]                         = Field(default_factory=list,
                                                          description="Required skills not found in user profile")
    match_score: float                            = Field(default=0.0, ge=0.0, le=1.0,
                                                          description="Overall profile-to-job similarity score")
