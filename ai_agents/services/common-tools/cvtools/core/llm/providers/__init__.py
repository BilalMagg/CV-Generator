from cvtools.core.llm.providers import groq
from cvtools.core.llm.providers import openai
from cvtools.core.llm.providers import google
from cvtools.core.llm.providers import mistralai

_REGISTRY: dict[str, object] = {
    "groq": groq,
    "openai": openai,
    "mistralai": mistralai,
    "google": google,
}

_MODEL_PREFIX_MAP: dict[str, str] = {
    "gpt-": "openai",
    "o1-": "openai",
    "o3-": "openai",
    "gemini-": "google",
    "llama-": "groq",
    "mixtral-": "groq",
    "mistral-": "mistralai",
    "codestral-": "mistralai",
    "claude-": None,
    "command-": None,
}


def resolve_provider(model: str) -> str | None:
    for prefix, provider in _MODEL_PREFIX_MAP.items():
        if model.startswith(prefix):
            return provider
    return None


def get_provider(name: str):
    module = _REGISTRY.get(name)
    if module is None:
        available = list(_REGISTRY)
        raise ValueError(
            f"Unknown provider '{name}'. Available providers: {available}"
        )
    return module


def list_providers() -> list[str]:
    return list(_REGISTRY)


__all__ = [
    "resolve_provider",
    "get_provider",
    "list_providers",
    "_REGISTRY",
]
