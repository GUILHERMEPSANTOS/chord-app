import re

import mir_eval

from benchmark.segments import Segment


DIMINISHED = {"dim", "dim7", "hdim7"}
ROOTS = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def chord_identity(label: str) -> tuple[int, str] | None:
    match = re.fullmatch(r"([A-G])([#b]?)(?::([^/]+))?(?:/.*)?", label)
    if not match:
        return None
    root = (ROOTS[match.group(1)] + {"#": 1, "b": -1, "": 0}[match.group(2)]) % 12
    return root, match.group(3) or "maj"


def _label_at(segments: list[Segment], point: float) -> str:
    for segment in segments:
        if segment.start <= point < segment.end:
            return segment.label
    return "N"


def align(reference: list[Segment], prediction: list[Segment]) -> tuple[list[str], list[str], list[float]]:
    start, end = reference[0].start, reference[-1].end
    boundaries = {start, end}
    for segment in reference + prediction:
        boundaries.add(max(start, min(end, segment.start)))
        boundaries.add(max(start, min(end, segment.end)))
    times = sorted(boundaries)
    intervals = [(left, right) for left, right in zip(times, times[1:]) if right > left]
    return (
        [_label_at(reference, (left + right) / 2) for left, right in intervals],
        [_label_at(prediction, (left + right) / 2) for left, right in intervals],
        [right - left for left, right in intervals],
    )


def _duration_score(comparisons, durations: list[float]) -> dict:
    valid = [(float(score), duration) for score, duration in zip(comparisons, durations) if score >= 0]
    correct = sum(duration for score, duration in valid if score == 1)
    scored = sum(duration for _, duration in valid)
    return {"correct_seconds": correct, "scored_seconds": scored, "accuracy": correct / scored if scored else None}


def _changes(segments: list[Segment], start: float, end: float) -> list[float]:
    return [
        segment.start for index, segment in enumerate(segments)
        if index > 0 and start < segment.start < end and segment.label != segments[index - 1].label
    ]


def _transition_score(reference: list[Segment], prediction: list[Segment], tolerance: float) -> dict:
    start, end = reference[0].start, reference[-1].end
    actual = _changes(reference, start, end)
    estimated = _changes(prediction, start, end)
    pairs = sorted(
        (abs(ref - pred), ref_index, pred_index)
        for ref_index, ref in enumerate(actual)
        for pred_index, pred in enumerate(estimated)
        if abs(ref - pred) <= tolerance
    )
    matched_ref, matched_pred = set(), set()
    errors = []
    for error, ref_index, pred_index in pairs:
        if ref_index not in matched_ref and pred_index not in matched_pred:
            matched_ref.add(ref_index)
            matched_pred.add(pred_index)
            errors.append(error)
    true_positives = len(errors)
    precision = true_positives / len(estimated) if estimated else (1.0 if not actual else 0.0)
    recall = true_positives / len(actual) if actual else (1.0 if not estimated else 0.0)
    return {
        "reference_count": len(actual),
        "predicted_count": len(estimated),
        "matched_count": true_positives,
        "precision": precision,
        "recall": recall,
        "f1": 2 * precision * recall / (precision + recall) if precision + recall else 0.0,
        "mean_absolute_error_seconds": sum(errors) / len(errors) if errors else None,
        "tolerance_seconds": tolerance,
    }


def score(reference: list[Segment], prediction: list[Segment], tolerance: float = 0.5) -> dict:
    ref_labels, pred_labels, durations = align(reference, prediction)
    majmin = _duration_score(mir_eval.chord.majmin(ref_labels, pred_labels), durations)
    sevenths = _duration_score(mir_eval.chord.sevenths(ref_labels, pred_labels), durations)

    dim_reference = 0.0
    dim_predicted = 0.0
    dim_correct = 0.0
    for actual, estimated, duration in zip(ref_labels, pred_labels, durations):
        actual_id = chord_identity(actual)
        estimated_id = chord_identity(estimated)
        actual_dim = actual_id is not None and actual_id[1] in DIMINISHED
        estimated_dim = estimated_id is not None and estimated_id[1] in DIMINISHED
        if actual_dim:
            dim_reference += duration
        if estimated_dim:
            dim_predicted += duration
        if actual_dim and actual_id == estimated_id:
            dim_correct += duration

    return {
        "reference_seconds": reference[-1].end - reference[0].start,
        "majmin": majmin,
        "sevenths": sevenths,
        "diminished": {
            "reference_seconds": dim_reference,
            "predicted_seconds": dim_predicted,
            "correct_seconds": dim_correct,
            "recall": dim_correct / dim_reference if dim_reference else None,
            "precision": dim_correct / dim_predicted if dim_predicted else None,
        },
        "transitions": _transition_score(reference, prediction, tolerance),
    }
