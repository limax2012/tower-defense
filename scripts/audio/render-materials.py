"""Render the authored Csound material voices into embedded mono PCM assets.

Run with Python 3: python scripts/audio/render-materials.py
Use --check to verify that the checked-in samples match a fresh render.
Windows uses a pinned portable Csound in .artifacts/audio-tools. Other platforms
can provide an equivalent Csound 6.18 executable with --csound.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import urllib.request
import wave
import zipfile


ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / ".artifacts" / "audio-tools"
OUTPUT = ROOT / "src" / "MaximalBastion" / "Audio" / "Samples"
ORCHESTRA = Path(__file__).with_name("materials.orc")
RATE = 44_100
CSOUND_URL = (
    "https://github.com/csound/csound/releases/download/6.18.1/"
    "Csound-6.18.1-windows-x64-binaries.zip"
)
CSOUND_SHA256 = "bd499ac6f476d98c6ae951a8fbfda365e594ef34bcf480c5a273281df940c220"
FAMILIES = {
    "shard_fan": (1, (0.118, 0.083), (0.006, 0.004)),
    "ember_coil": (2, (0.181, 0.157), (0.009, 0.009)),
    "frost_spire": (3, (0.098, 0.092), (0.005, 0.005)),
}


def csound_path(requested: str | None) -> Path:
    if requested:
        candidate = Path(requested).resolve()
        if not candidate.is_file():
            raise ValueError(f"Csound executable does not exist: {candidate}")
        return candidate
    if os.name != "nt":
        installed = shutil.which("csound")
        if installed:
            return Path(installed)
        raise ValueError("Install Csound 6.18 or pass --csound /path/to/csound.")
    archive = TOOLS / "csound-6.18.1-win64.zip"
    destination = TOOLS / "csound-6.18.1"
    executable = destination / "build" / "Release" / "csound.exe"
    if executable.is_file():
        return executable
    TOOLS.mkdir(parents=True, exist_ok=True)
    if not archive.is_file():
        print("Downloading the pinned portable Csound authoring tool.")
        urllib.request.urlretrieve(CSOUND_URL, archive)
    if hashlib.sha256(archive.read_bytes()).hexdigest() != CSOUND_SHA256:
        raise ValueError(f"Csound archive checksum mismatch: {archive}")
    with zipfile.ZipFile(archive) as bundle:
        for member in bundle.infolist():
            target = (destination / member.filename).resolve()
            if not target.is_relative_to(destination.resolve()):
                raise ValueError("Csound archive contains an invalid path.")
        bundle.extractall(destination)
    if not executable.is_file():
        raise ValueError("Csound archive does not contain the expected executable.")
    return executable


def read_float_wave(path: Path, frames: int) -> list[float]:
    contents = path.read_bytes()
    if contents[:4] != b"RIFF" or contents[8:12] != b"WAVE":
        raise ValueError(f"Not a RIFF WAV: {path}")
    offset = 12
    audio = None
    format_info = None
    while offset + 8 <= len(contents):
        kind, length = struct.unpack_from("<4sI", contents, offset)
        payload = contents[offset + 8 : offset + 8 + length]
        if kind == b"fmt ":
            format_info = struct.unpack_from("<HHIIHH", payload)
        elif kind == b"data":
            audio = payload
        offset += 8 + length + (length % 2)
    if format_info is None or audio is None:
        raise ValueError(f"Incomplete WAV: {path}")
    encoding, channels, rate, _, _, bits = format_info
    if (encoding, channels, rate, bits) != (3, 1, RATE, 32):
        raise ValueError(f"Expected mono 44.1 kHz float32 WAV: {format_info}")
    samples = list(struct.unpack(f"<{len(audio) // 4}f", audio))[:frames]
    if len(samples) != frames or any(not math.isfinite(value) for value in samples):
        raise ValueError(f"Incomplete or non-finite audio: {path}")
    return samples


def master(samples: list[float], peak: float) -> bytes:
    # The weighted correction removes DC without reopening either faded edge.
    attack = round(RATE * 0.001)
    release = round(RATE * 0.006)
    weights = [
        math.sin(math.pi * 0.5 * min(1, index / attack)) ** 2
        * math.sin(math.pi * 0.5 * min(1, (len(samples) - 1 - index) / release)) ** 2
        for index in range(len(samples))
    ]
    mean = sum(value * weight for value, weight in zip(samples, weights)) / sum(weights)
    faded = [(value - mean) * weight for value, weight in zip(samples, weights)]
    maximum = max(abs(value) for value in faded)
    if maximum < 1e-6:
        raise ValueError("The material render is silent.")
    gain = peak / maximum
    pcm = [round(value * gain * 32767) for value in faded]
    return struct.pack(f"<{len(pcm)}h", *pcm)


def render(executable: Path, check: bool) -> None:
    work = TOOLS / "renders"
    work.mkdir(parents=True, exist_ok=True)
    if not check:
        OUTPUT.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    environment["PATH"] = str(executable.parent) + os.pathsep + environment.get("PATH", "")
    environment["OPCODE6DIR64"] = str(executable.parent)
    manifest = []
    mismatches = []
    for family, (instrument, durations, extensions) in FAMILIES.items():
        for impact, layer in enumerate(("shot", "impact")):
            for stage in range(4):
                for variant in range(3):
                    duration = durations[impact] + extensions[impact] * stage + (variant - 1) * 0.001
                    frames = round(duration * RATE)
                    name = f"{family}-{layer}-{stage}-{variant}.wav"
                    score = work / "material.sco"
                    score.write_text(
                        f"i {instrument} 0 {frames / RATE:.10f} {impact} {stage} {variant}\ne\n",
                        encoding="ascii",
                    )
                    floating = work / "material-float.wav"
                    command = [str(executable), "-d", "-m0", "-W", "-f", "-o", str(floating), str(ORCHESTRA), str(score)]
                    process = subprocess.run(command, capture_output=True, text=True, env=environment)
                    if process.returncode:
                        raise RuntimeError(f"Csound failed for {name}:\n{process.stdout}\n{process.stderr}")
                    pcm = master(read_float_wave(floating, frames), 0.22 if impact else 0.28)
                    target = work / name
                    with wave.open(str(target), "wb") as output:
                        output.setparams((1, 2, RATE, frames, "NONE", "not compressed"))
                        output.writeframes(pcm)
                    contents = target.read_bytes()
                    destination = OUTPUT / name
                    if check:
                        if not destination.is_file() or destination.read_bytes() != contents:
                            mismatches.append(name)
                    else:
                        destination.write_bytes(contents)
                    values = struct.unpack(f"<{frames}h", pcm)
                    manifest.append({
                        "file": name,
                        "sha256": hashlib.sha256(contents).hexdigest(),
                        "durationSeconds": frames / RATE,
                        "peak": max(abs(value) for value in values) / 32768,
                        "rms": math.sqrt(sum(value * value for value in values) / frames) / 32768,
                        "dc": sum(values) / frames / 32768,
                    })
        print(f"{'Checked' if check else 'Rendered'} {family}: 24 material voices.")
    (work / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    if mismatches:
        raise ValueError("Samples differ from a fresh render: " + ", ".join(mismatches))
    print(f"{'Verified' if check else 'Wrote'} {len(manifest)} mono 44.1 kHz PCM16 samples.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--csound", help="Path to a Csound executable.")
    parser.add_argument("--check", action="store_true", help="Compare fresh renders with checked-in samples.")
    args = parser.parse_args()
    try:
        render(csound_path(args.csound), args.check)
    except (OSError, ValueError, RuntimeError, zipfile.BadZipFile) as error:
        print(error, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
