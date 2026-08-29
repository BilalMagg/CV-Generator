from pydantic import BaseModel, ConfigDict
from typing import Optional, List
from uuid import UUID


class OtherExperienceDetailDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    title: str
    description: Optional[str] = None
    startDate: Optional[str] = None
    endDate: Optional[str] = None
    isCurrent: bool = False
    image: Optional[str] = None


class OtherExperienceDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    title: str
    description: Optional[str] = None
    organization: Optional[str] = None
    startDate: Optional[str] = None
    endDate: Optional[str] = None
    isCurrent: bool = False
    otherExperienceDetails: List[OtherExperienceDetailDto] = []
