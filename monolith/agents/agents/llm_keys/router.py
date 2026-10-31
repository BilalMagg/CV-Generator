"""In-app LLM provider keys, pushed by the .NET backend (server-to-server).

The backend pushes ONE key per call after the user saves it in
Settings → AI Model, and re-pushes everything when the settings page loads
(heals this process after a restart). Resolution precedence in
``shared/llm/providers.py`` is: in-app key → env var.
"""
import logging

from fastapi import APIRouter
from pydantic import BaseModel
from typing import Optional

from shared.llm import keys as llm_keys

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/llm", tags=["llm-keys"])


class SetKeyRequest(BaseModel):
    provider: str
    api_key: str


class KeyStatus(BaseModel):
    provider: str
    masked: Optional[str] = None


@router.post("/keys")
async def set_key(request: SetKeyRequest):
    llm_keys.set_key(request.provider, request.api_key)
    return {"status": "ok", "provider": request.provider.strip().lower()}


@router.delete("/keys/{provider}")
async def remove_key(provider: str):
    removed = llm_keys.remove_key(provider)
    return {"status": "ok", "removed": removed}


@router.get("/keys")
async def list_keys():
    """Diagnostic only: which provider keys are held in memory, masked."""
    return {
        "status": "ok",
        "keys": [KeyStatus(provider=p, masked=llm_keys.describe(p))
                 for p, held in llm_keys.statuses().items() if held],
    }
