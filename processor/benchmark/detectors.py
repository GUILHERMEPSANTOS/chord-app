import shutil
import subprocess
import tempfile
from pathlib import Path
from typing import Protocol

from benchmark.segments import Segment, read_lab


class ChordDetector(Protocol):
    name: str

    def predict(self, normalized_wav: Path) -> list[Segment]: ...


class LvChordiaDetector:
    name = "lv-chordia"

    def predict(self, normalized_wav: Path) -> list[Segment]:
        from app.chords import simplify
        from lv_chordia.chord_recognition import chord_recognition

        raw = chord_recognition(audio_path=str(normalized_wav), chord_dict_name="submission")
        return [
            Segment(float(item["start_time"]), float(item["end_time"]), to_harte(simplify(item["chord"])))
            for item in raw
            if float(item["end_time"]) > float(item["start_time"])
        ]


class BtcDetector:
    name = "btc-ismir19"

    def __init__(self, checkout: Path, python_executable: Path):
        self.checkout = checkout.resolve()
        self.python_executable = python_executable.resolve()
        if not (self.checkout / "btc_model.py").is_file():
            raise FileNotFoundError(f"BTC model code not found in {self.checkout}")
        if not (self.checkout / "test" / "btc_model_large_voca.pt").is_file():
            raise FileNotFoundError("BTC large-vocabulary checkpoint is missing")
        if not self.python_executable.is_file():
            raise FileNotFoundError(f"BTC Python executable not found: {self.python_executable}")

    def predict(self, normalized_wav: Path) -> list[Segment]:
        with tempfile.TemporaryDirectory(prefix="chord-btc-") as directory:
            root = Path(directory)
            audio_dir = root / "audio"
            output_dir = root / "output"
            audio_dir.mkdir()
            output_dir.mkdir()
            shutil.copy2(normalized_wav, audio_dir / "track.wav")
            command = [
                str(self.python_executable), str(Path(__file__).with_name("btc_infer.py")),
                "--checkout", str(self.checkout),
                "--audio", str(audio_dir / "track.wav"),
                "--output", str(output_dir / "track.lab"),
            ]
            subprocess.run(command, cwd=self.checkout, check=True, timeout=1800, capture_output=True, text=True)
            return read_lab(output_dir / "track.lab")


def to_harte(label: str) -> str:
    if label == "N":
        return label
    suffixes = (
        ("m7b5", "hdim7"), ("dim7", "dim7"), ("maj7", "maj7"),
        ("dim", "dim"), ("m7", "min7"), ("7", "7"), ("m", "min"),
    )
    for suffix, quality in suffixes:
        if label.endswith(suffix):
            return f"{label[:-len(suffix)]}:{quality}"
    return f"{label}:maj"
