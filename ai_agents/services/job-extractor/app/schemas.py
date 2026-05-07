"""
Job Extractor schemas.
"""
from __future__ import annotations

from pydantic import BaseModel, Field

from app.models.job_model import JobRequirements


class ExtractorInput(BaseModel):
    job_description: str = Field(..., description="Raw job posting text to parse")
    language: str = Field(default="en", description="Language code of the job description")


class ExtractorOutput(JobRequirements):
    pass