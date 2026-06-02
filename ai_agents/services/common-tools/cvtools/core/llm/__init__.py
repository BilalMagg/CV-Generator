from typing import Any

from cvtools.core.llm.providers import resolve_provider, get_provider, list_providers


def get_llm(
    provider: str | None = None,
    model: str | None = None,
    temperature: float = 0.0,
    **kwargs: Any,
):
    if provider is None:
        if model:
            resolved = resolve_provider(model)
            if resolved is None:
                raise ValueError(
                    f"Could not auto-detect provider from model '{model}'. "
                    f"Please specify provider explicitly."
                )
            provider = resolved
        else:
            provider = "groq"

    mod = get_provider(provider)
    return mod.create(model=model, temperature=temperature, **kwargs)


__all__ = ["get_llm", "list_providers"]
