"""
pdf_converter.py  –  HTML & LaTeX → PDF Conversion Utilities
=============================================================
Shared tool for all CV microservices that need to generate PDF files.

Two conversion paths:
  1. html_to_pdf()   — uses WeasyPrint (full HTML/CSS/Unicode support)
  2. latex_to_pdf()  — uses pdflatex CLI (requires MiKTeX on Windows)

Both return the absolute path of the generated PDF file.

Dependencies:
    uv add weasyprint minio
    MiKTeX (for LaTeX): https://miktex.org/download
"""

import os
import subprocess
import tempfile
import uuid
from pathlib import Path


def html_to_pdf(html_content: str, output_path: str | None = None) -> str:
    """
    Convert an HTML string into a PDF file using WeasyPrint.

    WeasyPrint supports:
      - Full HTML5 / CSS3
      - Unicode characters (accents, dashes, arabic, etc.)
      - Custom fonts via @font-face
      - Page margins, headers, footers via @page

    Args:
        html_content: The full HTML document string.
        output_path:  Where to save the PDF. If None, saves to a temp
                      directory with a UUID filename.

    Returns:
        The absolute path to the generated PDF file.

    Raises:
        ImportError: If weasyprint is not installed.
        Exception:   If the conversion fails.
    """
    try:
        from weasyprint import HTML, CSS
        from weasyprint.text.fonts import FontConfiguration
    except ImportError:
        raise ImportError(
            "weasyprint is required for HTML→PDF conversion.\n"
            "Install it with: uv add weasyprint"
        )

    if not output_path:
        output_dir = tempfile.mkdtemp(prefix="cv_pdf_")
        output_path = os.path.join(output_dir, f"{uuid.uuid4().hex}.pdf")

    # Ensure the output directory exists
    Path(output_path).parent.mkdir(parents=True, exist_ok=True)

    font_config = FontConfiguration()

    # Base CSS for clean CV rendering
    base_css = CSS(string="""
        @page {
            margin: 2cm;
            size: A4;
        }
        body {
            font-family: 'DejaVu Sans', 'Liberation Sans', Arial, sans-serif;
            font-size: 11pt;
            line-height: 1.5;
            color: #222;
        }
        h1, h2, h3 {
            margin-top: 12pt;
            margin-bottom: 6pt;
        }
        p {
            margin: 4pt 0;
        }
    """, font_config=font_config)

    HTML(string=html_content).write_pdf(
        output_path,
        stylesheets=[base_css],
        font_config=font_config,
    )

    return os.path.abspath(output_path)


def latex_to_pdf(latex_content: str, output_path: str | None = None) -> str:
    """
    Convert a LaTeX string into a PDF file using pdflatex.

    Requires MiKTeX (Windows) or TexLive (Linux/macOS).
    Download MiKTeX: https://miktex.org/download

    Args:
        latex_content: The full LaTeX document string (starting with \\documentclass).
        output_path:   Where to save the PDF. If None, saves to a temp
                       directory with a UUID filename.

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
            capture_output=True,
            check=True,
            timeout=10,
        )
    except FileNotFoundError:
        raise FileNotFoundError(
            "pdflatex is not installed.\n"
            "Windows : install MiKTeX from https://miktex.org/download\n"
            "Linux   : sudo apt install texlive-full\n"
            "macOS   : brew install --cask mactex"
        )

    # Create a temp working directory for the LaTeX build
    work_dir = tempfile.mkdtemp(prefix="cv_latex_")
    tex_file = os.path.join(work_dir, "cv.tex")

    # Write the .tex source file
    with open(tex_file, "w", encoding="utf-8") as f:
        f.write(latex_content)

    # Run pdflatex twice (needed for cross-references, ToC, page numbers)
    for run in range(2):
        result = subprocess.run(
            [
                "pdflatex",
                "-interaction=nonstopmode",   # don't pause on errors
                "-output-directory", work_dir,
                tex_file,
            ],
            capture_output=True,
            text=True,
            timeout=60,
        )
        if result.returncode != 0:
            raise RuntimeError(
                f"pdflatex failed on run {run + 1}:\n"
                f"{result.stdout}\n{result.stderr}"
            )

    generated_pdf = os.path.join(work_dir, "cv.pdf")

    if not os.path.exists(generated_pdf):
        raise RuntimeError(
            f"pdflatex ran but no PDF found at: {generated_pdf}"
        )

    # Move to the desired output path if one was given
    if output_path:
        Path(output_path).parent.mkdir(parents=True, exist_ok=True)
        os.replace(generated_pdf, output_path)
        return os.path.abspath(output_path)

    return os.path.abspath(generated_pdf)