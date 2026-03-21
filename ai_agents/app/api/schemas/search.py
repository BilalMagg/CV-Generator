"""HTTP-facing request/response schemas for the RAG Search route."""
from __future__ import annotations

from typing import List
from uuid import UUID

from pydantic import BaseModel, Field

from app.models.user_model import ExperienceResponse, ProjectResponse, SkillResponse


class SearchRequest(BaseModel):
    user_id: UUID
    job_description: str = Field(..., min_length=50)


class SearchResponse(BaseModel):
    matched_skills: List[SkillResponse]
    matched_experiences: List[ExperienceResponse]
    matched_projects: List[ProjectResponse]
    gap_skills: List[str]
    match_score: float
