"""LaTeX CV body builder.

Fills an injectable LaTeX template (preamble + %=== BODY === marker) with the
structured CvSections, reusing the template's own macros (\\cvitem, \\experience,
\\formation) and styling so output matches the hand-written template.
"""
import logging
import os
import re
from pathlib import Path
from typing import Optional

from agents.template.schemas import CvEducation, CvExperience, CvSections, CvSkillGroup
from agents.template.placeholders import (
    PlaceholderBlock,
    match_section_data,
    parse_conditions,
    parse_placeholders,
)

logger = logging.getLogger(__name__)

BODY_MARKER = "%=== BODY ==="

_TEMPLATE_DIR = Path(__file__).resolve().parent / "templates"

_ESCAPE_RE = re.compile(r"([\\{}&%$#_~^])")

# Non-ASCII punctuation pdflatex (T1/latin) cannot typeset. Map to safe ASCII
# or LaTeX equivalents so LLM/author text never produces hard TeX errors.
_UNICODE_MAP = {
    "\u00a0": " ",                    # no-break space
    "\u202f": " ",                    # narrow no-break space (French spacing)
    "\u2011": "-",                    # non-breaking hyphen
    "\u2013": "--",                   # en dash
    "\u2014": "---",                  # em dash
    "\u2018": "'", "\u2019": "'",     # curly single quotes
    "\u201c": '"', "\u201d": '"',     # curly double quotes
    "\u2026": r"\ldots{}",            # ellipsis
    "\u2032": "'", "\u2033": '"',     # prime / double prime
}


def _sanitize_unicode(text: str) -> str:
    """Replace Unicode punctuation pdflatex can't render with ASCII/LaTeX."""
    if not text:
        return text
    for ch, repl in _UNICODE_MAP.items():
        if ch in text:
            text = text.replace(ch, repl)
    return text


def escape_latex(text: str) -> str:
    """Escape LaTeX special characters for display text."""
    if not text:
        return ""
    return _sanitize_unicode(_ESCAPE_RE.sub(r"\\\1", text))


def escape_url(url: str) -> str:
    """Escape a URL for \\href argument (the label is escaped separately)."""
    url = url.replace("\\", "")
    return url.replace("%", r"\%").replace("#", r"\#").replace("_", r"\_").replace("~", r"\~").replace("^", r"\^")


def load_template(template_name: str) -> str:
    """Load a bundled LaTeX template, falling back to 'default.tex'."""
    candidates = [Path(f"{template_name}.tex")]
    if not candidates[0].is_absolute():
        candidates[0] = _TEMPLATE_DIR / candidates[0]

    path = next((c for c in candidates if c.is_file()), _TEMPLATE_DIR / "default.tex")
    try:
        return path.read_text(encoding="utf-8")
    except OSError as e:
        logger.error("Failed to read template %s: %s", template_name, e)
        return (_TEMPLATE_DIR / "default.tex").read_text(encoding="utf-8")


def _build_header(sections: CvSections, photo_ref: Optional[str]) -> str:
    h = sections.header
    lines: list[str] = []
    lines.append("% Header")

    if photo_ref:
        lines.append(r"\begin{minipage}[c]{0.12\textwidth}")
        lines.append(r"    \includegraphics[width=\textwidth, height=\textwidth, keepaspectratio=false]{" + photo_ref + "}")
        lines.append(r"\end{minipage}")
        lines.append(r"\hfill")
        name_w, contact_w = "0.52\\textwidth", "0.33\\textwidth"
    else:
        name_w, contact_w = "0.62\\textwidth", "0.36\\textwidth"

    name = escape_latex(h.name)
    tagline = escape_latex(h.tagline)
    lines.append(r"\begin{minipage}[c]{" + name_w + "}")
    lines.append(r"    \centering")
    lines.append(r"    {\LARGE\color{darkblue}\textbf{" + name + "}}")
    if tagline:
        lines.append("    \\\\[1pt]")
        lines.append("    {\\small\\textbf{" + tagline + "}}")
    lines.append(r"\end{minipage}")

    contact_fields: list[str] = []
    if h.phone:
        contact_fields.append(r"\faPhone\ " + escape_latex(h.phone))
    if h.email:
        contact_fields.append(r"\faEnvelope\ \href{mailto:" + escape_url(h.email) + "}{" + escape_latex(h.email) + "}")
    if h.github:
        contact_fields.append(r"\faGithub\ \href{https://github.com/" + escape_url(h.github) + "}{" + escape_latex(h.github) + "}")
    if h.linkedin:
        contact_fields.append(r"\faLinkedin\ \href{https://www.linkedin.com/in/" + escape_url(h.linkedin) + "}{" + escape_latex(h.linkedin) + "}")

    if contact_fields or h.location:
        lines.append(r"\hfill")
        lines.append(r"\begin{minipage}[c]{" + contact_w + "}")
        lines.append(r"    \raggedleft\small")
        if h.location:
            lines.append("    " + escape_latex(h.location) + r"\\")
        for i, field in enumerate(contact_fields):
            suffix = r"\\" if i < len(contact_fields) - 1 else ""
            lines.append("    " + field + suffix)
        lines.append(r"\end{minipage}")

    lines.append("")
    lines.append(r"\vspace{3pt}")
    return "\n".join(lines)


def _itemize(bullets: list[str]) -> str:
    if not bullets:
        return ""
    inner = "\n".join(f"    \\item {escape_latex(b)}" for b in bullets)
    return (
        r"\begin{itemize}[leftmargin=*, itemsep=0pt, topsep=1pt, parsep=0pt]"
        "\n" + inner + "\n" +
        r"\end{itemize}"
    )


def _build_sections(sections: CvSections) -> str:
    blocks: list[str] = []

    about = escape_latex(sections.about).strip()
    if about:
        blocks.append(r"\section*{ABOUT ME}" + "\n" + about)

    experiences = [e for e in sections.experiences if (e.role or e.company)]
    if experiences:
        parts = []
        for e in experiences:
            bullets = _itemize(e.bullets)
            parts.append(
                "\\experience{"
                + escape_latex(e.role)
                + "}{" + escape_latex(e.dates)
                + "}{" + escape_latex(e.company)
                + "}{" + escape_latex(e.description)
                + "}{" + bullets + "}"
            )
        blocks.append(r"\section*{PROFESSIONAL EXPERIENCE}" + "\n" + "\n\n".join(parts))

    educations = [e for e in sections.educations if e.title]
    if educations:
        parts = []
        for e in educations:
            parts.append("\\formation{" + escape_latex(e.title) + "}{" + escape_latex(e.dates) + "}{" + escape_latex(e.institution) + "}")
        blocks.append(r"\section*{EDUCATION}" + "\n" + "\n".join(parts))

    projects = [p for p in sections.projects if p.name]
    if projects:
        parts = []
        for p in projects:
            bullets = _itemize(p.bullets)
            date_suffix = f" ({escape_latex(p.dates)})" if p.dates else ""
            lead = escape_latex(p.name) + date_suffix
            if bullets:
                parts.append(r"\textbf{" + lead + "}\n" + bullets)
            else:
                parts.append(r"\textbf{" + lead + "}")
        blocks.append(r"\section*{ACADEMIC PROJECTS}" + "\n" + "\n\n".join(parts))

    groups = [g for g in sections.skillGroups if g.title or g.items]
    if groups:
        inner = "\n".join(
            f"    \\item \\cvitem{{{escape_latex(g.title)}}}{{{', '.join(escape_latex(i) for i in g.items)}}}"
            for g in groups
        )
        blocks.append(
            r"\section*{TECHNICAL SKILLS}" + "\n" +
            r"\begin{itemize}[leftmargin=*, itemsep=0pt, topsep=1pt, parsep=0pt]"
            "\n" + inner + "\n" +
            r"\end{itemize}"
        )

    hackathons = [h for h in sections.hackathons if h.name]
    if hackathons:
        lines = []
        for h in hackathons:
            event = f" ({escape_latex(h.event)})" if h.event else ""
            text = r"\textbf{" + escape_latex(h.name) + event + "}"
            if h.description:
                text += ": " + escape_latex(h.description)
            lines.append(text + r"\\")
        blocks.append(r"\section*{HACKATHONS}" + "\n" + "\n".join(lines))

    languages = [l for l in sections.languages if l.name]
    if languages:
        parts = []
        for l in languages:
            label = escape_latex(l.name)
            if l.level:
                label += " (" + escape_latex(l.level) + ")"
            parts.append(label)
        blocks.append(r"\section*{LANGUAGES}" + "\n" + r" \textperiodcentered\ ".join(parts))

    soft = [escape_latex(s) for s in sections.softSkills if s]
    if soft:
        blocks.append(r"\section*{SOFT SKILLS}" + "\n" + r" \textperiodcentered\ ".join(soft))

    extras = [x for x in sections.extracurricular if x.title]
    if extras:
        items = []
        for x in extras:
            lead = escape_latex(x.title)
            if x.role or x.dates:
                meta = " \\textperiodcentered\\ ".join(
                    [p for p in (escape_latex(x.role), escape_latex(x.dates)) if p]
                )
                lead += f" --- {meta}"
            sub = _itemize(x.bullets)
            if sub:
                items.append("\\item \\textbf{" + lead + "}\n" + sub)
            else:
                items.append("\\item \\textbf{" + lead + "}")
        blocks.append(
            r"\section*{EXTRACURRICULAR ACTIVITIES}" + "\n" +
            r"\begin{itemize}[leftmargin=*, itemsep=0pt, topsep=1pt, parsep=0pt]"
            "\n" + "\n".join(items) + "\n" +
            r"\end{itemize}"
        )

    interests = [escape_latex(i) for i in sections.interests if i]
    if interests:
        blocks.append(r"\section*{INTERESTS}" + "\n" + r" \textperiodcentered\ ".join(interests))

    return "\n\n\\vspace{1pt}\n\n".join(blocks)


def _clamp_text(text: str, cond) -> str:
    """Apply character/word caps to a raw string (returns trimmed text)."""
    text = (text or "").strip()
    if cond.max_chars and cond.max_chars > 0 and len(text) > cond.max_chars:
        text = text[:max(0, cond.max_chars)].rstrip() + "..."
    if cond.max_words and cond.max_words > 0:
        words = text.split()
        if len(words) > cond.max_words:
            text = " ".join(words[:cond.max_words]) + " ..."
    return text.strip()


def _clamp_items_text(items: list, cond) -> list:
    """Apply per-item character/word caps to a list of bullet strings."""
    out: list[str] = []
    for it in items:
        t = (it or "").strip()
        if cond.per_item_chars and cond.per_item_chars > 0 and len(t) > cond.per_item_chars:
            t = t[:max(0, cond.per_item_chars)].rstrip() + "..."
        if cond.per_item_words and cond.per_item_words > 0:
            words = t.split()
            if len(words) > cond.per_item_words:
                t = " ".join(words[:cond.per_item_words]) + " ..."
        t = t.strip()
        if t:
            out.append(t)
    return out


def _cap_seq(items: list, cond) -> list:
    if cond.max_items and cond.max_items > 0:
        return items[:cond.max_items]
    return items


def _tex_text(data, cond) -> str:
    return escape_latex(_clamp_text(str(data or ""), cond))


def _tex_skills(data, cond) -> str:
    groups = [g for g in data if g.title or g.items]
    if cond.max_items and cond.max_items > 0:
        capped: list[CvSkillGroup] = []
        room = cond.max_items
        for g in groups:
            if room <= 0:
                break
            items = [i for i in g.items if i][:room]
            room -= len(items)
            capped.append(CvSkillGroup(title=g.title, items=items))
        groups = capped
    groups = [
        CvSkillGroup(title=g.title, items=_clamp_items_text(g.items, cond))
        for g in groups
    ]
    if not groups:
        return ""

    if cond.format == "bullets":
        parts = []
        for g in groups:
            lead = f"\\item \\textbf{{{escape_latex(g.title)}}}"
            if g.items:
                sub = "\n" + "\n".join(f"    \\item {escape_latex(i)}" for i in g.items) + "\n"
                parts.append(lead + "\n\\begin{itemize}[leftmargin=*, itemsep=0pt, topsep=1pt, parsep=0pt]\n" + sub + "\\end{itemize}")
            else:
                parts.append(lead)
        inner = "\n".join(parts)
        return (
            r"\begin{itemize}[leftmargin=*, itemsep=0pt, topsep=1pt, parsep=0pt]"
            "\n" + inner + "\n" + r"\end{itemize}"
        )

    inner = "\n".join(
        f"    \\item \\cvitem{{{escape_latex(g.title)}}}{{{', '.join(escape_latex(i) for i in g.items)}}}"
        for g in groups
    )
    return (
        r"\begin{itemize}[leftmargin=*, itemsep=0pt, topsep=1pt, parsep=0pt]"
        "\n" + inner + "\n" + r"\end{itemize}"
    )


def _tex_experience(data, cond) -> str:
    entries = [e for e in data if e.role or e.company]
    if cond.reverse_chronological:
        entries = list(reversed(entries))
    entries = _cap_seq(entries, cond)
    if cond.max_items and cond.max_items > 0:
        entries = [
            CvExperience(role=e.role, company=e.company, dates=e.dates,
                         description=e.description,
                         bullets=_clamp_items_text(e.bullets, cond))
            for e in entries
        ]
    if not entries:
        return ""
    parts = []
    for e in entries:
        parts.append(
            "\\experience{"
            + escape_latex(e.role)
            + "}{" + escape_latex(e.dates)
            + "}{" + escape_latex(e.company)
            + "}{" + escape_latex(e.description)
            + "}{" + _itemize(_clamp_items_text(e.bullets, cond)) + "}"
        )
    return "\n\n".join(parts)


def _tex_education(data, cond) -> str:
    entries = [e for e in data if e.title]
    if cond.reverse_chronological:
        entries = list(reversed(entries))
    entries = _cap_seq(entries, cond)
    if not entries:
        return ""
    return "\n".join(
        "\\formation{" + escape_latex(e.title) + "}{" + escape_latex(e.dates) + "}{" + escape_latex(e.institution) + "}"
        for e in entries
    )


def _tex_projects(data, cond) -> str:
    projects = [p for p in data if p.name]
    projects = _cap_seq(projects, cond)
    if not projects:
        return ""
    parts = []
    for p in projects:
        date_suffix = f" ({escape_latex(p.dates)})" if p.dates else ""
        lead = "\\textbf{" + escape_latex(p.name) + date_suffix + "}"
        bullets = _clamp_items_text(p.bullets, cond)
        if cond.format == "inline" and bullets:
            one_liners = ", ".join(escape_latex(b) for b in _cap_seq(bullets, cond))
            parts.append(lead + ": " + one_liners)
        else:
            sub = _itemize(bullets)
            if sub:
                parts.append(lead + "\n" + sub)
            else:
                parts.append(lead)
    return "\n\n".join(parts)


def _tex_hackathons(data, cond) -> str:
    entries = _cap_seq([h for h in data if h.name], cond)
    if not entries:
        return ""
    lines = []
    for h in entries:
        event = f" ({escape_latex(h.event)})" if h.event else ""
        text = "\\textbf{" + escape_latex(h.name) + event + "}"
        if h.description:
            text += ": " + escape_latex(_clamp_text(h.description, cond))
        lines.append(text + r"\\")
    return "\n".join(lines)


def _tex_languages(data, cond) -> str:
    entries = _cap_seq([l for l in data if l.name], cond)
    if not entries:
        return ""
    labels = []
    for l in entries:
        label = escape_latex(l.name)
        if l.level:
            label += " (" + escape_latex(l.level) + ")"
        labels.append(label)
    if cond.format == "bullets":
        inner = "\n".join(f"    \\item {x}" for x in labels)
        return (
            r"\begin{itemize}[leftmargin=*, itemsep=0pt, topsep=1pt, parsep=0pt]"
            "\n" + inner + "\n" + r"\end{itemize}"
        )
    return r" \textperiodcentered\ ".join(labels)


def _tex_soft(data, cond) -> str:
    items = _cap_seq([s for s in data if s], cond)
    if not items:
        return ""
    if cond.format == "bullets":
        inner = "\n".join(f"    \\item {escape_latex(x)}" for x in items)
        return (
            r"\begin{itemize}[leftmargin=*, itemsep=0pt, topsep=1pt, parsep=0pt]"
            "\n" + inner + "\n" + r"\end{itemize}"
        )
    return r" \textperiodcentered\ ".join(escape_latex(x) for x in items)


def _tex_extracurricular(data, cond) -> str:
    entries = _cap_seq([x for x in data if x.title], cond)
    if not entries:
        return ""
    items = []
    for x in entries:
        lead = escape_latex(x.title)
        if x.role or x.dates:
            meta = " \\textperiodcentered\\ ".join(p for p in (escape_latex(x.role), escape_latex(x.dates)) if p)
            lead += f" --- {meta}"
        sub = _itemize(_clamp_items_text(x.bullets, cond)) if x.bullets else ""
        if sub:
            items.append("\\item \\textbf{" + lead + "}\n" + sub)
        else:
            items.append("\\item \\textbf{" + lead + "}")
    return (
        r"\begin{itemize}[leftmargin=*, itemsep=0pt, topsep=1pt, parsep=0pt]"
        "\n" + "\n".join(items) + "\n" + r"\end{itemize}"
    )


_RENDERERS = {
    "summary": _tex_text,
    "about": _tex_text,
    "skills": _tex_skills,
    "experience": _tex_experience,
    "experiences": _tex_experience,
    "education": _tex_education,
    "educations": _tex_education,
    "projects": _tex_projects,
    "hackathons": _tex_hackathons,
    "languages": _tex_languages,
    "soft-skills": _tex_soft,
    "softskills": _tex_soft,
    "soft_skills": _tex_soft,
    "interests": _tex_soft,
    "extracurricular": _tex_extracurricular,
}


def _placeholder_section_tex(
    block: PlaceholderBlock,
    sections: CvSections,
    freeform: Optional[dict[int, str]] = None,
) -> str:
    key = (block.key or "").strip().lower()
    renderer = _RENDERERS.get(key)
    if renderer is not None:
        data = match_section_data(block.key, sections)
        if data is not None:
            content = renderer(data, parse_conditions(block.conditions))
            if content:
                title = block.title.strip() or (block.key or "Section")
                return r"\section*{" + escape_latex(title) + "}\n" + content

    tex = (freeform or {}).get(block.start, "").strip()
    if tex:
        tex = _sanitize_unicode(tex)
        if r"\section" not in tex:
            title = block.title.strip() or block.key or "Section"
            tex = r"\section*{" + escape_latex(title) + "}\n" + tex
        return tex

    # Safety net: templates are converted from the user's own CV, so the
    # EXAMPLE block holds the candidate's real content. When a section has no
    # live data (empty profile arrays, no free-form output) emit the example
    # rather than dropping the section — this keeps every authored section in
    # the generated PDF.
    example = (block.example or "").strip()
    if example:
        if r"\section" not in example:
            title = block.title.strip() or block.key or "Section"
            example = r"\section*{" + escape_latex(title) + "}\n" + example
        return example

    return ""


def _stitch_placeholders(
    template_src: str,
    sections: CvSections,
    freeform: Optional[dict[int, str]] = None,
) -> str:
    """Replace each declared placeholder block with its rendered section."""
    blocks = parse_placeholders(template_src)
    if not blocks:
        return template_src
    src = template_src
    for block in reversed(blocks):
        section_tex = _placeholder_section_tex(block, sections, freeform)
        src = src[:block.start] + section_tex + src[block.end:]
    return src


def build_cv_tex(
    template_src: str,
    sections: CvSections,
    photo_ref: Optional[str] = None,
    freeform: Optional[dict[int, str]] = None,
) -> str:
    header = _build_header(sections, photo_ref)

    if parse_placeholders(template_src):
        src = _stitch_placeholders(template_src, sections, freeform)
        if BODY_MARKER in src:
            src = src.replace(BODY_MARKER, header)
        elif r"\begin{document}" in src:
            src = src.replace(r"\begin{document}", r"\begin{document}" + "\n\n" + header)
        else:
            src = header + "\n\n" + src
        return src

    body = header + "\n\n" + _build_sections(sections)
    if BODY_MARKER not in template_src:
        logger.error("Template missing %s marker; appending body.", BODY_MARKER)
        return template_src + "\n\n" + body
    return template_src.replace(BODY_MARKER, body)


def build_cv_tex_body_only(sections: CvSections, photo_ref: Optional[str] = None) -> str:
    return _build_header(sections, photo_ref) + "\n\n" + _build_sections(sections)