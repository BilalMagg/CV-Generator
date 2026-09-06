import json
import logging
import re

from langchain_core.messages import SystemMessage, HumanMessage

from shared.llm.fallback import ainvoke_with_fallback
from agents.direct.schemas import (
    DirectChatRequest,
    DirectChatResponse,
    DirectMessageRequest,
    DirectMessageResponse,
)
from agents.direct.prompt import get_message_messages

logger = logging.getLogger(__name__)

_THINK_RE = re.compile(r" thinking.*? response", re.DOTALL | re.IGNORECASE)


def _clean(text: str) -> str:
    if not text:
        return ""
    text = _THINK_RE.sub("", text)
    text = text.strip()
    if text.startswith("```"):
        lines = text.split("\n")
        if lines and lines[0].strip().lower().startswith("```"):
            lines = lines[1:]
        if lines and lines[-1].strip().startswith("```"):
            lines = lines[:-1]
        text = "\n".join(lines).strip()
    return text.strip()


def _extract_json_object(text: str) -> str:
    start = text.find("{")
    end = text.rfind("}")
    if start != -1 and end != -1 and end > start:
        return text[start : end + 1]
    return text


async def generate_message(req: DirectMessageRequest) -> DirectMessageResponse:
    system, user = get_message_messages(req)
    _, content = await ainvoke_with_fallback(
        [SystemMessage(content=system), HumanMessage(content=user)],
        preferred_provider=req.provider,
        model=req.model,
        temperature=0.7,
        max_tokens=1024,
    )
    return _parse_message(content, req)


def _parse_message(content: str, req: DirectMessageRequest) -> DirectMessageResponse:
    text = _extract_json_object(_clean(content))
    subject, body = "", ""
    try:
        data = json.loads(text) if text else {}
        subject = str(data.get("subject", "") or "").strip()
        body = str(data.get("message", "") or "").strip()
    except Exception:
        # Not JSON -> treat the whole reply as the message body.
        body = content.strip()

    if not body:
        body = content.strip()

    return DirectMessageResponse(
        subject=subject,
        message=body,
        channel=req.channel,
        language=req.language,
    )


async def chat(req: DirectChatRequest) -> DirectChatResponse:
    messages: list = []
    if req.system:
        messages.append(SystemMessage(content=req.system))
    messages.append(HumanMessage(content=req.user))
    _, content = await ainvoke_with_fallback(
        messages,
        preferred_provider=req.provider,
        model=req.model,
        temperature=req.temperature,
        max_tokens=req.max_tokens,
    )
    return DirectChatResponse(text=content)
