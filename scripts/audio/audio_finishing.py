"""Offline, recipe-driven finishing of authored audio material.

analyze INPUT --out analysis.json
render RECIPE --out cooked
Outputs stay in repository .artifacts or an explicitly selected workspace.
Rendering is deterministic
for the same sources, recipe, NumPy and FFmpeg versions. Numerical reports describe
signal properties; they do not select an aesthetically suitable performance.
"""

from __future__ import annotations

import argparse
import copy
from functools import lru_cache
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess
import sys
import wave

import numpy as np


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_WORKSPACE = ROOT / ".artifacts" / "audio-finishing"
RATE = 44_100
AUDIO_EXTENSIONS = {".wav", ".flac", ".ogg", ".mp3", ".m4a", ".aiff", ".aif"}


def output_path(value: str | Path, workspace: Path | None = None) -> Path:
    workspace = (workspace or DEFAULT_WORKSPACE).resolve()
    path = Path(value)
    path = (workspace / path).resolve() if not path.is_absolute() else path.resolve()
    if not path.is_relative_to(workspace):
        raise ValueError("Authoring outputs must remain inside the selected workspace.")
    return path


def execute(command: list[str], data: bytes | None = None) -> bytes:
    process = subprocess.run(command, input=data, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if process.returncode:
        # Commands contain only media paths and numeric DSP settings, never API credentials.
        raise ValueError(process.stderr.decode("utf-8", errors="replace")[-2500:])
    return process.stdout


def filter_audio(signal: np.ndarray, expression: str) -> np.ndarray:
    raw = execute([
        "ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(RATE), "-ac", "1", "-i", "pipe:0",
        "-af", expression, "-f", "f32le", "-ac", "1", "-ar", str(RATE), "pipe:1",
    ], signal.astype("<f4").tobytes())
    return np.frombuffer(raw, dtype="<f4").astype(np.float64)


def decode(path: Path, mono: str = "auto") -> tuple[np.ndarray, dict]:
    path = path.resolve()
    if path.suffix.lower() not in AUDIO_EXTENSIONS:
        raise ValueError("Only explicit audio files can be opened as sources.")
    state = path.stat()
    signal, report = _decode_cached(str(path), state.st_mtime_ns, state.st_size, mono)
    # Layer envelopes and gains modify their arrays; callers never receive cached storage.
    return signal.copy(), copy.deepcopy(report)


@lru_cache(maxsize=96)
def _decode_cached(filename: str, modified_ns: int, byte_count: int, mono: str) -> tuple[np.ndarray, dict]:
    path = Path(filename)
    if path.suffix.lower() not in AUDIO_EXTENSIONS:
        raise ValueError("Only explicit audio files can be opened as sources.")
    probe = json.loads(execute([
        "ffprobe", "-v", "error", "-select_streams", "a:0", "-show_entries",
        "stream=channels,sample_rate,duration:format=duration", "-of", "json", str(path),
    ]))
    stream = probe["streams"][0]
    duration = float(probe.get("format", {}).get("duration", stream.get("duration", 0)))
    if duration > 120:
        raise ValueError("Analyze an explicit source excerpt of at most 120 seconds.")
    channels = min(2, int(stream["channels"]))
    raw = execute([
        "ffmpeg", "-v", "error", "-i", str(path), "-map", "0:a:0", "-vn", "-sn", "-dn",
        "-f", "f32le", "-ac", str(channels), "-ar", str(RATE), "pipe:1",
    ])
    values = np.frombuffer(raw, dtype="<f4").astype(np.float64).reshape(-1, channels)
    if len(values) < 4 or not np.isfinite(values).all():
        raise ValueError("Source has too few samples or non-finite audio.")
    power = np.mean(values ** 2, axis=0)
    report = {
        "sourceChannels": int(stream["channels"]), "sourceSampleRate": int(stream["sample_rate"]),
        "decodedSampleRate": RATE, "sourceSha256": hashlib.sha256(path.read_bytes()).hexdigest(),
    }
    if channels == 1:
        signal = values[:, 0]
        report.update({"monoDecision": "original mono", "stereoCorrelation": None, "monoCancellationDb": 0})
    else:
        averaged = np.mean(values, axis=1)
        centered = values - np.mean(values, axis=0)
        denominator = math.sqrt(float(np.sum(centered[:, 0] ** 2) * np.sum(centered[:, 1] ** 2)))
        correlation = float(np.sum(centered[:, 0] * centered[:, 1]) / denominator) if denominator > 1e-15 else 1
        cancellation = 10 * math.log10(max(float(np.mean(averaged ** 2)), 1e-20) / max(float(max(power)), 1e-20))
        if mono not in {"auto", "average", "left", "right"}:
            raise ValueError("Mono choice must be auto, average, left or right.")
        if mono == "auto" and cancellation < -7.5:
            channel = int(np.argmax(power))
            signal = values[:, channel]
            decision = ("left" if channel == 0 else "right") + " selected to avoid cancellation"
        elif mono in {"left", "right"}:
            signal = values[:, 0 if mono == "left" else 1]
            decision = mono + " explicitly selected"
        else:
            signal = averaged
            decision = "stereo average"
        report.update({"monoDecision": decision, "stereoCorrelation": correlation, "monoCancellationDb": cancellation})
    signal.setflags(write=False)
    return signal, report


def power_envelope(signal: np.ndarray, milliseconds: float = 2) -> np.ndarray:
    width = min(len(signal), max(1, round(RATE * milliseconds / 1000)))
    cumulative = np.concatenate(([0.0], np.cumsum(signal ** 2)))
    power = (cumulative[width:] - cumulative[:-width]) / width
    return np.sqrt(np.maximum(0, np.pad(power, (width // 2, width - 1 - width // 2), mode="edge")))


def candidate_regions(signal: np.ndarray) -> list[dict]:
    envelope = power_envelope(signal)
    maximum = float(np.max(envelope))
    if maximum < 1e-7:
        return []
    floor = float(np.quantile(envelope, 0.10))
    threshold = max(1e-6, maximum * .06, min(floor * 3, maximum * .15))
    active = envelope > threshold
    edges = np.diff(np.concatenate(([False], active, [False])).astype(np.int8))
    starts, ends = np.flatnonzero(edges == 1), np.flatnonzero(edges == -1)
    merged: list[list[int]] = []
    for start, end in zip(starts, ends):
        if merged and start - merged[-1][1] < RATE * .025:
            merged[-1][1] = int(end)
        else:
            merged.append([int(start), int(end)])
    merged = [(start, end) for start, end in merged if end - start >= RATE * .001]
    return [
        {"index": index, "startSeconds": start / RATE, "endSeconds": end / RATE,
         "durationSeconds": (end - start) / RATE, "peak": float(np.max(np.abs(signal[start:end]))),
         "energy": float(np.sum(signal[start:end] ** 2)), "thresholdRms": threshold}
        for index, (start, end) in enumerate(merged)
    ]


def measurements(signal: np.ndarray) -> dict:
    peak = float(np.max(np.abs(signal)))
    rms = float(np.sqrt(np.mean(signal ** 2)))
    spectrum = np.abs(np.fft.rfft(signal - np.mean(signal))) ** 2
    frequencies = np.fft.rfftfreq(len(signal), 1 / RATE)
    total = max(float(np.sum(spectrum)), 1e-20)
    bands = ((0, 65), (65, 180), (180, 600), (600, 1600), (1600, 4000), (4000, 8000), (8000, RATE / 2 + 1))
    regions = candidate_regions(signal)
    tail = signal[-min(len(signal), round(RATE * .010)):]
    return {
        "durationSeconds": len(signal) / RATE, "peak": peak, "peakDbfs": db(peak),
        "rms": rms, "rmsDbfs": db(rms), "crestDb": db(peak / max(rms, 1e-20)),
        "dc": float(np.mean(signal)), "firstSample": float(signal[0]), "lastSample": float(signal[-1]),
        "tail10msRmsDbfs": db(float(np.sqrt(np.mean(tail ** 2)))),
        "energyCentroidHz": float(np.sum(spectrum * frequencies) / total),
        "energyBands": {f"{low:g}-{high:g}Hz": float(np.sum(spectrum[(frequencies >= low) & (frequencies < high)]) / total) for low, high in bands},
        "firstDetectedOnsetSeconds": regions[0]["startSeconds"] if regions else None,
        "candidateRegions": regions,
    }


def db(value: float) -> float | None:
    return 20 * math.log10(value) if value > 1e-12 else None


def fade_weights(length: int, attack_ms: float, release_ms: float) -> np.ndarray:
    samples = np.arange(length)
    attack, release = max(1, round(RATE * attack_ms / 1000)), max(1, round(RATE * release_ms / 1000))
    return np.sin(np.minimum(1, samples / attack) * math.pi / 2) ** 2 * np.sin(np.minimum(1, (length - 1 - samples) / release) * math.pi / 2) ** 2


def stretch_body(signal: np.ndarray, factor: float, preserve_ms: float, blend_ms: float) -> np.ndarray:
    if not .60 <= factor <= 1.60:
        raise ValueError("Body duration scale must stay between 0.60 and 1.60; select better material for larger changes.")
    if factor == 1:
        return signal
    prefix = min(len(signal) - 4, round(RATE * preserve_ms / 1000))
    if prefix < 0:
        raise ValueError("Preserved transient length cannot be negative.")
    body = signal[prefix:]
    stretched = filter_audio(body, f"atempo={1 / factor:.12g}")
    if len(stretched) < 4:
        raise ValueError("Body is too short for onset-preserving time stretching.")
    blend = min(round(RATE * blend_ms / 1000), len(body), len(stretched))
    if blend:
        weight = np.linspace(0, 1, blend)
        stretched[:blend] = body[:blend] * (1 - weight) + stretched[:blend] * weight
    return np.concatenate((signal[:prefix], stretched))


def loop_segment(signal: np.ndarray, frames: int, crossfade_ms: float) -> np.ndarray:
    overlap = round(RATE * crossfade_ms / 1000)
    if overlap < 2 or overlap >= frames / 2 or len(signal) < frames + overlap:
        raise ValueError("Loop selection must contain duration + crossfade, with a crossfade shorter than half the loop.")
    loop = signal[:frames].copy()
    weight = .5 - .5 * np.cos(np.linspace(0, math.pi, overlap))
    loop[:overlap] = signal[frames:frames + overlap] * (1 - weight) + signal[:overlap] * weight
    return loop


def prepare_layer(layer: dict, sources: dict, frames: int, recipe_directory: Path) -> tuple[np.ndarray, dict]:
    source = sources[layer["source"]]
    path = Path(source["path"])
    if not path.is_absolute():
        path = (recipe_directory / path).resolve()
    signal, provenance = decode(path, layer.get("mono", "auto"))
    original_length = len(signal)
    filter_context_frames = 0
    selected_input_frames = original_length
    protected = layer.get("protected_anchor", False)
    if protected:
        forbidden = {"segment", "highpass_hz", "lowpass_hz", "lowpass_order", "eq", "filter_preroll_ms", "envelope", "body_scale", "rms_db", "loop_crossfade_ms"}
        if forbidden.intersection(layer) or layer.get("gain_db", 0) != 0 or layer.get("offset_ms", 0) != 0:
            raise ValueError("Protected anchors cannot be trimmed, filtered, stretched, enveloped, normalized, delayed or amplified.")
        if len(signal) > frames:
            raise ValueError("Output would truncate a protected anchor.")
        selected_start = 0
    else:
        segment = layer.get("segment", {})
        start = segment.get("start_seconds", "onset")
        if start == "onset":
            regions = candidate_regions(signal)
            if not regions:
                raise ValueError("No material onset was found; choose another source or an explicit segment.")
            candidate = int(segment.get("candidate", 0))
            if not 0 <= candidate < len(regions):
                raise ValueError("Candidate index is outside the analyzed material regions.")
            start = regions[candidate]["startSeconds"]
        selected_start = max(0, round(RATE * (float(start) - float(segment.get("preroll_ms", 2)) / 1000)))
        count = round(RATE * float(segment.get("duration_seconds", (len(signal) - selected_start) / RATE)))
        selected_end = min(len(signal), selected_start + count)
        selected_input_frames = selected_end - selected_start
        if selected_input_frames < 4:
            raise ValueError("Selected material segment is empty or too short.")
        filters = []
        if "highpass_hz" in layer:
            filters.append(f"highpass=f={float(layer['highpass_hz']):.9g}:p=2")
        if "lowpass_hz" in layer:
            lowpass_order = layer.get("lowpass_order", 2)
            if lowpass_order not in (2, 4, 6):
                raise ValueError("Low-pass order must be 2, 4 or 6.")
            filters.extend([f"lowpass=f={float(layer['lowpass_hz']):.9g}:p=2"] * (int(lowpass_order) // 2))
        elif "lowpass_order" in layer:
            raise ValueError("Low-pass order requires a cutoff frequency.")
        for eq in layer.get("eq", []):
            filters.append(f"equalizer=f={float(eq['hz']):.9g}:t=q:w={float(eq.get('q', .8)):.9g}:g={float(eq['db']):.9g}")
        if filters:
            context_ms = float(layer.get("filter_preroll_ms", 20))
            if not 0 <= context_ms <= 100:
                raise ValueError("Filter preroll must be between 0 and 100 ms.")
            context_start = max(0, selected_start - round(RATE * context_ms / 1000))
            filter_context_frames = selected_start - context_start
            # Warm causal filters on preceding source audio before extracting a body.
            contextual = filter_audio(signal[context_start:selected_end], ",".join(filters))
            signal = contextual[filter_context_frames:filter_context_frames + selected_input_frames]
            if len(signal) != selected_input_frames:
                raise ValueError("Filtering changed the selected material's sample count.")
        else:
            signal = signal[selected_start:selected_end]
        if "body_scale" in layer:
            signal = stretch_body(signal, float(layer["body_scale"]), float(layer.get("preserve_transient_ms", 15)), float(layer.get("stretch_blend_ms", 4)))
        offset = round(RATE * float(layer.get("offset_ms", 0)) / 1000)
        available = frames - offset
        if available < 4 or offset < 0:
            raise ValueError("Layer offset leaves no usable output duration.")
        if "loop_crossfade_ms" in layer:
            signal = loop_segment(signal, available, float(layer["loop_crossfade_ms"]))
        else:
            signal = signal[:available]
        envelope = layer.get("envelope", {})
        if "decay_ms" in envelope:
            time = np.arange(len(signal)) / RATE
            hold = float(envelope.get("hold_ms", 0)) / 1000
            signal *= np.exp(-np.maximum(0, time - hold) / (float(envelope["decay_ms"]) / 1000))
        if "loop_crossfade_ms" not in layer:
            weights = fade_weights(len(signal), float(envelope.get("attack_ms", .5)), float(envelope.get("release_ms", 6)))
            dc = float(np.sum(signal * weights) / np.sum(weights))
            signal = (signal - dc) * weights
        else:
            signal -= np.mean(signal)
        if "rms_db" in layer:
            target = 10 ** (float(layer["rms_db"]) / 20)
            rms = float(np.sqrt(np.mean(signal ** 2)))
            if rms < 1e-7:
                raise ValueError("Selected material is effectively silent.")
            gain = target / rms
            if db(gain) > float(layer.get("maximum_boost_db", 18)):
                raise ValueError("Layer normalization needs excessive boost; select a stronger material segment.")
            signal *= gain
    signal *= 10 ** (float(layer.get("gain_db", 0)) / 20)
    offset = round(RATE * float(layer.get("offset_ms", 0)) / 1000)
    output = np.zeros(frames, dtype=np.float64)
    output[offset:offset + len(signal)] += signal
    allowed_generation_fields = {"provider", "model_id", "request_id", "prompt", "parameters", "created_utc", "account_license_at_generation", "download_sha256"}
    generation = {key: value for key, value in source.get("generation", {}).items() if key in allowed_generation_fields}
    provenance.update({"sourceId": layer["source"], "path": str(path), "sourceFrames": original_length,
                       "selectedStartSeconds": selected_start / RATE, "role": layer["role"],
                       "selectedInputFrames": selected_input_frames, "filterPrerollSeconds": filter_context_frames / RATE,
                       "protectedAnchor": protected, "processing": layer, "generation": generation,
                       "processedMeasurements": measurements(output)})
    return output, provenance


def write_wave(path: Path, signal: np.ndarray) -> np.ndarray:
    pcm = np.rint(signal * 32768).clip(-32768, 32767).astype("<i2")
    with wave.open(str(path), "wb") as target:
        target.setparams((1, 2, RATE, len(pcm), "NONE", "not compressed"))
        target.writeframes(pcm.tobytes())
    return pcm.astype(np.float64) / 32768


def render(recipe_path: Path, destination: Path, *, workspace: Path | None = None) -> dict:
    destination = output_path(destination, workspace)
    recipe = json.loads(recipe_path.read_text(encoding="utf-8-sig"))
    if recipe.get("version") != 1:
        raise ValueError("Expected recipe version 1.")
    destination.mkdir(parents=True, exist_ok=True)
    renders = []
    for cue in recipe["renders"]:
        filename = cue["file"]
        if not re.fullmatch(r"[a-z0-9_-]+\.wav", filename):
            raise ValueError("Cooked filenames must be plain lowercase WAV basenames.")
        frames = round(float(cue["duration_seconds"]) * RATE)
        if frames < RATE * .01 or frames > RATE * 2:
            raise ValueError("Cooked samples must fit the existing 10 ms–2 s PCM loader bounds.")
        layers, provenance = [], []
        for layer in cue["layers"]:
            if layer["role"] not in {"anchor", "body", "detail"}:
                raise ValueError("A material layer needs an anchor, body or detail role.")
            try:
                signal, info = prepare_layer(layer, recipe["sources"], frames, recipe_path.parent)
            except ValueError as error:
                raise ValueError(f"{filename}, {layer['source']}: {error}") from error
            layers.append(signal)
            provenance.append(info)
        if not layers:
            raise ValueError("A cooked cue needs at least one layer.")
        mixed = np.sum(layers, axis=0)
        protected = any(layer.get("protected_anchor", False) for layer in cue["layers"])
        loop = bool(cue.get("loop", False))
        mastering = cue.get("mastering", {})
        if not protected:
            if loop:
                mixed -= np.mean(mixed)
            else:
                weights = fade_weights(frames, float(mastering.get("edge_attack_ms", .4)), float(mastering.get("edge_release_ms", 4)))
                mixed = (mixed - np.sum(mixed * weights) / np.sum(weights)) * weights
        peak = float(np.max(np.abs(mixed)))
        ceiling = float(mastering.get("peak_ceiling", .28))
        if peak < 1e-7 or not 0 < ceiling <= .95:
            raise ValueError("Silent render or invalid peak ceiling.")
        if protected and peak > ceiling:
            raise ValueError("Additional layers exceed the protected anchor's ceiling; lower their gains rather than renormalizing the anchor.")
        gain = 1.0 if protected else min(1.0, ceiling / peak)
        if "peak_target" in mastering and not protected:
            target = min(float(mastering["peak_target"]), ceiling)
            gain = target / peak
            if db(gain) > float(mastering.get("maximum_boost_db", 12)):
                raise ValueError("Final normalization needs excessive boost.")
        mixed *= gain
        output = destination / filename
        quantized = write_wave(output, mixed)
        metrics = measurements(quantized)
        warnings = []
        if abs(metrics["dc"]) > .006:
            warnings.append("DC offset exceeds 0.006.")
        if not loop and (abs(metrics["firstSample"]) > 1 / 32768 or abs(metrics["lastSample"]) > 1 / 32768):
            warnings.append("Finite cue has a nonzero edge; check the intended protected source.")
        if metrics["energyBands"]["0-65Hz"] > float(cue.get("qc", {}).get("maximum_sub65_energy", .08)):
            warnings.append("Sub-65 Hz energy exceeds this cue's configured allocation.")
        if metrics["energyBands"]["8000-22051Hz"] > float(cue.get("qc", {}).get("maximum_over8k_energy", .06)):
            warnings.append("Upper-band energy exceeds this cue's configured allocation.")
        if loop:
            difference = np.diff(quantized)
            metrics["loopSeamStep"] = float(abs(quantized[0] - quantized[-1]))
            metrics["interiorStepP99"] = float(np.quantile(np.abs(difference), .99))
            if metrics["loopSeamStep"] > max(.01, metrics["interiorStepP99"] * 1.5):
                warnings.append("Loop seam exceeds typical interior sample steps; inspect and audition the join.")
        renders.append({"file": filename, "purpose": cue.get("purpose", ""), "family": cue.get("family"),
                        "layer": cue.get("layer"), "stage": cue.get("stage"), "variant": cue.get("variant"),
                        "loop": loop, "protectedAnchor": protected, "masterGainDb": db(gain),
                        "sha256": hashlib.sha256(output.read_bytes()).hexdigest(), "measurements": metrics,
                        "warnings": warnings, "sources": provenance, "listeningStatus": "not assessed by numerical processing"})
        print(f"Rendered {filename}: {len(quantized) / RATE:.3f}s, peak {metrics['peak']:.3f}, {len(warnings)} QC observations.")
    manifest = {"recipeSha256": hashlib.sha256(recipe_path.read_bytes()).hexdigest(), "recipe": str(recipe_path),
                "sampleRate": RATE, "format": "mono signed PCM16 WAV", "numpy": np.__version__,
                "ffmpeg": execute(["ffmpeg", "-version"]).decode().splitlines()[0],
                "sourceSelection": "Explicit recipe selection or first detected region; aesthetic suitability is not inferred.", "renders": renders}
    (destination / "manifest.json").write_text(json.dumps(manifest, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    return manifest


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    analysis = commands.add_parser("analyze")
    analysis.add_argument("input", type=Path)
    analysis.add_argument("--mono", default="auto", choices=("auto", "average", "left", "right"))
    analysis.add_argument("--out", default="analysis.json")
    analysis.add_argument("--workspace", type=Path, help="Explicit scratch directory for analysis outputs.")
    renderer = commands.add_parser("render")
    renderer.add_argument("recipe", type=Path)
    renderer.add_argument("--out", default="cooked")
    renderer.add_argument("--workspace", type=Path, help="Explicit scratch directory for rendered outputs.")
    args = parser.parse_args()
    try:
        if args.command == "analyze":
            signal, report = decode(args.input.resolve(), args.mono)
            report["measurements"] = measurements(signal)
            report["selectionNote"] = "Regions locate signal activity, not a perceptual or stylistic quality ranking."
            destination = output_path(args.out, args.workspace)
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_text(json.dumps(report, indent=2, allow_nan=False) + "\n", encoding="utf-8")
            print(f"Analyzed {args.input.name}: {len(signal) / RATE:.3f}s; {report['monoDecision']}.")
        else:
            render(args.recipe.resolve(), output_path(args.out, args.workspace), workspace=args.workspace)
    except (OSError, ValueError, KeyError, IndexError) as error:
        print(str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
