FROM python:3.12-slim

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./app

ENV PROVIDERS_CACHE_DIR=/data/cache
RUN mkdir -p /data/cache

EXPOSE 8090

HEALTHCHECK --interval=10s --timeout=5s --retries=10 --start-period=20s \
  CMD curl -fsS http://127.0.0.1:8090/health || exit 1

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8090"]
