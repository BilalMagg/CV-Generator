"""
Template agent — logic to create and render CV based on the sections and the data.
"""
from langchain.agents import create_agent
from app.core.llm import get_llm
from app.agents.template_agent.schemas import TemplateInput, RenderedCV
from app.models.cv_model import CVSection
from app.agents.template_agent.prompt import LLMPrompt

async def render_template(input_data: TemplateInput, prompt : LLMPrompt) -> RenderedCV:

    sections = [
        CVSection(section_type="summary", content=f"Experienced {input_data.target_role}", order=0),
        CVSection(section_type="skills", content="Python, FastAPI, Docker", order=1),
    ]
    cv_data = (
        f"Target Role: {input_data.cv_draft.target_role}\n\n"
        f"Summary: {input_data.cv_draft.summary}\n\n"
        f"--- Experience ---\n"
        + "\n".join(
            f"- {exp.title} at {exp.company} ({exp.start_date.strftime('%Y-%m')}"
            + (f" to {exp.end_date.strftime('%Y-%m')}" if exp.end_date else " to Present")
            + f")\n  Description: {exp.description or 'N/A'}"
            for exp in input_data.cv_draft.matched_experiences
        )
        + "\n\n--- Projects ---\n"
        + "\n".join(
            f"- {proj.title} ({proj.status})"
            + (f" | Repo: {proj.repository_url}" if proj.repository_url else "")
            + (f" | Demo: {proj.demo_url}" if proj.demo_url else "")
            + f"\n  Description: {proj.description or 'N/A'}"
            + (f"\n  Achievements: {proj.achievements}" if proj.achievements else "")
            for proj in input_data.cv_draft.matched_projects
        )
        + "\n\n--- Skills ---\n"
        + "\n".join(
            f"- {skill.name} ({skill.level or 'N/A'})"
            + (f" | {skill.years_of_experience}y exp" if skill.years_of_experience else "")
            for skill in input_data.cv_draft.matched_skills
        )
        + "\n\n--- Gap Skills (missing but recommended) ---\n"
        + ", ".join(input_data.cv_draft.gap_skills)
    )
    model = get_llm()
    # prompt_value = prompt.get_prompt(input_data.template_type).invoke()
    system_prompt = prompt.get_prompt(input_data.template_type).format(
        target_role = input_data.target_role,
        cv_data = cv_data )
    
    client_agent = create_agent(model, tools =[], system_prompt=system_prompt)



    model_input = {"message": [
        {"role": "user",
         "content": "enerate a CV based on the provided data"}
    ]}
    result = client_agent.invoke(model_input)

    return RenderedCV(
        cv_code = result,
        template_type=input_data.template_id,
        sections=sections,
    )
