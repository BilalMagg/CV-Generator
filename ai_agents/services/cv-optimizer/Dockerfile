FROM python:3.11-slim

WORKDIR /app

COPY services/common-tools services/common-tools
RUN pip install --no-cache-dir -e ./services/common-tools

COPY services/cv-optimizer/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

COPY services/common-tools/cvtools/models/ ./app/models/

COPY services/cv-optimizer/app/ ./app/

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8004"]
