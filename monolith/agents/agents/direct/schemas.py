from typing import List, Optional
from pydantic import BaseModel


class DirectChatRequest(BaseModel):
    """A generic direct LLM call: caller supplies the exact system + user prompts.

    This is the lightest AI surface — no agent graph, no tools — used for direct
    functionality like tweaking prose or summarizing. Provider/model default to
    the global OpenRouter-first selection.
    """

    system: str = ""
    user: str = ""
    temperature: float = 0.7
    max_tokens: int = 1024
    provider: Optional[str] = None
    model: Optional[str] = None


class DirectChatResponse(BaseModel):
    text: str


class DirectMessageRequest(BaseModel):
    """Generate an outreach/application message (job application or network contact).

    Direct (non-agent) path for composing short professional messages from a job
    and the candidate's CV context.
    """

    channel: str = "LinkedIn"
    job_role: str = ""
    company_name: str = ""
    job_description: str = ""
    required_skills: List[str] = []
    responsibilities: List[str] = []
    contact_type: str = "recruiter"
    recipient_name: str = ""
    candidate_context: str = ""
    considerations: str = ""
    language: str = "English"
    provider: Optional[str] = None
    model: Optional[str] = None


class DirectMessageResponse(BaseModel):
    subject: str = ""
    message: str
    channel: str = "LinkedIn"
    language: str = "English"
