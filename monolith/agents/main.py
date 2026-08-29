"""
Propel Agents — consolidated FastAPI sidecar.

All 7 agents mounted on a single process (:8000).
"""
import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from shared.backend_client import create_client, close_client
from agents import all_routers

logging.basicConfig(level=logging.INFO, format="%(asctime)s | %(levelname)s | %(name)s: %(message)s")
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("Starting Propel Agents sidecar...")
    create_client()
    yield
    logger.info("Shutting down...")
    await close_client()


app = FastAPI(
    title="Propel Agents",
    version="2.0.0",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

for r in all_routers:
    app.include_router(r)


@app.get("/health")
async def health():
    return {"status": "ok", "service": "propel-agents"}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
