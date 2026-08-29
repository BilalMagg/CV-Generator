import logging
from fastapi import APIRouter, HTTPException
from agents.crawler.schemas import CrawlRequest, CrawlResponse
from agents.crawler.agent import crawl_jobs

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/agents/crawler", tags=["crawler"])


@router.post("/jobs", response_model=CrawlResponse)
async def crawl(request: CrawlRequest):
    try:
        jobs = await crawl_jobs(request.keyword, request.location, request.result_limit)
        return CrawlResponse(
            keyword=request.keyword, location=request.location,
            total_results=len(jobs), jobs=jobs,
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Crawl failed: {str(e)}")


@router.get("/health")
async def health_check():
    return {"status": "ok", "service": "crawler"}
