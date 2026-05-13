# Tools package
from cvtools.core.tools.pdf_converter import html_to_pdf, latex_to_pdf
from cvtools.core.tools.minio_storage import (
    get_minio_client,
    upload_pdf,
    download_pdf,
    get_template_object,
    ensure_templates_bucket,
    TEMPLATES_BUCKET,
)
from cvtools.core.tools.pdf_extractor import (extract_text_from_pdf, extract_text_from_pdf_bytes)
# double it and give it to the next person lmao
# from .pdf_converter import html_to_pdf, latex_to_pdf
# from .minio_storage import get_minio_client, upload_pdf, download_pdf
# from .pdf_extractor import extract_text_from_pdf, extract_text_from_pdf_bytes

__all__ = [
    "html_to_pdf",
    "latex_to_pdf",
    "get_minio_client",
    "upload_pdf",
    "download_pdf",
    "get_template_object",
    "ensure_templates_bucket",
    "TEMPLATES_BUCKET",
    "extract_text_from_pdf",
    "extract_text_from_pdf_bytes",
]