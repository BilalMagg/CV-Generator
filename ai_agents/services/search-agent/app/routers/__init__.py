from fastapi import APIRouter, status
from app.agent import match_candidate_data
from app.schemas import SearchInput, SearchOutput

router = APIRouter()


@router.post("/match", response_model=SearchOutput, status_code=status.HTTP_200_OK)
async def match(input_data: SearchInput) -> SearchOutput:
    return await match_candidate_data(input_data)


@router.get("/health")
async def health():
    return {"status": "ok", "service": "search-agent"}