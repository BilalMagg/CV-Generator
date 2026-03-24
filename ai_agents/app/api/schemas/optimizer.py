"""HTTP-facing request/response schemas for the CV Optimizer route."""
from __future__ import annotations

from typing import List

from pydantic import BaseModel

from app.models.cv_model import CVSection, OptimizedCV
from app.models.job_model import JobRequirements


class OptimizeRequest(BaseModel):
    job_id: str
    rendered_sections: List[CVSection]
    job_requirements: JobRequirements


class OptimizeResponse(BaseModel):
    optimized_cv: OptimizedCV
    suggestions: List[str]
