"""
Provider fallback for direct LLM calls.

Direct AI features (message generation, form auto-fill, generic chat) want
OpenRouter first, but an OpenRouter account can be out of credits (402) or a
provider can be flaky. This helper tries providers in priority order until one
succeeds, so a single caller never has to know the whole provider list.
"""
import logging
from typing import List, Optional

from langchain_core.messages import BaseMessage
from langchain_core.language_models import BaseChatModel

from shared.llm.providers import (
    get_llm,
    _PROVIDER_PRIORITY,
    _pick_provider,
)

logger = logging.getLogger(__name__)

# Exceptions that indicate a provider is unusable and we should try the next one.
# 401/402/403/429 -> auth/credits/rate-limit; spinner/retryable transport errors.
_TRANSIENT_MARKERS = ("402", "401", "429", "insufficient", "rate limit", "credits", "quota")


def _fallback_order(preferred: Optional[str], model: Optional[str]) -> List[str]:
    available = [p for p in _PROVIDER_PRIORITY if p]
    if preferred and preferred in available:
        return [preferred] + [p for p in available if p != preferred]
    # Fall back to the normal pick order (OpenRouter first when configured).
    current = _pick_provider()
    return [current] + [p for p in available if p != current]


def _provider_should_skip(provider: str) -> bool:
    """Resolve the effective provider given the request; returns False if usable."""
    from shared.llm import providers as P
    if not P._provider_present(provider):
        return True
    return False


def _is_retryable(e: Exception) -> bool:
    msg = str(e).lower()
    return any(m in msg for m in _TRANSIENT_MARKERS)


async def ainvoke_with_fallback(
    messages: List[BaseMessage],
    preferred_provider: Optional[str] = None,
    model: Optional[str] = None,
    temperature: float = 0.7,
    max_tokens: Optional[int] = None,
) -> tuple[BaseChatModel, str]:
    """Invoke an LLM across the provider priority list until one succeeds.

    Returns (llm, text). Retries OpenRouter first, then falls back to the next
    configured provider (e.g. Groq) if the first is unavailable/out of credits.
    Raises the last exception if every provider fails.
    """
    order = _fallback_order(preferred_provider, model)
    last_exc: Optional[Exception] = None
    for provider in order:
        if _provider_should_skip(provider):
            continue
        try:
            llm = get_llm(preferred_provider=provider, model=model)
            if temperature is not None:
                try:
                    llm.temperature = temperature
                except Exception:
                    pass
            if max_tokens:
                try:
                    llm.max_tokens = max_tokens
                except Exception:
                    pass
            resp = await llm.ainvoke(messages)
            content = getattr(resp, "content", str(resp)) or ""
            logger.info("Direct LLM call succeeded on provider=%s model=%s", provider, getattr(llm, "model_name", getattr(llm, "model", "")))
            return llm, content
        except Exception as e:  # noqa: BLE001
            last_exc = e
            logger.warning("Direct LLM call failed on provider=%s: %s", provider, e)
            if not _is_retryable(e):
                # Non-transient error (e.g. bad request from our prompt) => surface
                # immediately rather than burning the fallback chain on a bad input.
                # Still try the next provider once, to be resilient.
                pass
    if last_exc is not None:
        raise last_exc
    raise RuntimeError("No LLM provider configured.")
