import io
import logging
import requests
from pypdf import PdfReader
from typing import List

logger = logging.getLogger(__name__)


def download_pdf_from_url(url: str) -> bytes | None:
    try:
        response = requests.get(url, timeout=15)
        response.raise_for_status()
        return response.content
    except Exception as e:
        logger.error(f"Failed to download PDF from {url}: {e}")
        return None


def extract_text_from_pdf(pdf_content: bytes) -> str | None:
    try:
        pdf_reader = PdfReader(io.BytesIO(pdf_content))
        text_content = ""
        for page in pdf_reader.pages:
            extracted = page.extract_text()
            if extracted:
                text_content += extracted + "\n"
        return text_content if text_content.strip() else None
    except Exception as e:
        logger.error(f"Failed to extract text from PDF: {e}")
        return None


def extract_text_from_pdf_url(url: str) -> str | None:
    pdf_content = download_pdf_from_url(url)
    if pdf_content:
        return extract_text_from_pdf(pdf_content)
    return None


def get_chunks_from_pdf(pdf_url: str, chunk_size: int = 250, chunk_overlap: int = 50) -> List[str]:
    from langchain_text_splitters import RecursiveCharacterTextSplitter
    text_content = extract_text_from_pdf_url(pdf_url)
    if not text_content:
        return []
    text_splitter = RecursiveCharacterTextSplitter(
        chunk_size=chunk_size, chunk_overlap=chunk_overlap, length_function=len,
        is_separator_regex=False,
    )
    return text_splitter.split_text(text_content)


def get_chunks_from_text(text: str, chunk_size: int = 250, chunk_overlap: int = 50) -> List[str]:
    from langchain_text_splitters import RecursiveCharacterTextSplitter
    if not text:
        return []
    text_splitter = RecursiveCharacterTextSplitter(
        chunk_size=chunk_size, chunk_overlap=chunk_overlap, length_function=len,
        is_separator_regex=False,
    )
    return text_splitter.split_text(text)


def render_first_page_thumbnail(pdf_content: bytes, max_width: int = 512) -> bytes | None:
    """Rasterize the first page of a PDF to PNG bytes (Drive-style thumbnail).

    Returns None when PyMuPDF is unavailable or rendering fails — callers must
    treat a missing thumbnail as non-fatal.
    """
    try:
        import pymupdf  # PyMuPDF
    except ImportError:
        logger.error("pymupdf is not installed; install with `pip install pymupdf`")
        return None

    try:
        doc = pymupdf.open(stream=pdf_content, filetype="pdf")
        try:
            if doc.page_count == 0:
                return None
            page = doc[0]
            rect = page.rect
            if rect.width <= 0 or rect.height <= 0:
                return None
            # Downscale to max_width (never upscale), capped so huge pages can't
            # produce absurd pixmaps.
            zoom = min(max_width / rect.width, 1.0)
            pix = page.get_pixmap(matrix=pymupdf.Matrix(zoom, zoom))
            return pix.tobytes("png")
        finally:
            doc.close()
    except Exception as e:
        logger.error(f"Failed to render PDF thumbnail: {e}")
        return None
