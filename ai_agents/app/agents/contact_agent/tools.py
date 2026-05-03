"""
tools.py  –  Contact Agent Tools
==================================
Four tools, each with ONE responsibility:

  Tool 1 → extract_cv_text        converts OptimizedCV.final_sections → plain string
  Tool 2 → generate_email_subject uses job_title + company_name       → subject line
  Tool 3 → generate_email_body    uses subject + job_desc + cv_text + hint → body paragraph
  Tool 4 → send_email_with_cv     sends the email + attaches the PDF via pdf_url

Why Tool 1 exists separately:
  OptimizedCV.final_sections is a List[CVSection] (structured Pydantic objects).
  The LLM cannot use them directly. Tool 1 flattens them into a readable plain
  string so Tool 3 can generate a tailored, accurate email body from real CV data.

Why we don't need pdfplumber:
  The CV text is already available in final_sections as rendered markdown.
  OptimizedCV.pdf_url is only used in Tool 4 to attach the file — not to read it.

Call order enforced by prompt.py:
  Tool 1 → Tool 2 → Tool 3 → Tool 4

Data flow:
─────────────────────────────────────────────────────────────────────────────
  optimized_cv.final_sections ──────────────────► Tool 1 → cv_text: str
                                                                  │
  job_title + company_name ─────────────────────► Tool 2 → subject: str
                                                                  │
  subject + job_description + cv_text + hint ───► Tool 3 → body: str
                                                                  │
  recipient_email + subject + body + pdf_url ───► Tool 4 → SUCCESS / delivery_id
─────────────────────────────────────────────────────────────────────────────
"""

import os
import smtplib
import uuid
from email.mime.application import MIMEApplication
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText
from typing import Optional

import requests                           # pip install requests  (for downloading pdf_url)
from langchain_core.tools import tool
from langchain_groq import ChatGroq
from dotenv import load_dotenv

load_dotenv()

# Shared LLM instance used by Tools 2 & 3
_llm = ChatGroq(model="llama-3.3-70b-versatile", temperature=0.3)


# ─────────────────────────────────────────────────────────────────────────────
#  TOOL 1 – Extract plain text from OptimizedCV.final_sections
#
#  Input  : sections_json  →  JSON string of List[CVSection]
#                             (serialized before passing to the tool)
#  Output : plain text string of the full CV, section by section
#
#  Why JSON string?
#    LangGraph tools only accept simple Python types (str, int, etc.).
#    We serialize the CVSection list to JSON in agent.py before calling this tool,
#    and deserialize it here to rebuild the structured data.
# ─────────────────────────────────────────────────────────────────────────────
@tool
def extract_cv_text(sections_json: str) -> str:
    """
    Convert the OptimizedCV's final_sections (a JSON string) into a clean,
    readable plain-text string the LLM can use to generate the email body.

    Call this tool FIRST before generating the subject or body.

    Args:
        sections_json : JSON-serialized list of CVSection objects.
                        Each object has: section_type (str), content (str), order (int).
                        Example: '[{"section_type": "summary", "content": "...", "order": 0}, ...]'

    Returns:
        A plain-text string of the full CV, formatted as:
            === SUMMARY ===
            <content>

            === EXPERIENCE ===
            <content>
            ...

        Returns an error message string if parsing fails.
    """
    import json

    try:
        sections = json.loads(sections_json)
    except Exception as e:
        return f"Error: Could not parse sections_json — {str(e)}"

    if not sections:
        return "Error: sections_json is empty. No CV content to extract."

    # Sort by order field so sections appear in the correct display order
    sections_sorted = sorted(sections, key=lambda s: s.get("order", 0))

    parts = []
    for section in sections_sorted:
        section_type = section.get("section_type", "SECTION").upper()
        content      = section.get("content", "").strip()
        if content:
            parts.append(f"=== {section_type} ===\n{content}")

    return "\n\n".join(parts)


# ─────────────────────────────────────────────────────────────────────────────
#  TOOL 2 – Generate the email subject line
#
#  Input  : job_title, company_name
#  Output : short professional subject string (max ~10 words)
# ─────────────────────────────────────────────────────────────────────────────
@tool
def generate_email_subject(job_title: str, company_name: str) -> str:
    """
    Generate a short professional email subject line for a job application.
    Call this tool SECOND, after extracting the CV text.

    Args:
        job_title    : The exact job title from the job description.
                       Example: "Data Engineer"
        company_name : The name of the company being applied to.
                       Example: "Acme Corp"

    Returns:
        A single subject line string (max ~10 words).
        Example: "Application for Data Engineer Position – Acme Corp"
    """
    prompt = (
        f"Write ONE professional email subject line for a job application.\n"
        f"Position : {job_title}\n"
        f"Company  : {company_name}\n\n"
        f"Rules:\n"
        f"- Maximum 10 words\n"
        f"- Do NOT add quotes, explanation, or extra lines\n"
        f"- Return ONLY the subject line text\n"
    )
    response = _llm.invoke(prompt)
    return response.content.strip()


# ─────────────────────────────────────────────────────────────────────────────
#  TOOL 3 – Generate the email body paragraph
#
#  Input  : subject, job_description, cv_text, cover_letter_hint (optional)
#  Output : 3-4 sentence professional cover paragraph
#
#  Why all four inputs?
#    subject            → keeps body tone consistent with the subject line
#    job_description    → lets LLM highlight the most relevant candidate skills
#    cv_text            → gives LLM the actual candidate data to reference
#    cover_letter_hint  → respects any tone/focus direction from the candidate
# ─────────────────────────────────────────────────────────────────────────────
@tool
def generate_email_body(
    subject: str,
    job_description: str,
    cv_text: str,
    cover_letter_hint: str = "",
) -> str:
    """
    Generate a short professional email body for a job application sent to HR.
    Call this tool THIRD, after you have the subject and the CV text.

    Args:
        subject           : Subject line from generate_email_subject.
        job_description   : Full job description text from the Job Extractor.
        cv_text           : Plain text CV from extract_cv_text.
        cover_letter_hint : (optional) Candidate's direction for tone or key points.
                            Pass empty string "" if not provided.

    Returns:
        A 3-4 sentence plain-text email body paragraph.
    """
    hint_block = (
        f"\nCandidate's hint for tone/focus: {cover_letter_hint}\n"
        if cover_letter_hint else ""
    )

    prompt = (
        f"You are writing a job application email body on behalf of a candidate.\n"
        f"This email will be sent to the HR department of the target company.\n\n"
        f"Email subject    : {subject}\n"
        f"{hint_block}"
        f"\nJob Description  :\n{job_description}\n\n"
        f"Candidate CV     :\n{cv_text}\n\n"
        f"Write a SHORT professional email body (3-4 sentences) that:\n"
        f"1. Opens by referencing the position (stay consistent with the subject line)\n"
        f"2. Highlights 1-2 of the candidate's strongest skills that match the job description\n"
        f"3. Mentions that the CV is attached to this email\n"
        f"4. Closes politely and professionally\n\n"
        f"Rules:\n"
        f"- Write in first person (the candidate is the author)\n"
        f"- Plain text only — no bullet points, no markdown\n"
        f"- Do NOT include subject line, greeting with a name, or signature\n"
        f"- Return ONLY the body paragraph text\n"
    )
    response = _llm.invoke(prompt)
    return response.content.strip()


# ─────────────────────────────────────────────────────────────────────────────
#  TOOL 4 – Send the email with the CV PDF attached
#
#  Input  : recipient_email, subject, body, pdf_url
#  Output : success string with a unique delivery_id
#
#  pdf_url comes from OptimizedCV.pdf_url.
#  If it starts with "http", we download it first.
#  If it's a local file path, we read it directly from disk.
# ─────────────────────────────────────────────────────────────────────────────
@tool
def send_email_with_cv(
    recipient_email: str,
    subject: str,
    body: str,
    pdf_url: str = "",
) -> str:
    """
    Send the job-application email to the HR contact, optionally with the CV PDF attached.
    Call this tool LAST, after you have both the subject and the body.

    Args:
        recipient_email : HR / RH email address. Example: "rh@company.com"
        subject         : Subject line from generate_email_subject.
        body            : Body paragraph from generate_email_body.
        pdf_url         : Value of OptimizedCV.pdf_url.
                          Can be an HTTP URL (will be downloaded) or a local file path.
                          Pass empty string "" if OptimizedCV.pdf_url is None.

    Returns:
        A success message containing a unique delivery_id, or an error message string.
    """
    sender_email    = os.getenv("EMAIL_USER")
    sender_password = os.getenv("EMAIL_PASS")

    if not sender_email or not sender_password:
        return "Error: EMAIL_USER or EMAIL_PASS not set in environment variables."

    # ── Build the MIME message ────────────────────────────────────────────
    msg = MIMEMultipart("mixed")
    msg["From"]    = sender_email
    msg["To"]      = recipient_email
    msg["Subject"] = subject
    msg.attach(MIMEText(body, "plain", "utf-8"))

    # ── Attach PDF if pdf_url is provided ────────────────────────────────
    pdf_filename = None
    if pdf_url:
        try:
            if pdf_url.startswith("http://") or pdf_url.startswith("https://"):
                # Download the PDF from the URL
                response = requests.get(pdf_url, timeout=15)
                response.raise_for_status()
                pdf_data     = response.content
                pdf_filename = pdf_url.split("/")[-1] or "cv.pdf"
            else:
                # Treat as a local file path
                if not os.path.exists(pdf_url):
                    return (
                        f"Error: PDF not found at path '{pdf_url}'. "
                        f"Email was NOT sent."
                    )
                with open(pdf_url, "rb") as f:
                    pdf_data = f.read()
                pdf_filename = os.path.basename(pdf_url)

            attachment = MIMEApplication(pdf_data, _subtype="pdf")
            attachment.add_header(
                "Content-Disposition", "attachment", filename=pdf_filename
            )
            msg.attach(attachment)

        except requests.RequestException as e:
            return f"Error: Could not download PDF from '{pdf_url}' — {str(e)}"

    # ── Send via Gmail SMTP over SSL ──────────────────────────────────────
    try:
        with smtplib.SMTP_SSL("smtp.gmail.com", 465) as server:
            server.login(sender_email, sender_password)
            server.send_message(msg)
    except smtplib.SMTPAuthenticationError:
        return "Error: SMTP authentication failed. Check EMAIL_USER and EMAIL_PASS."
    except smtplib.SMTPException as e:
        return f"Error: SMTP error — {str(e)}"

    delivery_id  = str(uuid.uuid4())
    pdf_note     = f" with PDF attachment '{pdf_filename}'" if pdf_filename else " (no PDF attached)"
    return (
        f"SUCCESS: Email sent to '{recipient_email}'{pdf_note}. "
        f"delivery_id={delivery_id}"
    )