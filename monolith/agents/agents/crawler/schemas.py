from pydantic import BaseModel
from typing import Optional, List


class CrawlRequest(BaseModel):
    keyword: str
    location: str = ""
    result_limit: int = 20
    sites: List[str] = ["linkedin", "indeed"]


class JobResult(BaseModel):
    title: Optional[str] = None
    company: Optional[str] = None
    location: Optional[str] = None
    url: Optional[str] = None
    description: Optional[str] = None
    site: str = "unknown"


class CrawlResponse(BaseModel):
    keyword: str
    location: str
    total_results: int
    jobs: List[JobResult]
