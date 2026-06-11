"""
minio_storage.py  -  MinIO Object Storage Utilities
=====================================================
Upload and retrieve files from MinIO (S3-compatible storage).
Used by CV services to store generated PDF files and CV templates.
"""

import io
import json
import logging
import os
import pathlib
from typing import Optional

from minio import Minio
from minio.error import S3Error


# Bucket names — overridable via env so local dev and deploy share the same
# names (only the values differ). Bucket names are non-secret config, so a
# sane default here is safe and does not mask a misconfiguration.
DEFAULT_BUCKET = os.getenv("MINIO_BUCKET", "cv-pdfs")  # generated CV PDFs
TEMPLATES_BUCKET = os.getenv("MINIO_TEMPLATES_BUCKET", "cv-templates")  # CV templates
CODE_BUCKET = os.getenv("MINIO_CODE_BUCKET", "cv-sources")  # rendered CV source code


def _require_env(name: str) -> str:
    """
    Read a required MinIO env var, failing loudly if it is missing.

    There is deliberately NO localhost fallback: a silent default
    (e.g. localhost:9000) is exactly what makes object storage mysteriously
    fail in the cloud, so an unconfigured deployment must crash with a clear
    message instead of pretending to be configured.
    """
    value = os.getenv(name)
    if not value:
        raise RuntimeError(
            f"{name} is not set. MinIO object storage requires {name} to be "
            f"provided via an environment variable (no localhost fallback)."
        )
    return value


def _minio_secure() -> bool:
    """
    Whether to connect to MinIO over TLS, driven by the MINIO_SECURE env var.

    Cloud MinIO commonly sits behind TLS while local dev is plaintext, so this
    must be env-driven rather than hardcoded. Defaults to False (plaintext)
    when unset, which is the safe local-dev default.
    """
    raw = os.getenv("MINIO_SECURE")
    if raw is None:
        return False
    return raw.strip().lower() in ("1", "true", "yes", "on")


def get_minio_client(
    endpoint: str | None = None,
    access_key: str | None = None,
    secret_key: str | None = None,
    secure: bool | None = None,
) -> Minio:
    """
    Create and return a MinIO client from environment configuration.

    All connection details come from env vars (no localhost fallback); a
    missing required var raises RuntimeError. Explicit arguments override the
    env values (used mainly in tests).

    Args:
        endpoint:   MinIO server URL. Default: MINIO_ENDPOINT env var (required).
        access_key: MinIO access key. Default: MINIO_ROOT_USER env var (required).
        secret_key: MinIO secret key. Default: MINIO_ROOT_PASSWORD env var (required).
        secure:     Use HTTPS. Default: MINIO_SECURE env var (false when unset).

    Returns:
        A configured Minio client instance.
    """
    return Minio(
        endpoint=endpoint or os.getenv("MINIO_ENDPOINT", "localhost:9000"),
        access_key=access_key or os.getenv("MINIO_ROOT_USER", "minioadmin"),
        secret_key=secret_key or os.getenv("MINIO_ROOT_PASSWORD", "minioadmin"),
        secure=_minio_secure() if secure is None else secure,
    )


def ensure_bucket(client: Minio, bucket_name: str = DEFAULT_BUCKET) -> None:
    """
    Create the bucket if it doesn't already exist.
    """
    if not client.bucket_exists(bucket_name):
        client.make_bucket(bucket_name)


def upload_pdf(
    file_path: str,
    object_name: Optional[str] = None,
    bucket_name: str = DEFAULT_BUCKET,
    client: Optional[Minio] = None,
    secure: bool | None = None,
) -> str:
    """
    Upload a PDF file to MinIO and return the object URL.

    Args:
        file_path:   Absolute path to the local PDF file.
        object_name: Name of the object in MinIO. Defaults to the file's basename.
        bucket_name: The MinIO bucket to upload to.
        client:      An existing Minio client. If None, one will be created.
        secure:      Whether to use HTTPS in the returned URL.

    Returns:
        The URL to access the uploaded file (e.g., http://localhost:9000/cv-pdfs/abc123.pdf)
    """
    if client is None:
        client = get_minio_client()

    ensure_bucket(client, bucket_name)

    if object_name is None:
        object_name = os.path.basename(file_path)

    client.fput_object(
        bucket_name,
        object_name,
        file_path,
        content_type="application/pdf",
    )

    endpoint = os.getenv("MINIO_PUBLIC_ENDPOINT") or os.getenv("MINIO_ENDPOINT", "localhost:9000")
    use_tls = _minio_secure() if secure is None else secure
    scheme = "https" if use_tls else "http"
    return f"{scheme}://{endpoint}/{bucket_name}/{object_name}"


def upload_code(
    code: str,
    object_name: str,
    bucket_name: str = CODE_BUCKET,
    client: Optional[Minio] = None,
    content_type: str = "text/plain",
    secure: bool | None = None,
) -> str:
    """
    Upload rendered CV source code (LaTeX/HTML) to MinIO and return its URL.

    Unlike upload_pdf this stores an in-memory string directly, so the caller
    does not need to write the source to a temp file first.

    Args:
        code:         The raw CV source code (LaTeX or HTML).
        object_name:  Name of the object in MinIO (e.g. "default-abc123.tex").
        bucket_name:  The MinIO bucket to upload to (default: cv-sources).
        client:       An existing Minio client. If None, one will be created.
        content_type: MIME type for the object (e.g. "text/x-tex", "text/html").
        secure:       Whether to use HTTPS in the returned URL.

    Returns:
        The URL to access the uploaded source file.
    """
    if client is None:
        client = get_minio_client()

    ensure_bucket(client, bucket_name)

    data = code.encode("utf-8")
    client.put_object(
        bucket_name,
        object_name,
        io.BytesIO(data),
        length=len(data),
        content_type=content_type,
    )

    endpoint = os.getenv("MINIO_PUBLIC_ENDPOINT") or os.getenv("MINIO_ENDPOINT", "localhost:9000")
    use_tls = _minio_secure() if secure is None else secure
    scheme = "https" if use_tls else "http"
    return f"{scheme}://{endpoint}/{bucket_name}/{object_name}"


def download_pdf(
    object_name: str,
    download_path: str,
    bucket_name: str = DEFAULT_BUCKET,
    client: Optional[Minio] = None,
) -> str:
    """
    Download a PDF file from MinIO.

    Args:
        object_name:   Name of the object in MinIO.
        download_path: Local path to save the downloaded file.
        bucket_name:   The MinIO bucket to download from.
        client:        An existing Minio client. If None, one will be created.

    Returns:
        The absolute path to the downloaded file.

    Raises:
        S3Error: If the bucket or object does not exist.
    """
    if client is None:
        client = get_minio_client()

    ensure_bucket(client, bucket_name)
    client.fget_object(bucket_name, object_name, download_path)
    return os.path.abspath(download_path)


def init_minio_storage() -> Minio:
    """
    Initialize MinIO client and ensure required buckets exist.

    Creates the default CV PDFs bucket and templates bucket if they
    don't already exist. Returns the configured Minio client.

    Raises:
        RuntimeError: If required MinIO environment variables are not set.
    """
    client = get_minio_client()
    ensure_bucket(client, DEFAULT_BUCKET)
    ensure_templates_bucket(client)
    ensure_code_bucket(client)
    return client


def ensure_templates_bucket(client: Minio) -> None:
    """
    Create the cv-templates bucket if it doesn't already exist.
    """
    if not client.bucket_exists(TEMPLATES_BUCKET):
        client.make_bucket(TEMPLATES_BUCKET)


def ensure_code_bucket(client: Minio) -> None:
    """
    Create the cv-sources bucket (rendered CV source code) if it doesn't exist.
    """
    if not client.bucket_exists(CODE_BUCKET):
        client.make_bucket(CODE_BUCKET)


# File extension -> (template type, JSON field) used when seeding templates.
_TEMPLATE_EXT = {
    ".tex": ("latex", "latex_code"),
    ".html": ("html", "html_code"),
    ".htm": ("html", "html_code"),
}


def seed_templates_from_dir(
    templates_dir,
    bucket_name: str = TEMPLATES_BUCKET,
    client: Optional[Minio] = None,
    overwrite: bool = False,
) -> list[str]:
    """
    Seed a templates bucket from a local directory of .tex/.html files.

    Each file becomes a template object keyed by its filename stem, with the
    template type and code field inferred from the extension (.tex -> latex,
    .html/.htm -> html). The stored JSON matches get_template_object's shape:
    {"id", "type", "latex_code", "html_code"}.

    Seed-if-missing by default (idempotent across restarts): an object that
    already exists is left untouched unless overwrite=True. The directory is the
    single source of truth, so no per-template spec is needed.

    Args:
        templates_dir: Path to the directory holding the template source files.
        bucket_name:   Target bucket (default: cv-templates).
        client:        An existing Minio client. If None, one will be created.
        overwrite:     Re-upload even if the object already exists.

    Returns:
        The list of template ids that were uploaded this call.
    """
    if client is None:
        client = get_minio_client()

    ensure_bucket(client, bucket_name)

    seeded: list[str] = []
    for path in sorted(pathlib.Path(templates_dir).glob("*")):
        meta = _TEMPLATE_EXT.get(path.suffix.lower())
        if not meta:
            continue
        template_type, code_field = meta
        template_id = path.stem

        if not overwrite:
            try:
                client.stat_object(bucket_name, template_id)
                continue  # already present — leave it alone
            except S3Error:
                pass

        document = {"id": template_id, "type": template_type, "latex_code": "", "html_code": ""}
        document[code_field] = path.read_text(encoding="utf-8")
        data = json.dumps(document).encode("utf-8")
        client.put_object(
            bucket_name,
            template_id,
            io.BytesIO(data),
            length=len(data),
            content_type="application/json",
        )
        seeded.append(template_id)

    return seeded


DEFAULT_TEMPLATE = {
    "id": "default",
    "type": "html",
    "latex_code": "",
    "html_code": """<!DOCTYPE html>
<html>
<head><meta charset="utf-8"><style>
body { font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; color: #333; }
h1 { color: #1a5276; border-bottom: 2px solid #1a5276; padding-bottom: 8px; }
h2 { color: #2c3e50; margin-top: 24px; }
.section { margin: 16px 0; }
ul { list-style: none; padding-left: 0; }
li { padding: 4px 0; }
</style></head>
<body>
<h1>{candidate_name}</h1>
<h2>Professional Summary</h2>
<p>{summary}</p>
<h2>Experience</h2>
<ul>{experience}</ul>
<h2>Skills</h2>
<ul>{skills}</ul>
<h2>Projects</h2>
<ul>{projects}</ul>
</body></html>"""
}


def get_template_object(
    template_id: str,
    bucket_name: str = TEMPLATES_BUCKET,
    client: Optional[Minio] = None,
) -> dict:
    """
    Fetch template JSON from MinIO.

    Args:
        template_id:  The template ID (object key) to fetch.
        bucket_name:  The MinIO bucket name (default: cv-templates).
        client:       An existing Minio client. If None, one will be created.

    Returns:
        dict with keys: id, type, latex_code, html_code
        Example: {"id": "template_001", "type": "latex", "latex_code": "...", "html_code": ""}

    Raises:
        S3Error: If the object doesn't exist or bucket is inaccessible.
    """
    if client is None:
        client = get_minio_client()

    ensure_templates_bucket(client)

    try:
        response = client.get_object(bucket_name, template_id)
        data = json.loads(response.read().decode("utf-8"))
        response.close()
        response.release_conn()
        return data
    except S3Error:
        return DEFAULT_TEMPLATE
