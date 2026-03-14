from app.models.job_model import JobRequirements
from app.models.user_model import UserProfile
from app.models.cv_model import CVDraft

def match_candidate_data(job_reqs: JobRequirements, profile: UserProfile) -> CVDraft:
    """
    Agent 2: Matches extracted job requirements with the user's profile using RAG.
    """
    # TODO: Implement vector search and matching logic here
    print(f"Matching candidate {profile.name} to {job_reqs.job_role}...")
    return CVDraft(
        target_role=job_reqs.job_role,
        summary="A motivated software engineer...",
        matched_skills=profile.skills,
        matched_experiences=[],
        matched_projects=[]
    )
