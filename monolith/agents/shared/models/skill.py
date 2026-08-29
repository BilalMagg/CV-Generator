from pydantic import BaseModel, ConfigDict
from typing import Optional, List
from uuid import UUID


class SkillDetailDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    name: str
    description: Optional[str] = None
    proficiency: Optional[str] = None
    image: Optional[str] = None


class SkillDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    name: str
    description: Optional[str] = None
    proficiency: Optional[str] = None
    image: Optional[str] = None
    skillDetails: List[SkillDetailDto] = []
