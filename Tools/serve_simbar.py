"""Serve the local Unity WebGL build with the compression headers its loader needs."""

import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit


class UnityWebGLHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        plain = str(path)
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
        path = urlsplit(self.path).path.lower()
        if path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        elif path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        super().end_headers()


def main():
    project = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--directory", type=Path,
                        default=project / "Builds" / "SimBarPlayerWebGL")
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
