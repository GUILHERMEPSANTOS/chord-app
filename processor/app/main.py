"""Rotas HTTP do processador; delega áudio, YouTube, inferência e progresso aos módulos app."""

import asyncio
from pathlib import Path
import subprocess
import tempfile

from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from pydantic import BaseModel

from app.analysis import convert_and_analyze
from app.progress import get_progress, set_progress
from app.youtube import YouTubeDownloadError, canonical_youtube_url, download_youtube


app = FastAPI(title="ChordApp Processor")


class YouTubeRequest(BaseModel):
    url: str
    jobId: str | None = None


@app.get("/health")
def health():
    return {"status": "ok"}


@app.get("/progress/{job_id}")
def progress(job_id: str):
    value = get_progress(job_id)
    if value is None:
        raise HTTPException(404, "Progresso indisponível")
    return value


@app.post("/analyze")
async def analyze_upload(file: UploadFile = File(...), job_id: str | None = Form(None)):
    """Recebe MP3/WAV, aplica o limite de tamanho e apaga os temporários ao final."""
    set_progress(job_id, 5, "Recebendo áudio")
    suffix = Path(file.filename or "").suffix.lower()
    if suffix not in (".mp3", ".wav"):
        raise HTTPException(400, "Formato inválido")
    with tempfile.TemporaryDirectory() as directory:
        source = str(Path(directory) / ("input" + suffix))
        target = str(Path(directory) / "normalized.wav")
        size = 0
        with open(source, "wb") as output:
            while chunk := await file.read(1024 * 1024):
                size += len(chunk)
                if size > 30 * 1024 * 1024:
                    raise HTTPException(413, "Arquivo muito grande")
                output.write(chunk)
        try:
            return await asyncio.to_thread(convert_and_analyze, source, target, job_id)
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as exc:
            raise HTTPException(422, "Não foi possível decodificar o áudio") from exc


@app.post("/analyze-youtube")
async def analyze_youtube(request: YouTubeRequest):
    """Extrai áudio temporário do vídeo e reutiliza o mesmo fluxo de análise do upload."""
    url = canonical_youtube_url(request.url)
    if url is None:
        raise HTTPException(400, "URL do YouTube inválida")
    set_progress(request.jobId, 5, "Obtendo áudio do YouTube")
    with tempfile.TemporaryDirectory() as directory:
        try:
            source = await asyncio.to_thread(download_youtube, url, directory)
            set_progress(request.jobId, 12, "Áudio obtido")
            return await asyncio.to_thread(convert_and_analyze, str(source), str(Path(directory) / "normalized.wav"), request.jobId)
        except YouTubeDownloadError as exc:
            raise HTTPException(422, str(exc)) from exc
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as exc:
            raise HTTPException(422, "Não foi possível decodificar o áudio do vídeo") from exc
        except ValueError as exc:
            raise HTTPException(422, str(exc)) from exc
