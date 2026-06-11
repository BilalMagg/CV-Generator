from fastapi import APIRouter, status
from app.agent import deliver_cv, generate_email_content
from app.schemas import ContactInput, ContactOutput, GenerateEmailInput, GenerateEmailOutput

router = APIRouter()


@router.post("/deliver", response_model=ContactOutput, status_code=status.HTTP_200_OK)
async def deliver(input_data: ContactInput) -> ContactOutput:
    return await deliver_cv(input_data)


@router.post("/generate", response_model=GenerateEmailOutput, status_code=status.HTTP_200_OK)
async def generate(input_data: GenerateEmailInput) -> GenerateEmailOutput:
    return await generate_email_content(input_data)


@router.get("/health")
async def health():
    return {"status": "ok", "service": "contact-agent"}