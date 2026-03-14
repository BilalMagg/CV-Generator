from langchain_groq import ChatGroq
from app.core.config import settings
import logging

logger = logging.getLogger(__name__)

def get_llm(model: str = "llama-3.3-70b-versatile", temperature: float = 0.0) -> ChatGroq:
    """
    Returns a configured ChatOpenAI instance pointing to Groq's API.
    Provides a default model with 0 temperature for structured output tasks.
    """
    if not settings.GROQ_API_KEY:
        logger.warning("GROQ_API_KEY is not set.")
    
    return ChatGroq(
        model=model,
        api_key=settings.GROQ_API_KEY,
        temperature=temperature
    )
