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
"""
import pathlib
import sys

from cvtools import seed_templates_from_dir

TEMPLATES_DIR = pathlib.Path(__file__).resolve().parent.parent / "templates"


def main() -> None:
    overwrite = "--overwrite" in sys.argv
    seeded = seed_templates_from_dir(TEMPLATES_DIR, overwrite=overwrite)
    print("seeded:", seeded or "nothing (all present)")


if __name__ == "__main__":
    main()
