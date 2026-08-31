import logging

from fastapi import APIRouter, HTTPException

from agents.autofill.agent import autofill
from agents.autofill.schemas import AutofillRequest, AutofillResponse

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/agents/autofill", tags=["autofill"])


@router.post("/extract", response_model=AutofillResponse)
async def extract_autofill(request: AutofillRequest):
    try:
        if not request.text or not request.text.strip():
            raise HTTPException(status_code=422, detail="Empty description text")
        result = await autofill(request)
        return result
    except HTTPException:
        raise
    except Exception as e:
        logger.exception("Autofill extraction failed")
        raise HTTPException(status_code=500, detail=f"Autofill failed: {str(e)}")


@router.get("/health")
async def health_check():
    return {"status": "ok", "service": "autofill"}
