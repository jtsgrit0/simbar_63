"""Convert the five licensed recordings into local-only HLS fixtures.

The input is a private directory with track_001.mp3 through track_005.mp3.
The destination must be Git-ignored. This script does not download or upload.
Run with: uv run --with imageio-ffmpeg==0.8.1 python Tools/AudioStream/prepare-local.py --source-dir <private-directory> --output-dir <new-ignored-directory>
"""
import argparse
import json
import math
import pathlib
import re
import subprocess
import sys

import imageio_ffmpeg

ROOT = pathlib.Path(__file__).resolve().parents[2]
ALLOWED = {f'track_{n:03}' for n in range(1, 6)}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source-dir', required=True, type=pathlib.Path)
    parser.add_argument('--output-dir', required=True, type=pathlib.Path)
    args = parser.parse_args()
    source_dir = args.source_dir.resolve(strict=True)
    destination = args.output_dir.resolve()
    if subprocess.run(['git', 'check-ignore', '-q', str(destination / 'source.mp3')], cwd=ROOT).returncode:
        raise RuntimeError('Local fixture destination must be ignored by Git')
    if destination.exists():
        raise RuntimeError('Output directory must be new; choose a fresh ignored directory')
    for ident in sorted(ALLOWED):
        source = source_dir / (ident + '.mp3')
        if not source.is_file() or source.stat().st_size == 0:
            raise RuntimeError(f'{ident}: private source missing or empty')
    executable = imageio_ffmpeg.get_ffmpeg_exe()
    for ident in sorted(ALLOWED):
        output = destination / 'store' / 'stream' / ident
        source = source_dir / (ident + '.mp3')
        manifest = destination / 'manifests' / (ident + '.m3u8')
        manifest.parent.mkdir(parents=True, exist_ok=True)
        output.mkdir(parents=True, exist_ok=True)
        command = [executable, '-hide_banner', '-loglevel', 'error', '-nostdin', '-y', '-i', str(source),
                   '-vn', '-map', '0:a:0', '-map_metadata', '-1', '-c:a', 'aac', '-b:a', '192k',
                   '-ar', '44100', '-ac', '2', '-hls_time', '4', '-hls_playlist_type', 'vod',
                   '-hls_segment_filename', str(output / 'segment-%05d.ts'), str(manifest)]
        if subprocess.run(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode:
            raise RuntimeError(f'{ident}: HLS conversion failed')
        text = manifest.read_text(encoding='utf-8')
        entries = re.findall(r'^#EXTINF:([0-9.]+),\s*\n(segment-\d{5}\.ts)$', text, re.MULTILINE)
        if '#EXT-X-ENDLIST' not in text or not entries:
            raise RuntimeError(f'{ident}: incomplete HLS manifest')
        durations = []
        for n, (value, filename) in enumerate(entries):
            duration = float(value)
            if filename != f'segment-{n:05}.ts' or not 0 < duration <= 60 or not (output / filename).is_file():
                raise RuntimeError(f'{ident}: invalid HLS segment')
            durations.append(duration)
        (output / 'index.json').write_text(json.dumps({'durations': durations}), encoding='utf-8')
        print(f'{ident}: {len(durations)} segments, {math.ceil(sum(durations))} seconds')


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(f'Local audio preparation failed: {error}', file=sys.stderr)
        sys.exit(1)
