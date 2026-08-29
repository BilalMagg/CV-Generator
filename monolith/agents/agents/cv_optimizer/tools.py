from langchain_core.tools import tool
from typing import Optional
from shared.backend_client import get_user, get_user_experiences, get_user_projects, get_user_skills


@tool
async def get_user_profile(user_id: str) -> str:
    """Fetch the complete user profile including personal info, skills, and sections."""
    user = await get_user(user_id)
    return f"User: {user.firstName} {user.lastName}\nEmail: {user.email}\nProfile Complete: {user.isProfileComplete}"


@tool
async def get_user_cv_content(user_id: str) -> str:
    """Retrieve the user's CV content including experiences, projects, and skills."""
    experiences = await get_user_experiences(user_id)
    projects = await get_user_projects(user_id)
    skills = await get_user_skills(user_id)
    content_parts = []
    if experiences:
        content_parts.append("=== EXPERIENCE ===")
        for exp in experiences:
            content_parts.append(f"Title: {exp.title}")
            if exp.company:
                content_parts.append(f"Company: {exp.company}")
            if exp.description:
                content_parts.append(f"Description: {exp.description}")
            if exp.experienceDetails:
                for detail in exp.experienceDetails:
                    content_parts.append(f"  - {detail.title}: {detail.description or 'No description'}")
            content_parts.append("")
    if projects:
        content_parts.append("=== PROJECTS ===")
        for proj in projects:
            content_parts.append(f"Title: {proj.title}")
            if proj.description:
                content_parts.append(f"Description: {proj.description}")
            if proj.projectDetails:
                for detail in proj.projectDetails:
                    content_parts.append(f"  - {detail.title}: {detail.description or 'No description'}")
            content_parts.append("")
    if skills:
        content_parts.append("=== SKILLS ===")
        for skill in skills:
            skill_text = skill.name
            if skill.proficiency:
                skill_text += f" ({skill.proficiency})"
            content_parts.append(skill_text)
    return "\n".join(content_parts) if content_parts else "No CV content found for this user."


@tool
async def analyze_cv_for_job(user_id: str, job_description: str) -> str:
    """Analyze the user's CV against a specific job description to identify gaps and improvements."""
    cv_content = await get_user_cv_content(user_id)
    return f"CV Content:\n{cv_content}\n\nJob Description:\n{job_description}\n\n[Analysis would be performed here by the LLM]"
