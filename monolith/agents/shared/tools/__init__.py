from shared.tools.embeddings import get_embeddings_model
from shared.tools.pdf_utils import (
    download_pdf_from_url, extract_text_from_pdf, extract_text_from_pdf_url,
    get_chunks_from_pdf, get_chunks_from_text,
)
from shared.tools.rag_utils import retrieve_context_from_pdf_url, retrieve_context_from_text, retrieve_context_by_text
