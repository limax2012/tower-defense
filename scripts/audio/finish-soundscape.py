"""Rebuild the finished soundscape from checked-in material and procedural cores.

Install authoring dependencies with: python -m pip install -r scripts/audio/requirements.txt
Run: python scripts/audio/finish-soundscape.py
Verify without writing game assets: python scripts/audio/finish-soundscape.py --check

FFmpeg and FFprobe must be on PATH. The workspace-local .NET SDK is preferred.
No audio-generation service, account, credentials or network API is used.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile

# Keep import caches alongside other generated authoring files.
sys.dont_write_bytecode = True

try:
    import audio_finishing
except ModuleNotFoundError as error:
    if error.name == "numpy":
        raise SystemExit("Install audio authoring dependencies: python -m pip install -r scripts/audio/requirements.txt") from error
    raise


ROOT = Path(__file__).resolve().parents[2]
DIRECTORY = Path(__file__).resolve().parent
RECIPE = DIRECTORY / "soundscape.json"
MATERIALS = DIRECTORY / "source-materials"
FINISHED = ROOT / "src" / "MaximalBastion" / "Audio" / "Finished"
PROJECT = ROOT / "tests" / "MaximalBastion.Tests" / "MaximalBastion.Tests.csproj"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def dotnet_path(requested: str | None) -> Path:
    if requested:
        found = shutil.which(requested)
        candidate = Path(found or requested).resolve()
    else:
        candidate = ROOT / ".dotnet" / ("dotnet.exe" if os.name == "nt" else "dotnet")
        if not candidate.is_file():
            found = shutil.which("dotnet")
            if not found:
                raise ValueError("A .NET SDK is required to export procedural audio cores.")
            candidate = Path(found)
    if not candidate.is_file():
        raise ValueError(f".NET executable does not exist: {candidate}")
    return candidate


def export_cores(executable: Path, workspace: Path, no_build: bool) -> None:
    environment = os.environ.copy()
    environment["PATH"] = str(executable.parent) + os.pathsep + environment.get("PATH", "")
    if not no_build:
        print("Building the procedural audio core exporter.", flush=True)
        result = subprocess.run([
            str(executable), "build", str(PROJECT), "-c", "Release", "--disable-build-servers",
            "/nodeReuse:false", "/p:UseSharedCompilation=false",
        ], cwd=ROOT, env=environment, capture_output=True, text=True)
        (workspace / "build.log").write_text(result.stdout + result.stderr, encoding="utf-8")
        if result.returncode:
            raise ValueError(f"Audio core exporter build failed. See {workspace / 'build.log'}\n{result.stdout[-6000:]}{result.stderr[-2000:]}")
    result = subprocess.run([
        str(executable), "run", "--project", str(PROJECT), "-c", "Release", "--no-build", "--",
        "--export-audio-cores", str(workspace / "cores"),
    ], cwd=ROOT, env=environment, capture_output=True, text=True)
    if result.returncode:
        raise ValueError(f"Procedural audio export failed:\n{result.stdout}\n{result.stderr}")
    print(result.stdout.strip(), flush=True)


def expand_recipe(recipe: dict, workspace: Path) -> Path:
    for identifier, source in recipe["sources"].items():
        relative = Path(source["path"])
        if source.get("kind") == "procedural-core":
            if relative.parts[:1] != ("cores",) or len(relative.parts) != 2:
                raise ValueError(f"Invalid procedural core path: {identifier}")
            path = workspace / "cores" / relative.name
        else:
            path = (DIRECTORY / relative).resolve()
            if not path.is_relative_to(MATERIALS.resolve()):
                raise ValueError(f"Authored material must be inside source-materials: {identifier}")
        if path.suffix.lower() != ".wav" or not path.is_file():
            raise ValueError(f"Missing WAV source for {identifier}: {path}")
        expected = source.get("sha256", "")
        if not re.fullmatch(r"[0-9a-f]{64}", expected):
            raise ValueError(f"Missing source SHA-256 for {identifier}.")
        if digest(path) != expected:
            raise ValueError(f"Source bytes differ from soundscape.json: {identifier}. Review the material or procedural-core change before updating its recorded hash.")
        source["path"] = str(path)
    recipe["sourceRecipeSha256"] = digest(RECIPE)
    expanded = workspace / "expanded-soundscape.json"
    expanded.write_text(json.dumps(recipe, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    return expanded


def verify_finished(names: list[str], rendered: Path) -> None:
    actual = {path.name for path in FINISHED.glob("*.wav")}
    expected = set(names)
    failures = [f"missing {name}" for name in sorted(expected - actual)]
    failures += [f"unexpected {name}" for name in sorted(actual - expected)]
    failures += [f"different {name}" for name in names if name in actual and (FINISHED / name).read_bytes() != (rendered / name).read_bytes()]
    if failures:
        shown = "\n".join(failures[:20])
        remaining = f"\n... and {len(failures) - 20} more." if len(failures) > 20 else ""
        raise ValueError(f"Finished audio does not match fresh renders:\n{shown}{remaining}")
    print(f"Verified all {len(names)} finished WAVs byte-for-byte; game assets were not written.")


def publish_finished(names: list[str], rendered: Path) -> None:
    FINISHED.mkdir(parents=True, exist_ok=True)
    unexpected = sorted(path.name for path in FINISHED.glob("*.wav") if path.name not in set(names))
    if unexpected:
        raise ValueError("Finished contains WAVs outside the recipe; review them before replacing the bank: " + ", ".join(unexpected))
    changed = 0
    for name in names:
        destination = FINISHED / name
        contents = (rendered / name).read_bytes()
        if destination.is_file() and destination.read_bytes() == contents:
            continue
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(dir=FINISHED, prefix=name + ".", suffix=".tmp", delete=False) as stream:
                temporary = Path(stream.name)
                stream.write(contents)
            temporary.replace(destination)
        finally:
            if temporary is not None and temporary.exists():
                temporary.unlink()
        changed += 1
    print(f"Finished soundscape contains {len(names)} WAVs; updated {changed} files.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Compare fresh renders with game assets without replacing them.")
    parser.add_argument("--dotnet", help="Explicit .NET SDK executable.")
    parser.add_argument("--no-build", action="store_true", help="Use an already-built Release core exporter; source hashes still must match.")
    parser.add_argument("--work-dir", type=Path, help="Explicit scratch directory; defaults to .artifacts/audio-finishing.")
    args = parser.parse_args()
    try:
        for tool in ("ffmpeg", "ffprobe"):
            if not shutil.which(tool):
                raise ValueError(f"{tool} must be available on PATH.")
        recipe = json.loads(RECIPE.read_text(encoding="utf-8"))
        if recipe.get("version") != 1:
            raise ValueError("Expected soundscape recipe version 1.")
        names = [cue["file"] for cue in recipe["renders"]]
        if not names or len(names) != len(set(names)) or any(not re.fullmatch(r"[a-z0-9_-]+\.wav", name) for name in names):
            raise ValueError("The soundscape must have unique plain WAV filenames.")
        referenced = {layer["source"] for cue in recipe["renders"] for layer in cue["layers"]}
        if referenced != set(recipe["sources"]):
            raise ValueError("Recipe sources must exactly match the materials used by its layers.")
        workspace = (args.work_dir or audio_finishing.DEFAULT_WORKSPACE).resolve()
        if workspace.is_relative_to(FINISHED.resolve()) or FINISHED.resolve().is_relative_to(workspace):
            raise ValueError("Choose a scratch workspace separate from the finished game assets.")
        workspace.mkdir(parents=True, exist_ok=True)
        export_cores(dotnet_path(args.dotnet), workspace, args.no_build)
        expanded = expand_recipe(recipe, workspace)
        rendered = workspace / "rendered"
        audio_finishing.render(expanded, rendered, workspace=workspace)
        if args.check:
            verify_finished(names, rendered)
        else:
            publish_finished(names, rendered)
        return 0
    except (OSError, ValueError, KeyError, IndexError) as error:
        print(str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
