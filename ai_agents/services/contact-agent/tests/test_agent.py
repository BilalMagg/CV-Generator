"""
test_agent.py — Unit Tests for the Agent orchestration logic
=============================================================
Tests deliver_cv() with fully mocked LangGraph agent.

Run:
    cd ai_agents/services/contact-agent
    python -m pytest tests/test_agent.py -v
"""

import re
import uuid
from datetime import datetime
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from app.schemas import ContactInput, ContactOutput
from app.models.cv_model import CVSection, OptimizedCV


def _make_contact_input():
    """Build a valid ContactInput for testing."""
    return ContactInput(
        optimized_cv=OptimizedCV(
            job_id="test-job-001",
            final_sections=[
                CVSection(section_type="summary", content="Senior Python developer", order=0),
                CVSection(section_type="skills", content="Python, FastAPI, Docker", order=1),
            ],
            ats_score_estimate=90,
            optimization_notes=["Excellent match"],
            pdf_url="https://storage.example.com/cv.pdf",
            generated_at=datetime.utcnow(),
        ),
        job_title="Backend Engineer",
        company_name="Google",
        job_description="We need a backend engineer with Python experience.",
        recipient_email="mohssinengu@gmail.com",
        cover_letter_hint="Highlight distributed systems experience",
    )


def _make_agent_messages(tool_outputs: dict, final_ai_text: str):
    """
    Build a list of mock messages that simulates the LangGraph agent output.
    tool_outputs: dict of {tool_name: output_string}
    final_ai_text: the final AI message content
    """
    messages = []

    for tool_name, output in tool_outputs.items():
        tool_msg = MagicMock()
        tool_msg.name = tool_name
        tool_msg.content = output
        tool_msg.type = "tool"
        messages.append(tool_msg)

    ai_msg = MagicMock()
    ai_msg.type = "ai"
    ai_msg.content = final_ai_text
    ai_msg.name = None
    messages.append(ai_msg)

    return messages


class TestDeliverCv:

    @pytest.mark.asyncio
    @patch("app.agent._agent")
    async def test_successful_delivery(self, mock_agent):
        """deliver_cv should return success when the agent completes all 4 tools."""
        delivery_id = str(uuid.uuid4())
        tool_outputs = {
            "extract_cv_text": "=== SUMMARY ===\nSenior Python developer\n\n=== SKILLS ===\nPython, FastAPI, Docker",
            "generate_email_subject": "Application for Backend Engineer at Google",
            "generate_email_body": "Dear Hiring Manager,\n\nI am a senior Python developer...",
            "send_email_with_cv": f"SUCCESS: Email sent to 'mohssinengu@gmail.com' with PDF attachment 'cv.pdf'. delivery_id={delivery_id}",
        }
        final_text = f"Email successfully sent! delivery_id={delivery_id}"
        messages = _make_agent_messages(tool_outputs, final_text)

        mock_agent.ainvoke = AsyncMock(return_value={"messages": messages})

        inp = _make_contact_input()

        # Import deliver_cv here to pick up the patched _agent
        from app.agent import deliver_cv
        output = await deliver_cv(inp)

        assert output.success is True
        assert output.delivery_id == delivery_id
        assert "Application for Backend Engineer at Google" in output.subject_used
        assert output.error_message is None

    @pytest.mark.asyncio
    @patch("app.agent._agent")
    async def test_failed_delivery(self, mock_agent):
        """deliver_cv should return success=False when the send_email tool errors."""
        tool_outputs = {
            "extract_cv_text": "=== SUMMARY ===\nDev",
            "generate_email_subject": "Subject",
            "generate_email_body": "Body",
            "send_email_with_cv": "Error: SMTP authentication failed. Check EMAIL_USER and EMAIL_PASS.",
        }
        final_text = "Error: SMTP authentication failed."
        messages = _make_agent_messages(tool_outputs, final_text)

        mock_agent.ainvoke = AsyncMock(return_value={"messages": messages})

        inp = _make_contact_input()

        from app.agent import deliver_cv
        output = await deliver_cv(inp)

        assert output.success is False
        assert output.error_message is not None

    @pytest.mark.asyncio
    @patch("app.agent._agent")
    async def test_delivery_id_generated_when_missing(self, mock_agent):
        """If the tool output doesn't contain delivery_id, one should be generated."""
        tool_outputs = {
            "extract_cv_text": "CV text",
            "generate_email_subject": "Subject",
            "generate_email_body": "Body",
            "send_email_with_cv": "SUCCESS: Email sent to 'mohssinengu@gmail.com' (no PDF attached).",
        }
        final_text = "SUCCESS: Email sent."
        messages = _make_agent_messages(tool_outputs, final_text)

        mock_agent.ainvoke = AsyncMock(return_value={"messages": messages})

        inp = _make_contact_input()

        from app.agent import deliver_cv
        output = await deliver_cv(inp)

        assert output.success is True
        # delivery_id should still be a valid UUID
        assert output.delivery_id is not None
        uuid.UUID(output.delivery_id)  # Should not raise

    @pytest.mark.asyncio
    @patch("app.agent._agent")
    async def test_none_pdf_url_handled(self, mock_agent):
        """When pdf_url is None, it should be converted to empty string in user message."""
        tool_outputs = {
            "extract_cv_text": "CV text",
            "generate_email_subject": "Subject",
            "generate_email_body": "Body",
            "send_email_with_cv": "SUCCESS: Email sent. delivery_id=abc-123",
        }
        final_text = "Done. delivery_id=abc-123"
        messages = _make_agent_messages(tool_outputs, final_text)

        mock_agent.ainvoke = AsyncMock(return_value={"messages": messages})

        inp = _make_contact_input()
        inp.optimized_cv.pdf_url = None  # Explicitly set to None

        from app.agent import deliver_cv
        output = await deliver_cv(inp)

        # The agent should still be called (not crash)
        mock_agent.ainvoke.assert_called_once()
        assert output.success is True


# ═══════════════════════════════════════════════════════════════════════════════
#  _extract_tool_output helper
# ═══════════════════════════════════════════════════════════════════════════════

class TestExtractToolOutput:

    def test_finds_tool_by_name(self):
        from app.agent import _extract_tool_output

        msg = MagicMock()
        msg.name = "generate_email_subject"
        msg.content = "Application for ML Engineer"

        result = _extract_tool_output([msg], "generate_email_subject")
        assert result == "Application for ML Engineer"

    def test_returns_na_when_not_found(self):
        from app.agent import _extract_tool_output
        result = _extract_tool_output([], "nonexistent_tool")
        assert result == "N/A"

    def test_strips_whitespace(self):
        from app.agent import _extract_tool_output

        msg = MagicMock()
        msg.name = "send_email_with_cv"
        msg.content = "  SUCCESS  "

        result = _extract_tool_output([msg], "send_email_with_cv")
        assert result == "SUCCESS"
