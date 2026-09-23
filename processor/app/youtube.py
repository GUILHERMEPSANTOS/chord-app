"""Valida URLs do YouTube e baixa somente o áudio para um diretório temporário."""

import os
import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import parse_qs, urlparse


class YouTubeDownloadError(Exception):
    """Erro seguro para exibir ao usuário quando o áudio não pode ser obtido."""


def canonical_youtube_url(value: str) -> str | None:
    """Aceita apenas links HTTPS de vídeo dos domínios conhecidos do YouTube."""
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
        if parsed.path == "/watch":
            video_id = parse_qs(parsed.query).get("v", [None])[0]
        elif parsed.path.startswith("/shorts/"):
            video_id = parsed.path.removeprefix("/shorts/").strip("/")
        else:
            video_id = None
    else:
        return None
    return f"https://www.youtube.com/watch?v={video_id}" if video_id and re.fullmatch(r"[A-Za-z0-9_-]{11}", video_id) else None


def youtube_download_error(stderr: str) -> str:
    """Traduz diagnósticos do yt-dlp sem expor detalhes da sessão ou cookies."""
    message = stderr.lower()
    if "http error 429" in message or "sign in to confirm you" in message or "not a bot" in message:
        return "O YouTube exigiu verificação para obter este áudio. Configure cookies locais do YouTube ou envie um arquivo MP3/WAV."
    if "duration" in message and ("filter" in message or "900" in message):
        return "O vídeo ultrapassa o limite de 15 minutos."
    if "max-filesize" in message or "larger than max-filesize" in message:
        return "O áudio ultrapassa o limite de 30 MB."
    return "Não foi possível obter o áudio deste vídeo. Verifique se ele está público e disponível."


def youtube_cookie_args() -> list[str]:
    """Inclui um arquivo de cookies montado localmente, quando configurado."""
    path = os.environ.get("YOUTUBE_COOKIES_FILE")
    if not path:
        return []
    if not Path(path).is_file():
        raise YouTubeDownloadError("Arquivo de cookies do YouTube configurado, mas não encontrado no processador.")
    return ["--cookies", path]


def download_youtube(url: str, directory: str) -> Path:
    """Baixa um vídeo público limitado a 15 minutos/30 MB e retorna o arquivo temporário."""
    command = [
        sys.executable, "-m", "yt_dlp", "--no-playlist", "--match-filter", "duration <= 900",
        "--max-filesize", "30M", "--js-runtimes", "node", *youtube_cookie_args(),
        "-f", "bestaudio", "-o", os.path.join(directory, "source.%(ext)s"), url,
    ]
    try:
        subprocess.run(command, check=True, timeout=180, capture_output=True)
    except subprocess.CalledProcessError as exc:
        stderr = exc.stderr.decode(errors="replace") if isinstance(exc.stderr, bytes) else exc.stderr or ""
        raise YouTubeDownloadError(youtube_download_error(stderr)) from exc
    except subprocess.TimeoutExpired as exc:
        raise YouTubeDownloadError("A obtenção do áudio demorou demais; tente novamente.") from exc

    sources = [path for path in Path(directory).glob("source.*") if path.is_file() and path.suffix != ".part"]
    if len(sources) != 1 or sources[0].stat().st_size > 30 * 1024 * 1024:
        raise YouTubeDownloadError("Arquivo indisponível ou maior que 30 MB")
    return sources[0]
