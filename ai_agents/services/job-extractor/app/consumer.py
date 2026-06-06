from __future__ import annotations

import asyncio
import json
import logging
from typing import Optional

from confluent_kafka import KafkaError

from app.agent import extract_job_requirements
from app.core import backend_client
from app.core.config import settings
from app.schemas import ExtractorInput

logger = logging.getLogger(__name__)

_running: bool = True


def stop_consumer() -> None:
    global _running
    _running = False


def _poll_once() -> object | None:
    from confluent_kafka import Consumer
    global _consumer
    return _consumer.poll(timeout=1.0)


def _commit() -> None:
    global _consumer
    _consumer.commit(asynchronous=False)


_consumer: Consumer | None = None


def create_consumer() -> None:
    global _consumer
    from confluent_kafka import Consumer
    _consumer = Consumer({
        "bootstrap.servers": settings.KAFKA_BOOTSTRAP_SERVERS,
        "group.id": settings.KAFKA_GROUP_ID,
        "auto.offset.reset": "earliest",
        "enable.auto.commit": False,
        "allow.auto.create.topics": True,
    })
    _consumer.subscribe([settings.CONSUME_TOPIC])
    logger.info(
        "Kafka consumer subscribed to topic=%s (group=%s)",
        settings.CONSUME_TOPIC,
        settings.KAFKA_GROUP_ID,
    )


def close_consumer() -> None:
    global _consumer
    if _consumer:
        _consumer.close()
        logger.info("Kafka consumer closed.")
        _consumer = None


async def process_message(raw: dict) -> None:
    search_id = raw.get("search_id")
    job_url = raw.get("job_url") or ""
    raw_description = raw.get("raw_description") or ""
    title = raw.get("title") or ""
    company = raw.get("company") or ""
    location = raw.get("location") or ""
    source = raw.get("source", "unknown")

    input_text = raw_description or job_url

    extracted = None
    error_msg = None

    if input_text:
        try:
            extract_input = ExtractorInput(
                text=input_text,
                url=job_url if not raw_description else None,
            )
            extracted = await extract_job_requirements(extract_input)
            logger.info(
                "Extraction succeeded | search_id=%s job=%s company=%s",
                search_id, title, company,
            )
        except Exception as exc:
            error_msg = str(exc)
            logger.error(
                "Extraction failed | search_id=%s job=%s company=%s error=%s",
                search_id, title, company, error_msg,
            )
    else:
        error_msg = "No job description or URL provided"

    payload = {
        "enterpriseName": company,
        "enterpriseDescription": None,
        "jobRole": title,
        "rawDescription": raw_description,
        "responsibilities": [],
        "requiredSkills": [],
        "softSkills": [],
        "requiredExperienceYears": None,
        "seniorityLevel": None,
        "employmentType": None,
        "location": location,
        "locationType": None,
        "educationRequirements": None,
        "benefits": [],
        "sourceUrl": job_url,
        "searchId": search_id,
        "source": source,
        "overallConfidence": 0.0,
    }

    if extracted is not None:
        payload["enterpriseName"] = extracted.enterprise_name or company
        payload["enterpriseDescription"] = extracted.enterprise_description
        payload["jobRole"] = extracted.job_role or title
        payload["rawDescription"] = extracted.raw_description or raw_description
        payload["responsibilities"] = extracted.responsibilities
        payload["requiredSkills"] = extracted.required_skills
        payload["softSkills"] = extracted.soft_skills
        payload["requiredExperienceYears"] = extracted.required_experience_years
        payload["seniorityLevel"] = extracted.seniority_level
        payload["employmentType"] = extracted.employment_type
        payload["location"] = extracted.location or location
        payload["locationType"] = extracted.location_type
        payload["educationRequirements"] = extracted.education_requirements
        payload["benefits"] = extracted.benefits
        payload["sourceUrl"] = extracted.source_url or job_url
        payload["overallConfidence"] = extracted.overall_confidence

    client = backend_client.get_client()
    try:
        resp = await client.post("/api/job-offers/from-crawler", json=payload)
        if resp.is_success:
            logger.info(
                "Posted to from-crawler | search_id=%s job_id=%s",
                search_id, resp.json().get("data"),
            )
        else:
            logger.error(
                "from-crawler returned %d | search_id=%s body=%s",
                resp.status_code, search_id, resp.text,
            )
    except Exception as exc:
        logger.error(
            "Failed to POST to from-crawler | search_id=%s error=%s",
            search_id, exc,
        )


async def consume_loop() -> None:
    global _consumer

    logger.info(
        "Consumer loop started — listening on topic '%s'",
        settings.CONSUME_TOPIC,
    )

    while _running:
        msg = await asyncio.to_thread(_poll_once)

        if msg is None:
            continue

        if msg.error():
            if msg.error().code() == KafkaError._PARTITION_EOF:
                logger.debug("Reached end of partition — waiting for new messages.")
            else:
                logger.error("Kafka consumer error: %s", msg.error())
            continue

        try:
            raw = json.loads(msg.value().decode("utf-8"))
            logger.info(
                "Received raw job | search_id=%s title=%s company=%s",
                raw.get("search_id"), raw.get("title"), raw.get("company"),
            )
        except Exception as exc:
            logger.error("Failed to parse raw-job-urls message: %s", exc)
            await asyncio.to_thread(_commit)
            continue

        try:
            await process_message(raw)
        except Exception as exc:
            logger.error("Unhandled error processing message: %s", exc)

        await asyncio.to_thread(_commit)

    logger.info("Consumer loop exited cleanly.")
