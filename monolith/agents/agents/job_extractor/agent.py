import json
import logging
import re
from typing import Optional

from shared.llm import get_llm
from shared.tools.pdf_utils import extract_text_from_pdf_url, get_chunks_from_text
from shared.tools.rag_utils import retrieve_context_from_pdf_url
from agents.job_extractor.prompt import get_job_extractor_messages, JOB_EXTRACTOR_PROMPT
from agents.job_extractor.schemas import JobExtractionResult

logger = logging.getLogger(__name__)


def extract_text_from_url(url: str) -> str:
    if "pdf" in url.lower():
        extracted = extract_text_from_pdf_url(url)
        if extracted:
            return extracted
    raise ValueError("Could not extract text from URL")


def _strip_json_fences(text: str) -> str:
    """Take a model reply and extract the JSON payload (handles ```json fences and prose)."""
    text = text.strip()
    fence = re.match(r"^```(?:json)?\s*(.*?)\s*```$", text, re.DOTALL)
    if fence:
        return fence.group(1).strip()
    start, end = text.find("{"), text.rfind("}")
    if start != -1 and end > start:
        return text[start:end + 1]
    return text


def _parse_job_result(content: str) -> JobExtractionResult:
    """Parse the model's text reply into JobExtractionResult. Provider-agnostic
    (no tool calling / JSON-mode requirement) — works on any chat model.
    """
    raw = _strip_json_fences(content)
    try:
        data = json.loads(raw)
    except json.JSONDecodeError as e:
        logger.error("Job extractor: invalid JSON from model (snippet: %r)", raw[:300])
        raise RuntimeError(
            f"Job extractor: model returned invalid JSON: {e}. Snippet: {raw[:200]!r}"
        ) from None
    if not isinstance(data, dict):
        raise RuntimeError("Job extractor: model output was not a JSON object")
    return JobExtractionResult.model_validate(data)


async def extract_job_from_url(url: str, language: str = "English", workflow_id: str = "", user_id: str = "", provider: Optional[str] = None, model: Optional[str] = None) -> JobExtractionResult:
    text = extract_text_from_url(url)
    return await extract_job_from_text(text, language, workflow_id, user_id, provider, model)


async def extract_job_from_text(text: str, language: str = "English", workflow_id: str = "", user_id: str = "", provider: Optional[str] = None, model: Optional[str] = None) -> JobExtractionResult:
    context = None
    if workflow_id and user_id:
        context = await retrieve_context_from_pdf_url(text, workflow_id, user_id)
    messages = get_job_extractor_messages(text, JOB_EXTRACTOR_PROMPT, context or "")
    llm = get_llm(preferred_provider=provider, model=model)
    response = await llm.ainvoke(messages)
    return _parse_job_result(response.content)


async def extract_job_from_text_chunks(chunks: list[str], language: str = "English", provider: Optional[str] = None, model: Optional[str] = None) -> JobExtractionResult:
    full_text = "\n\n".join(chunks)
    messages = get_job_extractor_messages(full_text, JOB_EXTRACTOR_PROMPT)
    llm = get_llm(preferred_provider=provider, model=model)
    response = await llm.ainvoke(messages)
    return _parse_job_result(response.content)