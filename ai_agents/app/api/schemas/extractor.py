"""HTTP-facing request/response schemas for the Job Extractor route."""
from __future__ import annotations

from typing import List, Optional

from pydantic import BaseModel, Field


class ExtractRequest(BaseModel):
    job_description: str = Field(..., min_length=50, description="Raw job posting text")
    language: str        = Field(default="en")


class ExtractResponse(BaseModel):
    job_role: str
    extracted_skills: List[str]
    required_experience_years: Optional[int]
    keywords: List[str]
    seniority_level: Optional[str]
    employment_type: Optional[str]
    location_type: Optional[str]
    responsibilities: List[str]
    certifications: List[str]
    languages: List[str]
    confidence_score: float
