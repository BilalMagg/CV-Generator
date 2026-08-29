import logging
from fastapi import APIRouter, HTTPException
from agents.search.schemas import (
    ProfileMatchRequest,
    ProfileMatchResponse,
    SearchRequest,
    SearchResponse,
    SearchResultItem,
)
from agents.search.agent import match_candidate_profile, search_similar_cv_content

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/agents/search", tags=["search"])


@router.post("/match", response_model=ProfileMatchResponse)
async def match_profile(request: ProfileMatchRequest):
    """Profile matcher: .NET SearchInput → SearchOutput (matched_skills,
    matched_experiences, matched_projects, gap_skills, match_score)."""
    try:
        result = await match_candidate_profile(request.user_id, request.job_requirements)
        return ProfileMatchResponse(**result)
    except Exception as e:
        logger.exception("Profile matching failed")
        raise HTTPException(status_code=500, detail=f"Match failed: {str(e)}")


@router.post("/cv", response_model=SearchResponse)
async def search_cv(request: SearchRequest):
    try:
        results = await search_similar_cv_content(
            request.query, request.workflow_id, request.user_id
        )
        return SearchResponse(results=results, query=request.query)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Search failed: {str(e)}")


@router.get("/health")
async def health_check():
    return {"status": "ok", "service": "search-agent"}
