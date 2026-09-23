"""Isolated CPU inference for the upstream BTC large-vocabulary checkpoint."""

import argparse
import inspect
import sys
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--checkout", type=Path, required=True)
    parser.add_argument("--audio", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    sys.path.insert(0, str(args.checkout))
    import numpy as np
    import torch
    import yaml
    import librosa
    # Upstream BTC uses the removed NumPy alias in transformer_modules.py.
    np.float = float
    from btc_model import BTC_model
    from utils.hparams import HParams
    from utils.mir_eval_modules import idx2voca_chord

    config = HParams(**yaml.safe_load((args.checkout / "run_config.yaml").read_text(encoding="utf-8")))
    config.feature["large_voca"] = True
    config.model["num_chords"] = 170
    checkpoint_path = args.checkout / "test" / "btc_model_large_voca.pt"
    load_options = {"map_location": "cpu"}
    if "weights_only" in inspect.signature(torch.load).parameters:
        load_options["weights_only"] = False
    checkpoint = torch.load(checkpoint_path, **load_options)
    model = BTC_model(config=config.model).to("cpu")
    model.load_state_dict(checkpoint["model"])
    model.eval()

    audio, sample_rate = librosa.load(str(args.audio), sr=config.mp3["song_hz"], mono=True)
    chunk_samples = int(sample_rate * config.mp3["inst_len"])
    chunks = []
    position = 0
    while len(audio) > position + chunk_samples:
        chunks.append(librosa.cqt(
            audio[position:position + chunk_samples], sr=sample_rate,
            n_bins=config.feature["n_bins"],
            bins_per_octave=config.feature["bins_per_octave"],
            hop_length=config.feature["hop_length"],
        ))
        position += chunk_samples
    chunks.append(librosa.cqt(
        audio[position:], sr=sample_rate,
        n_bins=config.feature["n_bins"],
        bins_per_octave=config.feature["bins_per_octave"],
        hop_length=config.feature["hop_length"],
    ))
    feature = np.log(np.abs(np.concatenate(chunks, axis=1)) + 1e-6)
    seconds_per_step = config.mp3["inst_len"] / config.model["timestep"]
    audio_duration = len(audio) / sample_rate
    feature = (feature.T - checkpoint["mean"]) / checkpoint["std"]
    timestep = config.model["timestep"]
    padding = (-feature.shape[0]) % timestep
    if padding:
        feature = np.pad(feature, ((0, padding), (0, 0)), mode="constant")
    tensor = torch.tensor(feature, dtype=torch.float32).unsqueeze(0)
    labels = idx2voca_chord()
    indices = []
    with torch.no_grad():
        for offset in range(0, tensor.shape[1], timestep):
            encoded, _ = model.self_attn_layers(tensor[:, offset:offset + timestep, :])
            predicted, _ = model.output_layer(encoded)
            indices.extend(int(index) for index in predicted.reshape(-1).tolist())
    if padding:
        indices = indices[:-padding]
    if not indices:
        raise ValueError("BTC returned no chord frames")

    lines = []
    current = indices[0]
    start = 0.0
    for index, label in enumerate(indices[1:], 1):
        if label != current:
            end = min(audio_duration, index * seconds_per_step)
            if end > start:
                lines.append(f"{start:.6f} {end:.6f} {labels[current]}\n")
            start = end
            current = label
    if audio_duration > start:
        lines.append(f"{start:.6f} {audio_duration:.6f} {labels[current]}\n")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text("".join(lines), encoding="utf-8")


if __name__ == "__main__":
    main()
