"""Generate runtime music metadata from the project's playlist.json."""

import json
from pathlib import Path
from urllib.parse import unquote, urlsplit

PROJECT = Path(__file__).resolve().parent.parent
PLAYLIST = PROJECT / "playlist.json"
TRACK_IDS = tuple(f"track_{index:03d}" for index in range(1, 6))


def read_playlist(path=PLAYLIST):
    data = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    tracks = data.get("tracks")
    if not isinstance(tracks, list) or [track.get("id") for track in tracks] != list(TRACK_IDS):
        raise ValueError("playlist.json must keep track_001 through track_005 in their saved-game order.")
    runtime = []
    for track in tracks:
        if any(not isinstance(track.get(key), str) or not track[key].strip()
               for key in ("title", "artist", "audio_url")):
            raise ValueError(f"Missing title, artist or audio_url for {track['id']}.")
        address = urlsplit(track["audio_url"])
        if address.scheme != "https" or not address.netloc or not address.path.lower().endswith(".mp3"):
            raise ValueError(f"audio_url for {track['id']} must be a full HTTPS MP3 object URL.")
        runtime.append({**track, "filename": unquote(address.path.rsplit("/", 1)[-1])})
    return data, runtime


def write_if_changed(path, content):
    path = Path(path)
    if path.is_file() and path.read_text(encoding="utf-8") == content:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def prepare_streaming_playlist():
    data, tracks = read_playlist()
    encoded = json.dumps(tracks, ensure_ascii=False, indent=2)
    write_if_changed(PROJECT / "Tools/WebGLTemplate/FastGame/playlist.js",
                     "// Generated from /playlist.json by Tools/prepare_streaming_playlist.py.\n"
                     "// Keep numeric game indices stable; edit the root JSON, not this module.\n"
                     "export const streamingTracks = " + encoded + ";\n")
    write_if_changed(PROJECT / "Assets/StreamingAssets/playlist.json",
                     json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    return tracks


if __name__ == "__main__":
    tracks = prepare_streaming_playlist()
    print(f"Prepared {len(tracks)} remote tracks from {PLAYLIST}")
