"""
minio_storage.py  -  MinIO Object Storage Utilities
=====================================================
Upload and retrieve files from MinIO (S3-compatible storage).
Used by CV services to store generated PDF files.
"""

import os
from typing import Optional

from minio import Minio
from minio.error import S3Error


# Default bucket name for CV PDFs
DEFAULT_BUCKET = "cv-pdfs"


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
) -> str:
    """
    Upload a PDF file to MinIO and return the object URL.

    Args:
        file_path:   Absolute path to the local PDF file.
        object_name: Name of the object in MinIO. Defaults to the file's basename.
        bucket_name: The MinIO bucket to upload to.
        client:      An existing Minio client. If None, one will be created.

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
    return f"http://{endpoint}/{bucket_name}/{object_name}"


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
    """
    if client is None:
        client = get_minio_client()

    client.fget_object(bucket_name, object_name, download_path)
    return os.path.abspath(download_path)
