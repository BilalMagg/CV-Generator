import json
import re
import logging
from typing import List
from agents.categorize.schemas import CandidateNode
from shared.llm.providers import get_llm

logger = logging.getLogger(__name__)


def _strip_json_fences(text: str) -> str:
    text = text.strip()
    if text.startswith("```"):
        text = re.sub(r"^```[a-zA-Z]*\n?", "", text)
        text = re.sub(r"\n?```$", "", text)
        text = text.strip()
    return text


def categorize(text: str, candidates: List[CandidateNode], provider: str = None, model: str = None) -> List[str]:
    """Pick the taxonomy node ids that best describe the entity text. Returns [] on any failure."""
    if not text or not candidates:
        return []

    catalog = "\n".join(f"- {c.id}: {c.name} (aliases: {', '.join(c.keywords)})" for c in candidates)

    prompt = (
        "You are a categorization assistant. Given an entity description and a taxonomy of category nodes, "
        "return the JSON array of node ids that best apply to the entity. Choose nodes that are clearly "
        "supported by the text; include parent nodes only when no specific child applies. Respond with ONLY "
        "a JSON array of strings, no prose.\n\n"
        f"TAXONOMY ({len(candidates)} nodes):\n{catalog}\n\n"
        f"ENTITY DESCRIPTION:\n{text}\n\n"
        "Return the JSON array of applicable node ids:"
    )

    try:
        llm = get_llm(preferred_provider=provider, model=model)
        raw = llm.invoke(prompt)
        content = getattr(raw, "content", str(raw))
        parsed = json.loads(_strip_json_fences(content))
        if isinstance(parsed, list):
            allowed = {c.id for c in candidates}
            return [str(x) for x in parsed if str(x) in allowed]
    except Exception as exc:  # noqa: BLE001 - categorization is best-effort
        logger.warning("Categorization LLM failed: %s", exc)
    return []
