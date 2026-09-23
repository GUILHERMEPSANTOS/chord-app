FROM node:24-bookworm-slim AS node
FROM python:3.12-slim
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg git libsndfile1 && rm -rf /var/lib/apt/lists/*
COPY --from=node /usr/local/bin/node /usr/local/bin/node
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir 'torch==2.14.0+cpu' --index-url https://download.pytorch.org/whl/cpu
RUN pip install --no-cache-dir -r requirements.txt
COPY requirements-benchmark.txt .
RUN pip install --no-cache-dir -r requirements-benchmark.txt
RUN git init /opt/btc && git -C /opt/btc remote add origin https://github.com/jayg996/BTC-ISMIR19.git \
    && git -C /opt/btc fetch --depth 1 origin 2682317be668032e6e4b269ded36adaa2ad57df0 \
    && git -C /opt/btc checkout --detach FETCH_HEAD \
    && echo "1673d23f8f9a55ae7f9e8b80a51da616debb22675b8d8b67ea6ce0ef37b0ab51  /opt/btc/test/btc_model_large_voca.pt" | sha256sum -c -
COPY app ./app
COPY benchmark ./benchmark
COPY tests ./tests
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
