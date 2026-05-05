"""
Workflow-related models.
"""
from __future__ import annotations

from datetime import datetime
from typing import List, Dict, Any, Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class WorkflowNode(BaseModel):
    type: str
    config: Dict[str, Any] = Field(default_factory=dict)


class WorkflowResponse(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    id: UUID
    name: str
    description: str
    definition_json: str = Field(alias="definitionJson")
    is_active: bool = Field(alias="isActive")
    created_at: datetime = Field(alias="createdAt")

    @property
    def nodes(self) -> List[WorkflowNode]:
        import json
        try:
            data = json.loads(self.definition_json)
            return [WorkflowNode(**node) for node in data]
        except (json.JSONDecodeError, TypeError, ValueError):
            return []