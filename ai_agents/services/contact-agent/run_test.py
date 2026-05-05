"""
run_test.py  –  Test the Contact Agent logic from the terminal.
==========================================================
Run this from the service root:
    cd ai_agents/services/contact-agent
    python run_test.py
"""

import asyncio
import json
import os
from datetime import datetime
from dotenv import load_dotenv

# Load env vars for API keys
load_dotenv()

# We need to make sure 'app' is importable. 
from app.agent import deliver_cv
from app.schemas import ContactInput
from app.models.cv_model import CVSection, OptimizedCV

async def test_agent():
    print("\n🚀 Starting Contact Agent Test...\n")
    
    # Mock data for testing
    sections = [
        CVSection(section_type="summary", content="Experienced software engineer with a focus on AI.", order=0),
        CVSection(section_type="skills", content="Python, FastAPI, LangGraph, LLMs", order=1),
    ]
    
    optimized_cv = OptimizedCV(
        job_id="test-job-123",
        final_sections=sections,
        ats_score_estimate=85,
        optimization_notes=["Looks good"],
        pdf_url=None, 
        generated_at=datetime.utcnow()
    )
    
    contact_input = ContactInput(
        optimized_cv=optimized_cv,
        job_title="AI Engineer",
        company_name="Tech Innovations Inc.",
        job_description="We are looking for an AI Engineer proficient in Python and LangGraph.",
        recipient_email="mohssinengu@gmail.com", 
        cover_letter_hint="Emphasize my experience with agentic workflows."
    )
    
    print(f"📧 Sending test application to: {contact_input.recipient_email}")
    
    try:
        output = await deliver_cv(contact_input)
        
        print("\n--- Agent Result ---")
        print(f"Success      : {output.success}")
        if output.success:
            print(f"Subject      : {output.subject_used}")
            print(f"Delivery ID  : {output.delivery_id}")
        else:
            print(f"Error        : {output.error_message}")
            
    except Exception as e:
        print(f"\n❌ Test failed with error: {e}")

if __name__ == "__main__":
    asyncio.run(test_agent())
