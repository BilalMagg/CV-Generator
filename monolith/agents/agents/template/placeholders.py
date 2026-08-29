"""Template section placeholders: parser, known-key mapping, condition tokens.

Canonical annotation format (inert LaTeX comments, coexists with %=== BODY ===):

    %=== SECTION PLACEHOLDER ===
    % KEY: experience
    % TITLE: PROFESSIONAL EXPERIENCE
    % COND: maximum 5 entries
    % COND: each item max 120 characters
    % COND: reverse chronological order
    %=== EXAMPLE ===
    % \\experience{Web Developer (Internship)}{July -- September 2025}{...}{}{...}
    % {
    % \\begin{itemize}[leftmargin=*, itemsep=0pt, topsep=1pt, parsep=0pt]
    %     \\item Developed a web app for elderly people (MEAN stack)
    % \\end{itemize}
    % }
    %=== END EXAMPLE ===
    %=== END PLACEHOLDER ===

KEY values map onto the fixed CvSections model the CV section builder fills.
Keys with no backing data source are rendered FREE-FORM by the LLM following
the EXAMPLE style. Render-time conditions are enforced deterministically (hard
limits) and also surfaced to the LLM as a soft hint so content is shaped before
trimming.
"""
from dataclasses import dataclass, field
import re
from typing import Any, List, Optional

BLOCK_RE = re.compile(
    r"%=== SECTION PLACEHOLDER ===(.*?)%=== END PLACEHOLDER ===",
    re.DOTALL | re.IGNORECASE,
)
_EXAMPLE_RE = re.compile(
    r"%=== EXAMPLE ===(.*?)%=== END EXAMPLE ===",
    re.DOTALL | re.IGNORECASE,
)
_KEY_RE = re.compile(r"^\s*%\s*KEY:\s*(.+?)\s*$", re.IGNORECASE | re.MULTILINE)
_TITLE_RE = re.compile(r"^\s*%\s*TITLE:\s*(.+?)\s*$", re.IGNORECASE | re.MULTILINE)
_COND_RE = re.compile(r"^\s*%\s*COND:\s*(.+?)\s*$", re.IGNORECASE | re.MULTILINE)

# Keys the renderer knows how to fill. `header` is recognized but not rendered
# as a section (it drives the top-of-document header block). Keys without a
# CvSections backing (certifications, publications, ...) are rendered free-form
# by the LLM from the block's EXAMPLE + profile/job material; the editor flags
# only free-form blocks that carry NO example.
KNOWN_KEYS = {
    "summary", "about",
    "skills",
    "experience", "experiences",
    "education", "educations",
    "projects",
    "hackathons",
    "languages",
    "soft-skills", "softskills", "soft_skills",
    "interests",
    "extracurricular",
    "header",
}

_COND_MAX_CHARS = re.compile(r"max(?:imum)?\s*(\d+)\s*(?:chars?|characters)", re.IGNORECASE)
_COND_MAX_WORDS = re.compile(r"max(?:imum)?\s*(\d+)\s*words?", re.IGNORECASE)
_COND_MAX_ITEMS = re.compile(
    r"max(?:imum)?\s*(\d+)\s*(?:items?|entries?|projects?|bullets?|points?)",
    re.IGNORECASE,
)
_COND_PER_ITEM_CHARS = re.compile(
    r"(?:each|per)\s+item\s+max(?:imum)?\s*(\d+)\s*(?:chars?|characters)",
    re.IGNORECASE,
)
_COND_PER_ITEM_WORDS = re.compile(
    r"(?:each|per)\s+item\s+max(?:imum)?\s*(\d+)\s*words?",
    re.IGNORECASE,
)
_COND_BULLETS = re.compile(r"bullet|bulleted|list\s+items", re.IGNORECASE)
_COND_INLINE = re.compile(r"comma|inline|one\s+line|separated", re.IGNORECASE)
_COND_REVERSE = re.compile(
    r"reverse.*chronolog|chronolog.*reverse|most\s+recent|newest|latest",
    re.IGNORECASE,
)


@dataclass
class Conditions:
    max_chars: Optional[int] = None
    max_words: Optional[int] = None
    max_items: Optional[int] = None
    per_item_chars: Optional[int] = None
    per_item_words: Optional[int] = None
    format: str = "auto"          # auto | bullets | inline
    reverse_chronological: bool = False


@dataclass
class PlaceholderBlock:
    key: str
    title: str
    conditions: List[str] = field(default_factory=list)
    example: str = ""             # raw LaTeX (comment-prefix stripped)
    body: str = ""
    start: int = 0
    end: int = 0


def _extract_example(inner: str) -> str:
    """Strip `% `/`%` prefixes from the EXAMPLE block (inert comments)."""
    m = _EXAMPLE_RE.search(inner)
    if not m:
        return ""
    lines: List[str] = []
    for line in m.group(1).splitlines():
        lines.append(re.sub(r"^%\s?", "", line, count=1))
    return "\n".join(lines).strip()


def parse_placeholders(template_src: str) -> List[PlaceholderBlock]:
    """Return the declared placeholder blocks with their raw spans."""
    blocks: List[PlaceholderBlock] = []
    for m in BLOCK_RE.finditer(template_src):
        inner = m.group(1)
        key = (((_KEY_RE.search(inner)) or (None, ""))[1] or "").strip()
        title = (((_TITLE_RE.search(inner)) or (None, ""))[1] or "").strip()
        conds = [c.strip() for c in _COND_RE.findall(inner) if c.strip()]
        blocks.append(PlaceholderBlock(
            key=key,
            title=title,
            conditions=conds,
            example=_extract_example(inner),
            body=m.group(0),
            start=m.start(),
            end=m.end(),
        ))
    return blocks


def parse_conditions(lines: List[str]) -> Conditions:
    cond = Conditions()
    for line in (lines or []):
        m = _COND_PER_ITEM_CHARS.search(line)
        if m:
            cond.per_item_chars = int(m.group(1))
            continue
        m = _COND_PER_ITEM_WORDS.search(line)
        if m:
            cond.per_item_words = int(m.group(1))
            continue
        m = _COND_MAX_CHARS.search(line)
        if m:
            cond.max_chars = int(m.group(1))
            continue
        m = _COND_MAX_WORDS.search(line)
        if m:
            cond.max_words = int(m.group(1))
            continue
        m = _COND_MAX_ITEMS.search(line)
        if m:
            cond.max_items = int(m.group(1))
            continue
        if _COND_BULLETS.search(line):
            cond.format = "bullets"
            continue
        if _COND_INLINE.search(line):
            cond.format = "inline"
            continue
        if _COND_REVERSE.search(line):
            cond.reverse_chronological = True
    return cond


def match_section_data(key: str, sections) -> Optional[Any]:
    """Return the CvSections attribute backing a placeholder key (None if unknown)."""
    mapping = {
        "summary": "about",
        "about": "about",
        "skills": "skillGroups",
        "experience": "experiences",
        "experiences": "experiences",
        "education": "educations",
        "educations": "educations",
        "projects": "projects",
        "hackathons": "hackathons",
        "languages": "languages",
        "soft-skills": "softSkills",
        "softskills": "softSkills",
        "soft_skills": "softSkills",
        "interests": "interests",
        "extracurricular": "extracurricular",
    }
    attr = mapping.get((key or "").strip().lower())
    return getattr(sections, attr) if attr else None


def format_constraints_hint(template_src: str) -> str:
    """Serialise the declared section constraints for the LLM system prompt."""
    blocks = parse_placeholders(template_src)
    if not blocks:
        return ""
    lines = [
        "The user's chosen template declares these per-section constraints; "
        "respect them when shaping content:"
    ]
    MAX_EXAMPLE = 600  # per-block excerpt cap, keeps the prompt lean
    for b in blocks:
        label = b.title or b.key or "(unnamed)"
        conds = b.conditions or ["no explicit constraints"]
        lines.append(f"- {label} ({b.key or 'unknown key'}): " + "; ".join(conds))
        if b.example:
            excerpt = b.example.strip()[:MAX_EXAMPLE]
            lines.append(
                f"  Style reference for \"{label}\" (follow this shape/format, "
                "write YOUR OWN content from the candidate data, never copy "
                "this example's facts verbatim unless they trivially match):\n"
                + "\n".join(f"    {ln}" for ln in excerpt.splitlines())
            )
    lines.append(
        "Hard limits (character/item counts, list style) are enforced on render; "
        "write within them, prefer concise, quantifiable bullets."
    )
    return "\n".join(lines)