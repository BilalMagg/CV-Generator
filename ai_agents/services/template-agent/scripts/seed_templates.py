r"""Seed the cv-templates bucket from the bundled templates/ directory.

Thin CLI wrapper over cvtools.seed_templates_from_dir — the same helper the
service runs on startup. Run from the host venv with MinIO env vars pointing at
the running MinIO:

    $env:MINIO_ENDPOINT="localhost:9000"
    $env:MINIO_ROOT_USER="minioadmin"
    $env:MINIO_ROOT_PASSWORD="minioadmin"
    .\.venv\Scripts\python.exe services/template-agent/scripts/seed_templates.py [--overwrite]

By default existing templates are left untouched (seed-if-missing); pass
--overwrite to re-upload them after editing a template file.

Preview images: place a file named `{template_id}.preview.png` (or .jpg) in the
templates/ directory. The seed script uploads it to MinIO as `{template_id}.preview`.
Example: html_basic.preview.png  →  MinIO object key: html_basic.preview
"""
import io
import pathlib
import sys

from cvtools import seed_templates_from_dir
from cvtools.core.tools.minio_storage import get_minio_client, ensure_bucket, TEMPLATES_BUCKET

TEMPLATES_DIR = pathlib.Path(__file__).resolve().parent.parent / "templates"

_PREVIEW_EXTS = {".png", ".jpg", ".jpeg", ".webp"}


def seed_previews_from_dir(templates_dir: pathlib.Path, overwrite: bool = False) -> list[str]:
    """Upload preview images to MinIO as {template_id}.preview objects."""
    client = get_minio_client()
    ensure_bucket(client, TEMPLATES_BUCKET)
    seeded: list[str] = []
    for path in sorted(pathlib.Path(templates_dir).glob("*")):
        if path.suffix.lower() not in _PREVIEW_EXTS:
            continue
        # expect naming: {template_id}.preview.png or {template_id}.preview.jpg
        stem = path.stem  # e.g. "html_basic.preview"
        if not stem.endswith(".preview"):
            continue
        object_key = stem  # store as "html_basic.preview"
        if not overwrite:
            try:
                from minio.error import S3Error
                client.stat_object(TEMPLATES_BUCKET, object_key)
                continue  # already present
            except Exception:
                pass
        data = path.read_bytes()
        content_type = "image/png" if path.suffix.lower() == ".png" else "image/jpeg"
        client.put_object(
            TEMPLATES_BUCKET, object_key,
            io.BytesIO(data), length=len(data), content_type=content_type,
        )
        seeded.append(object_key)
    return seeded


def main() -> None:
    overwrite = "--overwrite" in sys.argv
    seeded_tpl = seed_templates_from_dir(TEMPLATES_DIR, overwrite=overwrite)
    print("templates seeded:", seeded_tpl or "nothing (all present)")
    seeded_prev = seed_previews_from_dir(TEMPLATES_DIR, overwrite=overwrite)
    print("previews seeded: ", seeded_prev or "nothing (all present)")


if __name__ == "__main__":
    main()
