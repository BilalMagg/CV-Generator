import logging
import re
from typing import Any, List

from shared.llm import ainvoke_with_fallback
from shared.tools.json_utils import parse_llm_json
from agents.bulk_extract.prompt import get_bulk_extract_messages
from agents.bulk_extract.schemas import BulkExtractRequest, BulkExtractResponse, BulkRow

logger = logging.getLogger(__name__)

_EMAIL_RE = re.compile(r"[\w.+-]+@[\w-]+\.[\w.-]+")


def _clean(value: Any) -> str:
    return str(value).strip() if value is not None else ""


def _split_specialties(value: Any) -> List[str]:
    if value is None:
        return []
    if isinstance(value, list):
        parts = [str(v) for v in value]
    else:
        parts = re.split(r"[,;/|]", str(value))
    out: List[str] = []
    seen: set[str] = set()
    for p in parts:
        s = p.strip(" .-—–\t")
        if s and s.lower() not in seen:
            seen.add(s.lower())
            out.append(s)
    return out


def _normalize(data: dict) -> List[BulkRow]:
    raw_rows = data.get("rows")
    if not isinstance(raw_rows, list):
        return []

    rows: List[BulkRow] = []
    seen: set[tuple[str, str]] = set()
    for item in raw_rows:
        if not isinstance(item, dict):
            continue
        company = _clean(item.get("company_name") or item.get("company") or item.get("name"))
        if not company:
            continue
        # The model may return several emails in one field — fan out one row per email.
        raw_email = _clean(item.get("email") or item.get("emails"))
        emails = _EMAIL_RE.findall(raw_email) if raw_email else []
        location = _clean(item.get("location")) or None
        contact = _clean(item.get("contact_name") or item.get("contact")) or None
        specialties = _split_specialties(item.get("specialties") or item.get("specialty"))

        targets = emails or [""]
        for email in targets:
            key = (company.lower(), email.lower())
            if key in seen:
                continue
            seen.add(key)
            rows.append(BulkRow(
                company_name=company,
                location=location,
                email=email or None,
                contact_name=contact,
                specialties=specialties,
            ))
    return rows


async def extract_rows(req: BulkExtractRequest) -> BulkExtractResponse:
    messages = get_bulk_extract_messages(req.text, req.language)
    _, content = await ainvoke_with_fallback(
        messages,
        preferred_provider=req.provider,
        model=req.model,
        temperature=0.1,
        max_tokens=4096,
    )
    data = parse_llm_json(content)
    if not data:
        logger.warning("Bulk extract: unsalvageable JSON; full content:\n%s", content)
        return BulkExtractResponse(rows=[])
    return BulkExtractResponse(rows=_normalize(data))
