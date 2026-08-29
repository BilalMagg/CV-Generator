import logging
from fastapi import APIRouter, HTTPException
from agents.contact.schemas import ContactRequest, ContactEmailResponse
from agents.contact.agent import contact_graph
from agents.contact.prompt import get_contact_email_messages
from langchain_core.messages import SystemMessage

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/agents/contact", tags=["contact"])


@router.post("/generate-email", response_model=ContactEmailResponse)
async def generate_contact_email(request: ContactRequest):
    try:
        from shared.backend_client import get_user
        user = await get_user(request.user_id)
        user_name = f"{user.firstName or ''} {user.lastName or ''}".strip() or "Applicant"
        messages = get_contact_email_messages(
            user_name, request.company_name,
            request.job_title or "Position", request.contact_type, request.language,
        )
        from shared.llm import get_llm
        llm = get_llm(preferred_provider=request.provider, model=request.model)
        response = await llm.ainvoke(messages)
        content = response.content
        lines = content.strip().split("\n")
        subject = lines[0].replace("Subject:", "").strip() if lines else "Job Application"
        body = "\n".join(lines[1:]).strip() if len(lines) > 1 else content
        return ContactEmailResponse(
            subject=subject, body=body, contact_type=request.contact_type, language=request.language,
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Email generation failed: {str(e)}")


@router.get("/health")
async def health_check():
    return {"status": "ok", "service": "contact-agent"}
