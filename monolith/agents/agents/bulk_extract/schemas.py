from typing import List, Optional

from pydantic import BaseModel, Field


class BulkExtractRequest(BaseModel):
    """A messy pasted blob (numbered companies, email lists, specialties)."""

    text: str = ""
    language: str = "English"
    provider: Optional[str] = None
    model: Optional[str] = None


class BulkRow(BaseModel):
    company_name: str = ""
    location: Optional[str] = None
    email: Optional[str] = None
    contact_name: Optional[str] = None
    specialties: List[str] = Field(default_factory=list)


class BulkExtractResponse(BaseModel):
    rows: List[BulkRow] = Field(default_factory=list)
