from langchain_core.messages import SystemMessage


COVER_LETTER_SYSTEM_PROMPT = """You are a professional cover letter writer. Create compelling, personalized cover letters that:
1. Are tailored to the specific job and company
2. Highlight relevant experience and skills
3. Show enthusiasm for the role
4. Maintain a professional tone
5. Are concise but impactful (1 page maximum)
6. Include specific examples from the candidate's background

Structure:
- Opening paragraph: Express interest and mention the specific role
- Body paragraphs: Connect your experience to the job requirements
- Closing paragraph: Reiterate interest and include a call to action

Write in {language} with a {tone} tone."""


async def generate_cover_letter(
    user_id: str,
    job_title: str,
    company_name: str,
    job_description: str,
    hiring_manager_name: str = None,
    hiring_manager_email: str = None,
    language: str = "English",
    tone: str = "professional",
):
    from shared.backend_client import get_user, get_user_experiences, get_user_projects, get_user_skills
    user = await get_user(user_id)
    experiences = await get_user_experiences(user_id)
    projects = await get_user_projects(user_id)
    skills = await get_user_skills(user_id)
    context_parts = []
    if user.firstName:
        context_parts.append(f"Candidate: {user.firstName} {user.lastName or ''}")
    for exp in experiences[:3]:
        context_parts.append(f"Experience: {exp.title} at {exp.company}")
        if exp.description:
            context_parts.append(f"  Description: {exp.description[:200]}")
    for skill in skills[:10]:
        context_parts.append(f"Skill: {skill.name} ({skill.proficiency or 'intermediate'})")
    context = "\n".join(context_parts)
    prompt = COVER_LETTER_SYSTEM_PROMPT.format(language=language, tone=tone)
    full_prompt = f"""{prompt}

Job Details:
- Position: {job_title}
- Company: {company_name}
- Job Description: {job_description[:1000]}

Candidate Profile:
{context}

Generate a compelling cover letter for this position."""

    from shared.llm import get_llm
    llm = get_llm()
    messages = [SystemMessage(content=full_prompt)]
    response = await llm.ainvoke(messages)
    return response.content
