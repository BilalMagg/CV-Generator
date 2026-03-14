from pydantic import BaseModel, Field
from typing import List, Optional

class ExperienceItem(BaseModel):
    title: str
    company: str
    description: str
    start_date: str
    end_date: Optional[str] = None

class ProjectItem(BaseModel):
    title: str
    role: str
    description: str
    technologies: List[str]

class UserProfile(BaseModel):
    user_id: str
    name: str
    email: str
    experiences: List[ExperienceItem] = Field(default_factory=list)
    projects: List[ProjectItem] = Field(default_factory=list)
    skills: List[str] = Field(default_factory=list)
