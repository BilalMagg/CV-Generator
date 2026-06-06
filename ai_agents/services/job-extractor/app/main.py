from contextlib import asynccontextmanager
import asyncio
import logging

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.core import backend_client
from app.core.config import settings
from app.consumer import consume_loop, create_consumer, close_consumer, stop_consumer
from app.routers import router

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

_consumer_task: asyncio.Task | None = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global _consumer_task

    logger.info("Job Extractor service starting up")
    backend_client.create_client()

    create_consumer()
    _consumer_task = asyncio.create_task(consume_loop(), name="kafka-consumer")

    yield

    logger.info("Job Extractor service shutting down")
    stop_consumer()

    if _consumer_task and not _consumer_task.done():
        _consumer_task.cancel()
        try:
            await _consumer_task
        except asyncio.CancelledError:
            pass

    close_consumer()
    await backend_client.close_client()


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
