#!/usr/bin/env python3
"""
One-time import of exported Cloud Save data into PostHog as per-player snapshots.

Reads the items.jsonl produced by export_cloud_save.py, merges each player's saved
keys, and sends ONE `cloud_save_snapshot` event per player to PostHog's /batch/
capture endpoint — with the useful fields flattened as event properties AND set as
person properties ($set), so People, cohorts, filters, and SQL all work over the
historical player base immediately. distinct_id is the UGS PlayerId, identical to
what the in-game PostHog sink sends, so future live events land on the same persons.

This is a snapshot (timestamped now), not fabricated history: Cloud Save holds
current state, not an event log, so there is nothing meaningful to backdate.
Re-running after a fresh export just writes a newer snapshot event per player.

Requirements: Python 3.8+, standard library only.

Usage:
  export POSTHOG_API_KEY=phc_...
  python3 import_snapshot_to_posthog.py --items export_2026_07_15/items.jsonl
  python3 import_snapshot_to_posthog.py --items .../items.jsonl --dry-run   # inspect first
"""

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request

DOTNET_EPOCH_TICKS = 621_355_968_000_000_000  # .NET ticks at 1970-01-01 (100ns units)


def parse_value(raw):
    """items.jsonl stores each saved object as a JSON string; tolerate raw objects too."""
    if isinstance(raw, str):
        try:
            return json.loads(raw)
        except (json.JSONDecodeError, ValueError):
            return None
    return raw if isinstance(raw, dict) else None


def ticks_to_iso(ticks):
    try:
        ticks = int(ticks)
        if ticks <= DOTNET_EPOCH_TICKS:
            return None
        return iso_from_unix((ticks - DOTNET_EPOCH_TICKS) / 10_000_000)
    except (TypeError, ValueError, OverflowError):
        return None


def epoch_ms_to_iso(ms):
    try:
        ms = int(ms)
        if ms <= 0:
            return None
        return iso_from_unix(ms / 1000)
    except (TypeError, ValueError, OverflowError):
        return None


def iso_from_unix(seconds):
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(seconds))


def build_snapshot(saves: dict) -> dict:
    """Flatten one player's merged Cloud Save keys into snapshot properties.
    Every field is optional — players may hold any subset of keys."""
    props = {"snapshot_source": "cloud_save_export", "keys_present": sorted(saves.keys())}

    profile = saves.get("player_profile") or {}
    if profile:
        props["name"] = profile.get("displayName")
        props["avatar_id"] = profile.get("avatarId")
        props["xp"] = profile.get("xp")
        props["crystal_balance"] = profile.get("crystalBalance")
        props["first_seen"] = epoch_ms_to_iso(profile.get("firstSeenUtc"))
        rewards = profile.get("unlockedRewardIds")
        props["rewards_unlocked"] = len(rewards) if isinstance(rewards, list) else 0

    stats = saves.get("PLAYER_STATS_PROFILE") or {}
    if stats:
        props["last_login"] = ticks_to_iso(stats.get("LastLoginTick"))

    vessels = (saves.get("VESSEL_STATS") or {}).get("Vessels") or {}
    if isinstance(vessels, dict) and vessels:
        games_by_vessel = {}
        for vessel, v in vessels.items():
            if isinstance(v, dict):
                try:
                    games_by_vessel[vessel] = int(v.get("GamesPlayed") or 0)
                except (TypeError, ValueError):
                    games_by_vessel[vessel] = 0
        if games_by_vessel:
            props["total_games"] = sum(games_by_vessel.values())
            props["favorite_vessel"] = max(games_by_vessel, key=games_by_vessel.get)

    progression = saves.get("GAME_MODE_PROGRESSION") or {}
    if progression:
        modes = progression.get("UnlockedModes")
        if isinstance(modes, list):
            props["unlocked_modes_count"] = len(modes)
            props["unlocked_modes"] = ",".join(str(m) for m in modes)
        intensities = progression.get("MaxUnlockedIntensity")
        if isinstance(intensities, dict) and intensities:
            try:
                props["max_intensity_unlocked"] = max(int(v) for v in intensities.values())
            except (TypeError, ValueError):
                pass
        plays = progression.get("IntensityPlayCounts")
        if isinstance(plays, dict) and plays:
            try:
                props["recorded_play_count"] = sum(int(v) for v in plays.values())
            except (TypeError, ValueError):
                pass

    hangar = saves.get("HANGAR_DATA") or {}
    if hangar:
        unlocked = hangar.get("UnlockedVessels")
        if isinstance(unlocked, list):
            props["vessels_unlocked"] = len(unlocked)
        props["selected_vessel"] = hangar.get("SelectedVessel")

    return {k: v for k, v in props.items() if v is not None}


def post_batch(host: str, api_key: str, events: list, max_attempts: int = 6) -> bool:
    """Send one batch. Returns True on success, False after exhausting retries on
    transient (5xx / network) failures. 4xx aborts the whole run — a bad key or
    malformed payload would fail every batch identically."""
    payload = json.dumps({"api_key": api_key, "batch": events}).encode()
    backoff = 2.0
    for attempt in range(1, max_attempts + 1):
        req = urllib.request.Request(
            f"{host.rstrip('/')}/batch/", data=payload, method="POST",
            headers={"Content-Type": "application/json",
                     "User-Agent": "cosmic-shore-snapshot-import/1.0"})
        try:
            with urllib.request.urlopen(req, timeout=30):
                return True
        except urllib.error.HTTPError as e:
            if 400 <= e.code < 500:
                body = e.read().decode(errors="replace")[:500]
                raise SystemExit(f"PostHog rejected the batch (HTTP {e.code}) — check the "
                                 f"project API key (phc_...) and host region "
                                 f"(EU vs US).\n{body}")
            if attempt == max_attempts:
                print(f"  batch failed (HTTP {e.code}) after {max_attempts} attempts — skipping.",
                      flush=True)
                return False
        except urllib.error.URLError as e:
            if attempt == max_attempts:
                print(f"  batch failed (network: {e.reason}) after {max_attempts} attempts — skipping.",
                      flush=True)
                return False
        print(f"  transient send failure, retrying in {backoff:.0f}s...", flush=True)
        time.sleep(backoff)
        backoff = min(backoff * 2, 60)
    return False


def main() -> None:
    parser = argparse.ArgumentParser(description="Import Cloud Save export into PostHog as snapshots.")
    parser.add_argument("--items", default=os.path.join("cloud_save_export", "items.jsonl"),
                        help="Path to items.jsonl from export_cloud_save.py "
                             "(default: cloud_save_export/items.jsonl — the export's default output).")
    parser.add_argument("--posthog-key", default=os.environ.get("POSTHOG_API_KEY"),
                        help="PostHog Project API key, phc_... (or env POSTHOG_API_KEY).")
    parser.add_argument("--host", default="https://eu.i.posthog.com",
                        help="PostHog ingestion host (default EU; use https://us.i.posthog.com for US).")
    parser.add_argument("--batch-size", type=int, default=100)
    parser.add_argument("--dry-run", action="store_true",
                        help="Print the first 3 snapshot payloads and totals; send nothing.")
    args = parser.parse_args()

    if not args.dry_run and not args.posthog_key:
        parser.error("--posthog-key required (or set POSTHOG_API_KEY), or use --dry-run.")

    if not os.path.exists(args.items):
        raise SystemExit(f"Export file not found: {args.items}\n"
                         f"Run export_cloud_save.py first (see Tools/Analytics/README.md), "
                         f"or pass --items <path-to-items.jsonl>.")

    # Merge items per player: {player_id: {save_key: parsed_value}}
    players = {}
    with open(args.items, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            item = json.loads(line)
            if item.get("access_class") not in (None, "default"):
                continue
            value = parse_value(item.get("value"))
            if value is None:
                continue
            players.setdefault(item["player_id"], {})[item["key"]] = value

    timestamp = iso_from_unix(time.time())
    events = []
    for player_id, saves in players.items():
        props = build_snapshot(saves)
        props["distinct_id"] = player_id
        # Person properties: make the same fields filterable on People/cohorts.
        props["$set"] = {k: v for k, v in props.items()
                         if k not in ("distinct_id", "$set", "snapshot_source", "keys_present")}
        events.append({"event": "cloud_save_snapshot", "timestamp": timestamp, "properties": props})

    if args.dry_run:
        for evt in events[:5]:
            print(json.dumps(evt, indent=2))
        print(f"\nDry run: would send {len(events)} snapshot event(s) to {args.host} "
              f"in {(len(events) + args.batch_size - 1) // args.batch_size} batch(es). "
              f"No network calls were made.")
        return

    batches_sent = 0
    batches_failed = 0
    players_sent = 0
    for i in range(0, len(events), args.batch_size):
        batch = events[i:i + args.batch_size]
        if post_batch(args.host, args.posthog_key, batch):
            batches_sent += 1
            players_sent += len(batch)
        else:
            batches_failed += 1
        print(f"  progress: {min(i + args.batch_size, len(events))}/{len(events)} processed", flush=True)

    print(f"Done: {players_sent} player snapshot(s) sent in {batches_sent} batch(es), "
          f"{batches_failed} batch(es) failed.")
    if batches_failed:
        sys.exit("Some batches failed — re-running the import is safe (players get a fresh "
                 "snapshot event; queries should use the latest per player).")
    print("They appear in PostHog → Activity as 'cloud_save_snapshot', and each player is "
          "now a Person with filterable properties.")


if __name__ == "__main__":
    main()
