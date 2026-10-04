from pydantic import BaseModel


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