"""Aggregate all APIRouters under the /api/v1 prefix."""
from fastapi import APIRouter

from app.api.routes.extractor import router as extractor_router
from app.api.routes.search    import router as search_router
from app.api.routes.template  import router as template_router
from app.api.routes.optimizer import router as optimizer_router
from app.api.routes.contact   import router as contact_router

api_router = APIRouter(prefix="/api/v1")

api_router.include_router(extractor_router)
api_router.include_router(search_router)
api_router.include_router(template_router)
api_router.include_router(optimizer_router)
api_router.include_router(contact_router)
