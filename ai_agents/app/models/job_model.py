from pydantic import BaseModel, Field
from typing import List, Optional

class JobRequest(BaseModel):
    user_id: str = Field(..., description="ID of the user requesting the CV")
    job_description: str = Field(..., description="Raw text of the job description")
    template_id: Optional[str] = Field(default="default", description="Template choice for the CV")

class JobRequirements(BaseModel):
    extracted_skills: List[str] = Field(default_factory=list, description="Core skills required for the job")
    required_experience_years: Optional[int] = Field(None, description="Years of experience required")
    job_role: str = Field(..., description="Normalized job category or title")
    keywords: List[str] = Field(default_factory=list, description="Important ATS keywords found")
