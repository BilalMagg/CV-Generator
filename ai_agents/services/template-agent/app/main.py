"""
Template Agent Service — renders CV sections from matched data.
"""
from contextlib import asynccontextmanager
import logging
from pathlib import Path
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from cvtools import init_minio_storage, seed_templates_from_dir

from app.core.config import settings
from app.routers import router

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("Template Agent service starting up")
    # Fail loud on boot if MinIO object storage is not configured, and
    # pre-create the buckets so a fresh MinIO volume works on first deploy.
    init_minio_storage()
    logger.info("MinIO object storage initialised")
    # Seed the default CV templates (seed-if-missing) so a fresh MinIO volume
    # has usable templates without a manual step. Best-effort: a seeding hiccup
    # must not stop the service — render falls back to DEFAULT_TEMPLATE.
    try:
        seeded = seed_templates_from_dir(Path(__file__).resolve().parent.parent / "templates")
        logger.info("Templates seeded: %s", seeded or "none (all present)")
    except Exception:
        logger.exception("Template seeding failed; continuing with DEFAULT_TEMPLATE fallback")
    yield
    logger.info("Template Agent service shutting down")


def create_app() -> FastAPI:
    app = FastAPI(
        title=settings.SERVICE_NAME,
        version=settings.VERSION,
        lifespan=lifespan,
    )
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )
    app.include_router(router, prefix="/api/v1")
    return app


app = create_app()