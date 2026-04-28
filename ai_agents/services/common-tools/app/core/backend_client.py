"""
Async HTTP client for the ASP.NET backend.

All backend responses follow the envelope:
    { "success": bool, "message": str | None, "data": T | None, "errors": object | None }

This client unpacks that envelope and returns typed Python models.
"""
import httpx
import logging
from typing import TypeVar, List
from uuid import UUID

from app.core.config import settings
from app.models.user_model import UserResponse, ExperienceResponse, ProjectResponse, SkillResponse
from app.models.workflow_model import WorkflowResponse

logger = logging.getLogger(__name__)

T = TypeVar("T")

_client: httpx.AsyncClient | None = None


def get_client() -> httpx.AsyncClient:
    if _client is None:
        raise RuntimeError("Backend client is not initialised. Did lifespan run?")
    return _client


def create_client() -> httpx.AsyncClient:
    global _client
    _client = httpx.AsyncClient(
        base_url=settings.BACKEND_BASE_URL,
        timeout=30.0,
        headers={"Content-Type": "application/json"},
    )
    return _client


async def close_client() -> None:
    global _client
    if _client:
        await _client.aclose()
        _client = None


async def _get(path: str) -> dict:
    client = get_client()
    response = await client.get(path)
    response.raise_for_status()
    body = response.json()
    if not body.get("success"):
        raise RuntimeError(f"Backend error on GET {path}: {body.get('message')}")
    return body["data"]


async def get_user(user_id: UUID) -> UserResponse:
    data = await _get(f"/api/users/{user_id}")
    return UserResponse.model_validate(data)


async def get_user_experiences(user_id: UUID) -> List[ExperienceResponse]:
    data = await _get("/api/experiences")
    all_items = [ExperienceResponse.model_validate(item) for item in data]
    return [e for e in all_items if e.user_id == user_id]


async def get_user_projects(user_id: UUID) -> List[ProjectResponse]:
    data = await _get("/api/projects")
    all_items = [ProjectResponse.model_validate(item) for item in data]
    return [p for p in all_items if p.user_id == user_id]


async def get_user_skills(user_id: UUID) -> List[SkillResponse]:
    data = await _get("/api/skills")
    all_items = [SkillResponse.model_validate(item) for item in data]
    return [s for s in all_items if s.user_id == user_id]


async def get_workflow(workflow_id: UUID) -> WorkflowResponse:
    data = await _get(f"/api/workflows/{workflow_id}")
    return WorkflowResponse.model_validate(data)