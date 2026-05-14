"""
main.py — Job Crawler Agent entry point.

Architecture:
  - FastAPI app exposes a /health endpoint (required for Docker healthchecks).
  - On startup the Kafka producer + consumer are initialised.
  - The consumer loop runs as a background asyncio.Task, staying alive
    for the entire service lifetime.
  - On shutdown the consumer task is cancelled, Kafka connections are closed.
"""
from __future__ import annotations

import asyncio
import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.core.config import settings
from app.kafka_client import close_kafka, create_consumer, create_producer
from app.consumer import consume_loop, _stop_consumer
from app.routers import router

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)-8s | %(name)s | %(message)s",
)
logger = logging.getLogger(__name__)

# Module-level reference so we can cancel on shutdown
_consumer_task: asyncio.Task | None = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global _consumer_task

    logger.info("Job Crawler service starting up")

    # ── Kafka setup ───────────────────────────────────────────────────────────
    create_producer()
    create_consumer()

    # ── Launch background consumer as an asyncio Task ─────────────────────────
    _consumer_task = asyncio.create_task(consume_loop(), name="kafka-consumer")

    yield  # ── Service is alive here ─────────────────────────────────────────

    # ── Graceful shutdown ─────────────────────────────────────────────────────
    logger.info("Job Crawler service shutting down")
    _stop_consumer()

    if _consumer_task and not _consumer_task.done():
        _consumer_task.cancel()
        try:
            await _consumer_task
        except asyncio.CancelledError:
            pass

    close_kafka()
    logger.info("Job Crawler service stopped.")


def create_app() -> FastAPI:
    app = FastAPI(
        title=settings.SERVICE_NAME,
        version=settings.VERSION,
        description="Kafka-driven job scraping agent. Listens on 'trigger-live-crawl', fans out to 'raw-job-urls'.",
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
