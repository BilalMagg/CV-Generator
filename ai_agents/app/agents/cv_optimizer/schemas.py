"""
CV Optimizer agent — internal I/O schemas.

Receives rendered sections and job requirements, returns the
polished final CV with an ATS score estimate.
"""
from __future__ import annotations

from typing import List

from pydantic import BaseModel, Field

from app.models.cv_model import CVSection, OptimizedCV
from app.models.job_model import JobRequirements


class OptimizerInput(BaseModel):
    job_id: str
    rendered_sections: List[CVSection]
    job_requirements: JobRequirements


class OptimizerOutput(BaseModel):
    optimized_cv: OptimizedCV
    suggestions: List[str] = Field(default_factory=list,
                                   description="Actionable improvement hints for the user")
