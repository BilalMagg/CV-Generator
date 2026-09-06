import datetime
import json
import logging
import re
from typing import Any, Optional

from shared.llm import get_llm, ainvoke_with_fallback
from agents.autofill.prompt import get_autofill_messages
from agents.autofill.schemas import AutofillRequest, AutofillResponse, FieldDef

logger = logging.getLogger(__name__)


def _strip_json_fences(text: str) -> str:
    """Take a model reply and extract the JSON payload (handles ```json fences and prose)."""
    text = text.strip()
    fence = re.match(r"^```(?:json)?\s*(.*?)\s*```$", text, re.DOTALL)
    if fence:
        return fence.group(1).strip()
    start, end = text.find("{"), text.rfind("}")
    if start != -1 and end > start:
        return text[start : end + 1]
    return text


def _parse_json(content: str) -> dict:
    raw = _strip_json_fences(content)
    try:
        data = json.loads(raw)
    except json.JSONDecodeError as e:
        logger.error("Autofill: invalid JSON from model (snippet: %r)", raw[:300])
        raise RuntimeError(f"Autofill: model returned invalid JSON: {e}. Snippet: {raw[:200]!r}") from None
    if not isinstance(data, dict):
        raise RuntimeError("Autofill: model output was not a JSON object")
    return data


def _normalize_date(val: Any) -> str:
    s = str(val).strip()
    if not s:
        return ""
    # Already a clean date?
    if re.fullmatch(r"\d{4}-\d{2}-\d{2}", s):
        return s
    # yyyy/mm/dd or yyyy.mm.dd
    m = re.search(r"(\d{4})[/.-](\d{1,2})[/.-](\d{1,2})", s)
    if m:
        y, mo, d = m.groups()
        try:
            return f"{y}-{int(mo):02d}-{int(d):02d}"
        except ValueError:
            return ""
    # A month name
    for fmt in ("%B %Y", "%b %Y", "%Y-%m-%d"):
        try:
            return datetime.datetime.strptime(s, fmt).strftime("%Y-%m-%d")
        except ValueError:
            continue
    # Just a year -> Jan 1
    m = re.fullmatch(r"(\d{4})", s)
    if m:
        return f"{m.group(1)}-01-01"
    return s


def _coerce(field: FieldDef, val: Any) -> Any:
    ft = (field.type or "text").lower()
    # Missing / empty marker -> leave blank
    if val is None:
        return None if ft == "number" else ""
    if isinstance(val, str) and not val.strip():
        return None if ft == "number" else ""

    if ft == "number":
        if isinstance(val, (int, float)):
            return val
        m = re.search(r"-?\d+(?:\.\d+)?", str(val).replace(",", "."))
        if not m:
            return None
        try:
            return float(m.group(0))
        except ValueError:
            return None

    if ft == "date":
        return _normalize_date(val)

    s = str(val).strip()

    if ft in ("select", "choice") and field.options:
        low = {str(o).strip().lower(): o for o in field.options}
        exact = low.get(s.lower())
        if exact is not None:
            return exact
        # fuzzy match
        best = None
        for token, original in low.items():
            if token in s.lower() or s.lower() in token:
                best = original
                break
        return best if best is not None else s

    # bool-ish
    if ft == "checkbox" or ft == "boolean":
        low = s.lower()
        if low in ("yes", "true", "1", "on"):
            return True
        if low in ("no", "false", "0", "off"):
            return False
        return s

    # list -> join into a readable string for textarea fields
    if isinstance(val, list):
        return ", ".join(str(v).strip() for v in val if str(v).strip())

    return s


async def autofill(req: AutofillRequest) -> AutofillResponse:
    messages = get_autofill_messages(req.text, req.entity_type, req.fields)
    _, content = await ainvoke_with_fallback(
        messages,
        preferred_provider=req.provider,
        model=req.model,
        temperature=0.2,
        max_tokens=1024,
    )
    data = _parse_json(content)

    values: dict[str, Any] = {}
    for field in req.fields:
        if field.name in data:
            values[field.name] = _coerce(field, data[field.name])

    return AutofillResponse(values=values)
