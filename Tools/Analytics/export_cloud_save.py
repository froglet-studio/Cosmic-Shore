#!/usr/bin/env python3
"""
Export all player Cloud Save data from Unity Gaming Services to JSONL files.

Follows Unity's official bulk-export guidance (support article 47770905934740):
enumerate players via the Cloud Save Admin API's "Get Players" endpoint, then page
each player's items. Endpoints/limits verified against the Cloud Save Admin OpenAPI
spec, 2026-07 (see Tools/Analytics/README.md for setup + DuckDB recipes).

Requirements:
  - Python 3.8+ (standard library only).
  - A UGS Service Account (Unity Cloud -> Administration -> Service Accounts) with the
    project-level "Cloud Save Viewer" role.

Usage:
  export UGS_KEY_ID=... UGS_SECRET_KEY=...
  python3 export_cloud_save.py --project-id <PROJECT_ID> --list-environments
  python3 export_cloud_save.py --project-id <PROJECT_ID> --environment production --out export_2026_07_15

Outputs (in --out directory):
  players.jsonl  one line per player: id + per-access-class {numKeys, totalSize}
  items.jsonl    one line per stored item: player_id, access_class, key,
                 value (JSON string), write_lock, modified, created

Rate limits (Cloud Save Admin API): 60 req/s AND 1,000 requests per 30 minutes,
per project per IP. The tool handles 429 + Retry-After and keeps going; a large
export simply takes multiple 30-minute windows. Player IDs are returned
alphabetically, so players who join mid-export can be missed in a single pass —
re-run (or diff on `modified`) for a clean cut.
"""

import argparse
import base64
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

ADMIN_BASE = "https://services.api.unity.com"
ACCESS_CLASSES = ("default", "public", "protected", "private")


def build_auth_header(key_id: str, secret: str) -> str:
    token = base64.b64encode(f"{key_id}:{secret}".encode()).decode()
    return f"Basic {token}"


def request_json(url: str, auth_header: str, max_attempts: int = 8) -> dict:
    """GET url with retry on 429 (Retry-After honored) and 5xx (exponential backoff)."""
    backoff = 2.0
    for attempt in range(1, max_attempts + 1):
        req = urllib.request.Request(url, headers={
            "Authorization": auth_header,
            "Accept": "application/json",
            "User-Agent": "cosmic-shore-cloudsave-export/1.0",
        })
        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                return json.loads(resp.read().decode())
        except urllib.error.HTTPError as e:
            if e.code == 429:
                retry_after = e.headers.get("Retry-After")
                # The 30-minute window cap can impose long waits; default generously.
                wait = float(retry_after) if retry_after else 60.0
                print(f"  rate limited (429): waiting {wait:.0f}s "
                      f"(admin API allows 1,000 requests per 30 min)...", flush=True)
                time.sleep(wait)
                continue
            if 500 <= e.code < 600 and attempt < max_attempts:
                print(f"  server error {e.code}: retrying in {backoff:.0f}s...", flush=True)
                time.sleep(backoff)
                backoff = min(backoff * 2, 120)
                continue
            body = e.read().decode(errors="replace")[:500]
            raise SystemExit(f"HTTP {e.code} on {url}\n{body}")
        except urllib.error.URLError as e:
            if attempt < max_attempts:
                print(f"  network error ({e.reason}): retrying in {backoff:.0f}s...", flush=True)
                time.sleep(backoff)
                backoff = min(backoff * 2, 120)
                continue
            raise SystemExit(f"Network failure on {url}: {e.reason}")
    raise SystemExit(f"Gave up after {max_attempts} attempts on {url}")


def list_environments(project_id: str, auth_header: str) -> list:
    url = f"{ADMIN_BASE}/unity/v1/projects/{project_id}/environments"
    data = request_json(url, auth_header)
    return data.get("results", data.get("environments", data if isinstance(data, list) else []))


def resolve_environment_id(project_id: str, auth_header: str, env_name: str) -> str:
    for env in list_environments(project_id, auth_header):
        if env.get("name") == env_name:
            return env.get("id")
    raise SystemExit(f"Environment '{env_name}' not found. "
                     f"Run with --list-environments to see what exists.")


def iter_players(project_id: str, env_id: str, auth_header: str):
    """Yield player entries from the paginated Get Players endpoint (limit max 100)."""
    base = (f"{ADMIN_BASE}/cloud-save/v1/data/projects/{project_id}"
            f"/environments/{env_id}/players")
    start = None
    while True:
        params = {"limit": 100}
        if start:
            params["start"] = start
        page = request_json(f"{base}?{urllib.parse.urlencode(params)}", auth_header)
        results = page.get("results", [])
        for entry in results:
            yield entry
        if not results:
            return
        next_link = (page.get("links") or {}).get("next")
        if not next_link:
            return
        start = results[-1].get("id")


def iter_items(project_id: str, env_id: str, player_id: str, access_class: str, auth_header: str):
    """Yield items for one player + access class, following the `after` cursor (pages of 20)."""
    suffix = "items" if access_class == "default" else f"{access_class}/items"
    base = (f"{ADMIN_BASE}/cloud-save/v1/data/projects/{project_id}"
            f"/environments/{env_id}/players/{player_id}/{suffix}")
    after = None
    while True:
        url = f"{base}?{urllib.parse.urlencode({'after': after})}" if after else base
        page = request_json(url, auth_header)
        results = page.get("results", [])
        for item in results:
            yield item
        if not results or not (page.get("links") or {}).get("next"):
            return
        after = results[-1].get("key")


def main() -> None:
    parser = argparse.ArgumentParser(description="Export UGS Cloud Save player data to JSONL.")
    parser.add_argument("--project-id", default=os.environ.get("UGS_PROJECT_ID"),
                        help="UGS project ID (or env UGS_PROJECT_ID). Unity Cloud -> project settings.")
    parser.add_argument("--key-id", default=os.environ.get("UGS_KEY_ID"),
                        help="Service account key ID (or env UGS_KEY_ID).")
    parser.add_argument("--secret",
                        default=os.environ.get("UGS_SECRET_KEY") or os.environ.get("UGS_SECRET"),
                        help="Service account secret key (or env UGS_SECRET_KEY / UGS_SECRET).")
    parser.add_argument("--environment", default="production",
                        help="Environment NAME to export (default: production).")
    parser.add_argument("--environment-id", default=None,
                        help="Environment ID (skips the name lookup).")
    parser.add_argument("--list-environments", action="store_true",
                        help="Print the project's environments and exit.")
    parser.add_argument("--players", default=None,
                        help="Optional comma-separated player IDs (or @file with one per line) "
                             "to export instead of enumerating everyone.")
    parser.add_argument("--all-access-classes", action="store_true",
                        help="Also export public/protected/private items where present "
                             "(default: only the 'default' class — all Cosmic Shore keys live there).")
    parser.add_argument("--out", default="cloud_save_export",
                        help="Output directory (default: cloud_save_export).")
    args = parser.parse_args()

    if not args.project_id or not args.key_id or not args.secret:
        parser.error("--project-id, --key-id and --secret are required "
                     "(or set UGS_PROJECT_ID / UGS_KEY_ID / UGS_SECRET).")

    auth_header = build_auth_header(args.key_id, args.secret)

    if args.list_environments:
        for env in list_environments(args.project_id, auth_header):
            print(f"{env.get('id')}  {env.get('name')}")
        return

    env_id = args.environment_id or resolve_environment_id(args.project_id, auth_header, args.environment)
    os.makedirs(args.out, exist_ok=True)
    players_path = os.path.join(args.out, "players.jsonl")
    items_path = os.path.join(args.out, "items.jsonl")

    explicit_players = None
    if args.players:
        if args.players.startswith("@"):
            with open(args.players[1:], encoding="utf-8") as f:
                explicit_players = [line.strip() for line in f if line.strip()]
        else:
            explicit_players = [p.strip() for p in args.players.split(",") if p.strip()]

    player_count = 0
    item_count = 0
    request_estimate = 0

    with open(players_path, "w", encoding="utf-8") as players_file, \
         open(items_path, "w", encoding="utf-8") as items_file:
        if explicit_players is not None:
            player_entries = ({"id": pid, "accessClasses": None} for pid in explicit_players)
        else:
            player_entries = iter_players(args.project_id, env_id, auth_header)

        for entry in player_entries:
            player_id = entry["id"]
            player_count += 1
            players_file.write(json.dumps({
                "player_id": player_id,
                "access_classes": entry.get("accessClasses"),
            }) + "\n")

            meta = entry.get("accessClasses") or {}
            if args.all_access_classes:
                # Only touch classes the metadata says are non-empty (or all, if unknown).
                classes = [c for c in ACCESS_CLASSES
                           if not meta or (meta.get(c) or {}).get("numKeys", 1 if not meta else 0)]
            else:
                classes = ["default"]

            for access_class in classes:
                for item in iter_items(args.project_id, env_id, player_id, access_class, auth_header):
                    item_count += 1
                    items_file.write(json.dumps({
                        "player_id": player_id,
                        "access_class": access_class,
                        "key": item.get("key"),
                        # Value stored as a JSON *string* so DuckDB's read_json_auto keeps a
                        # stable schema across heterogeneous keys; parse with json_extract.
                        "value": json.dumps(item.get("value")),
                        "write_lock": item.get("writeLock"),
                        "modified": item.get("modified"),
                        "created": item.get("created"),
                    }) + "\n")
                request_estimate += 1

            if player_count % 50 == 0:
                print(f"  {player_count} players, {item_count} items so far...", flush=True)

    print(f"Done. {player_count} players, {item_count} items.")
    print(f"  {players_path}")
    print(f"  {items_path}")
    print("Reminder: alphabetical enumeration can miss players who joined mid-export — "
          "re-run for a clean cut. DuckDB recipes: Tools/Analytics/README.md")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        sys.exit("\nInterrupted — partial files are valid JSONL up to the last line.")
