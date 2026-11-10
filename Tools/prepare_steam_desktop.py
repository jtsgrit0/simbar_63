"""Build the current native game as an isolated Windows x64 Electron player.

All downloaded tools, intermediate files and executables remain in ignored
Temp/ and Builds/. The source game is copied from its reviewed native release.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parent.parent
SHELL = ROOT / 'Tools/SteamDesktop'


def run(command, **kwargs):
    return subprocess.run([str(part) for part in command], check=True, **kwargs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--content', type=Path, default=ROOT / 'docs')
    parser.add_argument('--output', type=Path)
    parser.add_argument('--node', type=Path)
    parser.add_argument('--toolchain', type=Path, default=ROOT / 'Temp/SteamDesktopToolchain')
    args = parser.parse_args()
    node = args.node or shutil.which('node')
    if not node:
        retained = ROOT / 'Temp/SupabaseDatabaseTests/node/node-v26.13.0-win-x64/node.exe'
        node = retained if retained.is_file() else None
    if not node or not Path(node).is_file():
        raise SystemExit('Node.js 22.12+ is required. Provide --node <node.exe>.')
    node = Path(node).resolve()
    npm = node.parent / ('npm.cmd' if os.name == 'nt' else 'npm')
    if not npm.is_file():
        raise SystemExit('npm must be installed beside the selected Node executable.')
    toolchain = args.toolchain.resolve()
    toolchain.mkdir(parents=True, exist_ok=True)
    info = json.loads((SHELL / 'package.json').read_text(encoding='utf-8'))
    dependencies = {name: version for name, version in info['desktopToolchain'].items() if name != 'electron'}
    package = {'name': 'simbar-desktop-toolchain', 'private': True, 'version': '1.0.0', 'dependencies': dependencies}
    package_path = toolchain / 'package.json'
    desired = json.dumps(package, indent=2) + '\n'
    if not package_path.is_file() or package_path.read_text(encoding='utf-8') != desired:
        package_path.write_text(desired, encoding='utf-8', newline='\n')
    lock = SHELL / 'toolchain-lock.json'
    if lock.is_file():
        shutil.copy2(lock, toolchain / 'package-lock.json')
    env = dict(os.environ)
    env['PATH'] = str(node.parent) + os.pathsep + env.get('PATH', '')
    print('Preparing pinned Electron packaging toolchain...', flush=True)
    run([npm, 'install', '--prefix', toolchain, '--ignore-scripts', '--no-audit', '--no-fund'], env=env)
    command = [node, SHELL / 'build.mjs', '--content', args.content.resolve(), '--toolchain', toolchain]
    if args.output:
        command += ['--output', args.output.resolve()]
    try:
        built = run(command, env=env, capture_output=True, text=True)
    except subprocess.CalledProcessError as error:
        sys.stderr.write(error.stdout or '')
        sys.stderr.write(error.stderr or '')
        raise SystemExit(error.returncode) from error
    # Packager may log progress before its final JSON result.
    lines = built.stdout.strip().splitlines()
    result = json.loads(lines[-1])
    directory = Path(result['executable']).parent
    with (directory / 'resources/app.asar').open('rb') as source:
        result['shellAsarSha256'] = hashlib.file_digest(source, 'sha256').hexdigest()
    archive = directory.parent / ('SimBar-Windows-x64-' + result['runtime'] + '.zip')
    if archive.exists():
        raise FileExistsError('Existing archive retained: ' + str(archive))
    print('Creating Windows distribution archive...', flush=True)
    with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for file in sorted(directory.rglob('*')):
            if file.is_file():
                z.write(file, file.relative_to(directory))
    result['archive'] = str(archive)
    result['archiveBytes'] = archive.stat().st_size
    with archive.open('rb') as source:
        result['archiveSha256'] = hashlib.file_digest(source, 'sha256').hexdigest()
    manifest = directory.parent / 'build-manifest.json'
    manifest.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps({key: result[key] for key in ('runtime', 'electron', 'executable', 'archive', 'archiveBytes', 'archiveSha256')}, indent=2))


if __name__ == '__main__':
    main()
