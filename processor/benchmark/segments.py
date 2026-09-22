from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Segment:
    start: float
    end: float
    label: str


def read_lab(path: Path) -> list[Segment]:
    segments = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        parts = line.split(maxsplit=2)
        if len(parts) != 3:
            raise ValueError(f"{path}:{line_number}: expected start end chord")
        start, end = float(parts[0]), float(parts[1])
        if not 0 <= start < end or (segments and start < segments[-1].end - 1e-6):
            raise ValueError(f"{path}:{line_number}: invalid or overlapping interval")
        segments.append(Segment(start, end, parts[2].strip()))
    if not segments:
        raise ValueError(f"{path}: no chord segments")
    return segments


def write_lab(path: Path, segments: list[Segment]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        "".join(f"{segment.start:.6f} {segment.end:.6f} {segment.label}\n" for segment in segments),
        encoding="utf-8",
    )
