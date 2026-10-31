"""LaTeX → PDF compilation.

Two engines, resolved per machine (never hardcoded):

1. ``native``  — a pdflatex binary on PATH (what the full Docker image ships).
2. ``docker``  — the texlive/texlive image via ``docker run`` (mirrors the
                 user's Makefile flow; kept untouched so the current local
                 state keeps working exactly as before).

``tex_tooling_available()`` reports which engine would be used so callers can
degrade gracefully (e.g. generate the .tex source without a PDF) when neither
is present.
"""
import logging
import os
import re
import shutil
import subprocess
import tempfile
import time
from pathlib import Path
from typing import Any, Optional

logger = logging.getLogger(__name__)

LATEX_IMAGE = "texlive/texlive:latest"

# Optional override: auto (default) | native | docker | none
_LATEX_ENGINE_ENV = "PROPUL_LATEX_ENGINE"

_last_tex_check: float = 0.0
_last_tex_result: Optional[dict[str, Any]] = None


def _requested_engine() -> str:
    """Normalized PROPUL_LATEX_ENGINE value (invalid values fall back to auto)."""
    value = (os.environ.get(_LATEX_ENGINE_ENV) or "auto").strip().lower()
    return value if value in ("auto", "native", "docker", "none") else "auto"


def _native_pdflatex() -> Optional[str]:
    return shutil.which("pdflatex")


def tex_tooling_available(ttl: float = 300.0) -> dict[str, Any]:
    """Cached check of whether LaTeX compilation is usable, and via which engine.

    Returns ``{"tex_available": bool, "engine": "native"|"docker-texlive"|None,
    "reason": str|None}`` — ``reason`` explains why None when unavailable.
    """
    global _last_tex_check, _last_tex_result
    now = time.monotonic()
    if _last_tex_result is None or (now - _last_tex_check) > ttl:
        _last_tex_check = now
        engine = _requested_engine()

        if engine == "none":
            _last_tex_result = {"tex_available": False, "engine": None,
                                "reason": f"{_LATEX_ENGINE_ENV}=none"}
        elif engine in ("auto", "native") and _native_pdflatex():
            _last_tex_result = {"tex_available": True, "engine": "native", "reason": None}
        elif engine == "native":
            _last_tex_result = {"tex_available": False, "engine": None,
                                "reason": "pdflatex not found on PATH"}
        else:
            _last_tex_result = _docker_texlive_probe()
    return _last_tex_result


def _docker_texlive_probe() -> dict[str, Any]:
    docker = shutil.which("docker")
    if not docker:
        return {"tex_available": False, "engine": None,
                "reason": "no pdflatex on PATH and no docker command to run the texlive image"}
    try:
        result = subprocess.run(
            ["docker", "images", "-q", LATEX_IMAGE],
            capture_output=True, text=True, timeout=20,
        )
        ok = bool(result.stdout.strip())
        return {
            "tex_available": ok,
            "engine": "docker-texlive" if ok else None,
            "reason": None if ok else f"image {LATEX_IMAGE} not pulled",
        }
    except Exception as e:
        return {"tex_available": False, "engine": None, "reason": str(e)}


def _mount_path(path: Path) -> str:
    mount = path.resolve().as_posix()
    if len(mount) > 1 and mount[1] == ":":
        # Windows drive letter → C:/... (Docker Desktop accepts it)
        return mount
    return mount


def _tail(text: str, lines: int = 40) -> str:
    return "\n".join(text.strip().splitlines()[-lines:])


def _passes_saved_pdf(workdir: Path, command: list[str], timeout: int, max_passes: int,
                      label: str) -> tuple[Optional[bytes], str]:
    """Run the compile up to ``max_passes`` times and return (pdf_bytes, error).

    TeX exits 1 when it wants a rerun (hyperref outlines etc.), so the exit
    code alone is NOT a success signal. Real failures leave "! " error lines
    in main.log (missing image, bad macro, ...).
    """
    pdf = workdir / "main.pdf"
    log_file = workdir / "main.log"
    last_log_text = ""
    last_stderr = ""
    for _ in range(max_passes):
        logger.info("Compiling LaTeX (%s): %s", label, " ".join(command))
        result = subprocess.run(command, capture_output=True, text=True, timeout=timeout,
                                cwd=workdir)
        last_stderr = result.stderr or ""
        last_log_text = (log_file.read_text(encoding="utf-8", errors="replace")
                         if log_file.is_file() else "")

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


def _compile_native(tex_source: str, workdir: Path, timeout: int, max_passes: int) -> tuple[Optional[bytes], str]:
    pdflatex = _native_pdflatex()
    if pdflatex is None:
        return None, "pdflatex not found on PATH"
    (workdir / "main.tex").write_text(tex_source, encoding="utf-8")
    command = [pdflatex, "-interaction=nonstopmode", "main"]
    return _passes_saved_pdf(workdir, command, timeout, max_passes, "native")


def _compile_docker(tex_source: str, workdir: Path, timeout: int, max_passes: int) -> tuple[Optional[bytes], str]:
    (workdir / "main.tex").write_text(tex_source, encoding="utf-8")
    mount = _mount_path(workdir)
    script = "cd /workspace && pdflatex -interaction=nonstopmode main"
    command = ["docker", "run", "--rm", "-v", f"{mount}:/workspace", LATEX_IMAGE, "sh", "-c", script]
    return _passes_saved_pdf(workdir, command, timeout, max_passes, "docker")


def compile_latex_to_pdf(
    tex_source: str,
    photo_bytes: Optional[bytes] = None,
    photo_ext: str = "jpg",
    timeout: int = 300,
    max_passes: int = 3,
) -> tuple[Optional[bytes], str]:
    """Compile LaTeX to PDF (native pdflatex, or the dockerized texlive image).

    Returns (pdf_bytes, error_message_or_empty). On failure the returned
    bytes are None and the message contains the tail of main.log / stderr.
    """
    info = tex_tooling_available()
    if not info["tex_available"]:
        return None, f"LaTeX engine unavailable: {info.get('reason') or 'no compile engine found'}"

    workdir = Path(tempfile.mkdtemp(prefix="cvtex_"))
    try:
        if photo_bytes:
            ext = photo_ext.lstrip(".").lower() or "jpg"
            (workdir / f"photo.{ext}").write_bytes(photo_bytes)
        engine = info.get("engine")
        if engine == "native":
            return _compile_native(tex_source, workdir, timeout, max_passes)
        return _compile_docker(tex_source, workdir, timeout, max_passes)
    finally:
        shutil.rmtree(workdir, ignore_errors=True)
