import json
import logging
from typing import Annotated, TypedDict, Sequence, Optional
from uuid import UUID

from langchain_core.messages import BaseMessage, HumanMessage, SystemMessage, AIMessage
from langgraph.graph import StateGraph, END
from langgraph.graph.message import add_messages
from langgraph.prebuilt import ToolNode

from shared.backend_client import get_user, get_user_experiences, get_user_projects, get_user_skills
from shared.llm import get_llm
from agents.cv_optimizer.prompt import SYSTEM_PROMPT
from agents.cv_optimizer.db import save_message, get_conversation_messages, create_conversation

logger = logging.getLogger(__name__)


class AgentState(TypedDict):
    messages: Annotated[Sequence[BaseMessage], add_messages]
    user_id: str
    conversation_id: Optional[str]


def create_tool_graph(llm=None):
    from agents.cv_optimizer.tools import get_user_cv_content, analyze_cv_for_job, get_user_profile
    tools = [get_user_cv_content, analyze_cv_for_job, get_user_profile]
    if llm is None:
        llm = get_llm()
    llm = llm.bind_tools(tools)

    def agent_node(state: AgentState):
        messages = state["messages"]
        system_msg = SystemMessage(content=SYSTEM_PROMPT)
        full_messages = [system_msg] + list(messages)
        response = llm.invoke(full_messages)
        return {"messages": [response]}

    def should_continue(state: AgentState):
        last_message = state["messages"][-1]
        if hasattr(last_message, "tool_calls") and last_message.tool_calls:
            return "tools"
        return END

    graph = StateGraph(AgentState)
    graph.add_node("agent", agent_node)
    graph.add_node("tools", ToolNode(tools))
    graph.set_entry_point("agent")
    graph.add_conditional_edges("agent", should_continue, {"tools": "tools", END: END})
    graph.add_edge("tools", "agent")
    return graph.compile()


class CVOptimizerAgent:
    def __init__(self):
        self.graph = create_tool_graph()
        self._llm_provider = None
        self._llm_model = None

    def _ensure_graph(self, provider, model):
        if provider is None and model is None:
            return
        if provider == self._llm_provider and model == self._llm_model:
            return
        self.graph = create_tool_graph(get_llm(preferred_provider=provider, model=model))
        self._llm_provider = provider
        self._llm_model = model
        logger.info("cv-optimizer graph rebuilt for provider=%s model=%s", provider, model)

    async def chat(
        self, user_id: str, query: str, conversation_id: Optional[str] = None,
        workflow_id: Optional[str] = None, llm=None, provider=None, model=None,
    ) -> dict:
        self._ensure_graph(provider, model)
        if not conversation_id:
            conversation_id = create_conversation(user_id)
        history = get_conversation_messages(conversation_id)
        messages = []
        for msg in history:
            if msg["role"] == "user":
                messages.append(HumanMessage(content=msg["content"]))
            elif msg["role"] == "assistant":
                messages.append(AIMessage(content=msg["content"]))
        messages.append(HumanMessage(content=query))

        save_message(conversation_id, "user", query, user_id, msg_index=len(history))

        state = {"messages": messages, "user_id": user_id, "conversation_id": conversation_id}
        result = await self.graph.ainvoke(state)
        ai_message = result["messages"][-1]
        ai_content = ai_message.content if hasattr(ai_message, "content") else str(ai_message)

        save_message(
            conversation_id, "assistant", ai_content, user_id,
            token_count=len(ai_content.split()), msg_index=len(history) + 1,
        )

        return {
            "response": ai_content,
            "conversation_id": conversation_id,
            "messages": get_conversation_messages(conversation_id),
        }


agent = CVOptimizerAgent()
