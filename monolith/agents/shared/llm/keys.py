"""In-app stored LLM keys, pushed here by the .NET backend.

Deployments are single-tenant (one instance owner per app install), so the
sidecar keeps ONE in-memory provider→key map instead of a per-user structure:

- the backend pushes keys on save/delete (Settings → AI Model) and re-pushes
  everything on the settings page load (heals this process after a restart);
- resolution precedence: in-app key → env var;
- keys live only in process memory, never on disk.

Endpoints live in ``agents.llm_keys.router``.
"""
import logging
import threading

logger = logging.getLogger(__name__)

_lock = threading.Lock()
_KEYS: dict[str, str] = {}


def set_key(provider: str, api_key: str) -> None:
    provider = provider.strip().lower()
    if not provider or not api_key or not api_key.strip():
        return
    with _lock:
        _KEYS[provider] = api_key.strip()
    logger.info("In-app LLM key set for provider '%s'", provider)


def remove_key(provider: str) -> bool:
    with _lock:
        provider = provider.strip().lower()
        if provider in _KEYS:
            _KEYS.pop(provider)
            logger.info("In-app LLM key removed for provider '%s'", provider)
            return True
        return False


def resolve(provider: str) -> str | None:
    """In-app key for a provider, if any."""
    with _lock:
        return _KEYS.get(provider.strip().lower())


def statuses() -> dict[str, bool]:
    with _lock:
        return dict(_KEYS)


def describe(provider: str) -> str:
    """Short non-reversible hint of a stored key, for debug endpoints."""
    key = resolve(provider)
    if not key:
        return ""
    if len(key) < 12:
        return "•••"
    return f"{key[:6]}…{key[-4:]}"
