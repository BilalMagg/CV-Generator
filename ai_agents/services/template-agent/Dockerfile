FROM python:3.11-slim
WORKDIR /app
COPY services/common-tools ./common-tools
RUN pip install --no-cache-dir -e ./common-tools
COPY services/template-agent/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY services/template-agent/app ./app
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8003"]