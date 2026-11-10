"""Validate the editable manifest before it reaches either music runtime."""

import json
from pathlib import Path
import tempfile
import unittest

from prepare_streaming_playlist import read_playlist, TRACK_IDS


class StreamingPlaylistTests(unittest.TestCase):
    def manifest(self, tracks):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        path = Path(temporary.name) / "playlist.json"
        path.write_text(json.dumps({"tracks": tracks}), encoding="utf-8")
        return path

    def tracks(self):
        return [{"id": key, "title": "Song", "artist": "Band",
                 "audio_url": f"https://audio.example.test/{key}%20Song.mp3"}
                for key in TRACK_IDS]

    def test_public_object_urls_and_encoded_filenames(self):
        _, tracks = read_playlist(self.manifest(self.tracks()))
        self.assertEqual(tracks[0]["filename"], "track_001 Song.mp3")
        self.assertIn("%20", tracks[0]["audio_url"])

    def test_bucket_root_is_not_an_audio_object(self):
        tracks = self.tracks()
        tracks[0]["audio_url"] = "https://audio.example.test"
        with self.assertRaisesRegex(ValueError, "full HTTPS MP3 object URL"):
            read_playlist(self.manifest(tracks))

    def test_reordering_cannot_silently_change_saved_song_indices(self):
        tracks = self.tracks()
        tracks[0], tracks[1] = tracks[1], tracks[0]
        with self.assertRaisesRegex(ValueError, "saved-game order"):
            read_playlist(self.manifest(tracks))


if __name__ == "__main__":
    unittest.main()
