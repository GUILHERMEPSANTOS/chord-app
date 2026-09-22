import sys
import unittest
from unittest.mock import patch

from app import main


class ProgressTests(unittest.TestCase):
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


if __name__ == "__main__":
    unittest.main()
