"""
LLM catalog router — exposes available providers and their current models.
Consumed by the .NET backend (via gateway) and the frontend pickers.
"""
import logging
from typing import Optional
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

from shared.llm import (
    list_providers,
    list_models,
    PROVIDER_LABELS,
)

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/llm", tags=["llm"])


class ProviderInfo(BaseModel):
    name: str
    label: str
    available: bool
    has_key: bool


class ProvidersResponse(BaseModel):
    providers: list[ProviderInfo]


class ModelsResponse(BaseModel):
    provider: str
    models: list[str]


@router.get("/providers", response_model=ProvidersResponse)
async def get_providers():
    avail = list_providers()
    out = []
    for name, has_key in avail.items():
        out.append(
            ProviderInfo(
                name=name,
                label=PROVIDER_LABELS.get(name, name),
                available=bool(has_key),
                has_key=bool(has_key),
            )
        )
    return ProvidersResponse(providers=out)


@router.get("/models", response_model=ModelsResponse)
async def get_models(provider: str, refresh: Optional[bool] = False):
    if provider not in PROVIDER_LABELS:
        raise HTTPException(status_code=400, detail=f"Unknown provider: {provider}")
    models = list_models(provider)
    return ModelsResponse(provider=provider, models=models)


@router.get("/health")
async def health_check():
    return {"status": "ok", "service": "llm"}
