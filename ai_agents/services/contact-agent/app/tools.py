"""
tools.py  –  Contact Agent Tools
==================================
Four tools, each with ONE responsibility:

  Tool 1 → extract_cv_text        converts OptimizedCV.final_sections → plain string
  Tool 2 → generate_email_subject uses job_title + company_name       → subject line
  Tool 3 → generate_email_body    uses subject + job_desc + cv_text + hint → body paragraph
  Tool 4 → send_email_with_cv     sends the email + attaches the PDF via pdf_url
"""

import os
import smtplib
import uuid
from email.mime.application import MIMEApplication
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText
from typing import Optional

import requests                           # pip install requests
from app.core.config import settings

from langchain_core.tools import tool

from langchain_groq import ChatGroq

from dotenv import load_dotenv

load_dotenv()

# Shared LLM instance used by Tools 2 & 3
# Using Groq here to spread load across providers (agent uses Mistral)
_llm = ChatGroq(
    model=settings.TOOL_MODEL,
    temperature=0.3,
    #temperature=settings.LLM_TEMPERATURE,
)

@tool
def extract_cv_text(sections_json: str) -> str:
    """
    Convert the OptimizedCV's final_sections (a JSON string) into a clean,
    readable plain-text string the LLM can use to generate the email body.

    Call this tool FIRST before generating the subject or body.
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


@tool
def generate_email_subject(job_title: str, company_name: str) -> str:
    """
    Generate a short professional email subject line for a job application.
    Call this tool SECOND, after extracting the CV text.
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

@tool
def generate_email_body(
    subject: str,
    job_description: str,
    cv_text: str,
    cover_letter_hint: str = "",
) -> str:
    """
    Generate a professional email body for a job application sent to HR.
    Call this tool THIRD, after you have the subject and the CV text.
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
        f"CRITICAL: You MUST use real newline characters (\\n) between every section.\n"
        f"Do NOT write everything in one paragraph. Each block must be separated by a blank line.\n\n"
        f"Write the email body using this EXACT structure:\n\n"
        f"Dear Hiring Manager,\n\n"
        f"[1 sentence: who you are, your studies/role, and what position you are applying for]\n\n"
        f"[1 sentence: express motivation to join and contribute]\n\n"
        f"My key skills relevant to this role include:\n"
        f"- [Skill category]: [tools/technologies]\n"
        f"- [Skill category]: [tools/technologies]\n"
        f"- [Skill category]: [tools/technologies]\n\n"
        f"Please find my CV attached for a detailed overview of my background.\n\n"
        f"[1 sentence: availability for interview + thank you]\n\n"
        f"Sincerely,\n"
        f"[Full Name]\n"
        f"[Phone]\n"
        f"[Email]\n\n"
        f"Rules:\n"
        f"- Extract candidate name, phone, email from the CV\n"
        f"- Use ONLY plain text, real newlines between each block\n"
        f"- Skills must be bullet lines starting with '-'\n"
        f"- Do NOT put everything in one paragraph\n"
        f"- Return ONLY the email body, nothing else\n"
    )
    response = _llm.invoke(prompt)
    return response.content.strip()
    
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
    """
    # 1. Get credentials from settings
    smtp_server = settings.SMTP_SERVER or "smtp.gmail.com"
    smtp_port = settings.SMTP_PORT
    sender_email = settings.SMTP_USERNAME
    sender_password = settings.SMTP_PASSWORD

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
