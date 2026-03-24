"""FastAPI router — RAG Search  (POST /api/v1/search/match)"""
from fastapi import APIRouter, HTTPException

from app.api.schemas.search import SearchRequest, SearchResponse

router = APIRouter(prefix="/search", tags=["RAG Search"])


@router.post("/match", response_model=SearchResponse, summary="Match user profile against job requirements")
async def match_candidate(body: SearchRequest) -> SearchResponse:
    """
    Fetches the user's experiences, projects, and skills from the backend,
    then uses vector similarity to return the best matches for the job.
    """
    raise HTTPException(status_code=501, detail="RAG Search agent not yet implemented")
