"""FastAPI router — Contact / Delivery Agent  (POST /api/v1/contact/send)"""
from fastapi import APIRouter, HTTPException

from app.api.schemas.contact import ContactRequest, ContactResponse

router = APIRouter(prefix="/contact", tags=["CV Delivery"])


@router.post("/send", response_model=ContactResponse, summary="Send the optimised CV to the candidate")
async def send_cv(body: ContactRequest) -> ContactResponse:
    """
    Retrieves the finalised CV for the given job_id and delivers it
    to the candidate's email address.
    """
    raise HTTPException(status_code=501, detail="Contact agent not yet implemented")
