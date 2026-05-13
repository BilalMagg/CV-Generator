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

from cvtools.core.config import settings
from cvtools.models.user_model import UserResponse, ExperienceResponse, ProjectResponse, SkillResponse
from cvtools.models.workflow_model import WorkflowResponse

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


async def _get(path: str) -> dict | list:
    client = get_client()
    try:
        response = await client.get(path)
        response.raise_for_status()
        body = response.json()
        if not body.get("success"):
            logger.warning(f"Backend error on GET {path}: {body.get('message')}")
            return []
        return body.get("data") or []
    except Exception as e:
        logger.error(f"Failed to GET {path}: {str(e)}")
        return []


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


async def check_vectors_status(user_id: UUID) -> bool:
    client = get_client()
    # Assuming workflow-service is accessible on a specific URL or routed via an API Gateway.
    # We use BACKEND_BASE_URL assuming it routes /api/vectors appropriately.
    response = await client.get(f"/api/vectors/status/{user_id}")
    if response.status_code == 200:
        body = response.json()
        return body.get("data", False)
    return False


async def sync_vectors(user_id: UUID, chunks: list) -> bool:
    client = get_client()
    payload = {
        "userId": str(user_id),
        "chunks": chunks
    }
    response = await client.post("/api/vectors/sync", json=payload)
    return response.status_code == 200


async def search_vectors(user_id: UUID, query_text: str, query_vector: list, limit: int = 15) -> list:
    client = get_client()
    payload = {
        "userId": str(user_id),
        "queryText": query_text,
        "queryVector": query_vector,
        "limit": limit
    }
    response = await client.post("/api/vectors/search", json=payload)
    if response.status_code == 200:
        body = response.json()
        return body.get("data", [])
    return []