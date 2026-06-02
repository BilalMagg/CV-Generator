from langchain_groq import ChatGroq
from cvtools.core.config import settings


PROVIDER_NAME = "groq"


def create(model: str | None = None, **kwargs) -> ChatGroq:
    api_key = settings.GROQ_API_KEY or kwargs.pop("api_key", None)
    if api_key:
        return ChatGroq(model=model or "llama-3.3-70b-versatile", api_key=api_key, **kwargs)
    return ChatGroq(model=model or "llama-3.3-70b-versatile", **kwargs)
