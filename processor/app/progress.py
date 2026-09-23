"""Armazena o progresso temporário dos trabalhos e acompanha as cinco passagens do lv-chordia."""

import time


_progress: dict[str, tuple[int, str, float]] = {}


def set_progress(job_id: str | None, percent: int, stage: str) -> None:
    """Atualiza a porcentagem consultada pela API .NET; não grava dados no banco."""
    if job_id:
        _progress[job_id] = (percent, stage, time.monotonic())


def get_progress(job_id: str) -> dict | None:
    """Retorna o progresso recente ou None quando o trabalho não existe/expirou."""
    value = _progress.get(job_id)
    if value is None or time.monotonic() - value[2] > 86400:
        _progress.pop(job_id, None)
        return None
    return {"percent": value[0], "stage": value[1]}


class ProgressStderr:
    """Repassa stderr e transforma as mensagens de inferência do lv-chordia em progresso."""

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
