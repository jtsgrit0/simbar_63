"""Package the Unity WebGL player for static GitHub Pages hosting.

Run with Python 3: python Tools/prepare_github_pages.py
The responsive host comes from Tools/WebGLTemplate and survives Unity rebuilds.
Use --host-only to refresh the host without recopying game assets.
Release builds use raw assets so the host's native HTTP compression and Wasm
streaming compilation can run together. Existing .unityweb fallback assets
are preserved; older .br/.gz builds are unpacked for compatibility. For .br
files, Unity's bundled Brotli executable or Python `brotli` is required.
"""

import argparse
import gzip
import hashlib
import html
import importlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess


PROJECT = Path(__file__).resolve().parent.parent
TEMPLATE = PROJECT / "Tools/WebGLTemplate"
BUILD_ASSET_SUFFIXES = (".data", ".framework.js", ".wasm", ".loader.js")
COMPRESSION_SUFFIXES = ("", ".br", ".gz", ".unityweb")


def is_build_asset(path):
    return any(path.name.endswith(suffix + compression)
               for suffix in BUILD_ASSET_SUFFIXES for compression in COMPRESSION_SUFFIXES)


def build_assets(source):
    """Select the current build, ignoring old artifacts left by Unity rebuilds."""
    index = (source / "index.html").read_text(encoding="utf-8")
    result = {}
    for suffix in BUILD_ASSET_SUFFIXES:
        candidates = [path for path in (source / "Build").iterdir()
                      if path.is_file() and any(path.name.endswith(suffix + compression)
                                                for compression in COMPRESSION_SUFFIXES)]
        if len(candidates) > 1:
            candidates = [path for path in candidates
                          if re.search(r'["\'](?:[^"\']*/)?' + re.escape(path.name) + r'["\']', index)]
        if len(candidates) != 1:
            raise RuntimeError(f"Expected one current {suffix} asset in {source / 'Build'}, got {len(candidates)}")
        result[suffix] = candidates[0]
    return result


def deployed_path(relative):
    # Unity's fallback loader decodes .unityweb itself. Preserve those bytes and
    # filenames; only legacy HTTP-encoded builds need unpacking on GitHub Pages.
    return relative.with_suffix("") if relative.suffix in (".br", ".gz") else relative


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


def write_web_host(source, output, assets=None):
    """Use our responsive host instead of Unity's generated fixed-size page."""
    source_index = (source / "index.html").read_text(encoding="utf-8")
    assets = assets or build_assets(source)

    def asset_url(suffix):
        relative = deployed_path(assets[suffix].relative_to(source))
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
    hints = ['<link rel="preload" as="script" href="' + html.escape(build["loaderUrl"], quote=True) + '">']
    if config["frameworkUrl"].partition("?")[0].endswith(".js"):
        hints.append('<link rel="preload" as="script" href="' + html.escape(config["frameworkUrl"], quote=True) + '">')
    # Keep Unity's internal product metadata so saved preferences remain available.
    encoded = json.dumps(build, ensure_ascii=False, indent=2).replace("<", "\\u003c")
    stylesheet = output / "TemplateData/style.css"
    stylesheet.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(TEMPLATE / "style.css", stylesheet)
    index = (TEMPLATE / "index.html").read_text(encoding="utf-8")
    index = index.replace("__SIMBAR_BUILD_CONFIG__", encoded)
    index = index.replace("__SIMBAR_ASSET_HINTS__", "\n    ".join(hints))
    index = index.replace("__SIMBAR_ASSET_CACHE__", (TEMPLATE / "asset-cache.js").read_text(encoding="utf-8"))
    index = index.replace("__SIMBAR_STYLESHEET__", "TemplateData/style.css?v=" + file_version(stylesheet))
    (output / "index.html").write_text(index, encoding="utf-8", newline="\n")
    (output / ".nojekyll").touch()


def package_player(source, output, brotli=None, host_only=False):
    assets = build_assets(source)
    output.mkdir(parents=True, exist_ok=True)
    if not host_only:
        selected = set(assets.values())
        files = [path for path in sorted(source.rglob("*")) if path.is_file()
                 and not (path.parent == source / "Build" and is_build_asset(path) and path not in selected)]
        decompress = decompressor(brotli) if any(path.suffix == ".br" for path in files) else None
        deployed_files = set()
        for path in files:
            relative = path.relative_to(source)
            if relative.as_posix() == "index.html":
                continue
            target_relative = deployed_path(relative)
            deployed_files.add(target_relative)
            target = output / target_relative
            target.parent.mkdir(parents=True, exist_ok=True)
            if path.suffix == ".br":
                decompress(path, target)
            elif path.suffix == ".gz":
                with gzip.open(path, "rb") as stream, target.open("wb") as destination:
                    shutil.copyfileobj(stream, destination)
            else:
                shutil.copy2(path, target)
        # Remove obsolete player files so switching from raw to fallback assets
        # does not leave another full copy of the game in the deployment.
        for path in (output / "Build").iterdir():
            if path.is_file() and is_build_asset(path) and path.relative_to(output) not in deployed_files:
                path.unlink()
    write_web_host(source, output, assets)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=PROJECT / "Builds/Pages/SimBarPlayerWebGL")
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

    package_player(source, output, brotli=args.brotli, host_only=args.host_only)
    print(f"GitHub Pages player ready: {output}")
    for path in sorted((output / "Build").iterdir()):
        print(f"  {path.relative_to(output).as_posix()}: {path.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
