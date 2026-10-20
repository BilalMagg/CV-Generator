from langchain_core.messages import SystemMessage

_SYSTEM = """You are a precise data-extraction assistant. The user pastes a messy list of
companies they want to apply to. Each entry typically has a company name, sometimes a
location, one or more email addresses, and a list of specialties / areas of activity.

Return a single JSON object of this exact shape:
{"rows": [
  {"company_name": "...", "location": "...", "email": "...", "contact_name": "...", "specialties": ["...", "..."]}
]}

Rules:
1. Emit ONE row per (company, email) pair. If a company lists several email addresses,
   create one row per email, repeating the company name — the caller sends one email each.
2. `company_name` is required for every row. Never invent a company that is not in the text.
3. `location` is the city/country if present; otherwise null.
4. `email` must be a real address found in the text; null when the entry has none.
5. `contact_name` is the named person if one is given; otherwise null.
6. `specialties` is an array of the areas of activity / recruitment specialties / job
   families listed for that company (e.g. "Data Science", "Software Engineering",
   "Marketing"). Empty array when none are listed. Split comma / slash / newline separated lists.
7. Ignore numbering, headers, footers, and any commentary that is not company data.
8. Return ONLY the JSON object — no markdown fences, no prose."""


def get_bulk_extract_messages(text: str, language: str = "English") -> list[SystemMessage]:
    if not text or not text.strip():
        raise ValueError("Empty text")
    prompt = (
        f"{_SYSTEM}\n\n"
        f"The text is in {language}. Extract from this list:\n{text}"
    )
    return [SystemMessage(content=prompt)]


_MAP_SYSTEM = """You match job-application email templates to a list of target companies
so the user can send a tailored message to each one.

You are given a JSON object:
{"rows": [{"company_name": "...", "location": "...", "specialty": "..."}],
 "templates": [{"id": "...", "name": "...", "subject_template": "...", "body_template": "...", "intent": "..."}]}

For EVERY row, choose the single template whose intent best fits that company's specialty /
role / location. Return a JSON object of this exact shape:
{"template_ids": ["<template id or empty string>", ...]}

Rules:
1. `template_ids` MUST have exactly one entry per input row, in the SAME order.
2. Use the exact `id` string of a candidate template. Never invent an id.
3. If no template is a reasonable fit for a row, return an empty string "" for that row.
   Do NOT force a bad match — a blank entry tells the caller the user must pick one.
4. Match on the company's specialty/role/domain first, then location as a tie-breaker.
   The template `intent` and `subject_template` describe what each template targets.
5. Return ONLY the JSON object — no markdown fences, no prose."""


def get_template_map_messages(rows: list[dict], templates: list[dict]) -> list[SystemMessage]:
    import json

    if not rows:
        raise ValueError("No rows to map")
    if not templates:
        raise ValueError("No templates to choose from")
    payload = json.dumps({"rows": rows, "templates": templates}, ensure_ascii=False)
    return [SystemMessage(content=f"{_MAP_SYSTEM}\n\nData:\n{payload}")]
