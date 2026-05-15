"""
consumer.py — Background Kafka consumer loop.

Runs as an asyncio Task started in the FastAPI lifespan.
Blocking confluent-kafka calls (poll, commit) are dispatched via
asyncio.to_thread so they never block the event loop.
"""
from __future__ import annotations

import asyncio
import json
import logging

from confluent_kafka import KafkaError

from app.core.config import settings
from app.kafka_client import get_consumer, produce_message, flush_producer
from app.scraper import scrape_jobs_sync
from app.schemas import CrawlTriggerEvent, RawJobPackageEvent

logger = logging.getLogger(__name__)

# Set to False by the lifespan shutdown handler to exit the loop cleanly.
_running: bool = True


def _stop_consumer() -> None:
    global _running
    _running = False


def _poll_once() -> object | None:
    """Poll for one message — runs in a thread."""
    return get_consumer().poll(timeout=1.0)


def _commit() -> None:
    """Commit the last consumed offset — runs in a thread."""
    get_consumer().commit(asynchronous=False)


async def consume_loop() -> None:
    """
    Infinite async loop that:
      1. Polls Kafka for a CrawlTriggerEvent.
      2. Dispatches the blocking JobSpy scrape to a thread.
      3. Fan-outs each scraped job as a RawJobPackageEvent to Kafka.
      4. Commits the offset only after all messages are produced.
    """
    logger.info("Consumer loop started — listening on topic '%s'", settings.CONSUME_TOPIC)

    while _running:
        # ── 1. Poll (non-blocking from event-loop perspective) ────────────────
        msg = await asyncio.to_thread(_poll_once)

        if msg is None:
            # Timeout — no new message, keep looping
            continue

        if msg.error():
            if msg.error().code() == KafkaError._PARTITION_EOF:
                logger.debug("Reached end of partition — waiting for new messages.")
            else:
                logger.error("Kafka consumer error: %s", msg.error())
            continue

        # ── 2. Deserialise & validate the trigger message ─────────────────────
        try:
            raw = json.loads(msg.value().decode("utf-8"))
            trigger = CrawlTriggerEvent.model_validate(raw)
        except Exception as exc:
            logger.error("Failed to parse trigger message: %s | raw=%s", exc, msg.value())
            # Commit to skip malformed messages — they'd poison the queue
            await asyncio.to_thread(_commit)
            continue

        logger.info(
            "Received crawl request | search_id=%s keyword=%r location=%r limit=%d",
            trigger.search_id,
            trigger.keyword,
            trigger.location,
            trigger.result_limit,
        )

        # ── 3. Scrape (blocking call dispatched to thread pool) ───────────────
        results_per_site = max(1, trigger.result_limit // 2)  # Split between 2 sites
        rows = await asyncio.to_thread(
            scrape_jobs_sync,
            trigger.keyword,
            trigger.location,
            results_per_site,
        )

        logger.info(
            "Scrape complete | search_id=%s total_jobs_found=%d",
            trigger.search_id,
            len(rows),
        )

        # ── 4. Fan-out: one Kafka message per job ─────────────────────────────
        published = 0
        for row in rows:
            try:
                package = RawJobPackageEvent(
                    search_id=str(trigger.search_id),
                    job_url=row.get("job_url"),
                    raw_description=row.get("description"),
                    title=row.get("title"),
                    company=row.get("company"),
                    location=row.get("location"),
                    source=str(row.get("site", "unknown")),
                )
                produce_message(settings.PRODUCE_TOPIC, package.model_dump())
                published += 1
            except Exception as exc:
                logger.error(
                    "Failed to produce job package | search_id=%s error=%s",
                    trigger.search_id,
                    exc,
                )

        flush_producer()
        logger.info(
            "Pushed %d jobs to Kafka topic '%s' | search_id=%s",
            published,
            settings.PRODUCE_TOPIC,
            trigger.search_id,
        )

        # ── 5. Commit offset — only after all messages are produced ───────────
        await asyncio.to_thread(_commit)
        logger.info("Offset committed | search_id=%s", trigger.search_id)

    logger.info("Consumer loop exited cleanly.")
