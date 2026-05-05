"""
test_pdf_tools.py  –  Test PDF conversion + MinIO upload
========================================================
Run from: ai_agents/services/common-tools/
    python test_pdf_tools.py
"""

import os
import sys

# Add parent to path so we can import app
sys.path.insert(0, os.path.dirname(__file__))

from app.core.tools.pdf_converter import html_to_pdf, latex_to_pdf
from app.core.tools.minio_storage import upload_pdf, get_minio_client


def test_html_to_pdf():
    """Test 1: Convert HTML → PDF"""
    print("\n📄 Test 1: HTML → PDF conversion...")

    html = """
    <!DOCTYPE html>
    <html>
    <head>
        <style>
            body { font-family: Arial, sans-serif; margin: 40px; }
            h1 { color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px; }
            h2 { color: #3498db; }
            .section { margin-bottom: 20px; }
            .skills span { background: #ecf0f1; padding: 4px 10px; margin: 2px; border-radius: 4px; display: inline-block; }
        </style>
    </head>
    <body>
        <h1>Mohssine El Addaoui</h1>
        <p>AI Engineer | Python Developer</p>

        <div class="section">
            <h2>Summary</h2>
            <p>Experienced software engineer with expertise in AI agents, 
               microservices architecture, and LLM integration.</p>
        </div>

        <div class="section">
            <h2>Experience</h2>
            <p><strong>AI Engineer</strong> — Tech Innovations Inc. (2024-Present)</p>
            <p>Developed agentic workflows using LangGraph and integrated LLMs 
               for automated document processing.</p>
        </div>

        <div class="section">
            <h2>Skills</h2>
            <div class="skills">
                <span>Python</span>
                <span>FastAPI</span>
                <span>Docker</span>
                <span>LangChain</span>
                <span>PostgreSQL</span>
                <span>Groq</span>
            </div>
        </div>
    </body>
    </html>
    """

    try:
        pdf_path = html_to_pdf(html, "test_cv.pdf")
        file_size = os.path.getsize(pdf_path)
        print(f"   ✅ PDF created: {pdf_path}")
        print(f"   📏 Size: {file_size / 1024:.1f} KB")
        return pdf_path
    except ImportError as e:
        print(f"   ⚠️  Skipped: {e}")
        return None
    except Exception as e:
        print(f"   ❌ Failed: {e}")
        return None


def test_latex_to_pdf():
    """Test 2: Convert LaTeX → PDF"""
    print("\n📄 Test 2: LaTeX → PDF conversion...")

    latex = r"""
\documentclass[11pt]{article}
\usepackage[margin=1in]{geometry}
\usepackage{enumitem}

\begin{document}

\begin{center}
    {\LARGE \textbf{Mohssine El Addaoui}} \\[4pt]
    AI Engineer | Python Developer
\end{center}

\section*{Summary}
Experienced software engineer with expertise in AI agents, 
microservices architecture, and LLM integration.

\section*{Skills}
Python, FastAPI, Docker, LangChain, PostgreSQL, Groq

\end{document}
    """

    try:
        pdf_path = latex_to_pdf(latex, "test_cv_latex.pdf")
        file_size = os.path.getsize(pdf_path)
        print(f"   ✅ PDF created: {pdf_path}")
        print(f"   📏 Size: {file_size / 1024:.1f} KB")
        return pdf_path
    except FileNotFoundError as e:
        print(f"   ⚠️  Skipped (LaTeX not installed): {e}")
        return None
    except Exception as e:
        print(f"   ❌ Failed: {e}")
        return None


def test_minio_upload(pdf_path: str):
    """Test 3: Upload PDF to MinIO"""
    print("\n☁️  Test 3: Upload to MinIO...")

    try:
        # Check if MinIO is reachable
        client = get_minio_client()
        client.list_buckets()  # quick connectivity check

        url = upload_pdf(pdf_path, client=client)
        print(f"   ✅ Uploaded! URL: {url}")
        return url
    except Exception as e:
        print(f"   ❌ Failed (is MinIO running?): {e}")
        return None


if __name__ == "__main__":
    print("🚀 Testing PDF Tools in Common-Tools\n" + "=" * 45)

    # Test 1: HTML → PDF
    html_pdf = test_html_to_pdf()

    # Test 2: LaTeX → PDF
    latex_pdf = test_latex_to_pdf()

    # Test 3: Upload to MinIO (using whichever PDF was created)
    pdf_to_upload = html_pdf or latex_pdf
    if pdf_to_upload:
        test_minio_upload(pdf_to_upload)
    else:
        print("\n⚠️  No PDF was generated, skipping MinIO upload test.")

    print("\n" + "=" * 45)
    print("✅ Tests complete!")
