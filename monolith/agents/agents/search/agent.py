import logging
import re
from typing import Optional, Any

from shared.backend_client import (
    get_user, get_user_experiences, get_user_projects, get_user_skills,
)
from shared.tools.pdf_utils import get_chunks_from_text
from shared.tools.rag_utils import retrieve_context_from_text
from agents.search.schemas import SearchResultItem

logger = logging.getLogger(__name__)

_STOPWORDS = {
    "a", "an", "and", "are", "at", "be", "by", "for", "from", "in", "is",
    "of", "on", "or", "the", "to", "with",
}


def _norm(text: str) -> str:
    return re.sub(r"[^a-z0-9+#.]+", " ", (text or "").lower()).strip()


def _tokens(text: str) -> set[str]:
    return {t for t in _norm(text).split() if t and t not in _STOPWORDS}


def _substr_match(a: str, b: str) -> bool:
    """True when one normalized name is contained in the other (angle == AngularJS,
    react == react native, js == javascript)."""
    na, nb = _norm(a), _norm(b)
    if not na or not nb:
        return False
    return na in nb or nb in na


async def get_cv_text_chunks(user_id: str) -> list[str]:
    experiences = await get_user_experiences(user_id)
    projects = await get_user_projects(user_id)
    skills = await get_user_skills(user_id)
    text_parts = []
    for exp in experiences:
        text_parts.append(f"{exp.title} at {exp.company or 'Unknown'}")
        if exp.description:
            text_parts.append(exp.description)
        for detail in exp.experienceDetails:
            text_parts.append(f"{detail.title}: {detail.description or ''}")
    for proj in projects:
        text_parts.append(f"{proj.title} (Project)")
        if proj.description:
            text_parts.append(proj.description)
    for skill in skills:
        skill_text = skill.name
        if skill.proficiency:
            skill_text += f" ({skill.proficiency})"
        text_parts.append(skill_text)
    full_text = "\n".join(text_parts)
    return get_chunks_from_text(full_text)


async def search_similar_cv_content(
    query: str, workflow_id: str, user_id: str, min_score: float = 0.20
) -> list[SearchResultItem]:
    context = await retrieve_context_from_text(query, workflow_id, user_id, min_score)
    if not context:
        return []
    chunks = context.split("\n---\n")
    return [
        SearchResultItem(content=chunk.strip(), score=0.0, source="vector_search")
        for chunk in chunks if chunk.strip()
    ]


async def match_candidate_profile(user_id: str, job_requirements: dict) -> dict[str, Any]:
    """Deterministic profile matching against the user's own content.

    Mirrors the original search-agent /match contract (SearchInput → SearchOutput):
      matched_skills / matched_experiences / matched_projects / gap_skills / match_score.
    Works without vector search or an LLM so the CV-generation pipeline can't
    fail here; degraded input degrades gracefully to empty matches.
    """
    globals_user_id = user_id
    try:
        skills = await get_user_skills(globals_user_id)
    except Exception as e:  # noqa: BLE001
        logger.warning("match_candidate_profile: skills fetch failed: %s", e)
        skills = []
    try:
        experiences = await get_user_experiences(globals_user_id)
    except Exception as e:  # noqa: BLE001
        logger.warning("match_candidate_profile: experiences fetch failed: %s", e)
        experiences = []
    try:
        projects = await get_user_projects(globals_user_id)
    except Exception as e:  # noqa: BLE001
        logger.warning("match_candidate_profile: projects fetch failed: %s", e)
        projects = []

    req = job_requirements or {}
    extracted_skills = [s for s in (req.get("extracted_skills") or []) if isinstance(s, str) and s.strip()]
    keywords = [k for k in (req.get("keywords") or []) if isinstance(k, str) and k.strip()]
    job_role = req.get("job_role") or ""

    req_tokens = _tokens(" ".join([job_role, *extracted_skills, *keywords]))

    # 1. Skills: matched = user skill whose name aligns with a required skill.
    matched_skills = []
    user_skill_names = []
    for skill in skills:
        name = getattr(skill, "name", "") or ""
        user_skill_names.append(name)
        if any(_substr_match(name, rs) for rs in extracted_skills):
            matched_skills.append(skill)

    # 2. Experiences / projects: relevance = how many requirement tokens appear
    #    in the item's text. Keep the top matches.
    def _item_text(item, details_field: str) -> str:
        parts = [
            getattr(item, "title", "") or "",
            getattr(item, "company", "") or "",
            getattr(item, "description", "") or "",
        ]
        for detail in getattr(item, details_field, []) or []:
            parts.append(detail.get("title", "") if isinstance(detail, dict) else "")
            parts.append(detail.get("description", "") if isinstance(detail, dict) else "")
        for tech in getattr(item, "technologies", []) or []:
            parts.append(tech if isinstance(tech, str) else "")
        return " ".join(parts)

    def _rank(items, details_field: str, cap: int) -> list:
        scored = []
        for item in items:
            text = _norm(_item_text(item, details_field))
            if not text:
                continue
            hits = sum(1 for t in req_tokens if t and t in text)
            if hits > 0:
                scored.append((hits, item))
        scored.sort(key=lambda pair: pair[0], reverse=True)
        return [item for _, item in scored[:cap]]

    matched_experiences = _rank(experiences, "experienceDetails", cap=6)
    matched_projects = _rank(projects, "projectDetails", cap=6)

    # 3. Gap skills: required skills not covered by any user skill name.
    gap_skills = []
    for rs in extracted_skills:
        if not any(_substr_match(name, rs) for name in user_skill_names):
            if rs not in gap_skills:
                gap_skills.append(rs)

    # 4. Score: proportion of required skills covered.
    match_score = 0.0
    if extracted_skills:
        covered = len(extracted_skills) - len(gap_skills)
        match_score = round(covered / len(extracted_skills), 3)

    logger.info(
        "match_candidate_profile: user=%s role=%s skills_matched=%d exps=%d projects=%d gaps=%d score=%.3f",
        globals_user_id, job_role, len(matched_skills), len(matched_experiences),
        len(matched_projects), len(gap_skills), match_score,
    )

    return {
        "matched_skills": [s.model_dump() for s in matched_skills],
        "matched_experiences": [e.model_dump() for e in matched_experiences],
        "matched_projects": [p.model_dump() for p in matched_projects],
        "gap_skills": gap_skills,
        "match_score": match_score,
    }