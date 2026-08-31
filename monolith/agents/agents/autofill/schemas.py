from typing import Any, List, Optional

from pydantic import BaseModel, Field


class FieldDef(BaseModel):
    """A single form field the caller wants auto-filled.

    `type` mirrors the frontend input type (text/textarea/date/number/select/url).
    `options` carries accepted values for select/checkbox-like fields so the LLM can
    be constrained to a valid value.
    """

    name: str
    label: str = ""
    type: str = "text"
    options: List[str] = []
    help: str = ""


class AutofillRequest(BaseModel):
    """Extract structured field values from a pasted description blob.

    The caller supplies the exact list of fields it wants filled so the model never
    has to guess schema (keeps this agent generic for ANY form, not just a fixed set).
    """

    entity_type: str = "generic"
    text: str = ""
    fields: List[FieldDef] = []
    language: str = "English"
    provider: Optional[str] = None
    model: Optional[str] = None


class AutofillResponse(BaseModel):
    """Flat field-name -> value map. Values are coerced to the declared field type
    by the sidecar before returning (dates normalized, numbers cast, selects clamped)."""

    values: dict[str, Any] = Field(default_factory=dict)
