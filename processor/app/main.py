import asyncio
from contextlib import redirect_stderr
import logging
import os
import re
import subprocess
import sys
import tempfile
import threading
import time
from pathlib import Path
from urllib.parse import parse_qs, urlparse

import librosa
import numpy as np
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from pydantic import BaseModel
from lv_chordia.chord_recognition import chord_recognition

app = FastAPI(title="ChordApp Processor")
_progress: dict[str, tuple[int, str, float]] = {}
_model_lock = threading.Lock()


def set_progress(job_id: str | None, percent: int, stage: str) -> None:
    if job_id:
        _progress[job_id] = (percent, stage, time.monotonic())


class ProgressStderr:
    def __init__(self, original, job_id: str | None, dual: bool = False):
        self.original = original
        self.job_id = job_id
        self.dual = dual
        self.model_count = 0

    def write(self, value: str) -> int:
        if value.startswith("Inference:"):
            self.model_count += 1
            percent = 20 + self.model_count * (7 if self.dual else 12)
            set_progress(self.job_id, min(55 if self.dual else 80, percent), f"lv-chordia ({self.model_count}/5)")
        return self.original.write(value)

    def flush(self) -> None:
        self.original.flush()


class YouTubeRequest(BaseModel):
    url: str
    jobId: str | None = None


def canonical_youtube_url(value: str) -> str | None:
    try:
        parsed = urlparse(value)
        valid_port = parsed.port in (None, 443)
    except ValueError:
        return None
    if parsed.scheme != "https" or not valid_port or parsed.username or parsed.password:
        return None
    host = (parsed.hostname or "").lower()
    if host in ("youtu.be", "www.youtu.be"):
        video_id = parsed.path.strip("/")
    elif host in ("youtube.com", "www.youtube.com", "m.youtube.com"):
        video_id = parse_qs(parsed.query).get("v", [None])[0] if parsed.path == "/watch" else parsed.path.removeprefix("/shorts/").strip("/") if parsed.path.startswith("/shorts/") else None
    else:
        return None
    return f"https://www.youtube.com/watch?v={video_id}" if video_id and re.fullmatch(r"[A-Za-z0-9_-]{11}", video_id) else None


def youtube_download_error(stderr: str) -> str:
    message = stderr.lower()
    if "http error 429" in message or "sign in to confirm you" in message or "not a bot" in message:
        return "O YouTube exigiu verificação para obter este áudio. Configure cookies locais do YouTube ou envie um arquivo MP3/WAV."
    if "duration" in message and ("filter" in message or "900" in message):
        return "O vídeo ultrapassa o limite de 15 minutos."
    if "max-filesize" in message or "larger than max-filesize" in message:
        return "O áudio ultrapassa o limite de 30 MB."
    return "Não foi possível obter o áudio deste vídeo. Verifique se ele está público e disponível."


def youtube_cookie_args() -> list[str]:
    path = os.environ.get("YOUTUBE_COOKIES_FILE")
    if not path:
        return []
    if not Path(path).is_file():
        raise ValueError("Arquivo de cookies do YouTube configurado, mas não encontrado no processador.")
    return ["--cookies", path]
NOTES = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
ENHARMONIC = {"Db": "C#", "Eb": "D#", "Gb": "F#", "Ab": "G#", "Bb": "A#", "Cb": "B", "B#": "C", "Fb": "E", "E#": "F"}
MAJOR = np.array([6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88])
MINOR = np.array([6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17])


def simplify(label: str) -> str:
    if label == "N":
        return "N"
    root, _, quality = label.partition(":")
    root = ENHARMONIC.get(root, root)
    if root not in NOTES:
        return "N"
    quality = quality.split("/", 1)[0]
    if not quality:
        return root
    if quality == "hdim7":
        return root + "m7b5"
    if quality == "dim7":
        return root + "dim7"
    if quality == "dim":
        return root + "dim"
    if quality == "maj7":
        return root + "maj7"
    if quality == "min7":
        return root + "m7"
    if quality == "7":
        return root + "7"
    if quality.startswith("min"):
        return root + "m"
    if quality.startswith("maj"):
        return root
    return "N"


def estimate_key(path: str) -> str | None:
    audio, sample_rate = librosa.load(path, sr=22050, mono=True)
    chroma = librosa.feature.chroma_stft(y=audio, sr=sample_rate).mean(axis=1)
    if not np.isfinite(chroma).all() or np.max(chroma) < 1e-6:
        return None
    scores = [(float(np.corrcoef(chroma, np.roll(template, i))[0, 1]), NOTES[i] + suffix)
              for template, suffix in ((MAJOR, ""), (MINOR, "m")) for i in range(12)]
    return max(scores)[1]


def analyze(path: str, job_id: str | None = None, dual: bool = False) -> dict:
    with _model_lock, redirect_stderr(ProgressStderr(sys.stderr, job_id, dual)):
        raw = chord_recognition(audio_path=path, chord_dict_name="submission")
    set_progress(job_id, 57 if dual else 90, "Organizando segmentos lv-chordia")
    chords = []
    for item in raw:
        start, end = float(item["start_time"]), float(item["end_time"])
        if end > start:
            chords.append({"startTime": start, "endTime": end, "chord": simplify(item["chord"]), "confidence": None})
    key = estimate_key(path)
    set_progress(job_id, 60 if dual else 96, "Estimando o tom")
    result = {"key": key, "durationSeconds": float(librosa.get_duration(path=path)), "chords": chords}
    if not dual:
        set_progress(job_id, 100, "Concluído")
    return result


def analyze_both(path: str, job_id: str | None = None) -> dict:
    result = analyze(path, job_id, dual=True)
    result["results"] = {"lv-chordia": result["chords"]}
    result["modelErrors"] = {}
    set_progress(job_id, 65, "Iniciando BTC-ISMIR19")
    try:
        from benchmark.detectors import BtcDetector
        detector = BtcDetector(Path(os.environ.get("BTC_CHECKOUT", "/opt/btc")), Path(sys.executable))
        segments = detector.predict(Path(path))
        result["results"]["btc-ismir19"] = [
            {"startTime": segment.start, "endTime": segment.end,
             "chord": simplify(segment.label), "confidence": None}
            for segment in segments if segment.end > segment.start
        ]
        set_progress(job_id, 96, "Organizando segmentos BTC-ISMIR19")
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        logging.exception("BTC-ISMIR19 failed for job %s", job_id)
        result["modelErrors"]["btc-ismir19"] = str(exc)[:500]
    set_progress(job_id, 100, "Concluído")
    return result


def convert_and_analyze(source: str, target: str, job_id: str | None = None) -> dict:
    subprocess.run(["ffmpeg", "-v", "error", "-nostdin", "-y", "-i", source, "-ac", "1", "-ar", "22050", target], check=True, timeout=90, capture_output=True)
    set_progress(job_id, 18, "Áudio preparado")
    result = analyze_both(target, job_id)
    if result["durationSeconds"] < 1 or result["durationSeconds"] > 900:
        raise ValueError("Duração fora do limite")
    return result


@app.get("/health")
def health():
    return {"status": "ok"}


@app.get("/progress/{job_id}")
def progress(job_id: str):
    value = _progress.get(job_id)
    if value is None or time.monotonic() - value[2] > 86400:
        _progress.pop(job_id, None)
        raise HTTPException(404, "Progresso indisponível")
    return {"percent": value[0], "stage": value[1]}


@app.post("/analyze")
async def analyze_upload(file: UploadFile = File(...), job_id: str | None = Form(None)):
    set_progress(job_id, 5, "Recebendo áudio")
    suffix = Path(file.filename or "").suffix.lower()
    if suffix not in (".mp3", ".wav"):
        raise HTTPException(400, "Formato inválido")
    with tempfile.TemporaryDirectory() as directory:
        source = os.path.join(directory, "input" + suffix)
        target = os.path.join(directory, "normalized.wav")
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
    url = canonical_youtube_url(request.url)
    if url is None:
        raise HTTPException(400, "URL do YouTube inválida")
    set_progress(request.jobId, 5, "Obtendo áudio do YouTube")
    with tempfile.TemporaryDirectory() as directory:
        try:
            cookie_args = youtube_cookie_args()
        except ValueError as exc:
            raise HTTPException(422, str(exc)) from exc
        command = [sys.executable, "-m", "yt_dlp", "--no-playlist", "--match-filter", "duration <= 900", "--max-filesize", "30M", "--js-runtimes", "node", *cookie_args, "-f", "bestaudio", "-o", os.path.join(directory, "source.%(ext)s"), url]
        try:
            await asyncio.to_thread(subprocess.run, command, check=True, timeout=180, capture_output=True)
            sources = [p for p in Path(directory).glob("source.*") if p.is_file() and p.suffix != ".part"]
            if len(sources) != 1 or sources[0].stat().st_size > 30 * 1024 * 1024:
                raise HTTPException(422, "Arquivo indisponível ou maior que 30 MB")
            set_progress(request.jobId, 12, "Áudio obtido")
            return await asyncio.to_thread(convert_and_analyze, str(sources[0]), os.path.join(directory, "normalized.wav"), request.jobId)
        except subprocess.CalledProcessError as exc:
            stderr = exc.stderr.decode(errors="replace") if isinstance(exc.stderr, bytes) else exc.stderr or ""
            raise HTTPException(422, youtube_download_error(stderr)) from exc
        except subprocess.TimeoutExpired as exc:
            raise HTTPException(422, "A obtenção do áudio demorou demais; tente novamente.") from exc
        except ValueError as exc:
            raise HTTPException(422, str(exc)) from exc
