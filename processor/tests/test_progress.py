import sys
import unittest
from unittest.mock import patch

from app import main


class ProgressTests(unittest.TestCase):
    def test_youtube_bot_error_is_explained_without_exposing_diagnostics(self):
        diagnostic = "HTTP Error 429: Too Many Requests; Sign in to confirm you are not a bot; token=secret"
        message = main.youtube_download_error(diagnostic)
        self.assertIn("verificação", message)
        self.assertNotIn("secret", message)

    def test_dual_analysis_keeps_results_separate(self):
        from benchmark.segments import Segment

        with patch.object(main, "analyze", return_value={"key": "C", "durationSeconds": 2.0, "chords": [{"startTime": 0, "endTime": 2, "chord": "C", "confidence": None}]}), \
             patch("benchmark.detectors.BtcDetector") as detector:
            detector.return_value.predict.return_value = [Segment(0, 2, "G:7")]
            result = main.analyze_both("unused.wav", "dual-job")

        self.assertEqual("C", result["results"]["lv-chordia"][0]["chord"])
        self.assertEqual("G7", result["results"]["btc-ismir19"][0]["chord"])
        self.assertEqual(100, main.progress("dual-job")["percent"])

    def test_model_passages_advance_progress(self):
        observed = []

        def fake_recognition(**_):
            for index in range(5):
                print(f"Inference: test model {index}", file=sys.stderr)
                observed.append(main.progress("test-job")["percent"])
            return [{"start_time": 0, "end_time": 2, "chord": "C:maj"}]

        with patch.object(main, "chord_recognition", side_effect=fake_recognition), \
             patch.object(main, "estimate_key", return_value="C"), \
             patch.object(main.librosa, "get_duration", return_value=2):
            result = main.analyze("unused.wav", "test-job")

        self.assertEqual([32, 44, 56, 68, 80], observed)
        self.assertEqual(100, main.progress("test-job")["percent"])
        self.assertEqual("C", result["chords"][0]["chord"])

    def test_seventh_labels_are_preserved(self):
        self.assertEqual("C", main.simplify("C"))
        self.assertEqual("C7", main.simplify("C:7"))
        self.assertEqual("F#maj7", main.simplify("F#:maj7"))
        self.assertEqual("A#m7", main.simplify("Bb:min7"))
        self.assertEqual("Em7", main.simplify("E:min7/B"))
        self.assertEqual("Cdim", main.simplify("C:dim"))
        self.assertEqual("Cdim7", main.simplify("C:dim7"))
        self.assertEqual("Cm7b5", main.simplify("C:hdim7"))

    def test_analysis_uses_extended_vocabulary(self):
        with patch.object(main, "chord_recognition", return_value=[{"start_time": 0, "end_time": 2, "chord": "C:7"}]) as recognize, \
             patch.object(main, "estimate_key", return_value="C"), \
             patch.object(main.librosa, "get_duration", return_value=2):
            result = main.analyze("unused.wav")
        recognize.assert_called_once_with(audio_path="unused.wav", chord_dict_name="submission")
        self.assertEqual("C7", result["chords"][0]["chord"])


if __name__ == "__main__":
    unittest.main()
