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


class LinkedInRequest(BaseModel):
    """Generate LinkedIn content (post / comment / message, NOT job-application messages).

    Tool-specific fields are optional for the other tools (each tool uses only its
    fields). Provider/model default to the global OpenRouter-first selection and can
    be overridden per user.
    """

    tool: str = "post"  # post | comment | message
    language: str = "English"
    tone: str = "professional"  # professional | enthusiastic | storytelling | honest
    length: str = "medium"  # short | medium | long
    variants: int = 1

    # post
    context_type: str = ""  # hackathon | project | event | achievement | learning | other | ""
    context: str = ""
    mentions: List[str] = []
    include_hashtags: bool = True
    hashtag_count: int = 3

    # comment
    target_text: str = ""  # the post/comment being replied to
    points: List[str] = []

    # message (non-apply, personal/professional networking)
    recipient_name: str = ""
    relationship: str = "network"  # network | colleague | alumni | event | other
    purpose: str = "introduction"  # introduction | thanks | follow-up | referral | coffee_chat | other
    recipient_context: str = ""
    sender_context: str = ""  # what the author wants to say about themselves / the reason

    provider: Optional[str] = None
    model: Optional[str] = None


class LinkedInVariant(BaseModel):
    title: str = ""  # post headline; empty for comments/messages
    text: str
    hashtags: str = ""  # post only, "#a #b" list


class LinkedInResponse(BaseModel):
    tool: str
    variants: List[LinkedInVariant]
