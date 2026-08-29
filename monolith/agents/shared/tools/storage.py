"""MinIO object storage helpers.

Files live in MinIO; metadata (names, owners, types...) is stored in the
backend database. Used by the template agent to persist generated CV artifacts
and, in later increments, template sources and profile images.
"""
import io
import logging

from minio import Minio
from minio.error import S3Error

from shared.config import settings

logger = logging.getLogger(__name__)

_minio_client: Minio | None = None

CV_ARTIFACTS_BUCKET = "cv-artifacts"
CV_TEMPLATES_BUCKET = "cv-templates"
PROFILE_IMAGES_BUCKET = "profile-images"

# Buckets hold files referenced by their public URL (PDF preview iframes, downloads).
PUBLIC_READ_POLICY = (
    '{"Version":"2012-10-17","Statement":[{"Effect":"Allow",'
    '"Principal":{"AWS":["*"]},"Action":["s3:GetObject"],'
    '"Resource":["arn:aws:s3:::{bucket}/*"]}]}'
)


def get_minio_client() -> Minio:
    global _minio_client
    if _minio_client is None:
        _minio_client = Minio(
            settings.MINIO_ENDPOINT,
            access_key=settings.MINIO_ROOT_USER,
            secret_key=settings.MINIO_ROOT_PASSWORD,
            secure=settings.MINIO_SECURE,
        )
    return _minio_client


def ensure_bucket(bucket: str) -> None:
    client = get_minio_client()
    if not client.bucket_exists(bucket):
        client.make_bucket(bucket)
        logger.info("Created MinIO bucket '%s'", bucket)
    client.set_bucket_policy(bucket, PUBLIC_READ_POLICY.replace("{bucket}", bucket))
    logger.info("Set public-read policy on MinIO bucket '%s'", bucket)


def public_url(bucket: str, key: str) -> str:
    scheme = "https" if settings.MINIO_SECURE else "http"
    return f"{scheme}://{settings.MINIO_ENDPOINT}/{bucket}/{key}"


def upload_bytes(
    bucket: str,
    key: str,
    data: bytes,
    content_type: str = "application/octet-stream",
) -> str:
    ensure_bucket(bucket)
    client = get_minio_client()
    client.put_object(bucket, key, io.BytesIO(data), length=len(data), content_type=content_type)
    logger.info("Uploaded %d bytes to %s/%s", len(data), bucket, key)
    return public_url(bucket, key)


def get_object(bucket: str, key: str) -> bytes | None:
    client = get_minio_client()
    try:
        response = client.get_object(bucket, key)
        try:
            return response.read()
        finally:
            response.close()
            response.release_conn()
    except S3Error as e:
        if e.code == "NoSuchKey":
            logger.warning("MinIO object missing: %s/%s", bucket, key)
            return None
        logger.error("Failed to read MinIO object %s/%s: %s", bucket, key, e)
        return None
    except Exception as e:
        logger.error("Failed to read MinIO object %s/%s: %s", bucket, key, e)
        return None


def object_exists(bucket: str, key: str) -> bool:
    return get_object(bucket, key) is not None