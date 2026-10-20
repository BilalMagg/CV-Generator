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


class MapRow(BaseModel):
    """One extracted row handed to the AI mapper (no subject/body yet)."""

    company_name: str = ""
    location: Optional[str] = None
    specialty: Optional[str] = None


class MapTemplate(BaseModel):
    """One candidate template the AI may choose from."""

    id: str = ""
    name: str = ""
    subject_template: str = ""
    body_template: str = ""
    intent: Optional[str] = None


class TemplateMapRequest(BaseModel):
    rows: List[MapRow] = Field(default_factory=list)
    templates: List[MapTemplate] = Field(default_factory=list)
    provider: Optional[str] = None
    model: Optional[str] = None


class TemplateMapResponse(BaseModel):
    """Aligned with the request rows: the chosen template id ('' = no match)."""

    template_ids: List[str] = Field(default_factory=list)
