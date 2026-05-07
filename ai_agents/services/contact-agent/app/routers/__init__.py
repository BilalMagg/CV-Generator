from fastapi import APIRouter, status
from app.agent import deliver_cv
from app.schemas import ContactInput, ContactOutput

router = APIRouter()


@router.post("/deliver", response_model=ContactOutput, status_code=status.HTTP_200_OK)
async def deliver(input_data: ContactInput) -> ContactOutput:
    return await deliver_cv(input_data)


@router.get("/health")
async def health():
    return {"status": "ok", "service": "contact-agent"}