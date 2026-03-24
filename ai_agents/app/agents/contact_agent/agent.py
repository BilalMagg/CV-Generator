"""
Contact agent — logic to deliver the CV.
"""
from uuid import uuid4
from app.agents.contact_agent.schemas import ContactInput, ContactOutput

async def deliver_cv(input_data: ContactInput) -> ContactOutput:
    """
    TODO: Implement SMTP delivery logic.
    For now, returns success.
    """
    return ContactOutput(
        success=True,
        delivery_id=str(uuid4())
    )
