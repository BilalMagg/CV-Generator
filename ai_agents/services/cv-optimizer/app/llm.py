import os
from cvtools.core.llm import get_llm

_llm = None


def _get_llm():
    global _llm
    if _llm is None:
        provider = os.getenv("LLM_PROVIDER") or "mistralai"
        model = os.getenv("LLM_MODEL") or "mistral-small-latest"
        _llm = get_llm(provider=provider, model=model, temperature=0.3)
    return _llm
