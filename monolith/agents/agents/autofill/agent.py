import datetime
import logging
import re
from typing import Any, Optional

from shared.llm import get_llm, ainvoke_with_fallback
from shared.tools.json_utils import parse_llm_json
from agents.autofill.prompt import get_autofill_messages
from agents.autofill.schemas import AutofillRequest, AutofillResponse, FieldDef

logger = logging.getLogger(__name__)


def _parse_json(content: str) -> dict:
    data = parse_llm_json(content)
    if not data:
        logger.warning("Autofill: model JSON unsalvageable; full content:\n%s", content)
        raise RuntimeError(
            f"Autofill: model returned unsalvageable JSON (snippet: {content[:200]!r})"
        )
    return data


_SKILL_ALIASES = ("skill", "technolog", "tool")

# Deterministic safety net: when the model leaves a skills-ish textarea empty, pull
# the technologies actually named in the description straight from the text. Order
# of appearance is preserved; duplicates collapsed; capped to keep the field sane.
# key = lowercase matcher, value = canonical display casing.
_TECH_LEXICON = {
    "java": "Java",
    "spring boot": "Spring Boot",
    "spring": "Spring",
    "javascript": "JavaScript",
    "typescript": "TypeScript",
    "angular": "Angular",
    "react": "React",
    "vue": "Vue",
    "node.js": "Node.js",
    "node": "Node",
    "python": "Python",
    "django": "Django",
    "flask": "Flask",
    "fastapi": "FastAPI",
    "kotlin": "Kotlin",
    "golang": "Go",
    "rust": "Rust",
    "php": "PHP",
    "ruby": "Ruby",
    "swift": "Swift",
    "c#": "C#",
    "c++": "C++",
    ".net": ".NET",
    "asp.net": "ASP.NET",
    "sql": "SQL",
    "postgresql": "PostgreSQL",
    "mysql": "MySQL",
    "mariadb": "MariaDB",
    "mongodb": "MongoDB",
    "redis": "Redis",
    "kafka": "Kafka",
    "rabbitmq": "RabbitMQ",
    "grpc": "gRPC",
    "graphql": "GraphQL",
    "docker": "Docker",
    "docker-compose": "Docker Compose",
    "kubernetes": "Kubernetes",
    "k8s": "Kubernetes",
    "nginx": "nginx",
    "terraform": "Terraform",
    "ansible": "Ansible",
    "aws": "AWS",
    "azure": "Azure",
    "gcp": "GCP",
    "git": "Git",
    "github": "GitHub",
    "gitlab": "GitLab",
    "jenkins": "Jenkins",
    "github actions": "GitHub Actions",
    "ci/cd": "CI/CD",
    "html": "HTML",
    "css": "CSS",
    "sass": "Sass",
    "tailwind": "Tailwind",
    "bootstrap": "Bootstrap",
    "microservices": "Microservices",
    "mapstruct": "MapStruct",
    "maven": "Maven",
    "gradle": "Gradle",
    "hadoop": "Hadoop",
    "spark": "Spark",
    "tensorflow": "TensorFlow",
    "pytorch": "PyTorch",
    "numpy": "NumPy",
    "pandas": "pandas",
    "scikit-learn": "scikit-learn",
    "matplotlib": "Matplotlib",
    "keycloak": "Keycloak",
    "oauth2": "OAuth2",
    "oauth": "OAuth",
    "jwt": "JWT",
    "uvicorn": "uvicorn",
    "pydantic": "Pydantic",
    "sqlalchemy": "SQLAlchemy",
    "alembic": "Alembic",
    "celery": "Celery",
    "ssr": "SSR",
}


def _extract_skills(text: str) -> str:
    lower = text.lower()
    found: list[str] = []
    seen: set[str] = set()
    for matcher, canonical in _TECH_LEXICON.items():
        pattern = re.compile(r"(?<![a-z0-9])" + re.escape(matcher) + r"(?![a-z0-9])")
        if pattern.search(lower):
            key = matcher.lower()
            if any(key != fk and key in fk for fk in seen):
                continue  # subsumed by a longer match already found ("Spring" inside "Spring Boot")
            if key not in seen:
                seen.add(key)
                found.append(canonical)
    return ", ".join(found[:12])


def _is_skill_field(field: FieldDef) -> bool:
    name = (field.name or "").lower()
    label = (field.label or "").lower()
    return any(alias in name or alias in label for alias in _SKILL_ALIASES)


def _normalize_date(val: Any) -> str:
    s = _strip_accents(str(val)).strip()
    if not s:
        return ""
    # Already a clean date?
    if re.fullmatch(r"\d{4}-\d{2}-\d{2}", s):
        try:
            mo, d = int(s[5:7]), int(s[8:10])
            if not (1 <= mo <= 12 and 1 <= d <= 31):
                return ""
        except ValueError:
            return ""
        return s
    # yyyy/mm/dd or yyyy.mm.dd
    m = re.search(r"(\d{4})[/.-](\d{1,2})[/.-](\d{1,2})", s)
    if m:
        try:
            y, mo, d = int(m.group(1)), int(m.group(2)), int(m.group(3))
            if not (1 <= mo <= 12 and 1 <= d <= 31):
                return ""
            return f"{y}-{mo:02d}-{d:02d}"
        except ValueError:
            return ""
    # yyyy-mm (first of month)
    m = re.fullmatch(r"(\d{4})[/.-](\d{1,2})", s)
    if m:
        try:
            y, mo = int(m.group(1)), int(m.group(2))
            if not 1 <= mo <= 12:
                return ""
            return f"{y}-{mo:02d}-01"
        except ValueError:
            return ""
    # day-first numeric: dd/mm/yyyy or dd.mm.yyyy (year-first already handled above)
    m = re.search(r"(\d{1,2})[/.-](\d{1,2})[/.-](\d{4})", s)
    if m:
        try:
            a, b, y = int(m.group(1)), int(m.group(2)), int(m.group(3))
            if a > 12:  # unambiguous day/month
                d, mo = a, b
            elif b > 12:  # unambiguous month/day
                mo, d = a, b
            else:  # ambiguous -> prefer EU day/month
                d, mo = a, b
            if not (1 <= mo <= 12 and 1 <= d <= 31):
                return ""
            return f"{y}-{mo:02d}-{d:02d}"
        except ValueError:
            return ""
    # month/year, with optional day and French/English month names:
    #   "juillet 2025", "7 juillet 2025", "1er septembre 2025",
    #   "Jul 2025", "July, 2025", "5th july 2025"
    m = _MONTH_RE.search(s)
    if m:
        try:
            day = int(m.group("day")) if m.group("day") else 1
            mo = _FRENCH_MONTHS[m.group("month").lower()]
            y = int(m.group("year"))
            if not (1 <= day <= 31 and 1 <= mo <= 12 and 1000 <= y <= 9999):
                return ""
            return f"{y}-{mo:02d}-{day:02d}"
        except (TypeError, KeyError, ValueError):
            return ""
    # Just a year -> Jan 1
    m = re.fullmatch(r"(\d{4})", s)
    if m:
        return f"{m.group(1)}-01-01"
    return ""


def _strip_accents(value: str) -> str:
    try:
        import unicodedata

        return "".join(
            c for c in unicodedata.normalize("NFD", value) if unicodedata.category(c) != "Mn"
        )
    except Exception:
        return value


_FRENCH_MONTHS = {
    "janvier": 1, "janv": 1, "january": 1, "jan": 1,
    "fevrier": 2, "fevr": 2, "february": 2, "feb": 2,
    "mars": 3, "march": 3, "mar": 3,
    "avril": 4, "april": 4, "apr": 4,
    "mai": 5, "may": 5,
    "juin": 6, "june": 6, "jun": 6,
    "juillet": 7, "juil": 7, "july": 7, "jul": 7,
    "aout": 8, "august": 8, "aug": 8,
    "septembre": 9, "sept": 9, "september": 9,
    "octobre": 10, "oct": 10, "october": 10,
    "novembre": 11, "nov": 11,
    "decembre": 12, "dec": 12, "december": 12,
}
# Drop non-numeric sentinels and keep tokens, longest first ("septembre" wins over "sept").
_MONTH_TOKENS = [k for k, v in _FRENCH_MONTHS.items() if v is not None]
_MONTH_RE = re.compile(
    r"(?P<day>\d{1,2})?\s*(?:er|e|nd|rd|th|st)?\s*"
    r"(?P<month>" + "|".join(sorted(_MONTH_TOKENS, key=len, reverse=True)) + r")"
    r"\s*(?:de\s+|[/.,-]\s*)?(?P<year>\d{4})",
    re.IGNORECASE,
)


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

    for field in req.fields:
        if _is_skill_field(field) and not values.get(field.name):
            values[field.name] = _extract_skills(req.text)

    return AutofillResponse(values=values)
