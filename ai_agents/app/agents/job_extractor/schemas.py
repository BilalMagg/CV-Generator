"""
Job Extractor agent — internal I/O schemas.
"""
from __future__ import annotations

from pydantic import BaseModel, Field

from app.models.job_model import JobRequirements


class ExtractorInput(BaseModel):
    job_description: str = Field(..., description="Raw job posting text to parse")
    language: str        = Field(default="en", description="Language code of the job description")


class ExtractorOutput(JobRequirements):
    """
    Extends JobRequirements with a confidence_score injected by the LLM.
    Nothing extra here — confidence_score already lives on JobRequirements.
    """
