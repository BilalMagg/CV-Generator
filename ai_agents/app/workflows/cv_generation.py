from app.models.job_model import JobRequest
from app.models.user_model import UserProfile
from app.agents.job_extractor import extract_job_requirements
from app.agents.rag_search import match_candidate_data
from app.agents.cv_filler import fill_cv_template
from app.agents.cv_optimizer import optimize_cv
from app.agents.email_sender import deliver_cv
import os
import json
from datetime import datetime

def run_cv_generation_workflow(request: JobRequest) -> dict:
    """
    Orchestrates the 5-step AI CV Generation workflow.
    """
    try:
        # Create a run directory for logging
        timestamp = datetime.now().strftime('%Y%md_%H%M%S')
        run_dir = f"temp_runs/{request.user_id}_{timestamp}"
        os.makedirs(run_dir, exist_ok=True)
        
        # Mock fetching user profile from DB (in real life, fetch via user_id)
        profile = UserProfile(
            user_id=request.user_id,
            name="John Doe",
            email="johndoe@example.com",
            skills=["Python", "C#", "SQL"]
        )
        
        # Save input request
        with open(f"{run_dir}/00_input_request.json", "w") as f:
            f.write(request.model_dump_json(indent=2))

        # Step 1: Extract Job Requirements
        job_reqs = extract_job_requirements(request.job_description)
        
        # Save Agent 1 output
        with open(f"{run_dir}/01_extracted_requirements.json", "w") as f:
            f.write(job_reqs.model_dump_json(indent=2))

        # Step 2: Data Matching with RAG
        cv_draft = match_candidate_data(job_reqs, profile)
        
        # Save Agent 2 output
        with open(f"{run_dir}/02_rag_candidate_match.json", "w") as f:
            f.write(cv_draft.model_dump_json(indent=2))

        # Step 3: Fill CV Template
        filled_cv = fill_cv_template(cv_draft, request.template_id)
        
        # Save Agent 3 output
        with open(f"{run_dir}/03_filled_cv.json", "w") as f:
            f.write(filled_cv.model_dump_json(indent=2))

        # Step 4: Validate and Optimize CV
        optimized_cv = optimize_cv(filled_cv)
        
        # Save Agent 4 output
        with open(f"{run_dir}/04_optimized_cv.json", "w") as f:
            f.write(optimized_cv.model_dump_json(indent=2))

        # Step 5: Deliver CV
        delivery_success = deliver_cv(optimized_cv, profile.email)

        return {
            "success": True,
            "message": "CV generated and optimized successfully",
            "ats_score": optimized_cv.ats_score_estimate,
            "delivery_status": "sent" if delivery_success else "failed"
        }

    except Exception as e:
        return {
            "success": False,
            "message": f"Workflow failed: {str(e)}"
        }
