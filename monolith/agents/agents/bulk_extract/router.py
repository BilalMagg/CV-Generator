import logging

from fastapi import APIRouter, HTTPException

from agents.bulk_extract.agent import extract_rows, map_templates
from agents.bulk_extract.schemas import (
    BulkExtractRequest,
    BulkExtractResponse,
    TemplateMapRequest,
    TemplateMapResponse,
)

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


@router.post("/map-templates", response_model=TemplateMapResponse)
async def map_row_templates(request: TemplateMapRequest):
    try:
        if not request.rows:
            raise HTTPException(status_code=422, detail="No rows to map")
        if not request.templates:
            raise HTTPException(status_code=422, detail="No templates to choose from")
        return await map_templates(request)
    except HTTPException:
        raise
    except Exception as e:
        logger.exception("Template map failed")
        raise HTTPException(status_code=500, detail=f"Template map failed: {str(e)}")


@router.get("/health")
async def health_check():
    return {"status": "ok", "service": "bulk-extract"}
