"""Package the Unity WebGL player for static GitHub Pages hosting.

Run with Python 3: python Tools/prepare_github_pages.py
The responsive host comes from Tools/WebGLTemplate and survives Unity rebuilds.
Use --host-only to refresh the host without recopying/decompressing game assets.
Unity's bundled Brotli executable is discovered on Windows, or install the
Python `brotli` package. Override discovery with --brotli /path/to/brotli.
"""

import argparse
import hashlib
import importlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess


PROJECT = Path(__file__).resolve().parent.parent
TEMPLATE = PROJECT / "Tools/WebGLTemplate"


def brotli_executable(override):
    if override:
        executable = Path(override).resolve()
        if not executable.is_file():
            raise FileNotFoundError(f"Brotli executable not found: {executable}")
        return executable
    editors = Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Unity/Hub/Editor"
    candidates = sorted(editors.glob(
        "*/Editor/Data/PlaybackEngines/WebGLSupport/BuildTools/Brotli/win_x86_64/brotli.exe"
    ), reverse=True)
    if candidates:
        return candidates[0]
    executable = shutil.which("brotli")
    if executable:
        return Path(executable)
    raise RuntimeError("Install Python's brotli package or pass --brotli with a Brotli executable.")


def decompressor(override):
    if not override:
        try:
            module = importlib.import_module("brotli")
            return lambda source, target: target.write_bytes(module.decompress(source.read_bytes()))
        except ImportError:
            pass
    executable = brotli_executable(override)
    help_result = subprocess.run([str(executable), "--help"], capture_output=True, text=True)
    unity_cli = "--input" in (help_result.stdout + help_result.stderr)

    def decompress(source, target):
        if unity_cli:
            arguments = ["--force", "--decompress", "--input", str(source), "--output", str(target)]
        else:
            arguments = ["--force", "--decompress", "--output", str(target), str(source)]
        subprocess.run([str(executable), *arguments], check=True)

    return decompress


def file_version(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()[:16]


def write_web_host(source, output):
    """Use our responsive host instead of Unity's generated fixed-size page."""
    source_index = (source / "index.html").read_text(encoding="utf-8")

    def asset_url(suffix):
        candidates = [path for path in (source / "Build").iterdir()
                      if path.is_file() and (path.name.endswith(suffix) or path.name.endswith(suffix + ".br"))]
        if len(candidates) != 1:
            raise RuntimeError(f"Expected one {suffix} asset in {source / 'Build'}, got {len(candidates)}")
        relative = candidates[0].relative_to(source)
        if relative.suffix == ".br":
            relative = relative.with_suffix("")
        deployed = output / relative
        if not deployed.is_file():
            raise FileNotFoundError(f"Missing packaged asset: {deployed}. Run without --host-only first.")
        return relative.as_posix() + "?v=" + file_version(deployed)

    config = {
        "dataUrl": asset_url(".data"),
        "frameworkUrl": asset_url(".framework.js"),
        "codeUrl": asset_url(".wasm"),
        "streamingAssetsUrl": "StreamingAssets",
        "arguments": [],
    }
    for key, fallback in (("companyName", "DefaultCompany"), ("productName", "SimBar"), ("productVersion", "0.1.0")):
        match = re.search(rf'\b{key}:\s*("(?:[^"\\]|\\.)*")', source_index)
        config[key] = json.loads(match.group(1)) if match else fallback
    build = {"loaderUrl": asset_url(".loader.js"), "config": config}
    # Keep Unity's internal product metadata so saved preferences remain available.
    encoded = json.dumps(build, ensure_ascii=False, indent=2).replace("<", "\\u003c")
    stylesheet = output / "TemplateData/style.css"
    stylesheet.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(TEMPLATE / "style.css", stylesheet)
    index = (TEMPLATE / "index.html").read_text(encoding="utf-8")
    index = index.replace("__SIMBAR_BUILD_CONFIG__", encoded)
    index = index.replace("__SIMBAR_STYLESHEET__", "TemplateData/style.css?v=" + file_version(stylesheet))
    (output / "index.html").write_text(index, encoding="utf-8", newline="\n")
    (output / ".nojekyll").touch()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=PROJECT / "Builds/SimBarPlayerWebGL")
    parser.add_argument("--output", type=Path, default=PROJECT / "docs")
    parser.add_argument("--brotli", type=Path)
    parser.add_argument("--host-only", action="store_true",
                        help="Update the responsive host using assets already packaged in the output folder.")
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output.resolve()
    if not (source / "index.html").is_file():
        parser.error(f"No Unity WebGL index.html at {source}")
    if output == PROJECT or not output.is_relative_to(PROJECT):
        parser.error("The output must be a folder inside the project.")
    if output == source or output.is_relative_to(source) or source.is_relative_to(output):
        parser.error("Source and output folders must be separate.")

    output.mkdir(parents=True, exist_ok=True)
    if not args.host_only:
        files = sorted(path for path in source.rglob("*") if path.is_file())
        decompress = decompressor(args.brotli) if any(path.suffix == ".br" for path in files) else None
        for path in files:
            relative = path.relative_to(source)
            if relative.as_posix() == "index.html":
                continue
            target = output / relative
            if path.suffix == ".br":
                target = target.with_suffix("")
            target.parent.mkdir(parents=True, exist_ok=True)
            if path.suffix == ".br":
                decompress(path, target)
                compressed_copy = output / relative
                if compressed_copy.is_file():
                    compressed_copy.unlink()
            else:
                shutil.copy2(path, target)
    write_web_host(source, output)
    print(f"GitHub Pages player ready: {output}")
    for path in sorted((output / "Build").iterdir()):
        print(f"  {path.relative_to(output).as_posix()}: {path.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
