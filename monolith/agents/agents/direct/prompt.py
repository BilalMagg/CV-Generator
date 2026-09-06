from langchain_core.messages import SystemMessage, HumanMessage

from agents.direct.schemas import DirectMessageRequest


def _job_block(req: DirectMessageRequest) -> str:
    parts = []
    if req.company_name:
        parts.append(f"Company: {req.company_name}")
    if req.job_role:
        parts.append(f"Role: {req.job_role}")
    if req.job_description:
        parts.append(f"Job description:\n{req.job_description}")
    if req.required_skills:
        parts.append("Required skills: " + ", ".join(req.required_skills))
    if req.responsibilities:
        parts.append("Responsibilities: " + "; ".join(req.responsibilities))
    return "\n".join(parts)


def get_message_messages(req: DirectMessageRequest):
    job = _job_block(req)
    system = (
        "You are a professional networking/career assistant. Write a concise, "
        "polite outreach or application message for the candidate, grounded ONLY in "
        "the job information and the candidate context provided. Match the tone to the "
        f"channel ({req.channel}) and the contact type ({req.contact_type}). "
        f"Write in {req.language}. Respond ONLY with a JSON object of the form "
        '{"subject": <optional subject line>, "message": <the message body>}. '
        "Keep the message under ~180 words. Do not add any commentary outside the JSON. "
        "Rules: "
        "1. Never invent background, skills, education, experience, or any other fact "
        "that is not present in the CANDIDATE CONTEXT block. When the context is "
        "(none provided) or sparse, keep the message generic and short. "
        "2. Sign the message with the candidate's real contact details only as they "
        "appear in the context: one line each for Name, Phone, and Email when present, "
        "in that order. "
        "3. Never emit placeholder tokens such as [Your Name], [Phone Number], or "
        "[Email Address]. If no real name is in the context, end with 'Kind regards,' "
        "and nothing else."
    )
    recipient = f"Recipient: {req.recipient_name}\n" if req.recipient_name else ""
    user = (
        f"Channel: {req.channel}\n"
        f"Contact type: {req.contact_type}\n"
        f"{recipient}"
        f"{job}\n\n"
        f"CANDIDATE CONTEXT:\n{req.candidate_context or '(none provided)'}\n\n"
        f"Extra considerations: {req.considerations or 'None'}\n\n"
        f"Language: {req.language}"
    )
    return system, user
