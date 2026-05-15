from __future__ import annotations

from typing import Optional
from uuid import UUID

from pydantic import BaseModel, Field


class CrawlTriggerEvent(BaseModel):
    """
    Schema for messages consumed from the 'trigger-live-crawl' topic.
    Published by the .NET backend.
    """
    search_id: UUID = Field(..., description="Unique ID for this crawl run.")
    keyword: str = Field(..., description="Job title or skill to search for.")
    location: str = Field("", description="Location to search in (e.g. 'New York').")
    result_limit: int = Field(20, ge=1, le=50, description="Maximum total jobs to return.")


class RawJobPackageEvent(BaseModel):
    """
    Schema for messages produced to the 'raw-job-urls' topic.
    Consumed by the job-extractor agent.
    """
    search_id: str = Field(..., description="The search run ID this job belongs to.")
    job_url: Optional[str] = Field(None, description="Direct URL to the job posting.")
    raw_description: Optional[str] = Field(None, description="Raw job description text scraped from the listing.")
    title: Optional[str] = Field(None, description="Job title as listed.")
    company: Optional[str] = Field(None, description="Company name.")
    location: Optional[str] = Field(None, description="Job location.")
    source: str = Field(..., description="The job board source (e.g. 'linkedin', 'indeed').")
