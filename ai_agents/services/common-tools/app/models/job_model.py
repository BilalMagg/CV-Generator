"""
Job-related models — AI pipeline side only.
"""
from __future__ import annotations

from enum import Enum
from typing import List, Optional
from uuid import UUID

from pydantic import BaseModel, Field


class JobStatus(str, Enum):
    PENDING = "pending"
    PROCESSING = "processing"
    DONE = "done"
    FAILED = "failed"


class JobRequest(BaseModel):
    user_id: UUID
    job_description: str
    template_id: str = "default"
    workflow_id: Optional[UUID] = None


class JobRequirements(BaseModel):
    job_role: str
    extracted_skills: List[str] = Field(default_factory=list)
    required_experience_years: Optional[int] = None
    keywords: List[str] = Field(default_factory=list)
    seniority_level: Optional[str] = None
    employment_type: Optional[str] = None
    location_type: Optional[str] = None
    responsibilities: List[str] = Field(default_factory=list)
    certifications: List[str] = Field(default_factory=list)
    languages: List[str] = Field(default_factory=list)
    confidence_score: float = Field(default=0.0, ge=0.0, le=1.0)