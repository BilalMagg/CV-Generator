"""
Template Agent schemas.
"""
from __future__ import annotations

from typing import List, Optional

from pydantic import BaseModel, Field

from cvtools.models.cv_model import CVDraft, CVSection


# Input/Output schema
class TemplateInput(BaseModel):
    cv_draft: CVDraft
    template_id: str  = Field(default="default")
    target_role: str  = Field(..., description="Job role being targeted — used to tailor section wording")

    model_config = {
        "json_schema_extra": {
            "examples": [
                {
                    "target_role": "Senior Software Engineer",
                    "template_id": "default.json",
                    "cv_draft": {
                        "target_role": "Senior Software Engineer",
                        "summary": "Experienced full-stack developer with 5 years building scalable microservices and web applications.",
                        "matched_experiences": [
                            {
                                "id": "00000000-0000-0000-0000-000000000001",
                                "title": "Software Engineer",
                                "company": "Google",
                                "description": "Led development of a real-time data pipeline processing 1M events/day. Built microservices using Go and Python. Mentored 3 junior engineers.",
                                "start_date": "2021-06-01T00:00:00",
                                "end_date": "2024-01-01T00:00:00",
                                "status": "completed",
                                "user_id": "00000000-0000-0000-0000-000000000099"
                            },
                            {
                                "id": "00000000-0000-0000-0000-000000000002",
                                "title": "Junior Developer",
                                "company": "Amazon",
                                "description": "Developed REST APIs with FastAPI. Implemented CI/CD pipelines using GitHub Actions and Docker.",
                                "start_date": "2019-09-01T00:00:00",
                                "end_date": "2021-05-01T00:00:00",
                                "status": "completed",
                                "user_id": "00000000-0000-0000-0000-000000000099"
                            }
                        ],
                        "matched_projects": [
                            {
                                "id": "00000000-0000-0000-0000-000000000010",
                                "title": "CV Generator Platform",
                                "description": "AI-powered CV generation platform using LangChain agents and microservices architecture.",
                                "start_date": "2024-01-01T00:00:00",
                                "status": "active",
                                "repository_url": "https://github.com/user/cv-generator",
                                "user_id": "00000000-0000-0000-0000-000000000099"
                            }
                        ],
                        "matched_skills": [
                            {"id": "00000000-0000-0000-0000-000000000020", "name": "Python", "level": "Expert", "years_of_experience": 5, "user_id": "00000000-0000-0000-0000-000000000099"},
                            {"id": "00000000-0000-0000-0000-000000000021", "name": "Docker", "level": "Advanced", "years_of_experience": 3, "user_id": "00000000-0000-0000-0000-000000000099"},
                            {"id": "00000000-0000-0000-0000-000000000022", "name": "FastAPI", "level": "Advanced", "years_of_experience": 3, "user_id": "00000000-0000-0000-0000-000000000099"}
                        ],
                        "gap_skills": ["Kubernetes", "Terraform", "AWS"]
                    }
                }
            ]
        }
    }

class RenderedCV(BaseModel):
    cv_code : str | bytes
    template_id: str
    sections : Optional[List[CVSection]]


# class TemplateOutput(BaseModel):
#     sections: List[CVSection] = Field(default_factory=list)
#     template_used: str