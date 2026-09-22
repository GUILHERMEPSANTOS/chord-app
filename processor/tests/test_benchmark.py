import tempfile
import unittest
from pathlib import Path

from benchmark.detectors import to_harte
from benchmark.metrics import score
from benchmark.segments import Segment, read_lab, write_lab


class BenchmarkTests(unittest.TestCase):
    def test_duration_weighting_and_transitions(self):
        reference = [Segment(0, 3, "C:maj"), Segment(3, 4, "A:min")]
        prediction = [Segment(0, 2, "C:maj"), Segment(2, 4, "A:min")]

        result = score(reference, prediction, tolerance=0.5)

        self.assertAlmostEqual(0.75, result["majmin"]["accuracy"])
        self.assertEqual(0, result["transitions"]["matched_count"])
        self.assertEqual(1, result["transitions"]["reference_count"])

    def test_diminished_uses_reference_duration(self):
        reference = [Segment(0, 2, "B:hdim7"), Segment(2, 4, "C:dim")]
        prediction = [Segment(0, 2, "B:hdim7"), Segment(2, 4, "C:min")]

        result = score(reference, prediction)

        self.assertEqual(4, result["diminished"]["reference_seconds"])
        self.assertEqual(0.5, result["diminished"]["recall"])

    def test_unobserved_diminished_is_unavailable(self):
        result = score([Segment(0, 2, "C:maj")], [Segment(0, 2, "C:maj")])
        self.assertIsNone(result["diminished"]["recall"])

    def test_prediction_labels_follow_production_mapping(self):
        self.assertEqual("B:hdim7", to_harte("Bm7b5"))
        self.assertEqual("C:maj7", to_harte("Cmaj7"))
        self.assertEqual("A:min", to_harte("Am"))

    def test_lab_round_trip_and_overlap_rejection(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "track.lab"
            expected = [Segment(0, 1, "C:maj"), Segment(1, 2, "A:min")]
            write_lab(path, expected)
            self.assertEqual(expected, read_lab(path))
            path.write_text("0 2 C:maj\n1 3 A:min\n", encoding="utf-8")
            with self.assertRaises(ValueError):
                read_lab(path)


if __name__ == "__main__":
    unittest.main()
