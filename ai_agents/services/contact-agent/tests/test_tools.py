"""
test_tools.py — Unit Tests for Contact Agent Tools
====================================================
Pure unit tests that do NOT call external APIs or send real emails.
Uses mocking for LLM and SMTP interactions.

Run:
    cd ai_agents/services/contact-agent
    python -m pytest tests/test_tools.py -v
"""

import json
import uuid
from unittest.mock import patch, MagicMock

import pytest

from app.tools import extract_cv_text, generate_email_subject, generate_email_body, send_email_with_cv


# ═══════════════════════════════════════════════════════════════════════════════
#  Tool 1 — extract_cv_text
# ═══════════════════════════════════════════════════════════════════════════════

class TestExtractCvText:

    def test_normal_case_sorted_by_order(self):
        """Sections should be sorted by 'order' field and formatted with headers."""
        sections_json = json.dumps([
            {"section_type": "contact", "content": "John Doe - john@email.com", "order": 2},
            {"section_type": "summary", "content": "Experienced developer", "order": 1},
        ])
        result = extract_cv_text.invoke({"sections_json": sections_json})

        assert "=== SUMMARY ===" in result
        assert "=== CONTACT ===" in result
        # Summary (order=1) should appear before Contact (order=2)
        assert result.index("=== SUMMARY ===") < result.index("=== CONTACT ===")

    def test_empty_list(self):
        """Empty section list should return an error message."""
        result = extract_cv_text.invoke({"sections_json": "[]"})
        assert result == "Error: sections_json is empty. No CV content to extract."

    def test_invalid_json(self):
        """Malformed JSON should return a parse error."""
        result = extract_cv_text.invoke({"sections_json": "not valid json"})
        assert "Error: Could not parse sections_json" in result

    def test_section_without_content(self):
        """Sections with empty content should be skipped."""
        sections_json = json.dumps([
            {"section_type": "skills", "content": "", "order": 1},
        ])
        result = extract_cv_text.invoke({"sections_json": sections_json})
        assert result == ""

    def test_multiple_sections_all_have_content(self):
        """All sections with content should be included."""
        sections_json = json.dumps([
            {"section_type": "skills", "content": "Python, Java", "order": 1},
            {"section_type": "experience", "content": "5 years at Google", "order": 2},
            {"section_type": "education", "content": "MIT CS", "order": 3},
        ])
        result = extract_cv_text.invoke({"sections_json": sections_json})
        assert "=== SKILLS ===" in result
        assert "=== EXPERIENCE ===" in result
        assert "=== EDUCATION ===" in result
        assert "Python, Java" in result
        assert "5 years at Google" in result
        assert "MIT CS" in result

    def test_sections_without_order_field(self):
        """Missing 'order' field should default to 0 and not crash."""
        sections_json = json.dumps([
            {"section_type": "summary", "content": "A good dev"},
        ])
        result = extract_cv_text.invoke({"sections_json": sections_json})
        assert "=== SUMMARY ===" in result
        assert "A good dev" in result

    def test_sections_with_whitespace_content(self):
        """Content that is only whitespace should be treated as empty."""
        sections_json = json.dumps([
            {"section_type": "skills", "content": "   ", "order": 1},
        ])
        result = extract_cv_text.invoke({"sections_json": sections_json})
        assert result == ""

    def test_single_section(self):
        """A single section should be properly formatted."""
        sections_json = json.dumps([
            {"section_type": "summary", "content": "Software Engineer", "order": 0},
        ])
        result = extract_cv_text.invoke({"sections_json": sections_json})
        assert result == "=== SUMMARY ===\nSoftware Engineer"


# ═══════════════════════════════════════════════════════════════════════════════
#  Tool 2 — generate_email_subject  (LLM mocked)
# ═══════════════════════════════════════════════════════════════════════════════

class TestGenerateEmailSubject:

    @patch("app.tools._llm")
    def test_returns_subject_line(self, mock_llm):
        """Should return the LLM-generated subject line, stripped."""
        mock_response = MagicMock()
        mock_response.content = "  Application for Data Engineer at Acme Corp  "
        mock_llm.invoke.return_value = mock_response

        result = generate_email_subject.invoke({
            "job_title": "Data Engineer",
            "company_name": "Acme Corp",
        })
        assert result == "Application for Data Engineer at Acme Corp"
        mock_llm.invoke.assert_called_once()

    @patch("app.tools._llm")
    def test_prompt_contains_job_info(self, mock_llm):
        """The prompt sent to the LLM should contain job title and company name."""
        mock_response = MagicMock()
        mock_response.content = "Subject Line"
        mock_llm.invoke.return_value = mock_response

        generate_email_subject.invoke({
            "job_title": "ML Engineer",
            "company_name": "DeepMind",
        })
        prompt_sent = mock_llm.invoke.call_args[0][0]
        assert "ML Engineer" in prompt_sent
        assert "DeepMind" in prompt_sent


# ═══════════════════════════════════════════════════════════════════════════════
#  Tool 3 — generate_email_body  (LLM mocked)
# ═══════════════════════════════════════════════════════════════════════════════

class TestGenerateEmailBody:

    @patch("app.tools._llm")
    def test_returns_body_text(self, mock_llm):
        """Should return the LLM-generated email body, stripped."""
        mock_response = MagicMock()
        mock_response.content = "Dear Hiring Manager,\n\nI am excited..."
        mock_llm.invoke.return_value = mock_response

        result = generate_email_body.invoke({
            "subject": "Application for AI Engineer",
            "job_description": "We need an AI engineer",
            "cv_text": "Skills: Python, ML",
            "cover_letter_hint": "Focus on ML experience",
        })
        assert result == "Dear Hiring Manager,\n\nI am excited..."

    @patch("app.tools._llm")
    def test_prompt_contains_all_inputs(self, mock_llm):
        """The prompt should contain subject, job description, CV text, and hint."""
        mock_response = MagicMock()
        mock_response.content = "Body text"
        mock_llm.invoke.return_value = mock_response

        generate_email_body.invoke({
            "subject": "My Subject",
            "job_description": "Build ML models",
            "cv_text": "Python expert with 5 years",
            "cover_letter_hint": "Emphasize leadership",
        })
        prompt_sent = mock_llm.invoke.call_args[0][0]
        assert "My Subject" in prompt_sent
        assert "Build ML models" in prompt_sent
        assert "Python expert with 5 years" in prompt_sent
        assert "Emphasize leadership" in prompt_sent

    @patch("app.tools._llm")
    def test_empty_hint_no_crash(self, mock_llm):
        """Empty cover_letter_hint should not cause issues."""
        mock_response = MagicMock()
        mock_response.content = "Body"
        mock_llm.invoke.return_value = mock_response

        result = generate_email_body.invoke({
            "subject": "Subject",
            "job_description": "Desc",
            "cv_text": "CV",
            "cover_letter_hint": "",
        })
        assert result == "Body"


# ═══════════════════════════════════════════════════════════════════════════════
#  Tool 4 — send_email_with_cv  (SMTP mocked)
# ═══════════════════════════════════════════════════════════════════════════════

class TestSendEmailWithCv:

    @patch("app.tools.settings")
    @patch("app.tools.smtplib.SMTP_SSL")
    def test_send_email_success_no_pdf(self, mock_smtp_class, mock_settings):
        """Should send a plain email without PDF and return SUCCESS."""
        mock_settings.SMTP_SERVER = "smtp.gmail.com"
        mock_settings.SMTP_PORT = 465
        mock_settings.SMTP_USERNAME = "test@gmail.com"
        mock_settings.SMTP_PASSWORD = "password123"

        mock_server = MagicMock()
        mock_smtp_class.return_value.__enter__ = MagicMock(return_value=mock_server)
        mock_smtp_class.return_value.__exit__ = MagicMock(return_value=False)

        result = send_email_with_cv.invoke({
            "recipient_email": "mohssinengu@gmail.com",
            "subject": "Test Subject",
            "body": "Test Body",
            "pdf_url": "",
        })

        assert "SUCCESS" in result
        assert "delivery_id=" in result
        assert "mohssinengu@gmail.com" in result
        assert "no PDF attached" in result

    @patch("app.tools.settings")
    def test_send_email_missing_credentials(self, mock_settings):
        """Should return an error if SMTP credentials are missing."""
        mock_settings.SMTP_SERVER = "smtp.gmail.com"
        mock_settings.SMTP_PORT = 465
        mock_settings.SMTP_USERNAME = None
        mock_settings.SMTP_PASSWORD = None

        result = send_email_with_cv.invoke({
            "recipient_email": "mohssinengu@gmail.com",
            "subject": "Test",
            "body": "Body",
            "pdf_url": "",
        })
        assert "Error" in result
        assert "EMAIL_USER" in result or "not set" in result

    @patch("app.tools.settings")
    @patch("app.tools.smtplib.SMTP_SSL")
    def test_send_email_smtp_auth_failure(self, mock_smtp_class, mock_settings):
        """Should handle SMTP authentication errors gracefully."""
        import smtplib

        mock_settings.SMTP_SERVER = "smtp.gmail.com"
        mock_settings.SMTP_PORT = 465
        mock_settings.SMTP_USERNAME = "test@gmail.com"
        mock_settings.SMTP_PASSWORD = "wrong_password"

        mock_server = MagicMock()
        mock_server.login.side_effect = smtplib.SMTPAuthenticationError(535, b"Auth failed")
        mock_smtp_class.return_value.__enter__ = MagicMock(return_value=mock_server)
        mock_smtp_class.return_value.__exit__ = MagicMock(return_value=False)

        result = send_email_with_cv.invoke({
            "recipient_email": "mohssinengu@gmail.com",
            "subject": "Test",
            "body": "Body",
            "pdf_url": "",
        })
        assert "Error" in result
        assert "authentication" in result.lower()

    @patch("app.tools.settings")
    @patch("app.tools.smtplib.SMTP_SSL")
    @patch("app.tools.requests.get")
    def test_send_email_with_pdf_url(self, mock_get, mock_smtp_class, mock_settings):
        """Should download PDF from URL and attach it to the email."""
        mock_settings.SMTP_SERVER = "smtp.gmail.com"
        mock_settings.SMTP_PORT = 465
        mock_settings.SMTP_USERNAME = "test@gmail.com"
        mock_settings.SMTP_PASSWORD = "password123"

        # Mock PDF download
        mock_response = MagicMock()
        mock_response.content = b"%PDF-1.4 fake pdf data"
        mock_response.raise_for_status = MagicMock()
        mock_get.return_value = mock_response

        # Mock SMTP
        mock_server = MagicMock()
        mock_smtp_class.return_value.__enter__ = MagicMock(return_value=mock_server)
        mock_smtp_class.return_value.__exit__ = MagicMock(return_value=False)

        result = send_email_with_cv.invoke({
            "recipient_email": "mohssinengu@gmail.com",
            "subject": "Application",
            "body": "Dear HR...",
            "pdf_url": "https://storage.example.com/cv/john_doe_cv.pdf",
        })

        assert "SUCCESS" in result
        assert "delivery_id=" in result
        assert "john_doe_cv.pdf" in result
        mock_get.assert_called_once()

    @patch("app.tools.settings")
    @patch("app.tools.requests.get")
    def test_send_email_pdf_download_failure(self, mock_get, mock_settings):
        """Should return error if PDF URL download fails."""
        import requests as req

        mock_settings.SMTP_SERVER = "smtp.gmail.com"
        mock_settings.SMTP_PORT = 465
        mock_settings.SMTP_USERNAME = "test@gmail.com"
        mock_settings.SMTP_PASSWORD = "password123"

        mock_get.side_effect = req.RequestException("Connection timeout")

        result = send_email_with_cv.invoke({
            "recipient_email": "mohssinengu@gmail.com",
            "subject": "Test",
            "body": "Body",
            "pdf_url": "https://broken-url.com/cv.pdf",
        })
        assert "Error" in result
        assert "broken-url.com" in result

    @patch("app.tools.settings")
    def test_send_email_local_pdf_not_found(self, mock_settings):
        """Should return error if local PDF file doesn't exist."""
        mock_settings.SMTP_SERVER = "smtp.gmail.com"
        mock_settings.SMTP_PORT = 465
        mock_settings.SMTP_USERNAME = "test@gmail.com"
        mock_settings.SMTP_PASSWORD = "password123"

        result = send_email_with_cv.invoke({
            "recipient_email": "mohssinengu@gmail.com",
            "subject": "Test",
            "body": "Body",
            "pdf_url": "/nonexistent/path/cv.pdf",
        })
        assert "Error" in result
        assert "not found" in result.lower()
