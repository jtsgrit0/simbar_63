"""Run with: python -m unittest discover -s Tools -p 'test_*.py'."""

from functools import partial
import gzip
import hashlib
from http.client import HTTPConnection
from http.server import ThreadingHTTPServer
import json
from pathlib import Path
import os
import re
import shutil
import subprocess
import tempfile
import threading
import unittest
from unittest.mock import patch

import prepare_github_pages as pages
from serve_simbar import UnityWebGLHandler


class PackagingTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.source = Path(self.temporary.name) / "source"
        self.output = Path(self.temporary.name) / "output"
        (self.source / "Build").mkdir(parents=True)

    def make_build(self, compression=""):
        assets = {}
        for suffix, key in ((".data", "dataUrl"), (".framework.js", "frameworkUrl"),
                            (".wasm", "codeUrl"), (".loader.js", "loaderUrl")):
            name = "Fixture" + suffix + (compression if suffix != ".loader.js" else "")
            payload = ("fixture " + suffix).encode() + bytes(range(256))
            path = self.source / "Build" / name
            path.write_bytes(gzip.compress(payload) if name.endswith(".gz") else payload)
            assets[key] = (path, payload)
        references = ",\n".join(key + ': buildUrl + "/' + path.name + '"'
                                  for key, (path, _) in assets.items())
        (self.source / "index.html").write_text(
            'companyName: "Fixture Company", productName: "Fixture Game", '
            'productVersion: "1.2.3",\n' + references, encoding="utf-8")
        return assets

    def config(self):
        index = (self.output / "index.html").read_text(encoding="utf-8")
        return json.loads(re.search(r"const build = (\{.*?\n\});", index, re.S).group(1))

    def assert_packaged_assets(self, original, compression):
        config = self.config()
        for key, (source, expected) in original.items():
            url = config["loaderUrl"] if key == "loaderUrl" else config["config"][key]
            relative, version = url.split("?v=")
            target = self.output / relative
            self.assertEqual(target.read_bytes(), expected)
            self.assertEqual(version, hashlib.sha256(expected).hexdigest()[:16])
            self.assertTrue(target.name.endswith(compression) if compression and key != "loaderUrl"
                            else not target.name.endswith((".gz", ".br", ".unityweb")))
        self.assertEqual(config["config"]["productName"], "Fixture Game")
        self.assertEqual(config["config"]["companyName"], "Fixture Company")
        self.assertTrue((self.output / ".nojekyll").is_file())

    def test_fallback_bytes_preserved_and_old_artifacts_removed(self):
        assets = self.make_build(".unityweb")
        (self.output / "Build").mkdir(parents=True)
        # Both Unity and the deployment may contain files from an earlier build.
        (self.source / "Build/Fixture.data").write_bytes(b"stale source")
        (self.output / "Build/Fixture.data").write_bytes(b"stale deployed")
        (self.output / "Build/Old.wasm.br").write_bytes(b"stale compressed")
        with patch.object(pages, "decompressor", side_effect=AssertionError("Fallback must stay compressed")):
            pages.package_player(self.source, self.output)
        self.assert_packaged_assets(assets, ".unityweb")
        self.assertFalse((self.output / "Build/Fixture.data").exists())
        self.assertFalse((self.output / "Build/Old.wasm.br").exists())

    def test_raw_build_compatibility(self):
        assets = self.make_build()
        pages.package_player(self.source, self.output)
        self.assert_packaged_assets(assets, "")
        index = (self.output / "index.html").read_text(encoding="utf-8")
        self.assertIn('as="fetch" type="application/wasm" crossorigin="anonymous"', index)
        self.assertIn('href="' + self.config()["config"]["codeUrl"] + '"', index)
        self.assertNotIn("__SIMBAR_ASSET_HINTS__", index)

    def test_legacy_gzip_is_unpacked(self):
        assets = self.make_build(".gz")
        pages.package_player(self.source, self.output)
        self.assert_packaged_assets(assets, "")
        self.assertFalse(list((self.output / "Build").glob("*.gz")))

    def test_legacy_brotli_uses_decoder_and_raw_urls(self):
        assets = self.make_build(".br")
        with patch.object(pages, "decompressor", return_value=lambda source, target:
                          target.write_bytes(source.read_bytes())) as decoder:
            pages.package_player(self.source, self.output)
        decoder.assert_called_once_with(None)
        self.assert_packaged_assets(assets, "")

    def test_host_only_keeps_payload_and_hashes_existing_deployment(self):
        assets = self.make_build(".unityweb")
        pages.package_player(self.source, self.output)
        deployed = self.output / "Build" / assets["dataUrl"][0].name
        deployed.write_bytes(b"already deployed")
        with patch.object(pages, "decompressor", side_effect=AssertionError("Host-only must not decode")):
            pages.package_player(self.source, self.output, host_only=True)
        self.assertEqual(deployed.read_bytes(), b"already deployed")
        self.assertTrue(self.config()["config"]["dataUrl"].endswith(
            "?v=" + hashlib.sha256(b"already deployed").hexdigest()[:16]))

    def test_ambiguous_assets_fail_before_changing_deployment(self):
        self.make_build()
        (self.source / "Build/Other.data").write_bytes(b"ambiguous")
        (self.source / "index.html").write_text("no asset references", encoding="utf-8")
        with self.assertRaisesRegex(RuntimeError, "current .data asset"):
            pages.package_player(self.source, self.output)
        self.assertFalse(self.output.exists())


class QuietHandler(UnityWebGLHandler):
    def log_message(self, *args):
        pass


class CachePolicyTests(unittest.TestCase):
    def test_unity_caches_only_exact_hashed_build_urls_as_immutable(self):
        node = shutil.which("node")
        if not node:
            editors = Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Unity/Hub/Editor"
            installed = sorted(editors.glob(
                "*/Editor/Data/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/node/node.exe"), reverse=True)
            node = str(installed[0]) if installed else None
        if not node:
            self.skipTest("Node is required to execute the browser host cache policy")
        template = (pages.TEMPLATE / "index.html").read_text(encoding="utf-8")
        start = template.index("const immutableBuildUrls =")
        end = template.index("const loader =", start)
        script = """
const assert = require('node:assert/strict');
const build = {
  loaderUrl: 'Build/Game.loader.js?v=0123456789abcdef',
  config: {
    dataUrl: 'Build/0123456789abcdef0123456789abcdef.data.unityweb?v=0123456789abcdef',
    frameworkUrl: 'Build/0123456789abcdef0123456789abcdef.framework.js.unityweb?v=0123456789abcdef',
    codeUrl: 'Build/0123456789abcdef0123456789abcdef.wasm.unityweb?v=0123456789abcdef'
  }
};
const document = {baseURI: 'https://example.com/simbar/'};
""" + template[start:end] + """
for (const [url, expected] of [
  [build.config.dataUrl, 'immutable'],
  ['https://example.com/simbar/' + build.config.codeUrl, 'immutable'],
  ['Build/Game.data.unityweb', 'must-revalidate'],
  ['Build/Game.wasm.unityweb', 'no-store'],
  ['Build/Game.wasm.unityweb?v=0000000000000000', 'no-store'],
  ['Build/Game.data.unityweb?v=0123456789abcdef', 'must-revalidate'],
  [build.loaderUrl, 'no-store'],
  ['Build/0123456789abcdef0123456789abcdef.wasm.unityweb?v=0000000000000000', 'no-store'],
  ['https://elsewhere.com/simbar/' + build.config.codeUrl, 'no-store'],
  ['StreamingAssets/music.bundle', 'must-revalidate']
]) assert.equal(config.cacheControl(url), expected, url);
"""
        result = subprocess.run([node, "-e", script], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)


class ServerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory()
        root = Path(cls.temporary.name)
        (root / "Build").mkdir()
        for name in ("test.wasm", "test.data.br", "test.data.gz", "test.data.unityweb",
                     "0123456789abcdef0123456789abcdef.data.unityweb"):
            (root / "Build" / name).write_bytes(b"unchanged transfer bytes")
        (root / "index.html").write_text("fixture", encoding="utf-8")
        cls.server = ThreadingHTTPServer(("127.0.0.1", 0), partial(QuietHandler, directory=str(root)))
        cls.thread = threading.Thread(target=lambda: cls.server.serve_forever(poll_interval=0.05), daemon=True)
        cls.thread.start()

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.thread.join()
        cls.temporary.cleanup()

    def request(self, url):
        connection = HTTPConnection("127.0.0.1", self.server.server_port)
        try:
            connection.request("GET", url)
            response = connection.getresponse()
            body = response.read()
            self.assertEqual(response.status, 200)
            return response, body
        finally:
            connection.close()

    def test_fallback_is_binary_without_http_content_encoding(self):
        response, body = self.request("/Build/0123456789abcdef0123456789abcdef.data.unityweb?v=0123456789abcdef")
        self.assertEqual(body, b"unchanged transfer bytes")
        self.assertIsNone(response.getheader("Content-Encoding"))
        self.assertEqual(response.getheader("Content-Type"), "application/octet-stream")
        self.assertEqual(response.getheader("Cache-Control"), "public, max-age=31536000, immutable")

    def test_raw_wasm_and_legacy_compression_headers(self):
        response, _ = self.request("/Build/test.wasm")
        self.assertEqual(response.getheader("Content-Type"), "application/wasm")
        for extension, encoding in (("br", "br"), ("gz", "gzip")):
            with self.subTest(extension=extension):
                response, _ = self.request("/Build/test.data." + extension)
                self.assertEqual(response.getheader("Content-Encoding"), encoding)
                self.assertEqual(response.getheader("Content-Type"), "application/octet-stream")

    def test_unversioned_and_invalid_versions_revalidate(self):
        for url in ("/", "/index.html?v=0123456789abcdef", "/Build/test.wasm",
                    "/Build/test.wasm?v=latest", "/Build/test.wasm?v=0123456789abcdef",
                    "/Build/test.wasm?v=0123456789abcdef&v=duplicate",
                    "/Build/0123456789abcdef0123456789abcdef.data.unityweb?v=latest"):
            with self.subTest(url=url):
                response, _ = self.request(url)
                self.assertEqual(response.getheader("Cache-Control"), "no-cache")

    def test_missing_compressed_asset_is_not_cached_or_encoded(self):
        connection = HTTPConnection("127.0.0.1", self.server.server_port)
        try:
            connection.request("GET", "/Build/missing.data.br?v=0123456789abcdef")
            response = connection.getresponse()
            response.read()
            self.assertEqual(response.status, 404)
            self.assertEqual(response.getheader("Cache-Control"), "no-cache")
            self.assertIsNone(response.getheader("Content-Encoding"))
        finally:
            connection.close()


if __name__ == "__main__":
    unittest.main()
