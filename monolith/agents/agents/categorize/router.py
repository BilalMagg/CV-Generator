from fastapi import APIRouter
from agents.categorize.schemas import CategorizeRequest, CategorizeResponse
from agents.categorize.agent import categorize

router = APIRouter(prefix="/api/agents/categorize", tags=["categorize"])


@router.post("", response_model=CategorizeResponse)
async def categorize_endpoint(req: CategorizeRequest):
    ids = categorize(req.text, req.candidates, provider=req.provider, model=req.model)
    return CategorizeResponse(node_ids=ids)
