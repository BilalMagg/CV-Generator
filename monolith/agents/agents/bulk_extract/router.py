import logging

from fastapi import APIRouter, HTTPException

from agents.bulk_extract.agent import extract_rows
from agents.bulk_extract.schemas import BulkExtractRequest, BulkExtractResponse

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/agents/bulk-extract", tags=["bulk-extract"])


@router.post("/rows", response_model=BulkExtractResponse)
async def extract_bulk_rows(request: BulkExtractRequest):
    try:
        if not request.text or not request.text.strip():
            raise HTTPException(status_code=422, detail="Empty text")
        return await extract_rows(request)
    except HTTPException:
        raise
    except Exception as e:
        logger.exception("Bulk extract failed")
        raise HTTPException(status_code=500, detail=f"Bulk extract failed: {str(e)}")


@router.get("/health")
async def health_check():
    return {"status": "ok", "service": "bulk-extract"}
