from typing import List, Tuple

from langchain_core.messages import SystemMessage, HumanMessage

from agents.direct.schemas import DirectMessageRequest, LinkedInRequest


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

_TONE_DESCRIPTIONS = {
    "professional": "professional and polished, confident but not boastful",
    "enthusiastic": "energetic and upbeat, genuinely excited",
    "storytelling": "personal and narrative-driven, structured like a mini-story with a clear arc",
    "honest": "authentic and conversational, down-to-earth, a bit raw",
}


def _length_guidance(tool: str, length: str) -> str:
    if tool == "comment":
        return {
            "short": "Keep each comment short and punchy (under ~180 characters).",
            "medium": "Keep each comment concise (~250-350 characters).",
            "long": "Go a bit deeper but stay a comment, not a post (~350-500 characters).",
        }.get(length, "Keep each comment concise (~250-350 characters).")
    if tool == "post":
        return {
            "short": "Keep each post short and punchy (under ~280 characters).",
            "medium": "Aim for a comfortable medium length (~450-700 characters).",
            "long": "Aim for a longer read (up to ~2200 characters).",
        }.get(length, "Aim for a comfortable medium length (~450-700 characters).")
    return "Keep the message brief and easy to read (~120-150 words)."

_CONTEXT_TYPES = {
    "hackathon": "a hackathon (project sprints, teams, wins/learnings, time pressure)",
    "project": "a project (what it does, tech, your role, outcome)",
    "event": "an event (conference, meetup, workshop — what happened, what you took away)",
    "achievement": "an achievement (award, certification, milestone, promotion)",
    "learning": "a learning moment (a skill, a lesson, a tool you picked up)",
    "other": "an update or share",
}

_RELATIONSHIPS = {
    "network": "a professional you are connected to on LinkedIn but barely know",
    "colleague": "a current or former colleague",
    "alumni": "a fellow student/alumnus of the same school",
    "event": "someone you met at an event/conference",
    "other": "a professional contact",
}

_PURPOSES = {
    "introduction": "introduce yourself and start a conversation",
    "thanks": "thank them for something they did (recommendation, referral, talk, help)",
    "follow-up": "follow up after a conversation, event, or previous exchange",
    "referral": "ask for a referral or an introduction to someone in their network",
    "coffee_chat": "invite them to a short chat / call (informational or casual)",
    "other": "reach out with a specific reason",
}


def _field(label: str, value: str) -> str:
    return f"{label}:\n{value or '(none provided)'}" if value else ""


def _bullets(label: str, values: List[str]) -> str:
    values = [v.strip() for v in values if v and v.strip()]
    if not values:
        return ""
    return label + ":\n- " + "\n- ".join(values)


def _json_contract(tool: str, variants: int) -> str:
    if tool == "post":
        item = '{"title": <optional short headline or "">, "text": <the post body>, "hashtags": <comma-free, space-separated hashtags like "#ai #python" or "">}'
    else:
        item = '{"title": "", "text": <the comment/message body>, "hashtags": ""}'
    return (
        "Respond ONLY with a JSON object of the form "
        f'{{"tool": "{tool}", "variants": [<{variants} item(s)>]}} where each item matches '
        f"{item}. Provide EXACTLY {variants} distinct variant(s). "
        "Do not add any commentary, markdown, or prose outside the JSON."
    )


def get_linkedin_messages(req: LinkedInRequest) -> Tuple[str, str]:
    tool = (req.tool or "post").strip().lower()
    variants = max(1, min(int(req.variants or 1), 3))

    system_parts = [
        f"You are a LinkedIn ghostwriter. Write in {req.language or 'English'} with a "
        f"{_TONE_DESCRIPTIONS.get(req.tone, _TONE_DESCRIPTIONS['professional'])} tone.",
        _length_guidance(tool, req.length or "medium"),
    ]

    if tool == "post":
        ctype = _CONTEXT_TYPES.get(req.context_type, _CONTEXT_TYPES["other"])
        system_parts.append(
            f"The post is about {ctype}.\n"
            "Rules:\n"
            "1. Ground every claim ONLY in the provided CONTEXT — never invent facts, "
            "numbers, awards, or outcomes.\n"
            "2. If MENTIONS is provided, incorporate EVERY mentioned person into the post "
            "verbatim (their name or @handle exactly as written) where it fits naturally.\n"
            f"3. Include ~{req.hashtag_count} relevant hashtags at the end "
            + ("(required)." if req.include_hashtags else "only if it fits naturally, else omit.")
            + "\n"
            "4. Keep the post scannable: short sentences, generous line breaks, no walls of text. "
            "Aim for a conversational professional share that stops the reader mid-scroll.\n"
            "5. Text must be < 2200 characters."
        )
    elif tool == "comment":
        system_parts.append(
            "You are replying to a LinkedIn post/comment.\n"
            "Rules:\n"
            "1. Write a comment that clearly answers/engages with the TARGET content.\n"
            "2. Weave the user's POINTS in naturally — do not invent facts about the author.\n"
            "3. Be concise (max ~350 characters per comment), add value, and avoid generic "
            "'Great post!' filler unless the points genuinely support it.\n"
            "4. Do not use hashtags."
        )
    else:  # message
        rel = _RELATIONSHIPS.get(req.relationship, _RELATIONSHIPS["network"])
        purpose = _PURPOSES.get(req.purpose, _PURPOSES["other"])
        system_parts.append(
            f"The recipient is {rel}; the goal is to {purpose}.\n"
            "This is a personal/professional LinkedIn message, NOT a job application.\n"
            "Rules:\n"
            "1. Start with a short natural greeting using the recipient's name, then a "
            f"brief reason, then a specific, low-pressure ask or close. Sign off with your name.\n"
            "2. Ground everything ONLY in RECIPIENT CONTEXT and SENDER CONTEXT — never invent "
            "shared history or facts.\n"
            "3. Keep it under ~150 words. No subject line, no hashtags, no emoji spam."
        )

    system = "\n\n".join(system_parts) + "\n\n" + _json_contract(tool, variants)

    lines = [f"Language: {req.language or 'English'}", f"Tone: {req.tone or 'professional'}", f"Length guide: {req.length or 'medium'}"]
    if tool == "post":
        if req.context_type:
            lines.append(f"Context type: {req.context_type}")
        lines.append(_field("CONTEXT", req.context))
        lines.append(_bullets("MENTIONS", req.mentions))
        lines.append(f"Hashtags: {req.include_hashtags} (count ~{req.hashtag_count})")
    elif tool == "comment":
        lines.append(_field("TARGET POST/COMMENT", req.target_text))
        lines.append(_bullets("MY POINTS", req.points))
    else:  # message
        if req.recipient_name:
            lines.append(f"Recipient name: {req.recipient_name}")
        lines.append(f"Relationship: {req.relationship or 'network'}")
        lines.append(f"Purpose: {req.purpose or 'introduction'}")
        lines.append(_field("RECIPIENT CONTEXT", req.recipient_context))
        lines.append(_field("SENDER CONTEXT", req.sender_context))

    user = "\n\n".join(l for l in lines if l)
    return system, user