"""
Shared context for dynamic workflow execution.
Acts as a blackboard for agents to share data.
"""
from __future__ import annotations

from typing import Dict, Any, Optional, List
from uuid import UUID

from pydantic import BaseModel, Field

from app.models.user_model import UserResponse, ExperienceResponse, ProjectResponse, SkillResponse
from app.models.job_model import JobRequirements
from app.models.cv_model import CVSection, OptimizedCV


class WorkflowContext(BaseModel):
    """
    Central storage for a single workflow run. 
    Agents read from and write to this context.
    """
    user_id: UUID
    job_description: Optional[str] = None
    
    # User Data (usually fetched at start or by a 'fetch' node)
    user_profile: Optional[UserResponse] = None
    experiences: List[ExperienceResponse] = Field(default_factory=list)
    projects: List[ProjectResponse] = Field(default_factory=list)
    skills: List[SkillResponse] = Field(default_factory=list)

    # Agent Outputs
    job_requirements: Optional[JobRequirements] = None
    
    # Match Results
    matched_experiences: List[ExperienceResponse] = Field(default_factory=list)
    matched_projects: List[ProjectResponse] = Field(default_factory=list)
    matched_skills: List[SkillResponse] = Field(default_factory=list)
    gap_skills: List[str] = Field(default_factory=list)
    
    # Rendered & Optimized Content
    rendered_sections: List[CVSection] = Field(default_factory=list)
    optimized_cv: Optional[OptimizedCV] = None

    # Metadata
    execution_log: List[str] = Field(default_factory=list)
    shared_data: Dict[str, Any] = Field(default_factory=dict, 
                                        description="Catch-all for unstructured agent data")

    def log(self, message: str):
        self.execution_log.append(message)
