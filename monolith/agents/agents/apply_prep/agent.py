import json
import logging
from langchain_core.messages import SystemMessage, HumanMessage
from agents.apply_prep.schemas import (
    FormResponsesRequest,
    MessageRequest,
    FormResponsesResponse,
    FormResponseItem,
    MessageResponse,
)

logger = logging.getLogger(__name__)


def _strip_fences(text: str) -> str:
    t = text.strip()
    if t.startswith("```"):
        # drop leading ```json / ``` and trailing ```
        lines = t.split("\n")
        if lines and lines[0].strip().lower().startswith("```"):
            lines = lines[1:]
        if lines and lines[-1].strip().startswith("```"):
            lines = lines[:-1]
        t = "\n".join(lines).strip()
    return t


async def collect_cv_context(user_id: str) -> str:
    """Reuse the CV content collector from the cv_optimizer agent."""
    try:
        from agents.cv_optimizer.tools import get_user_cv_content
        return await get_user_cv_content(user_id)
    except Exception as e:  # pragma: no cover - best effort
        logger.warning("apply-prep: CV context collection failed: %s", e)
        return ""


def _job_block(req) -> str:
    parts = []
    if req.company_name:
        parts.append(f"Company: {req.company_name}")
    if req.job_role:
        parts.append(f"Role: {req.job_role}")
    if req.job_description:
        parts.append(f"Job description:\n{req.job_description}")
    if req.required_skills:
        parts.append("Required skills: " + ", ".join(req.required_skills))
    if req.responsibilities:
        parts.append("Responsibilities: " + "; ".join(req.responsibilities))
    return "\n".join(parts)


async def generate_form_responses(req: FormResponsesRequest) -> FormResponsesResponse:
    cv = await collect_cv_context(req.user_id)
    job = _job_block(req)
    fields_text = "\n".join(f"{i + 1}. {f}" for i, f in enumerate(req.fields))
    system = (
        "You are a career coach helping a candidate complete an external job-application form. "
        "Using the candidate's CV profile and the job information, answer each listed question "
        "concisely and professionally. Keep each answer to 1-3 sentences unless the question clearly "
        "needs more. Respond ONLY with a JSON object of the form "
        '{"responses": [{"field": <original question text>, "answer": <your answer>}]}. '
        "Do not add any commentary outside the JSON."
    )
    user = (
        f"JOB:\n{job}\n\nCANDIDATE CV:\n{cv}\n\n"
        f"QUESTIONS (answer each, in the same order):\n{fields_text}\n\n"
        f"Language: {req.language}"
    )
    from shared.llm import get_llm
    llm = get_llm(preferred_provider=req.provider, model=req.model)
    resp = await llm.ainvoke([SystemMessage(content=system), HumanMessage(content=user)])
    return _parse_form_responses(resp.content, req.fields)


def _parse_form_responses(content: str, fields: List[str]) -> FormResponsesResponse:
    text = _strip_fences(content)
    try:
        data = json.loads(text)
        items = data.get("responses", [])
        responses = [FormResponseItem(field=r.get("field", ""), answer=r.get("answer", "")) for r in items]
    except Exception:
        return FormResponsesResponse(responses=[FormResponseItem(field="", answer=content)])

    # Align counts if the model returned a different number of items.
    if len(responses) != len(fields):
        responses = [
            FormResponseItem(
                field=f,
                answer=next((r.answer for r in responses if r.field == f), ""),
            )
            for f in fields
        ]
    return FormResponsesResponse(responses=responses)


async def generate_message(req: MessageRequest) -> MessageResponse:
    cv = await collect_cv_context(req.user_id)
    job = _job_block(req)
    system = (
        "You are helping a candidate write a short, professional outreach message for a job application. "
        "Write a polite message addressed to the hiring contact for the role, grounded in the candidate's CV "
        "and the job. Keep it under ~150 words. Respond ONLY with a JSON object of the form "
        '{"message": <text>}. No commentary outside the JSON.'
    )
    user = (
        f"Channel: {req.channel}\n\nJOB:\n{job}\n\nCANDIDATE CV:\n{cv}\n\n"
        f"Extra considerations from the candidate: {req.considerations or 'None'}\n\n"
        f"Language: {req.language}"
    )
    from shared.llm import get_llm
    llm = get_llm(preferred_provider=req.provider, model=req.model)
    resp = await llm.ainvoke([SystemMessage(content=system), HumanMessage(content=user)])
    return MessageResponse(message=_extract_message(resp.content))


def _extract_message(content: str) -> str:
    text = _strip_fences(content)
    try:
        data = json.loads(text)
        return data.get("message", text)
    except Exception:
        return text
