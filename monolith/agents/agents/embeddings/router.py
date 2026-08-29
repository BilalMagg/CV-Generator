import logging
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel
from shared.tools.embeddings import get_embeddings_model

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/api/embeddings", tags=["embeddings"])


class EmbedRequest(BaseModel):
    texts: list[str]


class EmbedResponse(BaseModel):
    embeddings: list[list[float]]
    model: str
    dimensions: int


class EmbedQueryRequest(BaseModel):
    text: str


class EmbedQueryResponse(BaseModel):
    embedding: list[float]
    model: str
    dimensions: int


@router.post("/embed", response_model=EmbedResponse)
async def embed_texts(req: EmbedRequest):
    try:
        model = get_embeddings_model()
        embeddings = model.embed_documents(req.texts)
        return EmbedResponse(
            embeddings=embeddings,
            model=getattr(model, "model", "unknown"),
            dimensions=len(embeddings[0]) if embeddings else 0,
        )
    except Exception as e:
        logger.error("Embedding failed: %s", e)
        raise HTTPException(status_code=500, detail=f"Embedding failed: {e}")


@router.post("/embed-query", response_model=EmbedQueryResponse)
async def embed_query(req: EmbedQueryRequest):
    try:
        model = get_embeddings_model()
        embedding = model.embed_query(req.text)
        return EmbedQueryResponse(
            embedding=embedding,
            model=getattr(model, "model", "unknown"),
            dimensions=len(embedding),
        )
    except Exception as e:
        logger.error("Query embedding failed: %s", e)
        raise HTTPException(status_code=500, detail=f"Query embedding failed: {e}")


@router.get("/health")
async def health():
    return {"status": "ok", "service": "embeddings"}
