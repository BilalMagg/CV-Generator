"""
Search (RAG) agent — matches user data with job requirements.
"""
from app.schemas import SearchInput, SearchOutput
from app.core import backend_client


async def match_candidate_data(input_data: SearchInput) -> SearchOutput:
    """
    TODO: Implement Vector/RAG similarity logic.
    For now, fetches real data from backend and returns it all as 'matched'.
    """
    user_id = input_data.user_id

    experiences = await backend_client.get_user_experiences(user_id)
    projects = await backend_client.get_user_projects(user_id)
    skills = await backend_client.get_user_skills(user_id)

    return SearchOutput(
        matched_skills=skills,
        matched_experiences=experiences,
        matched_projects=projects,
        gap_skills=[],
        match_score=0.85
    )