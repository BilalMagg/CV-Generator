from app.models.job_model import JobRequirements

def extract_job_requirements(raw_description: str) -> JobRequirements:
    """
    Agent 1: Extracts structured job requirements from a raw text description.
    """
    # TODO: Implement NLP extraction logic here
    print("Extracting job requirements...")
    return JobRequirements(
        job_role="Software Engineer",
        extracted_skills=["Python", "FastAPI"],
        keywords=["RESTful", "Microservices"]
    )
