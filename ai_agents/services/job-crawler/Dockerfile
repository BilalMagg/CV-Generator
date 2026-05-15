# NOTE: This service does NOT depend on common-tools (no LLM or backend HTTP client needed).
# It installs only its own requirements.txt.

FROM python:3.11-slim

WORKDIR /app

COPY services/job-crawler/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

COPY services/job-crawler/app/ ./app/

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8006"]
