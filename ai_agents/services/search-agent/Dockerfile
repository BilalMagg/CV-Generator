FROM python:3.11-slim

WORKDIR /app

COPY services/search-agent/requirements.txt .
RUN apt-get update && apt-get install -y gcc python3-dev && pip install --no-cache-dir -r requirements.txt && apt-get remove -y gcc python3-dev && apt-get autoremove -y && rm -rf /var/lib/apt/lists/*
# Merge the common-tools and search-agent into a single app folder
COPY services/common-tools/app/ ./app/
COPY services/search-agent/app/ ./app/

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8002"]