from pydantic import BaseModel, Field
from typing import List, Optional

class CVDraft(BaseModel):
    target_role: str
    summary: str
    matched_skills: List[str]
    matched_experiences: List[dict] # Can be typed precisely later
    matched_projects: List[dict]
    missing_skills_suggestions: List[str] = Field(default_factory=list)

class OptimizedCV(BaseModel):
    final_content: CVDraft
    ats_score_estimate: int = Field(ge=0, le=100)
    optimization_notes: List[str] = Field(default_factory=list)
    pdf_url: Optional[str] = None
