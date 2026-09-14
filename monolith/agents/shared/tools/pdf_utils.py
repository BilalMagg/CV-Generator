import io
import logging
import os
import re
import requests
from pypdf import PdfReader
from typing import List

logger = logging.getLogger(__name__)

# Candidate Unicode TTF fonts so rendered cover letters keep accents/typography.
# Falls back to the core latin-1 Helvetica when none exists.
_UNICODE_FONT_CANDIDATES = [
    ("C:/Windows/Fonts/arial.ttf", "C:/Windows/Fonts/arialbd.ttf"),
    ("C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/segoeuib.ttf"),
    ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
    ("/System/Library/Fonts/Supplemental/Arial.ttf", "/System/Library/Fonts/Supplemental/Arial Bold.ttf"),
]


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


def _find_unicode_font() -> tuple[str, str | None] | None:
    """Find (regular, bold) font paths on disk, or None to use Helvetica."""
    for regular, bold in _UNICODE_FONT_CANDIDATES:
        if os.path.isfile(regular):
            return regular, bold if os.path.isfile(bold) else None
    return None


def _text_paragraphs(text: str) -> List[str]:
    """Split pasted text into paragraphs: blank-line runs delimit paragraphs;
    single newlines inside a paragraph collapse to spaces (soft wraps)."""
    normalized = text.replace("\r\n", "\n").replace("\r", "\n")
    paragraphs = []
    for block in re.split(r"\n\s*\n", normalized):
        para = " ".join(block.split())
        if para:
            paragraphs.append(para)
    return paragraphs


def _write_rich(pdf, font_name: str, paragraph: str, has_bold: bool) -> None:
    """Flow a paragraph to the PDF, rendering `**bold**` markers via fpdf2's
    native Markdown support — keeps multi_cell spacing/justification intact.
    Without a registered bold variant the markers are stripped to plain text."""
    from fpdf.enums import XPos, YPos

    if not has_bold:
        paragraph = re.sub(r"\*\*", "", paragraph)
        markdown = False
    else:
        markdown = True
    pdf.set_font(font_name, size=11)
    pdf.multi_cell(
        w=0, h=5.2, text=paragraph, align="J", markdown=markdown,
        new_x=XPos.LMARGIN, new_y=YPos.NEXT,
    )
    pdf.ln(2.5)


def render_text_pdf(text: str, title: str = "") -> bytes | None:
    """Render plain text to an A4 PDF via fpdf2. Best-effort: None on failure.

    The optional title is rendered as a centered bold heading. Paragraphs are
    auto-wrapped with page breaks; a Unicode TTF (with its bold variant) is
    used when available so accents render correctly — otherwise the core
    latin-1 Helvetica is used. Inline `**word**` markers render as bold.
    """
    try:
        from fpdf import FPDF
        from fpdf.enums import XPos, YPos
    except ImportError:
        logger.error("fpdf2 is not installed; install with `pip install fpdf2`")
        return None

    if not text or not text.strip():
        return None

    try:
        pdf = FPDF(orientation="P", unit="mm", format="A4")
        pdf.set_auto_page_break(auto=True, margin=20)
        pdf.set_left_margin(25)
        pdf.set_right_margin(25)
        pdf.set_top_margin(25)

        font = _find_unicode_font()
        has_bold = True  # Helvetica core includes bold
        if font:
            regular, bold = font
            pdf.add_font("BodyFont", fname=regular)
            if bold:
                pdf.add_font("BodyFont", style="B", fname=bold)
            else:
                has_bold = False  # no bold variant — render ** spans as plain
            font_name = "BodyFont"
        else:
            font_name = "Helvetica"  # core font covers latin-1 (French accents OK)

        pdf.add_page()
        pdf.set_font(font_name, size=11)

        if title.strip():
            title_style = "B" if (font and font[1]) else ""
            pdf.set_font(font_name, style=title_style, size=14)
            pdf.multi_cell(
                w=0, h=7, text=title.strip(), align="C",
                new_x=XPos.LMARGIN, new_y=YPos.NEXT,
            )
            pdf.set_font(font_name, style="", size=11)
            pdf.ln(4)

        for para in _text_paragraphs(text):
            if "**" in para:
                _write_rich(pdf, font_name, para, has_bold)
            else:
                pdf.set_font(font_name, style="", size=11)
                pdf.multi_cell(
                    w=0, h=5.2, text=para, align="J",
                    new_x=XPos.LMARGIN, new_y=YPos.NEXT,
                )
                pdf.ln(2.5)

        return bytes(pdf.output())
    except Exception as e:
        logger.error(f"Failed to render text PDF: {e}")
        return None
