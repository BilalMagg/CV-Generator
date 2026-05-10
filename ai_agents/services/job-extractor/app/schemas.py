from __future__ import annotations

from typing import Optional
from uuid import UUID

from pydantic import BaseModel, Field, model_validator


class ExtractorInput(BaseModel):
    text: Optional[str] = Field(None, alias="job_description")
    file_content: Optional[bytes] = None
    file_name: Optional[str] = None
    url: Optional[str] = None
    job_offer_id: Optional[UUID] = None
    language: str = "en"

    @model_validator(mode="after")
    def check_at_least_one_input(self):
        if not any([self.text, self.file_content, self.url, self.job_offer_id]):
            raise ValueError(
                "At least one of text, file_content, url, or job_offer_id must be provided"
            )
        return self


class ExtractorOutput(BaseModel):
    enterprise_name: Optional[str] = None
    enterprise_description: Optional[str] = None
    enterprise_logo_url: Optional[str] = None

    job_role: Optional[str] = None
    raw_description: Optional[str] = None
    responsibilities: list[str] = []
    required_skills: list[str] = []
    soft_skills: list[str] = []

    required_experience_years: Optional[int] = None
    seniority_level: Optional[str] = None
    employment_type: Optional[str] = None
    location: Optional[str] = None
    location_type: Optional[str] = None
    salary_range: Optional[str] = None
    currency: Optional[str] = None

    certifications: list[str] = []
    languages: list[str] = []
    education_requirements: Optional[str] = None

    benefits: list[str] = []
    application_deadline: Optional[str] = None
    contact_email: Optional[str] = None
    source_url: Optional[str] = None

    field_confidences: dict[str, float] = {}
    overall_confidence: float = 0.0
