FROM node:24-bookworm-slim AS node
FROM python:3.12-slim
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg libsndfile1 && rm -rf /var/lib/apt/lists/*
COPY --from=node /usr/local/bin/node /usr/local/bin/node
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir 'torch==2.14.0+cpu' --index-url https://download.pytorch.org/whl/cpu
RUN pip install --no-cache-dir -r requirements.txt
COPY app ./app
COPY tests ./tests
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
