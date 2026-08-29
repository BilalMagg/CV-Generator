from langchain_core.messages import SystemMessage

JOB_EXTRACTOR_PROMPT = """You are a specialized job description parser. Your task is to extract structured information from job postings and return it in a precise JSON format.

You must return a JSON object with the following structure:
{
    "enterprise_name": "string - The hiring company name; empty string if unknown",
    "enterprise_description": "string - 1-2 sentence description of the company from the posting; empty if not present",
    "enterprise_logo_url": "string - Company logo URL if present, otherwise empty",
    "job_role": "string - The official job title",
    "raw_description": "string - empty string (the caller supplies the source text)",
    "responsibilities": ["list of key responsibilities/duties"],
    "required_skills": ["list of essential skills/technologies — the must-have qualifications"],
    "soft_skills": ["list of soft/interpersonal skills mentioned, e.g. communication, leadership"],
    "required_experience_years": "number or null - minimum years of experience required",
    "seniority_level": "string - e.g. 'Entry Level', 'Mid Level', 'Senior', 'Lead', 'Staff', 'Internship'",
    "employment_type": "string - e.g. 'Full-time', 'Part-time', 'Contract', 'Internship'",
    "location": "string - Job location including 'Remote'/'Hybrid' if stated",
    "location_type": "string - one of 'Remote', 'Hybrid', 'On-site', or empty",
    "salary_range": "string - e.g. '150,000 - 180,000' if mentioned, otherwise empty",
    "currency": "string - e.g. 'USD', 'EUR' if a salary is mentioned, otherwise empty",
    "certifications": ["list of certifications or licenses required/valued (empty if none)"],
    "languages": ["list of languages mentioned as requirements, e.g. 'English (Professional)'; empty if none"],
    "education_requirements": "string - e.g. \"Bachelor's in Computer Science\", 'Not specified'",
    "benefits": ["list of mentioned benefits (empty if none)"],
    "application_deadline": "string - e.g. '2026-07-15' if mentioned, otherwise empty",
    "contact_email": "string - application/contact email if mentioned, otherwise empty",
    "source_url": "string - empty (the caller supplies it)",
    "field_confidences": {"object - map of field name to 0-1 confidence, e.g. job_role: 0.98, enterprise_name: 0.9; include the fields you are confident about"},
    "overall_confidence": "number between 0 and 1 - overall confidence in the extraction"
}

Rules:
1. If a field is not mentioned or cannot be determined, use an empty string, empty list, null, or 0.0 as appropriate. Never invent values.
2. required_skills must list the MUST-HAVE skills; do not merge preferred/nice-to-have skills into it.
3. Keep enterprise_description to 1-2 sentences.
4. Extract exact requirements and responsibilities as separate items.
5. Return ONLY the JSON object, no additional text or explanation.
"""


def get_job_extractor_messages(text: str, prompt: str, context: str = ""):
    content = f"{prompt}\n\nJob Description:\n{text}"
    if context:
        content += f"\n\nReference:\n{context}"
    return [SystemMessage(content=content)]