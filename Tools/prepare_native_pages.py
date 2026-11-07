"""Package the complete browser-native game without touching the Unity build.

Run ``python Tools/prepare_native_pages.py --output Temp/NativePreview`` to
review locally. The default output is docs. Music and researched posters are
copied for on-demand use, never preloaded on the game startup path.
"""

import argparse
import hashlib
import html
import json
from pathlib import Path
import re
import shutil
import tempfile


PROJECT = Path(__file__).resolve().parent.parent
TEMPLATE = PROJECT / "Tools/WebGLTemplate"
SOURCE = TEMPLATE / "FastGame"
MODULES = ("boot.js", "game.js", "core.js", "world.js", "booking.js", "floor.js",
           "navigation.js", "catalog.js", "legacy-save.js", "daily-poster.js", "music.js", "aftershow.js", "album-art.js")
ESSENTIAL_IMAGES = ("owner-female.png", "owner-ff.png", "owner-pet.png", "seoul.png", "venue.png")
ESSENTIAL_METADATA = ("venue.json", "sprites.json")
ICONS = ("setup", "cash", "fans", "time", "staff", "guestsati", "perrform", "upgrades", "guest", "events")
EXCLUDED = {"index.html", "runtime-manifest.json", "package.json", "package-lock.json",
            "golden-fixtures.json", "world-pixel.js"}
EXCLUDED_PARTS = {"fixtures", "tests", "node_modules", "__pycache__"}
VERSION = re.compile(r"[0-9a-f]{16}")


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def essential_images(source):
    metadata = source / "art/sprites.json"
    if not metadata.is_file():
        raise FileNotFoundError("Native runtime is incomplete: art/sprites.json")
    data = json.loads(metadata.read_text(encoding="utf-8"))
    atlases = data.get("atlases")
    if not atlases:
        return ESSENTIAL_IMAGES + ("sprites.png",)
    if not isinstance(atlases, dict):
        raise ValueError("Sprite atlases must be an object of local PNG assets.")
    names = []
    for entry in atlases.values():
        name = entry.get("file") if isinstance(entry, dict) else None
        if (not isinstance(name, str) or not re.fullmatch(r"[A-Za-z0-9_][A-Za-z0-9_./-]*\.png", name)
                or ".." in Path(name).parts or Path(name).is_absolute()):
            raise ValueError("Sprite atlas paths must stay inside the local art folder.")
        names.append(name)
    return ESSENTIAL_IMAGES + tuple(sorted(set(names)))


def runtime_files(source):
    files = []
    active_images = set(essential_images(source))
    for path in sorted(source.rglob("*")):
        relative = path.relative_to(source)
        if path.is_symlink():
            raise ValueError(f"Runtime symlinks are not supported: {relative}")
        if (not path.is_file() or path.name in EXCLUDED
                or any(part in EXCLUDED_PARTS for part in relative.parts)
                or path.name.startswith(("test_", "tests_"))):
            continue
        if path.suffix not in {".js", ".css", ".woff2", ".png", ".json", ".txt"}:
            continue
        # Keep retained former atlases in authoring, but the immutable runtime
        # contains only current manifest sheets plus essential venue/owner art.
        if relative.parent == Path("art") and path.suffix == ".png" and path.name not in active_images:
            continue
        files.append(path)
    required = set(MODULES) | {"style.css", "font.woff2", "OFL.txt"}
    required.update("art/" + name for name in essential_images(source) + ESSENTIAL_METADATA)
    required.update("art/icons/" + name + ".png" for name in ICONS)
    missing = required - {path.relative_to(source).as_posix() for path in files}
    if missing:
        raise FileNotFoundError("Native runtime is incomplete: " + ", ".join(sorted(missing)))
    return files


def snapshot(source):
    files = runtime_files(source)
    entries = [{"path": path.relative_to(source).as_posix(), "bytes": path.stat().st_size,
                "sha256": digest(path)} for path in files]
    encoded = json.dumps(entries, separators=(",", ":"), ensure_ascii=True).encode()
    version = hashlib.sha256(encoded).hexdigest()[:16]
    return files, {"version": version, "algorithm": "sha256", "files": entries}


def assert_separate(source, output):
    if source == output or source.is_relative_to(output) or output.is_relative_to(source):
        raise ValueError("Native source and output folders must be separate.")


def prepare_native_pages(source=SOURCE, output=PROJECT / "docs", *, template=TEMPLATE,
                         streaming=PROJECT / "Assets/StreamingAssets", retain=2):
    source, output, template = Path(source).resolve(), Path(output).resolve(), Path(template).resolve()
    assert_separate(source, output)
    if retain < 1:
        raise ValueError("At least one native release must be retained.")
    files, manifest = snapshot(source)
    version = manifest["version"]
    output.mkdir(parents=True, exist_ok=True)
    releases = output / "FastGame"
    releases.mkdir(exist_ok=True)
    release = releases / version
    # Assemble a complete immutable release before making the HTML reference it.
    if not release.exists():
        with tempfile.TemporaryDirectory(prefix=".native-stage-", dir=releases) as temporary:
            stage = Path(temporary) / version
            stage.mkdir()
            for path in files:
                target = stage / path.relative_to(source)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, target)
            (stage / "runtime-manifest.json").write_text(
                json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
            for entry in manifest["files"]:
                if digest(stage / entry["path"]) != entry["sha256"]:
                    raise RuntimeError("Source changed while packaging; run packaging again.")
            stage.rename(release)
    else:
        for entry in manifest["files"]:
            if not (release / entry["path"]).is_file() or digest(release / entry["path"]) != entry["sha256"]:
                raise RuntimeError(f"Existing release {version} does not match its content hash.")
    template_data = output / "TemplateData"
    template_data.mkdir(exist_ok=True)
    stylesheet = template_data / "style.css"
    shutil.copy2(template / "style.css", stylesheet)
    favicon = PROJECT / "docs/TemplateData/favicon.ico"
    if favicon.is_file() and favicon.resolve() != (template_data / "favicon.ico").resolve():
        shutil.copy2(favicon, template_data / "favicon.ico")
    if streaming and Path(streaming).is_dir():
        for folder in ("ScoutPosters", "Music"):
            root = Path(streaming) / folder
            if not root.is_dir():
                continue
            for path in sorted(root.rglob("*")):
                if path.is_file() and not path.name.endswith(".meta"):
                    target = output / "StreamingAssets" / folder / path.relative_to(root)
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(path, target)
    runtime = "FastGame/" + version
    hints = [f'<link rel="modulepreload" href="{runtime}/{module}">' for module in MODULES]
    hints.append(f'<link rel="preload" as="font" type="font/woff2" crossorigin href="{runtime}/font.woff2">')
    for name in essential_images(source):
        if (source / "art" / name).is_file():
            hints.append(f'<link rel="preload" as="image" href="{runtime}/art/{html.escape(name, quote=True)}">')
    for name in ESSENTIAL_METADATA:
        if (source / "art" / name).is_file():
            hints.append(f'<link rel="preload" as="fetch" type="application/json" crossorigin href="{runtime}/art/{name}">')
    index = (source / "index.html").read_text(encoding="utf-8")
    index = (index.replace("__SIMBAR_RUNTIME__", runtime)
             .replace("__SIMBAR_ASSET_HINTS__", "\n  ".join(hints))
             .replace("__SIMBAR_STYLESHEET__", "TemplateData/style.css?v=" + digest(stylesheet)[:16]))
    if "__SIMBAR_" in index:
        raise RuntimeError("Unresolved native HTML template placeholder.")
    # Preserve unrelated project mockups, the Unity fallback and prior release.
    (output / "index.html").write_text(index, encoding="utf-8", newline="\n")
    (output / ".nojekyll").touch()
    previous = sorted((path for path in releases.iterdir() if path.is_dir()
                       and VERSION.fullmatch(path.name) and path != release
                       and (path / "runtime-manifest.json").is_file()),
                      key=lambda path: path.stat().st_mtime_ns, reverse=True)
    for old in previous[max(0, retain - 1):]:
        resolved = old.resolve()
        if resolved.parent != releases.resolve() or not VERSION.fullmatch(resolved.name):
            raise RuntimeError("Refusing to remove a release outside the native output folder.")
        shutil.rmtree(resolved)
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=SOURCE)
    parser.add_argument("--output", type=Path, default=PROJECT / "docs")
    args = parser.parse_args()
    output = args.output.resolve()
    if output == PROJECT or not output.is_relative_to(PROJECT):
        parser.error("Output must be a folder inside the project.")
    manifest = prepare_native_pages(args.source, output)
    print(f"Native game ready: {output}; release {manifest['version']}; "
          f"{sum(entry['bytes'] for entry in manifest['files']):,} runtime bytes")


if __name__ == "__main__":
    main()
