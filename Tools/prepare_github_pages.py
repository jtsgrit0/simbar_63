"""Package the Unity WebGL player for static GitHub Pages hosting.

Run with Python 3: python Tools/prepare_github_pages.py
Unity's bundled Brotli executable is discovered on Windows, or install the
Python `brotli` package. Override discovery with --brotli /path/to/brotli.
"""

import argparse
import importlib
import os
from pathlib import Path
import shutil
import subprocess


PROJECT = Path(__file__).resolve().parent.parent


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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=PROJECT / "Builds/SimBarPlayerWebGL")
    parser.add_argument("--output", type=Path, default=PROJECT / "docs")
    parser.add_argument("--brotli", type=Path)
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output.resolve()
    if not (source / "index.html").is_file():
        parser.error(f"No Unity WebGL index.html at {source}")
    if output == PROJECT or not output.is_relative_to(PROJECT):
        parser.error("The output must be a folder inside the project.")
    if output == source or output.is_relative_to(source) or source.is_relative_to(output):
        parser.error("Source and output folders must be separate.")

    files = sorted(path for path in source.rglob("*") if path.is_file())
    decompress = decompressor(args.brotli) if any(path.suffix == ".br" for path in files) else None
    index = (source / "index.html").read_text(encoding="utf-8")
    output.mkdir(parents=True, exist_ok=True)
    for path in files:
        relative = path.relative_to(source)
        target = output / relative
        if path.suffix == ".br":
            target = target.with_suffix("")
            index = index.replace(relative.as_posix(), relative.with_suffix("").as_posix())
            # The default Unity template concatenates buildUrl with a filename.
            index = index.replace(path.name, target.name)
        target.parent.mkdir(parents=True, exist_ok=True)
        if path.suffix == ".br":
            decompress(path, target)
            compressed_copy = output / relative
            if compressed_copy.is_file():
                compressed_copy.unlink()
        elif relative.as_posix() != "index.html":
            shutil.copy2(path, target)
    if ".br\"" in index or ".br'" in index:
        raise RuntimeError("An unresolved Brotli URL remains in index.html.")
    index = index.replace('<html lang="en-us">', '<html lang="ko">')
    index = index.replace('<title>Unity Web Player | simbar_6</title>', '<title>SimBar</title>')
    index = index.replace('<div id="unity-build-title">simbar_6</div>',
                          '<div id="unity-build-title">SimBar</div>')
    index = index.replace('<meta charset="utf-8">', '<meta charset="utf-8">\n'
                          '    <meta name="description" content="SimBar — 한국어와 English로 즐기는 브라우저 바 운영 게임.">')
    (output / "index.html").write_text(index, encoding="utf-8", newline="\n")
    (output / ".nojekyll").touch()
    print(f"GitHub Pages player ready: {output}")
    for path in sorted((output / "Build").iterdir()):
        print(f"  {path.relative_to(output).as_posix()}: {path.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
