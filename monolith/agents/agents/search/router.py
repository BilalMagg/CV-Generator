import logging
from fastapi import APIRouter, HTTPException
from agents.search.schemas import (
    ProfileMatchRequest,
    ProfileMatchResponse,
)
from agents.search.agent import match_candidate_profile

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


@router.get("/health")
async def health_check():
    return {"status": "ok", "service": "search-agent"}
