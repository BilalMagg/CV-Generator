"""
kafka_client.py — Thin wrappers around confluent-kafka Producer and Consumer.

Both objects are held as module-level singletons and created / destroyed
inside the FastAPI lifespan, mirroring how backend_client.py manages
the httpx client in the other agents.
"""
from __future__ import annotations

import json
import logging
from typing import Callable

from confluent_kafka import Consumer, KafkaError, KafkaException, Producer

from app.core.config import settings

logger = logging.getLogger(__name__)

# ── Module-level singletons ───────────────────────────────────────────────────
_producer: Producer | None = None
_consumer: Consumer | None = None


# ── Producer ─────────────────────────────────────────────────────────────────

def create_producer() -> Producer:
    global _producer
    _producer = Producer(
        {
            "bootstrap.servers": settings.KAFKA_BOOTSTRAP_SERVERS,
            "acks": "all",               # Wait for full replication before ack
            "retries": 5,
            "retry.backoff.ms": 500,
        }
    )
    logger.info("Kafka producer created (brokers=%s)", settings.KAFKA_BOOTSTRAP_SERVERS)
    return _producer


def get_producer() -> Producer:
    if _producer is None:
        raise RuntimeError("Kafka producer is not initialised. Did lifespan run?")
    return _producer


def flush_producer() -> None:
    if _producer:
        _producer.flush(timeout=10)


def produce_message(topic: str, payload: dict) -> None:
    """Serialise payload to JSON and produce it to *topic*."""
    producer = get_producer()
    raw = json.dumps(payload).encode("utf-8")
    producer.produce(topic, value=raw, callback=_delivery_report)
    producer.poll(0)  # Trigger delivery callbacks without blocking


def _delivery_report(err, msg) -> None:
    if err:
        logger.error("Kafka delivery failed: %s", err)
    else:
        logger.debug(
            "Message delivered to %s [partition %d]", msg.topic(), msg.partition()
        )


# ── Consumer ─────────────────────────────────────────────────────────────────

def create_consumer() -> Consumer:
    global _consumer
    _consumer = Consumer(
        {
            "bootstrap.servers": settings.KAFKA_BOOTSTRAP_SERVERS,
            "group.id": settings.KAFKA_GROUP_ID,
            "auto.offset.reset": "earliest",
            "enable.auto.commit": False,  # Manual commit after successful processing
        }
    )
    _consumer.subscribe([settings.CONSUME_TOPIC])
    logger.info(
        "Kafka consumer subscribed to topic=%s (group=%s)",
        settings.CONSUME_TOPIC,
        settings.KAFKA_GROUP_ID,
    )
    return _consumer


def get_consumer() -> Consumer:
    if _consumer is None:
        raise RuntimeError("Kafka consumer is not initialised. Did lifespan run?")
    return _consumer


def close_kafka() -> None:
    """Gracefully shut down producer and consumer."""
    global _producer, _consumer
    if _producer:
        _producer.flush(timeout=10)
        logger.info("Kafka producer flushed and closed.")
        _producer = None
    if _consumer:
        _consumer.close()
        logger.info("Kafka consumer closed.")
        _consumer = None
