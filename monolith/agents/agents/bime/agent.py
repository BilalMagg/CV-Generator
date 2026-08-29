"""
BIME conversational agent — LangGraph react agent with tools.
Maintains conversation memory via the .NET backend API.
"""
import logging
from typing import Optional
from langchain_core.messages import SystemMessage, HumanMessage, AIMessage, ToolMessage
from langgraph.prebuilt import create_react_agent

from shared.llm import get_llm
from agents.bime.tools import (
    get_user_profile, list_applications, get_application_detail,
    list_companies, list_contacts, create_application,
    update_application_status, get_application_stats, list_schedules,
    get_entity_stats, search_profile,
    list_experiences, create_experience, update_experience, delete_experience,
    list_projects, create_project, update_project, delete_project,
    list_skills, create_skill, update_skill, delete_skill,
    list_certifications,
    get_taxonomy_tree, get_entity_tags, set_entity_tags, categorize_entities,
)
from agents.bime.prompts import BIME_SYSTEM_PROMPT

logger = logging.getLogger(__name__)


def _msg_text(message) -> str:
    """Extract plain-text content from an LangChain message (string or list-of-parts)."""
    content = getattr(message, "content", "")
    if isinstance(content, str):
        return content.strip()
    if isinstance(content, list):
        parts = []
        for part in content:
            if isinstance(part, dict) and part.get("type") == "text":
                parts.append(part.get("text", ""))
        return " ".join(parts).strip()
    return ""


def _clean(text: str) -> str:
    """Remove <think>...</think> reasoning blocks some models inject."""
    import re
    return re.sub(r"<think>.*?</think>", "", text, flags=re.DOTALL).strip()


def _extract_reply(messages) -> str:
    """Last AIMessage that carries real text content (ignores tool-call-only messages)."""
    for m in reversed(messages):
        if isinstance(m, AIMessage):
            text = _clean(_msg_text(m))
            if text:
                return text
    return ""


async def _summarize_fallback(messages, llm) -> str:
    """When the agent produced no prose, synthesize a short reply from tool results."""
    ctx: list[str] = []
    for m in messages:
        if isinstance(m, ToolMessage):
            ctx.append(f"Tool result: {str(m.content)[:2000]}")
        elif isinstance(m, HumanMessage) and not str(m.content).startswith("[User ID"):
            ctx.append(f"User: {m.content}")
    if not ctx:
        return "I've completed that for you."
    prompt = (
        "You are BIME. Based ONLY on the context below, write a short (1-3 sentence) "
        "reply to the user telling them what you found or did. Be concise and helpful.\n\n"
        + "\n".join(ctx)
    )
    try:
        resp = await llm.ainvoke(prompt)
        text = _clean(_msg_text(resp))
        return text or "I've completed that for you."
    except Exception:
        return "I've completed that action."


def _failure_message(err: Exception) -> str:
    """Turn a caught agent error into a helpful, non-crashing reply."""
    msg = str(err)
    lowered = msg.lower()
    if "413" in msg or "too large" in lowered or "payload" in lowered:
        return (
            "The current model's context window is too small for this conversation "
            "(the request exceeded its token limit). Please open the BIME model picker "
            "and choose a model with a larger context (e.g. a Llama-3.x model), then try again."
        )
    if "429" in msg or "rate" in lowered or "rate_limit" in lowered:
        return (
            "The model provider is rate-limiting requests right now. Please wait a few seconds "
            "and try again, or pick a different model in the BIME model picker."
        )
    return "I'm sorry, I ran into an error processing that. Please try again, or switch models in the BIME picker if it persists."


MAX_HISTORY = 8


def _build_messages(history, user_id, message, history_limit):
    messages = [SystemMessage(content=BIME_SYSTEM_PROMPT)]
    if history and history_limit > 0:
        for msg in history[-history_limit:]:
            if msg.get("role") == "user":
                messages.append(HumanMessage(content=msg["content"]))
            elif msg.get("role") == "assistant":
                messages.append(AIMessage(content=msg["content"]))
    messages.append(HumanMessage(content=f"[User ID: {user_id}] {message}"))
    return messages


TOOLS = [
    get_user_profile, list_applications, get_application_detail,
    list_companies, list_contacts, create_application,
    update_application_status, get_application_stats, list_schedules,
    get_entity_stats, search_profile,
    list_experiences, create_experience, update_experience, delete_experience,
    list_projects, create_project, update_project, delete_project,
    list_skills, create_skill, update_skill, delete_skill,
    list_certifications,
    get_taxonomy_tree, get_entity_tags, set_entity_tags, categorize_entities,
]


async def chat(
    message: str,
    user_id: str,
    conversation_id: Optional[str] = None,
    history: Optional[list[dict]] = None,
    provider: Optional[str] = None,
    model: Optional[str] = None,
) -> dict:
    """
    Send a message to BIME. Returns { reply, conversation_id }.
    History is passed in from the .NET backend (stored in BimeMessage table).
    provider/model optionally override the resolved default LLM.

    The request is retried with progressively shorter history so it fits small
    context windows (Groq free-tier models cap input tokens). Any failure is
    converted into a friendly reply — the endpoint never 500s.
    """
    llm = get_llm(preferred_provider=provider, model=model)

    last_err: Optional[Exception] = None
    # Try with the full recent history first, then shrink to bound token usage.
    for history_limit in (MAX_HISTORY, 4, 0):
        messages = _build_messages(history, user_id, message, history_limit)
        try:
            agent = create_react_agent(llm, TOOLS)
            result = await agent.ainvoke({"messages": messages})

            reply = _extract_reply(result["messages"])
            if not reply:
                reply = await _summarize_fallback(result["messages"], llm)
            if reply:
                return {"reply": reply, "conversation_id": conversation_id or ""}
            last_err = RuntimeError("agent produced no reply")
        except Exception as e:  # noqa: BLE001 - we want to degrade gracefully
            last_err = e
            logger.warning("BIME attempt failed (history_limit=%s): %s", history_limit, e)

    return {"reply": _failure_message(last_err), "conversation_id": conversation_id or ""}
