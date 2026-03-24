FROM python:3.11-slim

ENV PYTHONUNBUFFERED=1
ENV UV_SYSTEM_PYTHON=1

RUN apt-get update && apt-get install -y curl \
    && curl -Ls https://astral.sh/uv/install.sh | bash \
    && mv /root/.local/bin/uv /usr/local/bin/uv \
    && apt-get clean

WORKDIR /app

COPY pyproject.toml uv.lock ./
RUN uv sync --frozen --no-dev

COPY . .

EXPOSE 8000

CMD ["uv", "run", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]