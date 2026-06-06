from fastapi import APIRouter

from app.core.config import settings
from app.consumer import crawl_state

router = APIRouter()


@router.get("/health")
async def health():
    """
    Lightweight health check used by Docker Compose to determine
    when this service is ready before starting dependent services.
    """
    return {"status": "ok", "service": settings.SERVICE_NAME}


@router.get("/status")
async def crawler_status():
    """
    Current crawler state: whether it's idle or running, last search
    details, and job counts. Useful for observability and debugging.
    """
    return {
        "service": settings.SERVICE_NAME,
        **crawl_state,
    }
