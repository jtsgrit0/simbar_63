"""Serve the local Unity WebGL build with the compression headers its loader needs."""

import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import re
from urllib.parse import parse_qs, urlsplit


class UnityWebGLHandler(SimpleHTTPRequestHandler):
    def send_response(self, code, message=None):
        self._response_status = code
        super().send_response(code, message)

    def guess_type(self, path):
        plain = str(path)
        if plain.endswith(".unityweb"):
            return "application/octet-stream"
        for suffix in (".br", ".gz"):
            if plain.endswith(suffix):
                plain = plain[:-len(suffix)]
                break
        if plain.endswith(".wasm"):
            return "application/wasm"
        if plain.endswith(".js"):
            return "application/javascript"
        if plain.endswith(".data"):
            return "application/octet-stream"
        return super().guess_type(plain)

    def end_headers(self):
        request_url = urlsplit(self.path)
        path = request_url.path.lower()
        asset_response = getattr(self, "_response_status", 200) in (200, 304)
        if asset_response and path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        elif asset_response and path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        versions = parse_qs(request_url.query).get("v", [])
        versioned_asset = (asset_response and path.startswith("/build/")
                           and re.fullmatch(r"[0-9a-f]{32}\.(data|wasm|framework\.js)(\.unityweb)?", Path(path).name)
                           and len(versions) == 1 and re.fullmatch(r"[0-9a-f]{16}", versions[0]))
        self.send_header("Cache-Control", "public, max-age=31536000, immutable"
                         if versioned_asset else "no-cache")
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        super().end_headers()


def main():
    project = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--directory", type=Path,
                        default=project / "Builds" / "Pages" / "SimBarPlayerWebGL")
    args = parser.parse_args()
    folder = args.directory.resolve()
    if not (folder / "index.html").is_file():
        parser.error(f"No Unity WebGL index.html at {folder}. Finish the WebGL build first.")
    handler = partial(UnityWebGLHandler, directory=str(folder))
    server = ThreadingHTTPServer(("127.0.0.1", args.port), handler)
    print(f"SIMBAR: http://127.0.0.1:{args.port}/", flush=True)
    print(f"Serving {folder}; press Ctrl+C to stop.", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
