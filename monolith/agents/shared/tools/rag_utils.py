import logging
from typing import List, Optional, Dict, Any
from shared.backend_client import check_vectors_status, sync_vectors, search_vectors
from shared.tools.embeddings import get_embeddings_model
from shared.tools.pdf_utils import get_chunks_from_pdf, get_chunks_from_text

logger = logging.getLogger(__name__)


async def retrieve_context_from_pdf_url(
    pdf_url: str, workflow_id: str, user_id: str, min_score: float = 0.20
) -> Optional[str]:
    model = get_embeddings_model()
    if not await check_vectors_status(workflow_id):
        chunks = get_chunks_from_pdf(pdf_url)
        if not chunks:
            logger.warning("No chunks generated from PDF: %s", pdf_url)
            return None
        embeddings = model.embed_documents(chunks)
        await sync_vectors(workflow_id, [
            {"content": c, "embedding": e, "source": pdf_url} for c, e in zip(chunks, embeddings)
        ])
    return await retrieve_context_by_text("CV", workflow_id, user_id, min_score)


async def retrieve_context_from_text(
    text: str, source_label: str, user_id: str, min_score: float = 0.20
) -> Optional[str]:
    model = get_embeddings_model()
    if not await check_vectors_status(user_id):
        chunks = get_chunks_from_text(text)
        if not chunks:
            return None
        embeddings = model.embed_documents(chunks)
        await sync_vectors(user_id, [
            {"content": c, "embedding": e, "source": source_label} for c, e in zip(chunks, embeddings)
        ])
    return await retrieve_context_by_text(source_label, user_id, user_id, min_score)


async def retrieve_context_by_text(
    query_text: str, vector_store_id: str, user_id: str, min_score: float = 0.20
) -> Optional[str]:
    model = get_embeddings_model()
    query_embedding = model.embed_query(query_text)
    docs = await search_vectors(vector_store_id, query_text, query_embedding)
    if not docs:
        return None
    filtered = [doc for doc in docs if doc.get("score", 0) >= min_score]
    if not filtered:
        return None
    return "\n---\n".join([doc.get("content", "") for doc in filtered])
