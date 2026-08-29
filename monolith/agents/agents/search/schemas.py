from typing import Optional
from pydantic import BaseModel


class SearchRequest(BaseModel):
    query: str
    workflow_id: str
    user_id: str
    # Job description for similarity scoring
    target_job_title: Optional[str] = None
    target_job_description: Optional[str] = None
    target_required_skills: Optional[list[str]] = None
    target_preferred_skills: Optional[list[str]] = None


class SearchResultItem(BaseModel):
    content: str
    score: float
    source: str


class SearchResponse(BaseModel):
    results: list[SearchResultItem]
    query: str


class SimilarityScore(BaseModel):
    score: float
    explanation: str


class ProfileMatchRequest(BaseModel):
    """Contract for the .NET SearchInput DTO (snake_case JSON).

    job_requirements mirrors JobRequirements: job_role, extracted_skills,
    required_experience_years, keywords, seniority_level, employment_type,
    location_type, responsibilities, certifications.
    """

    user_id: str
    job_requirements: dict


class ProfileMatchResponse(BaseModel):
    """Mirrors the .NET SearchOutput DTO (snake_case JSON)."""

    matched_skills: list = []
    matched_experiences: list = []
    matched_projects: list = []
    gap_skills: list[str] = []
    match_score: float = 0.0