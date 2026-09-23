"""Orquestra os dois detectores e monta a resposta que a API .NET persiste."""

from contextlib import redirect_stderr
import logging
import os
from pathlib import Path
import subprocess
import sys
import threading

import librosa
from lv_chordia.chord_recognition import chord_recognition

from app.audio import estimate_key, normalize
from app.chords import simplify
from app.progress import ProgressStderr, set_progress


_model_lock = threading.Lock()


def analyze(path: str, job_id: str | None = None, dual: bool = False) -> dict:
    """Executa o lv-chordia e estima o tom global do mesmo WAV normalizado."""
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
    """Mantém previsões independentes; falha do BTC preserva a saída do lv-chordia."""
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
    """Normaliza uma vez, analisa com os dois modelos e valida a duração final."""
    normalize(source, target)
    set_progress(job_id, 18, "Áudio preparado")
    result = analyze_both(target, job_id)
    if result["durationSeconds"] < 1 or result["durationSeconds"] > 900:
        raise ValueError("Duração fora do limite")
    return result
