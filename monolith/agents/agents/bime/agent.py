"""
BIME conversational agent — LangGraph react agent with tools.
Maintains conversation memory via the .NET backend API.
"""
import logging
from typing import Optional
from langchain_core.messages import SystemMessage, HumanMessage, AIMessage
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
)
from agents.bime.prompts import BIME_SYSTEM_PROMPT

logger = logging.getLogger(__name__)

TOOLS = [
    get_user_profile, list_applications, get_application_detail,
    list_companies, list_contacts, create_application,
    update_application_status, get_application_stats, list_schedules,
    get_entity_stats, search_profile,
    list_experiences, create_experience, update_experience, delete_experience,
    list_projects, create_project, update_project, delete_project,
    list_skills, create_skill, update_skill, delete_skill,
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
    Send a message to BIME. Returns {reply, conversation_id}.
    History is passed in from the .NET backend (stored in BimeMessage table).
    provider/model optionally override the resolved default LLM.
    """
    llm = get_llm(preferred_provider=provider, model=model)

    system = SystemMessage(content=BIME_SYSTEM_PROMPT)

    messages = [system]
    if history:
        for msg in history:
            if msg["role"] == "user":
                messages.append(HumanMessage(content=msg["content"]))
            elif msg["role"] == "assistant":
                messages.append(AIMessage(content=msg["content"]))

    messages.append(HumanMessage(content=f"[User ID: {user_id}] {message}"))

    agent = create_react_agent(llm, TOOLS)
    result = await agent.ainvoke({"messages": messages})

    ai_messages = [m for m in result["messages"] if isinstance(m, AIMessage)]
    reply = ai_messages[-1].content if ai_messages else "I'm sorry, I couldn't process that."

    return {"reply": reply, "conversation_id": conversation_id or ""}
