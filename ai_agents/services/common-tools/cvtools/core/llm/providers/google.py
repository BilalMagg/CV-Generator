from langchain_google_genai import ChatGoogleGenerativeAI
from cvtools.core.config import settings


PROVIDER_NAME = "google"


def create(model: str | None = None, **kwargs) -> ChatGoogleGenerativeAI:
    api_key = settings.GOOGLE_API_KEY or kwargs.pop("api_key", None)
    if api_key:
        return ChatGoogleGenerativeAI(model=model or "gemini-2.0-flash", google_api_key=api_key, **kwargs)
    return ChatGoogleGenerativeAI(model=model or "gemini-2.0-flash", **kwargs)
