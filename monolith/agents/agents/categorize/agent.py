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


def _extract_json_array(text: str):
    """Best-effort extraction of a JSON array from an LLM response that may contain
    reasoning prose, markdown fences, or a wrapping object. Returns a list or None."""
    if not text:
        return None
    # 1) direct parse
    try:
        v = json.loads(text)
        if isinstance(v, list):
            return v
    except Exception:
        pass
    # 2) strip code fences then parse
    stripped = _strip_json_fences(text)
    try:
        v = json.loads(stripped)
        if isinstance(v, list):
            return v
    except Exception:
        pass
    # 3) find the outermost [ ... ] (works even when wrapped in prose / reasoning)
    start = stripped.find("[")
    end = stripped.rfind("]")
    if start != -1 and end != -1 and end > start:
        try:
            v = json.loads(stripped[start : end + 1])
            if isinstance(v, list):
                return v
        except Exception:
            pass
    # 4) maybe the model returned an object containing an array
    try:
        obj = json.loads(stripped)
        if isinstance(obj, dict):
            for val in obj.values():
                if isinstance(val, list):
                    return val
    except Exception:
        pass
    return None


def categorize(text: str, candidates: List[CandidateNode], provider: str = None, model: str = None) -> List[str]:
    """Pick the taxonomy node ids that best describe the entity text. Returns [] on any failure."""
    if not text or not candidates:
        return []

    indexed = list(enumerate(candidates, start=1))
    id_by_index = {i: c.id for i, c in indexed}
    name_to_id = {c.name.strip().lower(): c.id for c in candidates}
    all_ids = {c.id for c in candidates}

    lines = []
    for i, c in indexed:
        bits = [f"{i}. {c.name}"]
        path = (c.path or "").strip()
        if path:
            bits.append(f"[path: {path}]")
        aliases = [k for k in (c.keywords or []) if k]
        if aliases:
            bits.append(f"(aliases: {', '.join(aliases)})")
        lines.append(" ".join(bits))
    catalog = "\n".join(lines)

    prompt = (
        "You are a taxonomy categorization assistant. Given an entity description and a numbered taxonomy "
        "of category nodes, return the JSON array of node NUMBERS that best apply to the entity.\n\n"
        "Rules:\n"
        "- Be thorough and reason about what the entity IS and the broader domains, technologies, and "
        "skills it implies. For example, a technology like 'AWS', 'Docker' or 'Kubernetes' likely belongs "
        "under a 'DevOps' or 'Cloud' category even if those exact words never appear in the entity text.\n"
        "- Prefer the most specific (lowest-level / leaf) node that applies. Only choose a parent category "
        "when no more specific child fits.\n"
        "- A node applies if the entity, its meaning, or any of its aliases/keywords match. You may select "
        "multiple nodes.\n"
        "- Respond with ONLY a JSON array of integers (the node numbers), no prose, no explanation.\n\n"
        f"TAXONOMY ({len(candidates)} nodes):\n{catalog}\n\n"
        f"ENTITY DESCRIPTION:\n{text}\n\n"
        "Return the JSON array of applicable node numbers:"
    )

    try:
        llm = get_llm(preferred_provider=provider, model=model)
        raw = llm.invoke(prompt)
        content = getattr(raw, "content", str(raw)) or ""
        parsed = _extract_json_array(content)
        if not isinstance(parsed, list):
            return []
        result: List[str] = []
        for x in parsed:
            if isinstance(x, int) and x in id_by_index:
                result.append(id_by_index[x])
                continue
            if isinstance(x, str):
                s = x.strip()
                if s.isdigit() and int(s) in id_by_index:
                    result.append(id_by_index[int(s)])
                    continue
                key = s.lower()
                if key in name_to_id:
                    result.append(name_to_id[key])
                    continue
                if s in all_ids:
                    result.append(s)
                    continue
        # de-duplicate, preserve order
        seen = set()
        out = []
        for r in result:
            if r not in seen:
                seen.add(r)
                out.append(r)
        return out
    except Exception as exc:  # noqa: BLE001 - categorization is best-effort
        logger.warning("Categorization LLM failed: %s", exc)
    return []
