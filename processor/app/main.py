import asyncio
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from urllib.parse import parse_qs, urlparse

import librosa
import numpy as np
from fastapi import FastAPI, File, HTTPException, UploadFile
from pydantic import BaseModel
from lv_chordia.chord_recognition import chord_recognition

app = FastAPI(title="ChordApp Processor")


class YouTubeRequest(BaseModel):
    url: str


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
    if quality.startswith("min"):
        return root + "m"
    if quality.startswith("maj") or quality == "7":
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


def analyze(path: str) -> dict:
    raw = chord_recognition(audio_path=path, chord_dict_name="ismir2017")
    chords = []
    for item in raw:
        start, end = float(item["start_time"]), float(item["end_time"])
        if end > start:
            chords.append({"startTime": start, "endTime": end, "chord": simplify(item["chord"]), "confidence": None})
    return {"key": estimate_key(path), "durationSeconds": float(librosa.get_duration(path=path)), "chords": chords}


def convert_and_analyze(source: str, target: str) -> dict:
    subprocess.run(["ffmpeg", "-v", "error", "-nostdin", "-y", "-i", source, "-ac", "1", "-ar", "22050", target], check=True, timeout=90, capture_output=True)
    result = analyze(target)
    if result["durationSeconds"] < 1 or result["durationSeconds"] > 900:
        raise ValueError("Duração fora do limite")
    return result


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/analyze")
async def analyze_upload(file: UploadFile = File(...)):
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
            return await asyncio.to_thread(convert_and_analyze, source, target)
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as exc:
            raise HTTPException(422, "Não foi possível decodificar o áudio") from exc


@app.post("/analyze-youtube")
async def analyze_youtube(request: YouTubeRequest):
    url = canonical_youtube_url(request.url)
    if url is None:
        raise HTTPException(400, "URL do YouTube inválida")
    with tempfile.TemporaryDirectory() as directory:
        command = [sys.executable, "-m", "yt_dlp", "--no-playlist", "--match-filter", "duration <= 900", "--max-filesize", "30M", "--js-runtimes", "node", "-f", "bestaudio", "-o", os.path.join(directory, "source.%(ext)s"), url]
        try:
            await asyncio.to_thread(subprocess.run, command, check=True, timeout=180, capture_output=True)
            sources = [p for p in Path(directory).glob("source.*") if p.is_file() and p.suffix != ".part"]
            if len(sources) != 1 or sources[0].stat().st_size > 30 * 1024 * 1024:
                raise HTTPException(422, "Arquivo indisponível ou maior que 30 MB")
            return await asyncio.to_thread(convert_and_analyze, str(sources[0]), os.path.join(directory, "normalized.wav"))
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as exc:
            raise HTTPException(422, "Não foi possível obter o áudio deste vídeo") from exc
        except ValueError as exc:
            raise HTTPException(422, str(exc)) from exc
