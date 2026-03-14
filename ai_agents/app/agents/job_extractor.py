from app.models.job_model import JobRequirements
from app.core.llm import get_llm
from langchain_core.prompts import ChatPromptTemplate
import logging
import traceback

logger = logging.getLogger(__name__)

def extract_job_requirements(raw_description: str) -> JobRequirements:
    """
    Agent 1: Extracts structured job requirements from a raw text description using an LLM.
    """
    logger.info("Extracting job requirements via LLM...")
    
    llm = get_llm(model="llama-3.3-70b-versatile", temperature=0.0)
    structured_llm = llm.with_structured_output(JobRequirements)

    prompt = ChatPromptTemplate.from_messages([
        ("system", 
         "You are an expert technical recruiter and CV analyzer. "
         "Your task is to analyze the following job description and extract the canonical job role, "
         "the exact required years of experience (integer, fallback to null if not clearly stated), "
         "the core extracted skills, and the most critical ATS keywords. "
         "Do NOT hallucinate skills or requirements not present in the text."),
        ("human", "Job Description:\n{raw_description}")
    ])
    
    chain = prompt | structured_llm
    
    try:
        result = chain.invoke({"raw_description": raw_description})
        return result
    except Exception as e:
        logger.error(f"Failed to extract job requirements: {e}")
        print("\n=== LLM EXTRACTION ERROR ===")
        traceback.print_exc()
        print("==========================\n")
        # Fallback empty requirements to prevent pipeline crash if LLM fails
        return JobRequirements(
            job_role="Unknown Role",
            extracted_skills=[],
            required_experience_years=None,
            keywords=[]
        )
