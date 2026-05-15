from fastapi import APIRouter

from app.core.config import settings

router = APIRouter()


@router.get("/health")
async def health():
    """
    Lightweight health check used by Docker Compose to determine
    when this service is ready before starting dependent services.
    """
    return {"status": "ok", "service": settings.SERVICE_NAME}
