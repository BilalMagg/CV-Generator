from langchain_openai import ChatOpenAI
from cvtools.core.config import settings


PROVIDER_NAME = "openai"


def create(model: str | None = None, **kwargs) -> ChatOpenAI:
    api_key = settings.OPENAI_API_KEY or kwargs.pop("api_key", None)
    if api_key:
        return ChatOpenAI(model=model or "gpt-4o-mini", api_key=api_key, **kwargs)
    return ChatOpenAI(model=model or "gpt-4o-mini", **kwargs)
