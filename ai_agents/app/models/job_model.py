"""
Job-related models — AI pipeline side only (no backend equivalent).

JobRequest  : trigger payload sent by the frontend to the Python API.
JobRequirements : structured output from the LLM extraction agent.
JobStatus   : lifecycle of an AI job run.
"""
from __future__ import annotations

from enum import Enum
from typing import List, Optional
from uuid import UUID

from pydantic import BaseModel, Field


class JobStatus(str, Enum):
    PENDING    = "pending"
    PROCESSING = "processing"
    DONE       = "done"
    FAILED     = "failed"


class JobRequest(BaseModel):
    """Trigger payload — sent by the frontend to kick off the full CV workflow."""
    user_id: UUID           = Field(..., description="Backend user ID")
    job_description: str    = Field(..., description="Raw job posting text")
    template_id: str        = Field(default="default", description="CV template to use")
    workflow_id: Optional[UUID] = Field(default=None, description="If provided, executes a dynamic workflow from the backend")


class JobRequirements(BaseModel):
    """Structured output produced by the LLM extraction agent."""
    job_role: str                           = Field(..., description="Normalised job title / role")
    extracted_skills: List[str]             = Field(default_factory=list)
    required_experience_years: Optional[int] = None
    keywords: List[str]                     = Field(default_factory=list, description="ATS keywords")
    seniority_level: Optional[str]          = None   # "Junior" | "Mid" | "Senior" | "Lead"
    employment_type: Optional[str]          = None   # "Full-time" | "Part-time" | "Contract"
    location_type: Optional[str]            = None   # "Remote" | "Hybrid" | "On-site"
    responsibilities: List[str]             = Field(default_factory=list)
    certifications: List[str]               = Field(default_factory=list)
    languages: List[str]                    = Field(default_factory=list)
    confidence_score: float                 = Field(default=0.0, ge=0.0, le=1.0)
