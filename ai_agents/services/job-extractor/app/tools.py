import json
import logging
from uuid import UUID

import httpx
from cvtools.core.tools import extract_text_from_pdf_bytes

logger = logging.getLogger(__name__)


async def fetch_url_content(url: str) -> str:
    try:
        from bs4 import BeautifulSoup

        async with httpx.AsyncClient(timeout=30.0, follow_redirects=True) as client:
            resp = await client.get(url)
            resp.raise_for_status()
            content_type = resp.headers.get("content-type", "")
            if "application/json" in content_type:
                data = resp.json()
                return json.dumps(data)
            soup = BeautifulSoup(resp.text, "lxml")
            for tag in soup(["script", "style", "nav", "footer", "header"]):
                tag.decompose()
            text = soup.get_text(separator="\n", strip=True)
            return text
    except Exception as e:
        logger.error(f"Failed to fetch URL {url}: {e}")
        raise ValueError(f"Could not fetch content from URL: {url}")


async def load_file_content(content: bytes, filename: str) -> str:
    ext = filename.lower().rsplit(".", 1)[-1] if "." in filename else ""
    if ext == "pdf":
        return extract_text_from_pdf_bytes(content)
    if ext in ("txt", "md", "csv"):
        return content.decode("utf-8")
    if ext == "json":
        data = json.loads(content)
        if isinstance(data, dict):
            return data.get("description") or data.get("body") or data.get("text") or json.dumps(data)
        return json.dumps(data)
    return content.decode("utf-8", errors="replace")


async def fetch_job_offer(job_offer_id: UUID) -> str:
    from app.core import backend_client
    client = backend_client.get_client()
    try:
        resp = await client.get(f"/api/job-offers/{job_offer_id}")
        resp.raise_for_status()
        body = resp.json()
        data = body.get("data") or body
        if isinstance(data, dict):
            return data.get("description") or data.get("body") or json.dumps(data)
        return str(data)
    except httpx.ConnectError:
        raise ValueError("Job offer service is not available yet")
    except httpx.HTTPStatusError as e:
        if e.response.status_code == 404:
            raise ValueError(f"Job offer {job_offer_id} not found")
        raise ValueError(f"Failed to fetch job offer: {e}")
