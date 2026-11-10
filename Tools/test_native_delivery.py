"""Native deployment contract: coherent cache versions and scoped output writes."""

import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import prepare_native_pages as native
import prepare_github_pages as unity


class NativeDeliveryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        self.source, self.template, self.output = root / "source", root / "template", root / "output"
        self.source.mkdir()
        self.template.mkdir()
        for name in native.MODULES:
            (self.source / name).write_text("export const fixture = 1;", encoding="utf-8")
        for name in ("style.css", "font.woff2", "OFL.txt"):
            (self.source / name).write_bytes(b"fixture")
        (self.source / "art").mkdir()
        for name in native.ESSENTIAL_IMAGES:
            (self.source / "art" / name).write_bytes(b"image fixture")
        for name in native.DEFERRED_IMAGES:
            target = self.source / "art" / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(b"desk scene fixture")
        (self.source / "art/sprites.png").write_bytes(b"legacy sprite fixture")
        for name in native.ESSENTIAL_METADATA:
            (self.source / "art" / name).write_text("{}")
        (self.source / "art/icons").mkdir()
        for name in native.ICONS:
            (self.source / "art/icons" / (name + ".png")).write_bytes(b"icon fixture")
        (self.source / "index.html").write_text(
            '<html>__SIMBAR_ASSET_HINTS__<link href="__SIMBAR_STYLESHEET__">'
            '<script type="module" src="__SIMBAR_RUNTIME__/boot.js"></script></html>', encoding="utf-8")
        (self.template / "style.css").write_bytes(b"host css")

    def package(self, **kwargs):
        return native.prepare_native_pages(self.source, self.output, template=self.template,
                                           streaming=None, **kwargs)

    def test_complete_immutable_release_is_referenced_by_html(self):
        manifest = self.package()
        release = self.output / "FastGame" / manifest["version"]
        self.assertEqual(json.loads((release / "runtime-manifest.json").read_text()), manifest)
        for entry in manifest["files"]:
            self.assertEqual(hashlib.sha256((release / entry["path"]).read_bytes()).hexdigest(), entry["sha256"])
        index = (self.output / "index.html").read_text()
        self.assertNotIn("__SIMBAR_", index)
        self.assertIn("FastGame/" + manifest["version"] + "/boot.js", index)
        self.assertIn('as="image"', index)

    def test_payload_change_versions_all_module_urls(self):
        first = self.package()
        (self.source / "core.js").write_bytes(b"changed gameplay")
        second = self.package()
        self.assertNotEqual(first["version"], second["version"])
        self.assertIn(second["version"], (self.output / "index.html").read_text())
        self.assertTrue((self.output / "FastGame" / first["version"] / "core.js").is_file())

    def test_package_is_reproducible_and_excludes_development_files(self):
        (self.source / "fixtures").mkdir()
        (self.source / "fixtures/legacy.json").write_text("{}")
        (self.source / "golden-fixtures.json").write_text("{}")
        (self.source / "world-pixel.js").write_text("obsolete renderer")
        first = self.package()
        self.assertEqual(self.package(), first)
        self.assertFalse(any("fixture" in entry["path"] or entry["path"] == "world-pixel.js"
                             for entry in first["files"]))

    def test_unrelated_files_and_nonrelease_directories_survive_pruning(self):
        self.output.mkdir()
        (self.output / "mobile_menu_mockup.html").write_text("user mockup")
        (self.output / "unity.html").write_text("fallback")
        (self.output / "FastGame").mkdir()
        unrelated = self.output / "FastGame/not-a-release"
        unrelated.mkdir()
        (unrelated / "keep.txt").write_text("user file")
        versions = []
        for i in range(3):
            (self.source / "core.js").write_text(str(i))
            versions.append(self.package(retain=1)["version"])
        self.assertFalse((self.output / "FastGame" / versions[0]).exists())
        self.assertEqual((unrelated / "keep.txt").read_text(), "user file")
        self.assertEqual((self.output / "mobile_menu_mockup.html").read_text(), "user mockup")
        self.assertEqual((self.output / "unity.html").read_text(), "fallback")

    def test_missing_runtime_does_not_replace_current_index(self):
        self.output.mkdir()
        (self.output / "index.html").write_text("working game")
        (self.source / "core.js").unlink()
        with self.assertRaises(FileNotFoundError):
            self.package()
        self.assertEqual((self.output / "index.html").read_text(), "working game")

    def test_missing_sprite_art_is_rejected_before_release(self):
        (self.source / "art/sprites.png").unlink()
        with self.assertRaisesRegex(FileNotFoundError, "sprites.png"):
            self.package()
        self.assertFalse((self.output / "index.html").exists())

    def test_owner_desk_scenes_are_packaged_without_startup_preloads(self):
        manifest = self.package()
        files = {entry["path"] for entry in manifest["files"]}
        index = (self.output / "index.html").read_text()
        for name in native.DEFERRED_IMAGES:
            self.assertIn("art/" + name, files)
            self.assertNotIn(name, index)

    def test_remote_music_does_not_copy_local_mp3s_into_new_delivery(self):
        streaming = self.source.parent / "streaming"
        (streaming / "Music").mkdir(parents=True)
        (streaming / "ScoutPosters").mkdir()
        audio = streaming / "Music/Example.mp3"
        audio.write_bytes(b"local original")
        (streaming / "ScoutPosters/example.png").write_bytes(b"poster")
        native.prepare_native_pages(self.source, self.output, template=self.template, streaming=streaming)
        self.assertTrue(audio.is_file())
        self.assertFalse((self.output / "StreamingAssets/Music").exists())
        self.assertTrue((self.output / "StreamingAssets/ScoutPosters/example.png").is_file())

    def test_missing_selected_owner_desk_scene_prevents_release(self):
        (self.source / "art" / native.DEFERRED_IMAGES[1]).unlink()
        with self.assertRaisesRegex(FileNotFoundError, "office/desk-edison.png"):
            self.package()
        self.assertFalse((self.output / "index.html").exists())

    def test_multidirectional_atlas_images_are_required_and_preloaded(self):
        (self.source / "art/sprites.png").unlink()
        (self.source / "art/sprites.json").write_text(json.dumps({"atlases": {
            "walk": {"file": "walk.png"}, "poses": {"file": "poses.png"}}}))
        (self.source / "art/walk.png").write_bytes(b"walk fixture")
        with self.assertRaisesRegex(FileNotFoundError, "poses.png"):
            self.package()
        (self.source / "art/poses.png").write_bytes(b"pose fixture")
        self.package()
        index = (self.output / "index.html").read_text()
        self.assertIn('/art/walk.png', index)
        self.assertIn('/art/poses.png', index)
        self.assertNotIn('/art/sprites.png', index)

    def test_atlas_paths_cannot_escape_art_folder(self):
        for name in ("../outside.png", "https://example.org/sprites.png", "C:/sprites.png"):
            (self.source / "art/sprites.json").write_text(json.dumps({"atlases": {"bad": {"file": name}}}))
            with self.assertRaisesRegex(ValueError, "inside the local art folder"):
                self.package()

    def test_overlapping_source_output_is_rejected(self):
        with self.assertRaises(ValueError):
            native.prepare_native_pages(self.source, self.source / "build", template=self.template)
        with self.assertRaises(ValueError):
            native.prepare_native_pages(self.source, self.source.parent, template=self.template)

    def test_corrupted_existing_immutable_release_is_rejected(self):
        first = self.package()
        (self.output / "FastGame" / first["version"] / "core.js").write_text("corruption")
        with self.assertRaisesRegex(RuntimeError, "does not match"):
            self.package()

    def test_combined_cli_preserves_unity_host_before_native_entry(self):
        def package_unity(source, output, **kwargs):
            output.mkdir()
            (output / "Build").mkdir()
            (output / "index.html").write_text("generated unity host")

        def package_native(*, output):
            self.assertEqual((output / "unity.html").read_text(), "generated unity host")
            (output / "index.html").write_text("native host")
            return {"version": "fixture"}

        with patch.object(unity, "PROJECT", self.source.parent), \
                patch("sys.argv", ["prepare_github_pages.py", "--source", str(self.source), "--output", str(self.output)]), \
                patch.object(unity, "package_player", side_effect=package_unity), \
                patch.object(native, "prepare_native_pages", side_effect=package_native):
            unity.main()
        self.assertEqual((self.output / "index.html").read_text(), "native host")

    def test_combined_cli_unity_only_skips_native_packaging(self):
        def package_unity(source, output, **kwargs):
            output.mkdir()
            (output / "Build").mkdir()
            (output / "index.html").write_text("generated unity host")

        with patch.object(unity, "PROJECT", self.source.parent), \
                patch("sys.argv", ["prepare_github_pages.py", "--source", str(self.source), "--output", str(self.output), "--unity-only"]), \
                patch.object(unity, "package_player", side_effect=package_unity), \
                patch.object(native, "prepare_native_pages", side_effect=AssertionError("Native packaging should be skipped")):
            unity.main()
        self.assertEqual((self.output / "index.html").read_text(), "generated unity host")


if __name__ == "__main__":
    unittest.main()
