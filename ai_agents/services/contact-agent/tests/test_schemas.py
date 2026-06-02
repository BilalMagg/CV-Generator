"""
test_schemas.py — Unit Tests for Contact Agent Schemas
======================================================
Validates Pydantic models (ContactInput, ContactOutput) and their constraints.

Run:
    cd ai_agents/services/contact-agent
    python -m pytest tests/test_schemas.py -v
"""

import pytest
from datetime import datetime
# pyrefly: ignore [missing-import]
from pydantic import ValidationError

from app.schemas import ContactInput, ContactOutput
from app.models.cv_model import CVSection, OptimizedCV


# ═══════════════════════════════════════════════════════════════════════════════
#  Helpers
# ═══════════════════════════════════════════════════════════════════════════════

def _make_optimized_cv(**overrides):
    """Create a valid OptimizedCV with sensible defaults."""
    defaults = dict(
        job_id="test-job-001",
        final_sections=[
            CVSection(section_type="summary", content="Senior dev", order=0),
            CVSection(section_type="skills", content="Python, FastAPI", order=1),
        ],
        ats_score_estimate=85,
        optimization_notes=["Good match"],
        pdf_url=None,
        generated_at=datetime.utcnow(),
    )
    defaults.update(overrides)
    return OptimizedCV(**defaults)


def _make_contact_input(**overrides):
    """Create a valid ContactInput with sensible defaults."""
    defaults = dict(
        optimized_cv=_make_optimized_cv(),
        job_title="AI Engineer",
        company_name="Tech Corp",
        job_description="Looking for an AI Engineer with Python experience.",
        recipient_email="mohssinengu@gmail.com",
        cover_letter_hint=None,
    )
    defaults.update(overrides)
    return ContactInput(**defaults)


# ═══════════════════════════════════════════════════════════════════════════════
#  ContactInput Tests
# ═══════════════════════════════════════════════════════════════════════════════

class TestContactInput:

    def test_valid_input(self):
        """A fully valid ContactInput should construct without errors."""
        inp = _make_contact_input()
        assert inp.job_title == "AI Engineer"
        assert inp.company_name == "Tech Corp"
        assert inp.recipient_email == "mohssinengu@gmail.com"
        assert inp.cover_letter_hint is None

    def test_with_cover_letter_hint(self):
        """Optional cover_letter_hint should be accepted."""
        inp = _make_contact_input(cover_letter_hint="Focus on leadership")
        assert inp.cover_letter_hint == "Focus on leadership"

    def test_with_pdf_url(self):
        """optimized_cv can include a pdf_url."""
        cv = _make_optimized_cv(pdf_url="https://storage.example.com/cv.pdf")
        inp = _make_contact_input(optimized_cv=cv)
        assert inp.optimized_cv.pdf_url == "https://storage.example.com/cv.pdf"

    def test_missing_required_field_job_title(self):
        """Missing job_title should raise ValidationError."""
        with pytest.raises(ValidationError):
            ContactInput(
                optimized_cv=_make_optimized_cv(),
                company_name="Corp",
                job_description="Desc",
                recipient_email="test@test.com",
            )

    def test_missing_required_field_recipient_email(self):
        """Missing recipient_email should raise ValidationError."""
        with pytest.raises(ValidationError):
            ContactInput(
                optimized_cv=_make_optimized_cv(),
                job_title="Engineer",
                company_name="Corp",
                job_description="Desc",
            )

    def test_missing_optimized_cv(self):
        """Missing optimized_cv should raise ValidationError."""
        with pytest.raises(ValidationError):
            ContactInput(
                job_title="Engineer",
                company_name="Corp",
                job_description="Desc",
                recipient_email="test@test.com",
            )

    def test_optimized_cv_with_empty_sections(self):
        """An OptimizedCV with no sections should still be valid."""
        cv = _make_optimized_cv(final_sections=[])
        inp = _make_contact_input(optimized_cv=cv)
        assert len(inp.optimized_cv.final_sections) == 0

    def test_ats_score_boundaries(self):
        """ats_score_estimate must be between 0 and 100."""
        cv = _make_optimized_cv(ats_score_estimate=0)
        assert cv.ats_score_estimate == 0

        cv = _make_optimized_cv(ats_score_estimate=100)
        assert cv.ats_score_estimate == 100

        with pytest.raises(ValidationError):
            _make_optimized_cv(ats_score_estimate=-1)

        with pytest.raises(ValidationError):
            _make_optimized_cv(ats_score_estimate=101)


# ═══════════════════════════════════════════════════════════════════════════════
#  ContactOutput Tests
# ═══════════════════════════════════════════════════════════════════════════════

class TestContactOutput:

    def test_successful_output(self):
        """A successful ContactOutput should have success=True and no error."""
        out = ContactOutput(
            success=True,
            delivery_id="abc-123",
            subject_used="Application for AI Engineer at Tech Corp",
            error_message=None,
        )
        assert out.success is True
        assert out.delivery_id == "abc-123"
        assert out.subject_used == "Application for AI Engineer at Tech Corp"
        assert out.error_message is None
        assert isinstance(out.sent_at, datetime)

    def test_failed_output(self):
        """A failed ContactOutput should include an error_message."""
        out = ContactOutput(
            success=False,
            delivery_id="xyz-456",
            subject_used="N/A",
            error_message="SMTP authentication failed",
        )
        assert out.success is False
        assert out.error_message == "SMTP authentication failed"

    def test_sent_at_auto_generated(self):
        """sent_at should be auto-generated if not provided."""
        out = ContactOutput(
            success=True,
            delivery_id="test-id",
            subject_used="Subject",
        )
        assert out.sent_at is not None
        assert isinstance(out.sent_at, datetime)

    def test_missing_delivery_id(self):
        """delivery_id is required."""
        with pytest.raises(ValidationError):
            ContactOutput(
                success=True,
                subject_used="Subject",
            )
