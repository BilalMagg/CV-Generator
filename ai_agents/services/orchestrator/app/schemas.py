"""
Orchestrator schemas.
"""
from __future__ import annotations

from uuid import UUID

from pydantic import BaseModel, Field

from app.models.job_model import JobRequest as CommonJobRequest


class JobRequest(BaseModel):
    user_id: UUID
    job_description: str
    template_id: str = "default"
    workflow_id: UUID | None = None


class WorkflowResult(BaseModel):
    success: bool
    message: str
    ats_score: int | None = None
    delivery_status: str | None = None
    job_id: str | None = None