"""
Async HTTP client for the ASP.NET backend.

All backend responses follow the envelope:
    { "success": bool, "message": str | None, "data": T | None, "errors": object | None }

This client unpacks that envelope and returns typed Python models.
"""
import httpx
import logging
from typing import TypeVar, List, Any
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
    logger.info("Creating backend client with base_url=%s", settings.BACKEND_BASE_URL)
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
    full_url = str(client.base_url) + path
    try:
        logger.info("GET %s", full_url)
        response = await client.get(path)
        logger.info("GET %s → %d", full_url, response.status_code)
        response.raise_for_status()
        body = response.json()
        if not body.get("success"):
            logger.warning("Backend error on GET %s: %s", full_url, body.get("message"))
            return {}
        data = body.get("data") or {}
        logger.info("GET %s → data_type=%s", full_url, type(data).__name__)
        if isinstance(data, list):
            logger.info("GET %s → list length=%d", full_url, len(data))
        return data
    except Exception as e:
        logger.error("Failed to GET %s: %s", full_url, str(e))
        return {}


async def get_user(user_id: UUID) -> UserResponse:
    data = await _get(f"/api/users/{user_id}")
    return UserResponse.model_validate(data)


async def get_user_experiences(user_id: UUID) -> List[ExperienceResponse]:
    data = await _get(f"/api/user-content/experiences?userId={user_id}")
    if isinstance(data, list):
        result = [ExperienceResponse.model_validate(item) for item in data]
        logger.info("get_user_experiences(%s) → %d items", user_id, len(result))
        return result
    logger.warning("get_user_experiences(%s) → data is not a list (type=%s)", user_id, type(data).__name__)
    return []


async def get_user_projects(user_id: UUID) -> List[ProjectResponse]:
    data = await _get(f"/api/user-content/projects?userId={user_id}")
    if isinstance(data, list):
        result = [ProjectResponse.model_validate(item) for item in data]
        logger.info("get_user_projects(%s) → %d items", user_id, len(result))
        return result
    logger.warning("get_user_projects(%s) → data is not a list (type=%s)", user_id, type(data).__name__)
    return []


async def get_user_skills(user_id: UUID) -> List[SkillResponse]:
    data = await _get(f"/api/user-content/skills?userId={user_id}")
    if isinstance(data, list):
        result = [SkillResponse.model_validate(item) for item in data]
        logger.info("get_user_skills(%s) → %d items", user_id, len(result))
        return result
    logger.warning("get_user_skills(%s) → data is not a list (type=%s)", user_id, type(data).__name__)
    return []


async def _get_single(path: str) -> dict | None:
    client = get_client()
    full_url = str(client.base_url) + path
    try:
        logger.info("GET %s", full_url)
        response = await client.get(path)
        logger.info("GET %s → %d", full_url, response.status_code)
        if response.status_code == 404:
            logger.warning("GET %s → 404 (not found)", full_url)
            return None
        response.raise_for_status()
        body = response.json()
        if not body.get("success"):
            logger.warning("Backend error on GET %s: %s", full_url, body.get("message"))
            return None
        data = body.get("data") or None
        logger.info("GET %s → found=%s", full_url, data is not None)
        return data
    except Exception as e:
        logger.warning("Failed to GET %s: %s", full_url, str(e))
        return None


async def get_experience(experience_id: UUID) -> ExperienceResponse | None:
    data = await _get_single(f"/api/user-content/experiences/{experience_id}")
    if data:
        return ExperienceResponse.model_validate(data)
    return None


async def get_project(project_id: UUID) -> ProjectResponse | None:
    data = await _get_single(f"/api/user-content/projects/{project_id}")
    if data:
        return ProjectResponse.model_validate(data)
    return None


async def get_workflow(workflow_id: UUID) -> WorkflowResponse:
    data = await _get(f"/api/workflows/{workflow_id}")
    return WorkflowResponse.model_validate(data)


async def check_vectors_status(user_id: UUID) -> bool:
    client = get_client()
    path = f"/api/vectors/status/{user_id}"
    full_url = str(client.base_url) + path
    logger.info("GET %s", full_url)
    response = await client.get(path)
    logger.info("GET %s → %d", full_url, response.status_code)
    if response.status_code == 200:
        body = response.json()
        result = body.get("data", False)
        logger.info("check_vectors_status(%s) → %s", user_id, result)
        return result
    logger.warning("check_vectors_status(%s) failed with status %d", user_id, response.status_code)
    return False


async def sync_vectors(user_id: UUID, chunks: list) -> bool:
    client = get_client()
    path = "/api/vectors/sync"
    full_url = str(client.base_url) + path
    payload = {
        "userId": str(user_id),
        "chunks": chunks
    }
    logger.info("POST %s (userId=%s, chunks=%d)", full_url, user_id, len(chunks))
    response = await client.post(path, json=payload)
    logger.info("POST %s → %d", full_url, response.status_code)
    return response.status_code == 200


async def search_vectors(user_id: UUID, query_text: str, query_vector: list, limit: int = 15) -> list:
    client = get_client()
    path = "/api/vectors/search"
    full_url = str(client.base_url) + path
    payload = {
        "userId": str(user_id),
        "queryText": query_text,
        "queryVector": query_vector,
        "limit": limit
    }
    logger.info("POST %s (userId=%s, query='%s...', limit=%d)", full_url, user_id, query_text[:50], limit)
    response = await client.post(path, json=payload)
    logger.info("POST %s → %d", full_url, response.status_code)
    if response.status_code == 200:
        body = response.json()
        data = body.get("data", [])
        logger.info("search_vectors returned %d results", len(data))
        return data
    logger.warning("search_vectors failed with status %d", response.status_code)
    return []