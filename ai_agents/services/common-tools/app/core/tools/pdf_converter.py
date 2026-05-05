"""
pdf_converter.py  –  HTML & LaTeX → PDF Conversion Utilities
=============================================================
Shared tool for all CV microservices that need to generate PDF files.

Two conversion paths:
  1. html_to_pdf()  — uses WeasyPrint (pure Python, no system deps)
  2. latex_to_pdf()  — uses pdflatex CLI  (requires LaTeX installed)

Both return the absolute path of the generated PDF file.
"""

import os
import subprocess
import tempfile
import uuid
from pathlib import Path


def html_to_pdf(html_content: str, output_path: str | None = None) -> str:
    """
    Convert an HTML string into a PDF file using WeasyPrint.

    Args:
        html_content: The full HTML document string (including <html>, <head>, <body>).
        output_path:  Where to save the PDF. If None, saves to a temp directory
                      with a UUID filename.

    Returns:
        The absolute path to the generated PDF file.

    Raises:
        ImportError: If weasyprint is not installed.
        Exception:   If the conversion fails.
    """
    try:
        from weasyprint import HTML  # lazy import to avoid hard dependency
    except ImportError:
        raise ImportError(
            "weasyprint is required for HTML→PDF conversion. "
            "Install it with: pip install weasyprint"
        )

    if not output_path:
        output_dir = tempfile.mkdtemp(prefix="cv_pdf_")
        output_path = os.path.join(output_dir, f"{uuid.uuid4().hex}.pdf")

    # Ensure the output directory exists
    Path(output_path).parent.mkdir(parents=True, exist_ok=True)

    HTML(string=html_content).write_pdf(output_path)

    return os.path.abspath(output_path)


def latex_to_pdf(latex_content: str, output_path: str | None = None) -> str:
    """
    Convert a LaTeX string into a PDF file using pdflatex.

    Args:
        latex_content: The full LaTeX document string (starting with \\documentclass).
        output_path:   Where to save the PDF. If None, saves to a temp directory
                       with a UUID filename.

    Returns:
        The absolute path to the generated PDF file.

    Raises:
        FileNotFoundError: If pdflatex is not installed on the system.
        RuntimeError:      If pdflatex compilation fails.
    """
    # Check if pdflatex is available
    try:
        subprocess.run(
            ["pdflatex", "--version"],
            capture_output=True, check=True, timeout=10
        )
    except FileNotFoundError:
        raise FileNotFoundError(
            "pdflatex is required for LaTeX→PDF conversion. "
            "Install MiKTeX (Windows) or TexLive (Linux/macOS)."
        )

    # Create a temp directory for the LaTeX build
    work_dir = tempfile.mkdtemp(prefix="cv_latex_")
    tex_file = os.path.join(work_dir, "cv.tex")

    # Write the .tex file
    with open(tex_file, "w", encoding="utf-8") as f:
        f.write(latex_content)

    # Run pdflatex twice (for cross-references like ToC, page numbers, etc.)
    for _ in range(2):
        result = subprocess.run(
            [
                "pdflatex",
                "-interaction=nonstopmode",
                "-output-directory", work_dir,
                tex_file,
            ],
            capture_output=True,
            text=True,
            timeout=60,
        )
        if result.returncode != 0:
            raise RuntimeError(
                f"pdflatex compilation failed:\n{result.stdout}\n{result.stderr}"
            )

    generated_pdf = os.path.join(work_dir, "cv.pdf")

    if not os.path.exists(generated_pdf):
        raise RuntimeError(
            f"pdflatex ran successfully but no PDF was generated at {generated_pdf}"
        )

    # Move to the desired output path if specified
    if output_path:
        Path(output_path).parent.mkdir(parents=True, exist_ok=True)
        os.replace(generated_pdf, output_path)
        return os.path.abspath(output_path)

    return os.path.abspath(generated_pdf)
