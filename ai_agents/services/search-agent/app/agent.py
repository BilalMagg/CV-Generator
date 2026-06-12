import os
import asyncio
from typing import List
from app.schemas import SearchInput, SearchOutput
from app.core import backend_client
from cvtools.core.llm import get_llm as _get_base_llm
from cvtools.models.user_model import ExperienceResponse, ProjectResponse, SkillResponse
from langchain_core.prompts import ChatPromptTemplate
from langchain_core.output_parsers import JsonOutputParser
from google import genai

print("[SEARCH-AGENT v2] job_role defaults to empty string, _get_single handles 404 silently")

_genai_client: genai.Client | None = None

def _get_genai_client() -> genai.Client:
    global _genai_client
    if _genai_client is None:
        _genai_client = genai.Client(api_key=os.getenv("GOOGLE_API_KEY"))
    return _genai_client

def _embed_text(text: str) -> List[float]:
    client = _get_genai_client()
    response = client.models.embed_content(
        model=os.getenv("EMBEDDING_MODEL", "gemini-embedding-001"),
        contents=text,
        config={"output_dimensionality": 768}
    )
    return response.embeddings[0].values

def _embed_texts(texts: List[str]) -> List[List[float]]:
    return [_embed_text(t) for t in texts]

def get_llm():
    provider = os.getenv("LLM_PROVIDER") or "google"
    model = os.getenv("LLM_MODEL") or "gemini-2.0-flash"
    return _get_base_llm(provider=provider, model=model, temperature=0)

async def _initial_sync(user_id: str):
    print(f"Performing initial vector sync for user {user_id}...")
    experiences = await backend_client.get_user_experiences(user_id)
    projects = await backend_client.get_user_projects(user_id)
    skills = await backend_client.get_user_skills(user_id)

    chunks = []
    exp_texts = [f"Experience: {e.title}" + (f" at {e.company}" if e.company else "") + (f". {e.description}" if e.description else "") for e in experiences]
    proj_texts = [f"Project: {p.title}" + (f". {p.description}" if p.description else "") + (f". Achievements: {p.achievements}" if p.achievements else "") for p in projects]
    skill_texts = [f"Skill: {s.name}" + (f" (Level: {s.level})" if s.level else "") for s in skills]

    all_texts = exp_texts + proj_texts + skill_texts
    if not all_texts:
        return experiences, projects, skills

    all_vectors = _embed_texts(all_texts)
    idx = 0
    for i, exp in enumerate(experiences):
        chunks.append({"sourceType": "experience", "sourceId": str(exp.id), "content": exp_texts[i], "embedding": all_vectors[idx]})
        idx += 1
    for i, proj in enumerate(projects):
        chunks.append({"sourceType": "project", "sourceId": str(proj.id), "content": proj_texts[i], "embedding": all_vectors[idx]})
        idx += 1
    for i, skill in enumerate(skills):
        chunks.append({"sourceType": "skill", "sourceId": str(skill.id), "content": skill_texts[i], "embedding": all_vectors[idx]})
        idx += 1

    if chunks:
        await backend_client.sync_vectors(user_id, chunks)

    return experiences, projects, skills

async def match_candidate_data(input_data: SearchInput) -> SearchOutput:
    user_id_str = str(input_data.user_id)
    print(f"[MATCH] user={user_id_str[:8]} job_role='{input_data.job_requirements.job_role}' skills={input_data.job_requirements.extracted_skills}")

    # 1. Skills are needed for gap analysis
    skills = await backend_client.get_user_skills(input_data.user_id)
    user_skill_names = [s.name for s in skills]

    # 2. Check if vectors exist — only sync once if missing
    has_vectors = await backend_client.check_vectors_status(input_data.user_id)
    if not has_vectors:
        all_experiences, all_projects, _ = await _initial_sync(input_data.user_id)
    else:
        all_experiences = []
        all_projects = []

    # 3. Generate query vector from Job Requirements
    job_reqs = input_data.job_requirements
    query_text = f"Role: {job_reqs.job_role or 'Unknown'}. Skills: {', '.join(job_reqs.extracted_skills)}. Keywords: {', '.join(job_reqs.keywords)}"

    query_vector = _embed_text(query_text)

    # 4. Search — results now include Content directly
    search_results = await backend_client.search_vectors(input_data.user_id, query_text, query_vector, limit=15)

    context_chunks = []
    for res in search_results:
        source_id = res.get("sourceId")
        source_type = res.get("sourceType")
        content = res.get("content", "")
        if content:
            context_chunks.append(f"- {source_type.upper()} (ID: {source_id}): {content}")

    context = "\n".join(context_chunks)

    # 5. LLM Reasoning with strict cross-referencing
    prompt = ChatPromptTemplate.from_template("""
    You are a strict matching agent in a multi-agent CV generator.

    JOB REQUIREMENTS:
    {job_reqs}

    CANDIDATE SKILLS INVENTORY (complete list):
    {user_skills}

    --- RETRIEVED CONTEXT FROM CANDIDATE PROFILE ---
    {cv_context}
    -----------------------------------------------

    MATCHING RULES (strictly follow these):
    1. A skill ID should ONLY be in matched_skill_ids if the skill NAME appears in the job's extracted_skills list OR closely matches any of the job's keywords.
    2. An experience ID should ONLY be in matched_experience_ids if the person's title/description clearly relates to the job role, required skills, or keywords.
    3. A project ID should ONLY be in matched_project_ids if the project description clearly relates to the job requirements or keywords.
    4. gap_skills MUST include every skill from extracted_skills and keywords that is NOT present in the CANDIDATE SKILLS INVENTORY.
    5. match_score should reflect the proportion of required skills/keywords the candidate has: 1.0 = all present, 0.5 = half, 0.0 = none.

    Output a JSON object with this exact structure:
    {{
        "matched_experience_ids": ["uuid"],
        "matched_project_ids": ["uuid"],
        "matched_skill_ids": ["uuid"],
        "gap_skills": ["skill name"],
        "match_score": 0.8
    }}

    CRITICAL: YOU MUST OUTPUT ONLY VALID JSON. DO NOT OUTPUT ANY EXPLANATIONS, PREAMBLES, OR MARKDOWN BLOCKS. YOUR ENTIRE RESPONSE MUST BE A SINGLE JSON OBJECT.
    Only include IDs that are exactly present in the RETRIEVED CONTEXT.
    """)

    chain = prompt | get_llm() | JsonOutputParser()

    try:
        if not context.strip():
            result = {"matched_experience_ids": [], "matched_project_ids": [], "matched_skill_ids": [], "gap_skills": job_reqs.extracted_skills, "match_score": 0.0}
        else:
            result = chain.invoke({"job_reqs": job_reqs.model_dump_json(), "user_skills": ", ".join(user_skill_names), "cv_context": context})
    except Exception as e:
        print(f"LLM matching failed: {e}")
        return SearchOutput(
            matched_skills=[],
            matched_experiences=[],
            matched_projects=[],
            gap_skills=job_reqs.extracted_skills,
            match_score=0.0
        )

    matched_exp_ids = set(result.get("matched_experience_ids", []))
    matched_proj_ids = set(result.get("matched_project_ids", []))
    matched_skill_ids = set(result.get("matched_skill_ids", []))
    
    # Auto-correct Llama-3 logic errors
    total_reqs = len(job_reqs.extracted_skills) + len(job_reqs.keywords)
    has_matches = bool(matched_skill_ids or matched_exp_ids or matched_proj_ids)
    if has_matches and float(result.get("match_score", 0.0)) == 0.0:
        result["match_score"] = 1.0
        
    if total_reqs == 1 and has_matches:
        result["gap_skills"] = []
        result["match_score"] = 1.0

    print(f"STEP 5: matched IDs — {len(matched_exp_ids)} experiences, {len(matched_proj_ids)} projects, {len(matched_skill_ids)} skills")

    # 6. Build final result objects
    if not has_vectors:
        final_experiences = [e for e in all_experiences if str(e.id) in matched_exp_ids]
        final_projects = [p for p in all_projects if str(p.id) in matched_proj_ids]
    else:
        exp_fetches = [backend_client.get_experience(uid) for uid in matched_exp_ids]
        proj_fetches = [backend_client.get_project(uid) for uid in matched_proj_ids]
        fetched = await asyncio.gather(*exp_fetches, *proj_fetches)
        n_exp = len(matched_exp_ids)
        final_experiences = [r for r in fetched[:n_exp] if r is not None]
        final_projects = [r for r in fetched[n_exp:] if r is not None]

    final_skills = [s for s in skills if str(s.id) in matched_skill_ids]

    return SearchOutput(
        matched_skills=final_skills,
        matched_experiences=final_experiences,
        matched_projects=final_projects,
        gap_skills=result.get("gap_skills", []),
        match_score=float(result.get("match_score", 0.0))
    )
