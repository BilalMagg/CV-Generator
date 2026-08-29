from pydantic import BaseModel, ConfigDict
from typing import Optional, List
from uuid import UUID


class UserResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    email: str
    firstName: Optional[str] = None
    lastName: Optional[str] = None
    username: Optional[str] = None
    isProfileComplete: bool = False


class ExperienceResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    title: str
    description: Optional[str] = None
    company: Optional[str] = None
    startDate: Optional[str] = None
    endDate: Optional[str] = None
    isCurrent: bool = False
    experienceDetails: list = []
    technologies: list = []


class ProjectResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    title: str
    description: Optional[str] = None
    company: Optional[str] = None
    startDate: Optional[str] = None
    endDate: Optional[str] = None
    isCurrent: bool = False
    projectDetails: list = []
    technologies: list = []


class SkillResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    name: str
    description: Optional[str] = None
    proficiency: Optional[str] = None


class EducationResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    institutionName: Optional[str] = None
    degreeType: Optional[str] = None
    fieldOfStudy: Optional[str] = None
    startDate: Optional[str] = None
    endDate: Optional[str] = None
    status: Optional[str] = None
    description: Optional[str] = None


class HackathonResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    name: str
    organization: Optional[str] = None
    date: Optional[str] = None
    description: Optional[str] = None
    role: Optional[str] = None
    result: Optional[str] = None


class InterestResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    name: str


class LanguageResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    name: str
    level: Optional[str] = None


class CertificationResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    name: str
    issuingOrganization: Optional[str] = None
    issueDate: Optional[str] = None
    credentialUrl: Optional[str] = None


class CvSectionResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    sectionType: str
    title: Optional[str] = None
    content: Optional[str] = None
    displayOrder: int = 0
    isVisible: bool = True
