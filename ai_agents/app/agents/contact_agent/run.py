"""
run.py  –  Contact Agent Terminal Runner
==========================================
Run this file directly from the contact_agent folder:

    cd path/to/CV-Generator/ai_agents
    python -m app.agents.contact_agent.run

It will ask you for the inputs one by one, then fire the agent
and show you what each tool does in real time.
"""

import asyncio
import json
from datetime import datetime

from dotenv import load_dotenv
load_dotenv()

from app.models.cv_model import CVSection, OptimizedCV
from app.agents.contact_agent.schemas import ContactInput
from app.agents.contact_agent.agent import deliver_cv


# ── Helpers ───────────────────────────────────────────────────────────────────

def ask(prompt: str, default: str = "") -> str:
    """Ask the user for input, showing a default value if provided."""
    if default:
        value = input(f"{prompt} [{default}]: ").strip()
        return value if value else default
    return input(f"{prompt}: ").strip()


def print_separator():
    print("\n" + "─" * 60 + "\n")


# ── Main loop ─────────────────────────────────────────────────────────────────

async def main():
    print("\n📧  Contact Agent — Terminal Runner")
    print("=" * 60)
    print("This agent will:")
    print("  1. Read your CV sections")
    print("  2. Generate a professional subject line")
    print("  3. Generate a tailored email body")
    print("  4. Send the email to the HR contact with your CV attached")
    print("\nType 'exit' at any prompt to quit.\n")

    while True:
        print_separator()
        print("📝  Enter the job information:\n")

        # ── Collect inputs ────────────────────────────────────────────────────

        job_title = ask("Job title (e.g. Data Engineer)")
        if job_title.lower() == "exit":
            break

        company_name = ask("Company name (e.g. Acme Corp)")
        if company_name.lower() == "exit":
            break

        print("\nJob description (paste it, then press Enter twice when done):")
        lines = []
        while True:
            line = input()
            if line.lower() == "exit":
                break
            if line == "" and lines and lines[-1] == "":
                break
            lines.append(line)
        job_description = "\n".join(lines).strip()
        if not job_description:
            print("⚠️  Job description cannot be empty.")
            continue

        print_separator()
        print("📄  CV Sections")
        print("Enter your CV sections one by one.")
        print("Available types: summary, experience, projects, skills, education")
        print("Press Enter with empty section type when done.\n")

        sections = []
        order = 0
        while True:
            section_type = ask(f"Section type (or press Enter to finish)").strip().lower()
            if not section_type:
                if not sections:
                    print("⚠️  You need at least one CV section.")
                    continue
                break
            print(f"Content for '{section_type}' (press Enter twice when done):")
            content_lines = []
            while True:
                line = input()
                if line == "" and content_lines and content_lines[-1] == "":
                    break
                content_lines.append(line)
            content = "\n".join(content_lines).strip()
            if content:
                sections.append(CVSection(
                    section_type=section_type,
                    content=content,
                    order=order,
                ))
                order += 1
                print(f"✅  Section '{section_type}' added.\n")

        print_separator()
        print("📎  CV PDF & Email\n")

        pdf_path = ask(
            "Path to your CV PDF (press Enter to skip attachment)",
            default=""
        ).strip()

        hr_email = ask("HR contact email (recipient)")
        if hr_email.lower() == "exit":
            break

        hint = ask(
            "Cover letter hint — tone or key points to highlight (press Enter to skip)",
            default=""
        ).strip()

        # ── Build the ContactInput ────────────────────────────────────────────

        optimized_cv = OptimizedCV(
            job_id=f"job-{company_name.lower().replace(' ', '-')}",
            final_sections=sections,
            ats_score_estimate=0,       # not relevant for the contact agent
            optimization_notes=[],
            pdf_url=pdf_path if pdf_path else None,
            generated_at=datetime.utcnow(),
        )

        contact_input = ContactInput(
            optimized_cv=optimized_cv,
            job_title=job_title,
            company_name=company_name,
            job_description=job_description,
            recipient_email=hr_email,
            cover_letter_hint=hint if hint else None,
        )

        # ── Run the agent ─────────────────────────────────────────────────────

        print_separator()
        print("🚀  Running the agent...\n")
        print("  ⏳ Tool 1 — Extracting CV text from sections...")
        print("  ⏳ Tool 2 — Generating subject line...")
        print("  ⏳ Tool 3 — Generating email body...")
        print("  ⏳ Tool 4 — Sending email...\n")

        try:
            output = await deliver_cv(contact_input)
        except Exception as e:
            print(f"\n❌  Agent crashed: {e}")
            continue

        # ── Show results ──────────────────────────────────────────────────────

        print_separator()
        if output.success:
            print("✅  Email sent successfully!\n")
            print(f"  📌 Subject      : {output.subject_used}")
            print(f"  📬 Sent to      : {hr_email}")
            print(f"  🆔 Delivery ID  : {output.delivery_id}")
            print(f"  🕒 Sent at      : {output.sent_at}")
        else:
            print("❌  Failed to send email.\n")
            print(f"  Error: {output.error_message}")

        print_separator()
        again = ask("Send another email? (yes / no)", default="no").lower()
        if again not in ("yes", "y"):
            print("\n👋  Bye!\n")
            break


if __name__ == "__main__":
    asyncio.run(main())