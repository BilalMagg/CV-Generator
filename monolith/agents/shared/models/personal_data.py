from pydantic import BaseModel, ConfigDict
from typing import List, Optional
from uuid import UUID


class AddressDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    label: Optional[str] = None
    streetAddress: Optional[str] = None
    city: Optional[str] = None
    postalCode: Optional[str] = None
    country: Optional[str] = None
    isDefault: bool = False


class SocialLinksDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    linkedin: Optional[str] = None
    github: Optional[str] = None
    website: Optional[str] = None
    twitter: Optional[str] = None
    facebook: Optional[str] = None
    instagram: Optional[str] = None
    other: Optional[str] = None


class CourseDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    title: str
    description: Optional[str] = None
    institution: Optional[str] = None


class CertificationDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    title: str
    description: Optional[str] = None
    institution: Optional[str] = None
    date: Optional[str] = None


class AchievementDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    title: str
    description: Optional[str] = None
    date: Optional[str] = None
    image: Optional[str] = None


class LanguageDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    name: str
    proficiency: Optional[str] = None


class EducationalBackgroundDto(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    schoolName: Optional[str] = None
    degree: Optional[str] = None
    fieldOfStudy: Optional[str] = None
    startDate: Optional[str] = None
    endDate: Optional[str] = None
    description: Optional[str] = None
    grade: Optional[str] = None


class PersonalDataResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    userId: UUID
    firstName: Optional[str] = None
    lastName: Optional[str] = None
    email: Optional[str] = None
    phone: Optional[str] = None
    summary: Optional[str] = None
    dateOfBirth: Optional[str] = None
    gender: Optional[str] = None
    nationality: Optional[str] = None
    maritalStatus: Optional[str] = None
    addresses: List[AddressDto] = []
    socialLinks: Optional[SocialLinksDto] = None
    educationalBackground: List[EducationalBackgroundDto] = []
    courses: List[CourseDto] = []
    certifications: List[CertificationDto] = []
    achievements: List[AchievementDto] = []
    languages: List[LanguageDto] = []
