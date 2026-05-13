"""
CV Optimizer schemas.
"""
from pydantic import BaseModel , Field

class OptimizerInput(BaseModel):
    job_data: str = Field(..., description="Job offer content")
    candidate_name: str = Field(..., min_length=2, description="Candidate name")
    session_id: str = Field(..., min_length=1, description="Session ID for chat history")
    user_focus: str | None = Field(None, description="Sections or aspects to prioritize")


class OptimizerOutput(BaseModel):
    ats_score_before: int = Field(..., ge=0, le=100, description="ATS Score before optimization")
    ats_score_after: int = Field(..., ge=0, le=100, description="ATS Score after optimization")
    improvement: int = Field(..., description="Score improvement points")
    file_path: str = Field(..., description="Path where the optimized file is saved")