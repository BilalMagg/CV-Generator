import json
import logging
import re
from typing import List
from langchain_core.messages import SystemMessage, HumanMessage
from agents.apply_prep.schemas import (
    FormResponsesRequest,
    MessageRequest,
    FormResponsesResponse,
    FormResponseItem,
    MessageResponse,
)

logger = logging.getLogger(__name__)

_THINK_RE = re.compile(r"<think>.*?</think>", re.DOTALL | re.IGNORECASE)


def _clean_llm_output(text: str) -> str:
    if not text:
        return ""
    # 1) Drop <think>…</think> reasoning blocks emitted by some models.
    text = _THINK_RE.sub("", text)
    # 2) Drop ```json / ``` code fences if the model wrapped the JSON.
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
    """Return the first {...} span so any stray prose around the JSON is ignored."""
    start = text.find("{")
    end = text.rfind("}")
    if start != -1 and end != -1 and end > start:
        return text[start:end + 1]
    return text


def _section(title: str, items, fields) -> str:
    if not items:
        return ""
    parts = [f"=== {title} ==="]
    for it in items:
        line = " | ".join(str(getattr(it, f, "") or "") for f in fields if getattr(it, f, ""))
        if line.strip():
            parts.append(line)
    return "\n".join(parts) + "\n"


async def collect_cv_context(user_id: str) -> str:
    """Fetch the candidate's structured profile directly (no @tool wrapper) so a failure in
    one section cannot silently drop the whole CV context."""
    try:
        from shared.backend_client import (
            get_user_experiences,
            get_user_projects,
            get_user_skills,
            get_user_educations,
            get_user_languages,
            get_user_certifications,
        )
    except Exception as e:  # pragma: no cover - import guard
        logger.warning("apply-prep: backend client import failed: %s", e)
        return ""

    async def safe(name, fn):
        try:
            return await fn(user_id)
        except Exception as ex:
            logger.warning("apply-prep: %s fetch failed: %s", name, ex)
            return None

    experiences = await safe("experiences", get_user_experiences)
    projects = await safe("projects", get_user_projects)
    skills = await safe("skills", get_user_skills)
    educations = await safe("educations", get_user_educations)
    languages = await safe("languages", get_user_languages)
    certifications = await safe("certifications", get_user_certifications)

    cv = ""
    cv += _section("EXPERIENCE", experiences, ["title", "company", "description", "startDate", "endDate"])
    cv += _section("PROJECTS", projects, ["title", "description"])
    cv += _section("SKILLS", skills, ["name", "proficiency"])
    cv += _section("EDUCATION", educations, ["degree", "school", "field", "description"])
    cv += _section("LANGUAGES", languages, ["name", "level"])
    cv += _section("CERTIFICATIONS", certifications, ["name", "issuer"])
    return cv.strip()


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
    if not cv:
        logger.warning("apply-prep: no CV context for user %s", req.user_id)
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
    text = _extract_json_object(_clean_llm_output(content))
    if not text:
        return FormResponsesResponse(responses=[FormResponseItem(field="", answer=content)])
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
    if not cv:
        logger.warning("apply-prep: no CV context for user %s", req.user_id)
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
    text = _extract_json_object(_clean_llm_output(content))
    if not text:
        return content
    try:
        data = json.loads(text)
        return data.get("message", text)
    except Exception:
        return text
