"""Verify the deployed public API with a publishable key, without creating data."""

import json
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parent.parent


def verify():
    config = {}
    for line in (ROOT / ".env").read_text(encoding="utf-8-sig").splitlines():
        if line.strip() and not line.lstrip().startswith("#") and "=" in line:
            name, value = line.split("=", 1)
            config[name.strip()] = value.strip()
    base = config["SUPABASE_URL"].rstrip("/")
    key = config["SUPABASE_PUBLISHABLE_KEY"]
    if not base.startswith("https://") or not key.startswith("sb_publishable_"):
        raise ValueError("Expected an HTTPS project URL and public publishable key")
    checks = []

    def request(path, body=None):
        headers = {"apikey": key}
        if body is not None:
            headers["Content-Type"] = "application/json"
        req = Request(base + path, data=None if body is None else json.dumps(body).encode(), headers=headers)
        try:
            with urlopen(req, timeout=30) as response:
                return response.status, json.loads(response.read())
        except HTTPError as error:
            return error.code, json.loads(error.read())

    def check(name, condition):
        if not condition:
            raise AssertionError(name)
        checks.append(name)

    status, tracks = request("/rest/v1/tracks?select=id,title,band_id&order=sort_order")
    check("Public catalogue returns five canonical tracks", status == 200 and
          [track["id"] for track in tracks] == [f"track_{i:03d}" for i in range(1, 6)])
    for table in ("rooms", "room_visits", "room_publications"):
        status, error = request(f"/rest/v1/{table}?select=*")
        check(f"Anonymous direct access to {table} is denied (HTTP {status})",
              status in (401, 403) and error.get("code") == "42501")
    status, directory = request("/rest/v1/rpc/list_public_rooms", {})
    check("Public directory RPC is available", status == 200 and isinstance(directory, list))
    status, room = request("/rest/v1/rpc/get_published_room", {"p_slug": "simbar-api-check-missing-slug"})
    check("Unknown share link reveals no draft", status == 200 and room is None)
    status, error = request("/rest/v1/rpc/create_room", {"p_template_id": "starter_club", "p_title": "Must be denied"})
    check(f"Anonymous room creation is denied (HTTP {status})",
          status in (401, 403) and error.get("code") == "42501")
    status, settings = request("/auth/v1/settings")
    check("Hosted Auth endpoint is reachable", status == 200)
    auth_flags = {name: settings.get(name) for name in ("disable_signup", "mailer_autoconfirm")}
    auth_flags["external"] = settings.get("external", {})
    # Public settings do not disclose site_url, redirect allowlists or password minimum.
    report = {"project_url": base, "passed": len(checks), "checks": checks, "public_auth_flags": auth_flags}
    destination = ROOT / "Temp/SupabaseSetup/public-api-verification.json"
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Supabase public API: {len(checks)} checks passed; no data created.")
    return report


if __name__ == "__main__":
    verify()
