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
