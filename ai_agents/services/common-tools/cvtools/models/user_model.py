"""
Domain models that mirror the ASP.NET backend DTOs exactly.
"""
from __future__ import annotations

from datetime import datetime
from typing import List, Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict


class _BackendModel(BaseModel):
    model_config = ConfigDict(populate_by_name=True, from_attributes=True)


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


class ExperienceResponse(_BackendModel):
    id: UUID
    title: str
    company: Optional[str] = None
    description: Optional[str] = None
    start_date: datetime
    end_date: Optional[datetime] = None
    reference_url: Optional[str] = None
    status: str
    user_id: UUID
    ai_summary_json: Optional[str] = None
    description_embedding: Optional[List[float]] = None


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
    status: str
    user_id: UUID
    skills_json: Optional[str] = None
    ai_summary_json: Optional[str] = None
    description_embedding: Optional[List[float]] = None


class SkillResponse(_BackendModel):
    id: UUID
    name: str
    level: Optional[str] = None
    category: Optional[str] = None
    years_of_experience: Optional[int] = None
    user_id: UUID
    name_embedding: Optional[List[float]] = None