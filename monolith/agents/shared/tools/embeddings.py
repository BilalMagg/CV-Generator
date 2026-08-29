from langchain_core.embeddings import Embeddings
from langchain_openai import OpenAIEmbeddings
from langchain_mistralai import MistralAIEmbeddings
import os
import logging
from shared.config import settings

logger = logging.getLogger(__name__)

_local_model = None
_local_dimension = None


class _LocalEmbeddings(Embeddings):
    """Local sentence-transformers embeddings — no API, no rate limits."""

    def __init__(self, model_name: str):
        self._model_name = model_name

    def _get_model(self):
        global _local_model, _local_dimension
        if _local_model is None:
            from sentence_transformers import SentenceTransformer
            logger.info("Loading local embedding model %s (first load may download)", self._model_name)
            _local_model = SentenceTransformer(self._model_name)
            _local_dimension = _local_model.get_embedding_dimension() if hasattr(_local_model, "get_embedding_dimension") else _local_model.get_sentence_embedding_dimension()
        return _local_model

    @property
    def dimension(self) -> int:
        self._get_model()
        return _local_dimension

    def embed_documents(self, texts: list[str]) -> list[list[float]]:
        if not texts:
            return []
        vectors = self._get_model().encode(texts, batch_size=32, normalize_embeddings=True, show_progress_bar=False)
        return [v.tolist() for v in vectors]

    def embed_query(self, text: str) -> list[float]:
        vector = self._get_model().encode(
            [text], batch_size=1, normalize_embeddings=True, show_progress_bar=False
        )[0]
        return vector.tolist()


def _get_local_embeddings() -> _LocalEmbeddings | None:
    """Build local embeddings if sentence-transformers is available."""
    try:
        import sentence_transformers  # noqa: F401
    except ImportError:
        logger.warning("sentence-transformers not installed; local embeddings unavailable")
        return None
    return _LocalEmbeddings(settings.LOCAL_EMBEDDING_MODEL)


def embedding_dimension() -> int:
    """Return the dimension of the active embedding model (used for DB sync)."""
    local = _get_local_embeddings()
    if local is not None:
        return local.dimension
    return 768  # fallback for Mistral mistral-embed


def get_embeddings_model() -> Embeddings:
    """Return the best available embedding model.

    Priority:
    1. Local sentence-transformers (no API, no rate limits)
    2. OmniRoute with the configured model
    3. Mistral direct (fallback)
    """
    local = _get_local_embeddings()
    if local is not None:
        logger.info("Using local embedding model: %s", settings.LOCAL_EMBEDDING_MODEL)
        return local

    logger.warning("Local embedding model unavailable; falling back to API providers")
    omni_key = settings.OMNIROUTE_API_KEY or os.getenv("OMNIROUTE_API_KEY")
    if omni_key:
        return _OmniRouteEmbeddings(
            base_url=settings.OMNIROUTE_BASE_URL,
            api_key=omni_key,
            model=settings.OMNIROUTE_EMBEDDING_MODEL,
        )

    mistral_key = settings.MISTRAL_API_KEY or os.getenv("MISTRAL_API_KEY")
    if mistral_key:
        return MistralAIEmbeddings(model="mistral-embed")

    raise RuntimeError(
        "No embedding provider available. Install sentence-transformers or "
        "set OMNIROUTE_API_KEY / MISTRAL_API_KEY."
    )


class _OmniRouteEmbeddings(OpenAIEmbeddings):
    """OpenAI-compatible embeddings that fall back to Mistral on provider errors."""

    def embed_documents(self, texts: list[str]) -> list[list[float]]:
        try:
            return super().embed_documents(texts)
        except Exception as e:
            if "No credentials for embedding provider" in str(e):
                logger.warning("OmniRoute provider unavailable (%s), falling back to Mistral", e)
                mistral_key = settings.MISTRAL_API_KEY or os.getenv("MISTRAL_API_KEY")
                if mistral_key:
                    fallback = MistralAIEmbeddings(model="mistral-embed")
                    return fallback.embed_documents(texts)
            raise

    def embed_query(self, text: str) -> list[float]:
        try:
            return super().embed_query(text)
        except Exception as e:
            if "No credentials for embedding provider" in str(e):
                logger.warning("OmniRoute provider unavailable (%s), falling back to Mistral", e)
                mistral_key = settings.MISTRAL_API_KEY or os.getenv("MISTRAL_API_KEY")
                if mistral_key:
                    fallback = MistralAIEmbeddings(model="mistral-embed")
                    return fallback.embed_query(text)
            raise
