"""Create a Pages artifact without republishing the former public audio host.

This copies the site; it never changes or deletes the source files. The former
host is read from the existing legacy playlist instead of being placed in code
or build logs. The active runtime must already use the protected audio gateway.
"""

import argparse
import hashlib
import json
from pathlib import Path
import re
from urllib.parse import urlsplit


EXPECTED_IDS = {f"track_{n:03}" for n in range(1, 6)}
DISABLED_HOST = b"audio-disabled.invalid"


def prepare(source: Path, output: Path) -> None:
    source = source.resolve(strict=True)
    output = output.resolve()
    if not source.is_dir() or output.exists() or output == source or source in output.parents:
        raise ValueError("Output must be a new directory outside the source directory.")

    playlist = json.loads((source / "playlist.json").read_text(encoding="utf-8"))
    tracks = playlist.get("tracks")
    if not isinstance(tracks, list) or len(tracks) != 5 or {t.get("id") for t in tracks} != EXPECTED_IDS:
        raise ValueError("The legacy playlist must contain exactly the five approved track IDs.")
    urls = [urlsplit(t.get("audio_url", "")) for t in tracks]
    if (any(u.scheme != "https" or not u.hostname or u.username or u.password or u.query or u.fragment
            or not u.path.lower().endswith(".mp3") for u in urls)
            or len({u.hostname for u in urls}) != 1
            or len({u.path for u in urls}) != 5):
        raise ValueError("The legacy playlist must identify five distinct source files on one HTTPS host.")
    old_host = urls[0].hostname.encode("ascii")
    if old_host == DISABLED_HOST or not old_host.endswith(b".r2.dev"):
        raise ValueError("The former R2 host is missing or is not valid.")

    index = (source / "index.html").read_text(encoding="utf-8")
    active_versions = set(re.findall(r"FastGame/([0-9a-f]{16})/", index))
    if len(active_versions) != 1:
        raise ValueError("The published index must identify one active runtime.")
    active = next(iter(active_versions))
    runtime = source / "FastGame" / active
    manifest = json.loads((runtime / "runtime-manifest.json").read_text(encoding="utf-8"))
    if manifest.get("version") != active or manifest.get("algorithm") != "sha256":
        raise ValueError("The active runtime manifest is invalid.")
    for entry in manifest.get("files", []):
        path = Path(entry["path"])
        if path.is_absolute() or ".." in path.parts:
            raise ValueError("The active runtime contains an invalid manifest path.")
        data = (runtime / path).read_bytes()
        if old_host.lower() in data.lower():
            raise ValueError("The active runtime still references the former audio host.")
        if len(data) != entry["bytes"] or hashlib.sha256(data).hexdigest() != entry["sha256"]:
            raise ValueError("An active runtime file does not match its manifest.")
    if not manifest.get("files"):
        raise ValueError("The active runtime manifest is empty.")

    output.mkdir(parents=True, exist_ok=False)
    files = 0
    altered = 0
    found_paths = set()
    source_urls = [u.geturl().encode("utf-8") for u in urls]
    for path in source.rglob("*"):
        if path.is_symlink():
            raise ValueError("Pages source must not contain symbolic links.")
        rel = path.relative_to(source)
        dest = output / rel
        if path.is_dir():
            dest.mkdir(parents=True, exist_ok=True)
            continue
        if not path.is_file():
            raise ValueError("Pages source must contain only regular files and directories.")
        data = path.read_bytes()
        found_paths.update(i for i, address in enumerate(source_urls) if address in data)
        if old_host.lower() in data.lower():
            if runtime in path.parents:
                raise ValueError("The active runtime still references the former audio host.")
            if path.suffix.lower() not in {".html", ".js", ".json", ".css", ".txt"}:
                raise ValueError("A non-text asset contains the former audio host.")
            data, count = re.subn(re.escape(old_host), DISABLED_HOST, data, flags=re.IGNORECASE)
            if count:
                altered += 1
        if old_host.lower() in data.lower():
            raise ValueError("An output file still contains the former audio host.")
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(data)
        files += 1
    if found_paths != set(range(5)) or not altered:
        raise ValueError("The five legacy audio references were not all accounted for.")
    print(f"Prepared {files} files; sanitized {altered} legacy files; active runtime {active} verified.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=Path("docs"))
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    prepare(args.source, args.output)
