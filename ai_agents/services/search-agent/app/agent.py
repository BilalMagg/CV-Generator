"""
Search (Hybrid RAG) agent — matches user data with job requirements using remote Vector DB.
"""
import os
import json
from typing import List, Dict, Any
from app.schemas import SearchInput, SearchOutput
from app.core import backend_client
from langchain_google_genai import GoogleGenerativeAIEmbeddings, ChatGoogleGenerativeAI
from langchain_core.prompts import ChatPromptTemplate
from langchain_core.output_parsers import JsonOutputParser

def get_llm():
    model_name = os.getenv("LLM_MODEL", "gemini-1.5-flash")
    return ChatGoogleGenerativeAI(model=model_name, temperature=0)

def get_embeddings():
    model_name = os.getenv("EMBEDDING_MODEL", "models/embedding-001")
    return GoogleGenerativeAIEmbeddings(model=model_name)

async def sync_user_data_to_vector_db(user_id: str, experiences, projects, skills):
    """
    Checks if the C# Vector Database has the user's embeddings. 
    If not, it fetches them, embeds them, and syncs them via HTTP.
    """
    embeddings_model = get_embeddings()
    chunks = []
    
    for exp in experiences:
        content = f"Experience: {exp.title}"
        if exp.company: content += f" at {exp.company}"
        if exp.description: content += f". {exp.description}"
        # Generate the embedding vector
        vector = embeddings_model.embed_query(content)
        chunks.append({
            "sourceType": "experience",
            "sourceId": str(exp.id),
            "content": content,
            "embedding": vector
        })
        
    for proj in projects:
        content = f"Project: {proj.title}"
        if proj.description: content += f". {proj.description}"
        if proj.achievements: content += f". Achievements: {proj.achievements}"
        vector = embeddings_model.embed_query(content)
        chunks.append({
            "sourceType": "project",
            "sourceId": str(proj.id),
            "content": content,
            "embedding": vector
        })
        
    for skill in skills:
        content = f"Skill: {skill.name}"
        if skill.level: content += f" (Level: {skill.level})"
        vector = embeddings_model.embed_query(content)
        chunks.append({
            "sourceType": "skill",
            "sourceId": str(skill.id),
            "content": content,
            "embedding": vector
        })

    if chunks:
        await backend_client.sync_vectors(user_id, chunks)

async def match_candidate_data(input_data: SearchInput) -> SearchOutput:
    user_id_str = str(input_data.user_id)

    # 1. Fetch original structured data
    experiences = await backend_client.get_user_experiences(input_data.user_id)
    projects = await backend_client.get_user_projects(input_data.user_id)
    skills = await backend_client.get_user_skills(input_data.user_id)

    # 2. Sync to Vector DB if necessary
    has_vectors = await backend_client.check_vectors_status(input_data.user_id)
    if not has_vectors:
        print(f"Syncing vectors for user {user_id_str}...")
        await sync_user_data_to_vector_db(input_data.user_id, experiences, projects, skills)
    
    # 3. Generate query vector from Job Requirements
    job_reqs = input_data.job_requirements
    query_text = f"Role: {job_reqs.job_role}. Skills: {', '.join(job_reqs.extracted_skills)}. Keywords: {', '.join(job_reqs.keywords)}"
    
    embeddings_model = get_embeddings()
    query_vector = embeddings_model.embed_query(query_text)
    
    # 4. Perform highly optimized BMO Hybrid Search in the C# Database
    search_results = await backend_client.search_vectors(input_data.user_id, query_text, query_vector, limit=15)
    
    # Map the search results back to the structured content for the LLM context
    context_chunks = []
    for res in search_results:
        source_id = res.get("sourceId")
        source_type = res.get("sourceType")
        
        content = ""
        if source_type == "experience":
            item = next((e for e in experiences if str(e.id) == source_id), None)
            if item: content = f"{item.title} at {item.company}. {item.description}"
        elif source_type == "project":
            item = next((p for p in projects if str(p.id) == source_id), None)
            if item: content = f"{item.title}. {item.description}"
        elif source_type == "skill":
            item = next((s for s in skills if str(s.id) == source_id), None)
            if item: content = f"{item.name} ({item.level})"
            
        if content:
            context_chunks.append(f"- {source_type.upper()} (ID: {source_id}): {content}")
            
    context = "\n".join(context_chunks)

    # 5. LLM Reasoning using BMO explicit context formatting
    prompt = ChatPromptTemplate.from_template("""
    You are a matching agent in a multi-agent CV generator.
    
    JOB REQUIREMENTS:
    {job_reqs}

    --- RELEVANT CONTEXT FROM CANDIDATE PROFILE ---
    {cv_context}
    -----------------------------------------------

    TASK:
    Analyze the candidate data against the job requirements.
    Identify which specific experiences, projects, and skills match the job.
    Also identify any missing critical skills (gap skills) from the job requirements.
    Provide an overall match score between 0.0 and 1.0.

    Output a JSON object with this exact structure:
    {{
        "matched_experience_ids": ["uuid-string", ...],
        "matched_project_ids": ["uuid-string", ...],
        "matched_skill_ids": ["uuid-string", ...],
        "gap_skills": ["skill1", "skill2"],
        "match_score": 0.85
    }}
    Only include IDs that are exactly present in the RELEVANT CONTEXT.
    """)

    chain = prompt | get_llm() | JsonOutputParser()
    
    try:
        if not context.strip():
            result = {"matched_experience_ids": [], "matched_project_ids": [], "matched_skill_ids": [], "gap_skills": job_reqs.extracted_skills, "match_score": 0.0}
        else:
            result = chain.invoke({"job_reqs": job_reqs.model_dump_json(), "cv_context": context})
    except Exception as e:
        print(f"LLM matching failed: {e}")
        return SearchOutput(
            matched_skills=skills,
            matched_experiences=experiences,
            matched_projects=projects,
            gap_skills=[],
            match_score=0.5
        )

    matched_exp_ids = set(result.get("matched_experience_ids", []))
    matched_proj_ids = set(result.get("matched_project_ids", []))
    matched_skill_ids = set(result.get("matched_skill_ids", []))

    final_experiences = [e for e in experiences if str(e.id) in matched_exp_ids]
    final_projects = [p for p in projects if str(p.id) in matched_proj_ids]
    final_skills = [s for s in skills if str(s.id) in matched_skill_ids]

    return SearchOutput(
        matched_skills=final_skills,
        matched_experiences=final_experiences,
        matched_projects=final_projects,
        gap_skills=result.get("gap_skills", []),
        match_score=float(result.get("match_score", 0.0))
    )