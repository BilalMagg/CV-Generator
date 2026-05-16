"""
kafka_client.py — Thin wrappers around confluent-kafka Producer and Consumer.

Both objects are held as module-level singletons and created / destroyed
inside the FastAPI lifespan, mirroring how backend_client.py manages
the httpx client in the other agents.
"""
from __future__ import annotations

import json
import logging

from confluent_kafka import Consumer, KafkaError, Producer
from confluent_kafka.admin import AdminClient, NewTopic

from app.core.config import settings

logger = logging.getLogger(__name__)

# ── Module-level singletons ───────────────────────────────────────────────────
_producer: Producer | None = None
_consumer: Consumer | None = None


# ── Topic Provisioning ────────────────────────────────────────────────────────

def ensure_topics_exist() -> None:
    """
    Pre-create all topics this service needs using the Admin API.
    This is more reliable than relying on auto-creation from consumer poll(),
    which can fail on the first attempt with UNKNOWN_TOPIC_OR_PART.
    """
    admin = AdminClient({"bootstrap.servers": settings.KAFKA_BOOTSTRAP_SERVERS})
    topics_to_create = [
        NewTopic(settings.CONSUME_TOPIC, num_partitions=1, replication_factor=1),
        NewTopic(settings.PRODUCE_TOPIC, num_partitions=1, replication_factor=1),
        NewTopic(settings.SUMMARY_TOPIC, num_partitions=1, replication_factor=1),
    ]
    result = admin.create_topics(topics_to_create)
    for topic, future in result.items():
        try:
            future.result()
            logger.info("Topic '%s' created successfully.", topic)
        except Exception as e:
            # TOPIC_ALREADY_EXISTS is perfectly normal on restart
            if "TOPIC_ALREADY_EXISTS" in str(e):
                logger.info("Topic '%s' already exists — skipping.", topic)
            else:
                logger.error("Failed to create topic '%s': %s", topic, e)


# ── Producer ─────────────────────────────────────────────────────────────────

def create_producer() -> Producer:
    global _producer
    _producer = Producer(
        {
            "bootstrap.servers": settings.KAFKA_BOOTSTRAP_SERVERS,
            "acks": "all",
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
    producer.poll(0)


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
            "enable.auto.commit": False,
            # Explicitly allow this client to trigger topic auto-creation on poll.
            # Newer confluent-kafka versions default this to False.
            "allow.auto.create.topics": True,
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
