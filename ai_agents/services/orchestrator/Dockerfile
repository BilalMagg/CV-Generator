FROM python:3.11-slim

WORKDIR /app


COPY services/common-tools services/common-tools
RUN pip install --no-cache-dir -e ./services/common-tools

COPY app/ ./app/

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]