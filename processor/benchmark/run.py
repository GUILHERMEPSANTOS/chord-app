import argparse
import hashlib
import json
import os
import platform
import subprocess
import tempfile
import threading
import time
from importlib.metadata import version
from pathlib import Path

import psutil

from benchmark.detectors import BtcDetector, LvChordiaDetector
from benchmark.metrics import score
from benchmark.segments import read_lab, write_lab


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def measure(action):
    process = psutil.Process()
    stop = threading.Event()
    peak = 0

    def sample():
        nonlocal peak
        while not stop.is_set():
            processes = [process]
            try:
                processes.extend(process.children(recursive=True))
            except psutil.Error:
                pass
            current = 0
            for item in processes:
                try:
                    current += item.memory_info().rss
                except psutil.Error:
                    pass
            peak = max(peak, current)
            stop.wait(0.05)

    sampler = threading.Thread(target=sample, daemon=True)
    sampler.start()
    started = time.perf_counter()
    try:
        result = action()
        return result, time.perf_counter() - started, peak
    finally:
        stop.set()
        sampler.join()


def normalize(audio: Path, target: Path) -> None:
    subprocess.run(
        ["ffmpeg", "-v", "error", "-nostdin", "-y", "-i", str(audio), "-ac", "1", "-ar", "22050", str(target)],
        check=True,
        timeout=90,
        capture_output=True,
    )


def pooled(rows: list[dict], detector_name: str) -> dict:
    results = [row["detectors"][detector_name]["metrics"] for row in rows]

    def duration_accuracy(name: str) -> dict:
        correct = sum(item[name]["correct_seconds"] for item in results)
        duration = sum(item[name]["scored_seconds"] for item in results)
        return {"correct_seconds": correct, "scored_seconds": duration, "accuracy": correct / duration if duration else None}

    diminished_reference = sum(item["diminished"]["reference_seconds"] for item in results)
    diminished_correct = sum(item["diminished"]["correct_seconds"] for item in results)
    matches = sum(item["transitions"]["matched_count"] for item in results)
    actual = sum(item["transitions"]["reference_count"] for item in results)
    predicted = sum(item["transitions"]["predicted_count"] for item in results)
    precision = matches / predicted if predicted else None
    recall = matches / actual if actual else None

    return {
        "tracks": len(results),
        "majmin": duration_accuracy("majmin"),
        "sevenths": duration_accuracy("sevenths"),
        "diminished": {
            "correct_seconds": diminished_correct,
            "reference_seconds": diminished_reference,
            "recall": diminished_correct / diminished_reference if diminished_reference else None,
        },
        "transitions": {
            "matched_count": matches,
            "reference_count": actual,
            "predicted_count": predicted,
            "precision": precision,
            "recall": recall,
            "f1": 2 * precision * recall / (precision + recall) if precision is not None and recall is not None and precision + recall else None,
        },
        "inference_seconds": sum(row["detectors"][detector_name]["inference_seconds"] for row in rows),
        "peak_rss_bytes_max": max(row["detectors"][detector_name]["peak_rss_bytes"] for row in rows),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Evaluate chord detectors against full-track .lab annotations")
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--detector", choices=("lv-chordia", "btc-ismir19"), action="append", required=True)
    parser.add_argument("--btc-checkout", type=Path)
    parser.add_argument("--btc-python", type=Path)
    parser.add_argument("--transition-tolerance", type=float, default=0.5)
    args = parser.parse_args()

    if args.transition_tolerance <= 0:
        parser.error("transition tolerance must be positive")
    names = list(dict.fromkeys(args.detector))
    detectors = []
    btc_provenance = None
    for name in names:
        if name == "lv-chordia":
            detectors.append(LvChordiaDetector())
        else:
            if args.btc_checkout is None or args.btc_python is None:
                parser.error("BTC requires --btc-checkout and --btc-python")
            btc = BtcDetector(args.btc_checkout, args.btc_python)
            detectors.append(btc)
            revision = subprocess.run(
                ["git", "rev-parse", "HEAD"], cwd=btc.checkout,
                capture_output=True, text=True, check=False,
            )
            btc_provenance = {
                "checkout_revision": revision.stdout.strip() if revision.returncode == 0 else None,
                "large_vocabulary_checkpoint_sha256": sha256(btc.checkout / "test" / "btc_model_large_voca.pt"),
            }

    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    tracks = manifest.get("tracks", [])
    if not tracks:
        parser.error("manifest must contain at least one annotated track")
    root = args.manifest.parent.resolve()
    args.output.mkdir(parents=True, exist_ok=True)
    rows = []

    for track in tracks:
        track_id = track["id"]
        if not track_id or not all(char.isalnum() or char in "-_" for char in track_id):
            raise ValueError(f"unsafe track id: {track_id}")
        audio = (root / track["audio"]).resolve()
        annotation = (root / track["annotation"]).resolve()
        if not audio.is_file() or not annotation.is_file():
            raise FileNotFoundError(f"missing audio or annotation for {track_id}")
        reference = read_lab(annotation)
        row = {
            "id": track_id,
            "source": track["source"],
            "license": track["license"],
            "audio_sha256": sha256(audio),
            "annotation_sha256": sha256(annotation),
            "detectors": {},
        }

        with tempfile.TemporaryDirectory(prefix="chord-eval-") as directory:
            normalized = Path(directory) / "normalized.wav"
            _, normalize_seconds, normalize_peak = measure(lambda: normalize(audio, normalized))
            row["normalization_seconds"] = normalize_seconds
            row["normalization_peak_rss_bytes"] = normalize_peak

            for detector in detectors:
                prediction, elapsed, peak = measure(lambda: detector.predict(normalized))
                prediction_path = args.output / "predictions" / f"{track_id}.{detector.name}.lab"
                write_lab(prediction_path, prediction)
                row["detectors"][detector.name] = {
                    "inference_seconds": elapsed,
                    "processing_seconds": normalize_seconds + elapsed,
                    "peak_rss_bytes": peak,
                    "prediction": str(prediction_path),
                    "metrics": score(reference, prediction, args.transition_tolerance),
                }
        rows.append(row)
        print(f"Evaluated {track_id}: {', '.join(names)}", flush=True)

    report = {
        "schema_version": 1,
        "environment": {
            "platform": platform.platform(),
            "python": platform.python_version(),
            "logical_cpus": os.cpu_count(),
            "lv_chordia": version("lv-chordia") if "lv-chordia" in names else None,
            "mir_eval": version("mir_eval"),
            "btc": btc_provenance,
        },
        "detector_order": names,
        "tracks": rows,
        "summary": {name: pooled(rows, name) for name in names},
    }
    (args.output / "report.json").write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Report: {args.output / 'report.json'}")


if __name__ == "__main__":
    main()
