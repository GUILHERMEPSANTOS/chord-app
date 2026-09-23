# Processador Python

Este serviço FastAPI recebe áudio da API .NET, executa os detectores localmente e devolve o tom estimado e os acordes com tempos em segundos. A API .NET é responsável por salvar os resultados no PostgreSQL; o Python não acessa o banco.

## Onde começar

| Arquivo ou pasta | Função |
| --- | --- |
| `app/main.py` | Ponto de entrada HTTP: define as rotas `/health`, `/progress/{job_id}`, `/analyze` e `/analyze-youtube`. |
| `app/youtube.py` | Valida URLs, chama o yt-dlp, aplica os limites do vídeo e traduz falhas de download. |
| `app/audio.py` | Converte o áudio com FFmpeg para o WAV usado pelos modelos e estima o tom global. |
| `app/analysis.py` | Executa lv-chordia e BTC-ISMIR19 separadamente e monta a resposta. Se o BTC falhar, preserva o resultado do lv-chordia. |
| `app/chords.py` | Converte os rótulos dos detectores para os nomes de acordes aceitos pela aplicação. |
| `app/progress.py` | Mantém o progresso recente em memória e converte mensagens das cinco passagens do lv-chordia em porcentagens. |
| `benchmark/` | Código separado para avaliar previsões contra anotações de referência; não participa do atendimento das rotas. |
| `tests/` | Testes das regras e da orquestração, usando modelos simulados para serem rápidos. |
| `Dockerfile` | Instala Python, FFmpeg, dependências e pesos necessários para execução local. |

## Fluxo de uma análise

1. A API .NET envia um arquivo para `POST /analyze` ou uma URL para `POST /analyze-youtube`, com um `job_id`.
2. A rota cria uma pasta temporária. No caso do YouTube, `youtube.py` baixa somente o áudio; no upload, a rota copia o arquivo recebido.
3. `audio.py` converte o arquivo com FFmpeg para um WAV normalizado. Uma falha de decodificação vira HTTP 422.
4. `analysis.py` executa o lv-chordia, converte seus rótulos com `chords.py` e estima o tom. Em seguida executa o BTC-ISMIR19 e guarda seus acordes em uma lista separada.
5. `progress.py` registra marcos consultados em `GET /progress/{job_id}`. A porcentagem é aproximada e não representa o tempo restante.
6. A resposta contém `key`, `durationSeconds`, `chords`, `results` por modelo e `modelErrors`. A API .NET salva esses dados e permite escolher qual modelo mostrar.
7. Ao sair da rota, `TemporaryDirectory` apaga o áudio baixado, o upload e o WAV convertido, inclusive quando há erro.

`asyncio.to_thread` em `main.py` executa download e inferência bloqueantes fora do loop HTTP do FastAPI. A análise ainda usa CPU intensivamente e pode demorar vários minutos. O bloqueio em `analysis.py` evita que duas chamadas simultâneas ao lv-chordia misturem a saída de progresso.

## Executar e testar

Na raiz do repositório:

```sh
docker compose up --build
docker compose exec processor python -m unittest discover -s tests -v
```

O serviço é acessível pela API .NET em `http://processor:8000` dentro da rede do Compose. A porta do processador não é exposta ao computador. Os testes simulam a inferência; para medir precisão real, use áudio com acordes anotados e siga [o guia de avaliação](../benchmark/README.md).

As dependências Python estão em `requirements.txt`. `YOUTUBE_COOKIES_FILE` é opcional e aponta para um arquivo de cookies montado dentro do contêiner conforme `compose.cookies.example.yaml`; não coloque cookies no Git.
