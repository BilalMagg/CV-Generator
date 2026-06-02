"""
test_integration.py — Integration Tests for the Contact Agent
==============================================================
These tests exercise the full FastAPI app and, optionally, send a REAL email.

UNIT-LEVEL integration (mocked agent):
    python -m pytest tests/test_integration.py -v -k "not real_email"

LIVE EMAIL test (sends to mohssinengu@gmail.com — requires valid .env):
    python -m pytest tests/test_integration.py -v -k "real_email" --run-live

To enable the live email test, pass --run-live flag.
"""

import uuid
from datetime import datetime
from unittest.mock import AsyncMock, patch, MagicMock

import pytest
# pyrefly: ignore [missing-import]
from httpx import AsyncClient, ASGITransport

from app.main import app
from app.models.cv_model import CVSection, OptimizedCV


# ═══════════════════════════════════════════════════════════════════════════════
#  Fixtures
# ═══════════════════════════════════════════════════════════════════════════════

def _build_deliver_payload(**overrides):
    """Build a valid JSON payload for POST /api/v1/deliver."""
    sections = [
        {"section_type": "summary", "content": "Experienced software engineer specializing in AI and distributed systems.", "order": 0},
        {"section_type": "skills", "content": "Python, FastAPI, LangGraph, Docker, Kubernetes, PostgreSQL", "order": 1},
        {"section_type": "experience", "content": "5 years at Google working on ML pipelines", "order": 2},
        {"section_type": "education", "content": "MSc Computer Science — MIT", "order": 3},
        {"section_type": "contact", "content": "John Doe | john.doe@email.com | +1-555-0100", "order": 4},
    ]

    payload = {
        "optimized_cv": {
            "job_id": "integration-test-001",
            "final_sections": sections,
            "ats_score_estimate": 92,
            "optimization_notes": ["Strong match for backend role"],
            "pdf_url": None,
            "generated_at": datetime.utcnow().isoformat(),
        },
        "job_title": "Senior Backend Engineer",
        "company_name": "OpenAI",
        "job_description": "We are looking for a Senior Backend Engineer with experience in Python, distributed systems, and ML infrastructure.",
        "recipient_email": "mohssinengu@gmail.com",
        "cover_letter_hint": "Emphasize experience with ML pipelines and distributed systems.",
    }
    payload.update(overrides)
    return payload


# ═══════════════════════════════════════════════════════════════════════════════
#  API Endpoint Tests (mocked agent — no real email)
# ═══════════════════════════════════════════════════════════════════════════════

class TestDeliverEndpoint:

    @pytest.mark.asyncio
    @patch("app.routers.deliver_cv")
    async def test_deliver_endpoint_success(self, mock_deliver_cv):
        """POST /api/v1/deliver should return 200 with a ContactOutput on success."""
        from app.schemas import ContactOutput

        mock_deliver_cv.return_value = ContactOutput(
            success=True,
            delivery_id="test-delivery-id",
            subject_used="Application for Senior Backend Engineer at OpenAI",
            error_message=None,
        )

        transport = ASGITransport(app=app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            response = await client.post("/api/v1/deliver", json=_build_deliver_payload())

        assert response.status_code == 200
        data = response.json()
        assert data["success"] is True
        assert data["delivery_id"] == "test-delivery-id"
        assert "Senior Backend Engineer" in data["subject_used"]

    @pytest.mark.asyncio
    @patch("app.routers.deliver_cv")
    async def test_deliver_endpoint_failure(self, mock_deliver_cv):
        """POST /api/v1/deliver should return 200 but with success=False on agent failure."""
        from app.schemas import ContactOutput

        mock_deliver_cv.return_value = ContactOutput(
            success=False,
            delivery_id=str(uuid.uuid4()),
            subject_used="N/A",
            error_message="SMTP authentication failed",
        )

        transport = ASGITransport(app=app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            response = await client.post("/api/v1/deliver", json=_build_deliver_payload())

        assert response.status_code == 200
        data = response.json()
        assert data["success"] is False
        assert data["error_message"] is not None

    @pytest.mark.asyncio
    async def test_deliver_endpoint_validation_error(self):
        """POST /api/v1/deliver with missing fields should return 422."""
        transport = ASGITransport(app=app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            response = await client.post("/api/v1/deliver", json={"job_title": "Engineer"})

        assert response.status_code == 422

    @pytest.mark.asyncio
    async def test_health_endpoint(self):
        """GET /api/v1/health should return 200 with service name."""
        transport = ASGITransport(app=app)
        async with AsyncClient(transport=transport, base_url="http://test") as client:
            response = await client.get("/api/v1/health")

        assert response.status_code == 200
        data = response.json()
        assert data["status"] == "ok"
        assert data["service"] == "contact-agent"


# ═══════════════════════════════════════════════════════════════════════════════
#  LIVE Integration Test — Sends a REAL email
#  Only runs with: pytest --run-live
# ═══════════════════════════════════════════════════════════════════════════════

def pytest_configure(config):
    config.addinivalue_line("markers", "live: mark test as a live integration test")


@pytest.mark.live
class TestLiveEmail:
    """
    ⚠️ These tests send REAL emails to mohssinengu@gmail.com.
    They require valid SMTP credentials in .env (EMAIL_USER, EMAIL_PASS).

    Run with:  python -m pytest tests/test_integration.py -v -k "real_email" --run-live
    """

    @pytest.mark.asyncio
    async def test_real_email_send(self):
        """
        End-to-end test: calls deliver_cv() with real LLM + real SMTP.
        Sends a real email to mohssinengu@gmail.com.
        """
        from app.agent import deliver_cv
        from app.schemas import ContactInput

        inp = ContactInput(
            optimized_cv=OptimizedCV(
                job_id="live-test-001",
                final_sections=[
                    CVSection(section_type="summary", content="Mohssine El Addaoui — Software Engineering student at EMSI with a passion for AI and microservices.", order=0),
                    CVSection(section_type="skills", content="Python, FastAPI, Angular, .NET, Docker, Kubernetes, LangChain, Kafka", order=1),
                    CVSection(section_type="experience", content="Intern at TechCorp — Built ML pipelines using Python and FastAPI", order=2),
                    CVSection(section_type="education", content="EMSI Marrakech — Software Engineering, 2024", order=3),
                    CVSection(section_type="contact", content="Mohssine El Addaoui | mohssinengu@gmail.com | +212-600-000000", order=4),
                ],
                ats_score_estimate=88,
                optimization_notes=["Good skills match"],
                pdf_url=None,
                generated_at=datetime.utcnow(),
            ),
            job_title="Full Stack Developer",
            company_name="Test Company (Integration Test)",
            job_description="Looking for a Full Stack Developer proficient in Python and Angular.",
            recipient_email="mohssinengu@gmail.com",
            cover_letter_hint="This is an integration test email — please ignore.",
        )

        output = await deliver_cv(inp)

        print(f"\n📧 Live Email Test Result:")
        print(f"   Success     : {output.success}")
        print(f"   Subject     : {output.subject_used}")
        print(f"   Delivery ID : {output.delivery_id}")
        if output.error_message:
            print(f"   Error       : {output.error_message}")

        assert output.success is True, f"Email delivery failed: {output.error_message}"
        assert output.delivery_id is not None
