# Developer Guide: Implementing AI Agents

This guide explains how to implement and extend agents within the `ai_agents/` service while respecting the established architecture.

## 📁 Directory Structure

```text
app/
├── agents/             # 🧠 Agent Logic (Core business logic)
│   └── <agent_name>/
│       ├── __init__.py
│       ├── agent.py    # Main async function(s) for the agent
│       └── schemas.py  # Internal agent I/O Pydantic models
├── api/                # 🌐 API Layer (FastAPI)
│   ├── routes/         # HTTP Routers per agent
│   ├── schemas/        # HTTP Request/Response Pydantic models
│   └── __init__.py     # Route aggregation
├── core/               # 🛠️ Shared Tools & Infrastructure
│   ├── llm.py          # LLM Factory (Groq, OpenAI, etc.)
│   ├── backend_client.py # Client for the ASP.NET API
│   └── config.py       # Pydantic Settings
├── models/             # 📄 Domain Models (Shared across the app)
└── workflows/          # 🔄 Orchestration (Multi-agent flows)
```

---

## 🛠️ How to Implement a New Agent

Follow these steps to add a new agent (e.g., `CoverLetterAgent`):

### 1. Define Internal Schemas
Create `app/agents/cover_letter_agent/schemas.py`:
- `CoverLetterInput`: Data required for the logic.
- `CoverLetterOutput`: What the agent returns.

### 2. Implement Agent Logic
Create `app/agents/cover_letter_agent/agent.py`:
- Use `async def` functions.
- Use the internal schemas for input/output.
- Access external tools (LLM, DB) via `app.core`.

### 3. Expose the API
- **HTTP Schema**: Create `app/api/schemas/cover_letter.py` defining `CoverLetterRequest` and `CoverLetterResponse`.
- **Router**: Create `app/api/routes/cover_letter.py`.
- **Register**: Add the new router to `app/api/routes/__init__.py`.

---

## 🏗️ Core Patterns to Respect

### 1. Dynamic Workflow Orchestration
The system now supports **dynamic workflows** controlled by the backend. 
- Use the `WorkflowContext` blackboard in `app/workflows/context.py` to share state between agents.
- The `DynamicOrchestrator` (`app/workflows/dynamic_orchestrator.py`) handles the execution of node sequences fetched from the backend.

### 2. Separation of Concerns
- **Agent Package**: Pure logic. Uses internal schemas for standard calls, but should be integrated into the `DynamicOrchestrator` by mapping from the `WorkflowContext`.
- **API Layer**: Handles HTTP validation and proxies to either the static or dynamic orchestrator.

### 3. Async First
- All network calls (to backend or LLMs) and workflow steps must be `async`.
- Use the shared `httpx` client in `app.core.backend_client`.

### 4. Shared Context Blackboard
When adding a new agent to a dynamic workflow:
1. Ensure the `WorkflowContext` has fields for your agent's requirements/outputs.
2. Update `DynamicOrchestrator.run_dynamic_workflow` to recognize your new node `type` and perform the necessary mapping.
