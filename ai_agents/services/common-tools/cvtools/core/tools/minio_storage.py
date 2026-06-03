"""
minio_storage.py  -  MinIO Object Storage Utilities
=====================================================
Upload and retrieve files from MinIO (S3-compatible storage).
Used by CV services to store generated PDF files and CV templates.
"""

import json
import os
from typing import Optional

from minio import Minio
from minio.error import S3Error


# Default bucket name for CV PDFs
DEFAULT_BUCKET = "cv-pdfs"

# Bucket name for CV templates
TEMPLATES_BUCKET = "cv-templates"


def get_minio_client(
    endpoint: str | None = None,
    access_key: str | None = None,
    secret_key: str | None = None,
    secure: bool = False,
) -> Minio:
    """
    Create and return a MinIO client.

    Args:
        endpoint:   MinIO server URL (default: from MINIO_ENDPOINT env var or localhost:9000)
        access_key: MinIO access key (default: from MINIO_ROOT_USER env var)
        secret_key: MinIO secret key (default: from MINIO_ROOT_PASSWORD env var)
        secure:     Whether to use HTTPS (default: False for local dev)

    Returns:
        A configured Minio client instance.
    """
    return Minio(
        endpoint=endpoint or os.getenv("MINIO_ENDPOINT", "localhost:9000"),
        access_key=access_key or os.getenv("MINIO_ROOT_USER", "minioadmin"),
        secret_key=secret_key or os.getenv("MINIO_ROOT_PASSWORD", "minioadmin"),
        secure=secure,
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
    secure: bool = False,
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

    endpoint = os.getenv("MINIO_ENDPOINT", "localhost:9000")
    scheme = "https" if secure else "http"
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
