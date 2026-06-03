"""
minio_storage.py  -  MinIO Object Storage Utilities
=====================================================
Upload and retrieve files from MinIO (S3-compatible storage).
Used by CV services to store generated PDF files and CV templates.
"""

import json
import logging
import os
from typing import Optional

from minio import Minio
from minio.error import S3Error


# Bucket names — overridable via env so local dev and deploy share the same
# names (only the values differ). Bucket names are non-secret config, so a
# sane default here is safe and does not mask a misconfiguration.
DEFAULT_BUCKET = os.getenv("MINIO_BUCKET", "cv-pdfs")  # generated CV PDFs
TEMPLATES_BUCKET = os.getenv("MINIO_TEMPLATES_BUCKET", "cv-templates")  # CV templates


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
        endpoint=endpoint or _require_env("MINIO_ENDPOINT"),
        access_key=access_key or _require_env("MINIO_ROOT_USER"),
        secret_key=secret_key or _require_env("MINIO_ROOT_PASSWORD"),
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

    endpoint = _require_env("MINIO_ENDPOINT")
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


def ensure_templates_bucket(client: Minio) -> None:
    """
    Create the cv-templates bucket if it doesn't already exist.
    """
    if not client.bucket_exists(TEMPLATES_BUCKET):
        client.make_bucket(TEMPLATES_BUCKET)


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

    response = client.get_object(bucket_name, template_id)
    data = json.loads(response.read().decode("utf-8"))
    response.close()
    response.release_conn()
    return data


def init_minio_storage() -> None:
    """
    Validate MinIO configuration at service startup and pre-create buckets.

    Call this from a service's startup hook so a misconfigured deployment
    fails loudly on boot (like an unset encryption key) instead of on the
    first upload:

      * Missing required env vars -> RuntimeError (always fatal).
      * Bad credentials / permissions (S3Error) -> re-raised (fatal).
      * MinIO simply not reachable yet -> logged warning, startup continues;
        the buckets are then created on first write via ensure_bucket().
    """
    # Fail loud on misconfiguration regardless of whether MinIO is reachable.
    _require_env("MINIO_ENDPOINT")
    _require_env("MINIO_ROOT_USER")
    _require_env("MINIO_ROOT_PASSWORD")

    try:
        client = get_minio_client()
        ensure_bucket(client, DEFAULT_BUCKET)
        ensure_bucket(client, TEMPLATES_BUCKET)
    except S3Error:
        # Reachable but the operation was rejected (e.g. bad creds / access
        # denied) — that is a real misconfiguration, so surface it loudly.
        raise
    except Exception as exc:  # noqa: BLE001 - transient connectivity at boot
        logging.getLogger(__name__).warning(
            "MinIO config is valid but the server was not reachable at startup "
            "(%s); buckets '%s' and '%s' will be created on first write.",
            exc, DEFAULT_BUCKET, TEMPLATES_BUCKET,
        )
