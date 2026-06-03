import importlib

_REGISTRY: dict[str, str] = {
    "groq": "cvtools.core.llm.providers.groq",
    "openai": "cvtools.core.llm.providers.openai",
    "mistralai": "cvtools.core.llm.providers.mistralai",
    "google": "cvtools.core.llm.providers.google",
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
    module_path = _REGISTRY.get(name)
    if module_path is None:
        available = list(_REGISTRY)
        raise ValueError(
            f"Unknown provider '{name}'. Available providers: {available}"
        )
    return importlib.import_module(module_path)


def list_providers() -> list[str]:
    return list(_REGISTRY)


__all__ = [
    "resolve_provider",
    "get_provider",
    "list_providers",
]
