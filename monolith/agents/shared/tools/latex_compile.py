"""LaTeX → PDF compilation via the texlive Docker image.

Mirrors the user's Makefile flow (docker run --rm -v "$(CURDIR):/workspace"
texlive/texlive:latest) but trimmed to what a CV needs: two pdflatex passes.
"""
import logging
import re
import shutil
import subprocess
import tempfile
import time
from pathlib import Path
from typing import Any, Optional

logger = logging.getLogger(__name__)

LATEX_IMAGE = "texlive/texlive:latest"

_last_tex_check: float = 0.0
_last_tex_result: Optional[dict[str, Any]] = None


def tex_tooling_available(ttl: float = 300.0) -> dict[str, Any]:
    """Cached check of whether Docker + the texlive image are usable."""
    global _last_tex_check, _last_tex_result
    now = time.monotonic()
    if _last_tex_result is None or (now - _last_tex_check) > ttl:
        _last_tex_check = now
        docker = shutil.which("docker")
        if not docker:
            _last_tex_result = {"tex_available": False, "reason": "docker command not found on PATH"}
        else:
            try:
                result = subprocess.run(
                    ["docker", "images", "-q", LATEX_IMAGE],
                    capture_output=True, text=True, timeout=20,
                )
                ok = bool(result.stdout.strip())
                _last_tex_result = {
                    "tex_available": ok,
                    "reason": None if ok else f"image {LATEX_IMAGE} not pulled",
                }
            except Exception as e:
                _last_tex_result = {"tex_available": False, "reason": str(e)}
    return _last_tex_result


def _mount_path(path: Path) -> str:
    mount = path.resolve().as_posix()
    if len(mount) > 1 and mount[1] == ":":
        # Windows drive letter → C:/... (Docker Desktop accepts it)
        return mount
    return mount


def _tail(text: str, lines: int = 40) -> str:
    return "\n".join(text.strip().splitlines()[-lines:])


def compile_latex_to_pdf(
    tex_source: str,
    photo_bytes: Optional[bytes] = None,
    photo_ext: str = "jpg",
    timeout: int = 300,
    max_passes: int = 3,
) -> tuple[Optional[bytes], str]:
    """Compile LaTeX to PDF using the dockerized texlive image.

    Returns (pdf_bytes, error_message_or_empty). On failure the returned
    bytes are None and the message contains the tail of main.log / stderr.
    """
    workdir = Path(tempfile.mkdtemp(prefix="cvtex_"))
    try:
        (workdir / "main.tex").write_text(tex_source, encoding="utf-8")
        if photo_bytes:
            ext = photo_ext.lstrip(".").lower() or "jpg"
            (workdir / f"photo.{ext}").write_bytes(photo_bytes)

        mount = _mount_path(workdir)
        script = "cd /workspace && pdflatex -interaction=nonstopmode main"
        command = [
            "docker", "run", "--rm",
            "-v", f"{mount}:/workspace",
            LATEX_IMAGE, "sh", "-c", script,
        ]

        pdf = workdir / "main.pdf"
        log_file = workdir / "main.log"
        last_log_text = ""
        last_stderr = ""
        for _ in range(max_passes):
            logger.info("Compiling LaTeX via docker: %s", " ".join(command))
            result = subprocess.run(command, capture_output=True, text=True, timeout=timeout)
            last_stderr = result.stderr or ""
            last_log_text = (log_file.read_text(encoding="utf-8", errors="replace")
                             if log_file.is_file() else "")

            # TeX exits 1 when it wants a rerun (hyperref outlines etc.), so the
            # exit code is NOT a reliable success signal. Real failures leave
            # "! " error lines in the log (missing image, bad macro, ...).
            hard_errors = [l.strip() for l in last_log_text.splitlines() if l.strip().startswith("! ")]
            if pdf.is_file() and not hard_errors:
                logger.info("LaTeX compiled OK (%d bytes)", pdf.stat().st_size)
                return pdf.read_bytes(), ""
            if hard_errors:
                break

        if last_log_text:
            message = f"pdflatex failed.\n--- main.log (tail) ---\n" + _tail(last_log_text)
        else:
            message = "pdflatex failed; no log produced."
        if last_stderr.strip():
            message += "\n--- stderr (tail) ---\n" + _tail(last_stderr)
        return None, message
    except subprocess.TimeoutExpired as e:
        logger.error("LaTeX compile timed out after %ss", timeout)
        return None, f"pdflatex timed out after {timeout}s: {e}"
    except Exception as e:
        logger.error("LaTeX compile failed: %s", e)
        return None, f"LaTeX compile error: {e}"
    finally:
        shutil.rmtree(workdir, ignore_errors=True)