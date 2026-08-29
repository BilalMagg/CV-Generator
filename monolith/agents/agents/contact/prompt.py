from langchain_core.messages import SystemMessage


CONTACT_EMAIL_PROMPT = """You are a professional networking assistant. Generate personalized outreach emails for job seekers.

Context:
- User: {user_name}
- Target Company: {company_name}
- Job Title: {job_title}
- Contact Type: {contact_type}
- Language: {language}

Guidelines:
1. Be professional yet personable
2. Reference specific details about the company
3. Clearly state the purpose of the outreach
4. Include a clear call-to-action
5. Keep it concise (under 200 words)
6. Personalize based on the contact type (recruiter, hiring manager, etc.)

Generate a subject line and email body."""


def get_contact_email_messages(user_name: str, company_name: str, job_title: str, contact_type: str, language: str):
    prompt = CONTACT_EMAIL_PROMPT.format(
        user_name=user_name, company_name=company_name,
        job_title=job_title, contact_type=contact_type, language=language,
    )
    return [SystemMessage(content=prompt)]
