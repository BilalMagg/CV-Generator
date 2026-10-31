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
    try:
        from shared.tools.latex_compile import tex_tooling_available
        info = tex_tooling_available()
        latex = {"latex_engine_available": bool(info.get("tex_available")),
                 "latex_engine": info.get("engine"),
                 "latex_engine_reason": info.get("reason")}
    except Exception:  # capability probe must never break the health check
        latex = {"latex_engine_available": False, "latex_engine": None, "latex_engine_reason": "probe failed"}
    return {"status": "ok", "service": "propel-agents", **latex}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
