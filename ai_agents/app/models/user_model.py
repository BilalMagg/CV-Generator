"""
Domain models that mirror the ASP.NET backend DTOs exactly.

Field names use snake_case with aliases for the camelCase JSON the backend returns.
The `model_config` with `populate_by_name=True` lets us use either form internally.
"""
from __future__ import annotations

from datetime import datetime
from typing import List, Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict


# ---------------------------------------------------------------------------
# Shared config for all backend-mirror models
# ---------------------------------------------------------------------------
class _BackendModel(BaseModel):
    model_config = ConfigDict(populate_by_name=True, from_attributes=True)


# ---------------------------------------------------------------------------
# User
# ---------------------------------------------------------------------------
class UserResponse(_BackendModel):
    id: UUID
    first_name: str
    last_name: str
    email: str
    role: str
    is_active: bool
    created_at: datetime

    @property
    def full_name(self) -> str:
        return f"{self.first_name} {self.last_name}"


# ---------------------------------------------------------------------------
# Experience  (mirrors ExperienceResponseDto)
# ---------------------------------------------------------------------------
class ExperienceResponse(_BackendModel):
    id: UUID
    title: str
    company: Optional[str] = None
    description: Optional[str] = None
    start_date: datetime
    end_date: Optional[datetime] = None
    reference_url: Optional[str] = None
    status: str                        # "Ongoing" | "Completed"
    user_id: UUID
    ai_summary_json: Optional[str] = None  # JSON blob; parse lazily if needed
    description_embedding: Optional[List[float]] = None  # 384-dim, from pgvector


# ---------------------------------------------------------------------------
# Project  (mirrors ProjectResponseDto)
# ---------------------------------------------------------------------------
class ProjectResponse(_BackendModel):
    id: UUID
    title: str
    description: Optional[str] = None
    role: Optional[str] = None
    achievements: Optional[str] = None
    start_date: datetime
    end_date: Optional[datetime] = None
    repository_url: Optional[str] = None
    demo_url: Optional[str] = None
    status: str                        # "Ongoing" | "Completed" | "Paused"
    user_id: UUID
    skills_json: Optional[str] = None         # JSON list of skill names; parse lazily
    ai_summary_json: Optional[str] = None
    description_embedding: Optional[List[float]] = None  # 384-dim, from pgvector


# ---------------------------------------------------------------------------
# Skill  (mirrors SkillResponseDto)
# ---------------------------------------------------------------------------
class SkillResponse(_BackendModel):
    id: UUID
    name: str
    level: Optional[str] = None        # "Beginner" | "Intermediate" | "Advanced" | "Expert"
    category: Optional[str] = None     # "Technical" | "Soft" | "Language" | …
    years_of_experience: Optional[int] = None
    user_id: UUID
    name_embedding: Optional[List[float]] = None  # 384-dim, from pgvector
