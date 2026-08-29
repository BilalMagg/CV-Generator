from pydantic import BaseModel, ConfigDict
from typing import Optional, List
from uuid import UUID


class ExperienceDetailDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    title: str
    description: Optional[str] = None
    startDate: Optional[str] = None
    endDate: Optional[str] = None
    isCurrent: bool = False
    image: Optional[str] = None


class ExperienceDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    title: str
    description: Optional[str] = None
    company: Optional[str] = None
    startDate: Optional[str] = None
    endDate: Optional[str] = None
    isCurrent: bool = False
    experienceDetails: List[ExperienceDetailDto] = []
    technologies: List[str] = []
