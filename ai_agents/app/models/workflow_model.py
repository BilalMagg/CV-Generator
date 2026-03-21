"""
Workflow-related models mirroring the backend and defining the dynamic node structure.
"""
from __future__ import annotations

from datetime import datetime
from typing import List, Dict, Any, Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class WorkflowNode(BaseModel):
    """Represents a single step in a dynamic workflow."""
    type: str = Field(..., description="Agent type (e.g., 'extractor', 'search', 'template', 'optimizer', 'contact')")
    config: Dict[str, Any] = Field(default_factory=dict, description="Agent-specific configuration")


class WorkflowResponse(BaseModel):
    """Mirrors WorkflowResponseDto from the C# backend."""
    model_config = ConfigDict(populate_by_name=True)

    id: UUID
    name: str
    description: str
    definition_json: str = Field(alias="definitionJson")
    is_active: bool = Field(alias="isActive")
    created_at: datetime = Field(alias="createdAt")

    @property
    def nodes(self) -> List[WorkflowNode]:
        """Parses the definition_json string into a list of WorkflowNode objects."""
        import json
        try:
            data = json.loads(self.definition_json)
            return [WorkflowNode(**node) for node in data]
        except (json.JSONDecodeError, TypeError, ValueError):
            return []
