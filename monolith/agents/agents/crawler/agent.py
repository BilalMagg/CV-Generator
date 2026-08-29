import asyncio
import logging
from typing import List
from agents.crawler.schemas import JobResult
from agents.crawler.scraper import scrape_jobs_sync

logger = logging.getLogger(__name__)


async def crawl_jobs(keyword: str, location: str, result_limit: int = 20) -> List[JobResult]:
    raw_jobs = await asyncio.to_thread(scrape_jobs_sync, keyword, location, result_limit)
    results = []
    for job in raw_jobs:
        results.append(JobResult(
            title=job.get("title"),
            company=job.get("company"),
            location=job.get("location"),
            url=job.get("url") or job.get("job_url"),
            description=job.get("description") or job.get("raw_description"),
            site=job.get("site") or job.get("source", "unknown"),
        ))
    return results
