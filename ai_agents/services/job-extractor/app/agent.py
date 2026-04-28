"""
Job Extractor agent — extracts structured requirements from job descriptions.
"""
from app.schemas import ExtractorInput, ExtractorOutput
from app.models.job_model import JobRequirements


async def extract_job_requirements(input_data: ExtractorInput) -> ExtractorOutput:
    """
    TODO: Implement LLM logic with LangChain/Groq.
    For now, returns a stub.
    """
    reqs = JobRequirements(
        job_role="Software Engineer",
        extracted_skills=["Python", "FastAPI"],
        confidence_score=0.9
    )
    return ExtractorOutput(**reqs.model_dump())