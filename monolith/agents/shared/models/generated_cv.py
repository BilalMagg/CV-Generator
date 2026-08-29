from pydantic import BaseModel, ConfigDict
from typing import Optional, List
from uuid import UUID
from datetime import datetime


class GeneratedCv(BaseModel):
    id: UUID
    cvData: str
    templateName: str = "modern"
    generatedAt: datetime


class GeneratedCvResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    workflowId: UUID
    cvData: str
    templateName: str
    version: int
    createdAt: datetime


class FinalizedCvMetadata(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: UUID
    fileName: str
    fileSize: int
    cvData: str
    templateName: str
    version: int
    createdAt: datetime


class FinalizedCvResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    message: str
    metadata: FinalizedCvMetadata
