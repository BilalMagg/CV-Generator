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

### 1. Separation of Concerns
- **Agent Package**: Pure logic. Should not know about FastAPI or HTTP status codes.
- **API Layer**: Handles HTTP validation, parsing, and error responses (`HTTPException`).

### 2. Async First
- All network calls (to backend or LLMs) and workflow steps must be `async`.
- Use the shared `httpx` client in `app.core.backend_client`.

### 3. Domain Models vs. Agent Schemas
- Use `app.models` for objects that cross multiple agent boundaries (like `UserResponse` or `JobRequirements`).
- Use `app.agents.<name>.schemas` for data strictly private to one agent's implementation.

### 4. External Tools
If you need a new tool (e.g., a web searcher or a specific PDF parser), add it to `app/core/`. Ensure it is configurable via `app/core/config.py`.
