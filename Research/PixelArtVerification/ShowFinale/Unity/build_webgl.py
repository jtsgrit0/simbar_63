"""Invoke the connected Unity build with an explicit scene and target."""
import json
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[4]
dry_run = "--run" not in sys.argv
command = [
    str(Path.home() / "AppData/Local/Unity/bin/unity.exe"),
    "command", "build", "--port", "7800", "--json",
    "--target", "WebGL",
    "--outputPath", "Builds/Pages/SimBarPlayerWebGL",
    "--options", json.dumps(["DetailedBuildReport"]),
    "--scenes", json.dumps(["Assets/Scenes/SimBar_Reference.unity"]),
    "--confirm", "true", "--dry_run", str(dry_run).lower(),
]
result = subprocess.run(command, cwd=root, capture_output=True, text=True, encoding="utf-8")
name = "build-dry-run.json" if dry_run else "build-queued.json"
Path(__file__).with_name(name).write_text(result.stdout, encoding="utf-8")
print(result.stdout)
if result.stderr:
    print(result.stderr, file=sys.stderr)
sys.exit(result.returncode)
