import re

from pydantic import BaseModel, field_validator
from typing import Optional, List, Dict

_YEARS_RE = re.compile(r"(\d+(?:[.,]\d+)?)")


class JobRequest(BaseModel):
    """Sidecar input aligned to the .NET JobExtractionRequest contract.

    At least one of job_description / url / job_offer_id must be provided.
    Fields may be null/absent — the router decides which extraction path to take.
    """

    job_description: Optional[str] = None
    url: Optional[str] = None
    job_offer_id: Optional[str] = None
    language: str = "English"
    provider: Optional[str] = None
    model: Optional[str] = None


class JobExtractionResult(BaseModel):
    """Rich extraction output matching the .NET `ExtractionFullResult` DTO.

    The .NET backend reads the JSON response with snake_case naming:
      enterprise_name, enterprise_description, enterprise_logo_url, job_role,
      raw_description, responsibilities, required_skills, soft_skills,
      required_experience_years, seniority_level, employment_type, location,
      location_type, salary_range, currency, certifications, languages,
      education_requirements, benefits, application_deadline, contact_email,
      source_url, field_confidences, overall_confidence.
    """

    enterprise_name: str = ""
    enterprise_description: str = ""
    enterprise_logo_url: str = ""
    job_role: str = ""
    raw_description: str = ""
    responsibilities: List[str] = []
    required_skills: List[str] = []
    soft_skills: List[str] = []
    required_experience_years: Optional[float] = None
    seniority_level: str = ""
    employment_type: str = ""
    location: str = ""
    location_type: str = ""
    salary_range: str = ""
    currency: str = ""
    certifications: List[str] = []
    languages: List[str] = []
    education_requirements: str = ""
    benefits: List[str] = []
    application_deadline: str = ""
    contact_email: str = ""
    source_url: str = ""
    field_confidences: Dict[str, float] = {}
    overall_confidence: float = 0.0

    @field_validator("required_experience_years", mode="before")
    @classmethod
    def _coerce_years(cls, v):
        """Tolerate stringy LLM output (""5+"", ""3-5 years"", ""5""); the first
        number wins (the lower bound of a stated range)."""
        if v is None or isinstance(v, (int, float)):
            return v
        m = _YEARS_RE.search(str(v).replace(",", "."))
        if not m:
            return None
        try:
            return float(m.group(1))
        except ValueError:
            return None