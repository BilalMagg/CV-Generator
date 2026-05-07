"""
FastAPI application entry point.

Mounts all agent routers under /api/v1, sets up CORS,
and manages the httpx backend client lifecycle via lifespan.
"""
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.core.config import settings
from app.core import backend_client
from app.api.routes import api_router


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Startup: create the shared httpx client
    backend_client.create_client()
    yield
    # Shutdown: close gracefully
    await backend_client.close_client()


def create_app() -> FastAPI:
    app = FastAPI(
        title=settings.PROJECT_NAME,
        version=settings.VERSION,
        description="AI-powered CV generation pipeline — consumes data from the ASP.NET backend.",
        docs_url="/docs",
        redoc_url="/redoc",
        lifespan=lifespan,
    )

    # CORS — update origins as needed
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    # Mount all agent routers under /api/v1
    app.include_router(api_router)

    @app.get("/health", tags=["Health"])
    async def health_check():
        return {"status": "ok", "version": settings.VERSION}

    return app


app = create_app()
#cd CV-Generator/ai_agents/services/cv-optimizer