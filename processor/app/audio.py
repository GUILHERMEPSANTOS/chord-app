"""Normaliza áudio com FFmpeg e estima o tom a partir de cromagramas."""

import subprocess

import librosa
import numpy as np

from app.chords import NOTES


MAJOR = np.array([6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88])
MINOR = np.array([6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17])


def normalize(source: str, target: str) -> None:
    """Converte o arquivo temporário para WAV mono a 22.050 Hz, usado pelos dois modelos."""
    subprocess.run(
        ["ffmpeg", "-v", "error", "-nostdin", "-y", "-i", source, "-ac", "1", "-ar", "22050", target],
        check=True, timeout=90, capture_output=True,
    )


def estimate_key(path: str) -> str | None:
    """Estima um único tom global; não representa modulações durante a música."""
    audio, sample_rate = librosa.load(path, sr=22050, mono=True)
    chroma = librosa.feature.chroma_stft(y=audio, sr=sample_rate).mean(axis=1)
    if not np.isfinite(chroma).all() or np.max(chroma) < 1e-6:
        return None
    scores = [(float(np.corrcoef(chroma, np.roll(template, i))[0, 1]), NOTES[i] + suffix)
              for template, suffix in ((MAJOR, ""), (MINOR, "m")) for i in range(12)]
    return max(scores)[1]
