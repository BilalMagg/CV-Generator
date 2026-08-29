from langchain_core.tools import tool
import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from shared.config import settings


@tool
def send_email(to_email: str, subject: str, body: str) -> str:
    """Send an email using SMTP."""
    if not settings.SMTP_USERNAME or not settings.SMTP_PASSWORD:
        return "Error: SMTP credentials not configured"
    msg = MIMEMultipart()
    msg["From"] = settings.SMTP_USERNAME
    msg["To"] = to_email
    msg["Subject"] = subject
    msg.attach(MIMEText(body, "plain"))
    try:
        with smtplib.SMTP(settings.SMTP_SERVER, settings.SMTP_PORT) as server:
            server.starttls()
            server.login(settings.SMTP_USERNAME, settings.SMTP_PASSWORD)
            server.send_message(msg)
        return f"Email sent successfully to {to_email}"
    except Exception as e:
        return f"Failed to send email: {str(e)}"


@tool
def check_email_exists(email: str) -> bool:
    """Check if an email address appears to be valid format."""
    import re
    pattern = r'^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$'
    return bool(re.match(pattern, email))
