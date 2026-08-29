"""
JobSpy wrapper — cleaned for consolidated project.
"""
from __future__ import annotations
import logging
import math
from typing import Any
import pandas as pd

logger = logging.getLogger(__name__)


def _sanitize(value: Any) -> Any:
    if value is None:
        return None
    if isinstance(value, float) and (math.isnan(value) or math.isinf(value)):
        return None
    try:
        if pd.isna(value):
            return None
    except (TypeError, ValueError):
        pass
    return value


def _clean_row(row: dict) -> dict:
    return {k: _sanitize(v) for k, v in row.items()}


def scrape_jobs_sync(keyword: str, location: str, results_per_site: int) -> list[dict]:
    from jobspy import scrape_jobs
    logger.info("Scraping | keyword=%r location=%r results_per_site=%d", keyword, location, results_per_site)
    try:
        df: pd.DataFrame = scrape_jobs(
            site_name=["linkedin", "indeed"],
            search_term=keyword, location=location,
            results_wanted=results_per_site, hours_old=72, country_indeed="Morocco",
        )
    except Exception as exc:
        logger.error("JobSpy scrape failed: %s", exc)
        return []
    if df is None or df.empty:
        logger.warning("JobSpy returned no results for keyword=%r", keyword)
        return []
    logger.info("JobSpy returned %d raw rows", len(df))
    return [_clean_row(row) for row in df.to_dict(orient="records")]
