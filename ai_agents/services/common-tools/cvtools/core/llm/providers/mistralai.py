from langchain_mistralai import ChatMistralAI
from cvtools.core.config import settings


PROVIDER_NAME = "mistralai"


def create(model: str | None = None, **kwargs) -> ChatMistralAI:
    api_key = settings.MISTRAL_API_KEY or kwargs.pop("api_key", None)
    if api_key:
        return ChatMistralAI(model=model or "mistral-small-latest", api_key=api_key, **kwargs)
    return ChatMistralAI(model=model or "mistral-small-latest", **kwargs)
