"""Copy PostHog's history into the analytics server, once (Shombith, 2026-11-06: "what about the older data").

    sudo -u froglet-analytics env FROGLET_ANALYTICS_CONFIG=/var/lib/froglet-analytics/config.json \\
        POSTHOG_PERSONAL_API_KEY=phx_... \\
        /var/lib/froglet-analytics/venv/bin/python analytics_server.py import-posthog --project <id> [--dry-run]

What it does:
- asks PostHog's query API (HogQL) for every event the game sends (Docs/Analytics/EVENT_SCHEMA.json) and every
  $identify, one day at a time, oldest first, 10,000 rows a page;
- keeps each event's own properties and drops everything PostHog added (every "$" key except $set), so no IP address
  ($ip) or GeoIP detail is ever stored; the country comes only from PostHog's own lookup ($geoip_country_code), two
  letters, the same as live events;
- stores them through the same checks as live events, with their own timestamps (Store.ingest(trusted=True)). An
  event already stored is stored once, so running it again is safe. The double-sends of the #1639 bug are kept and
  flagged, exactly as they happened; every count collapses them.

The personal API key needs only the "query:read" scope. It is read from the environment, never from a file or the
command line (so it never lands in the shell history or the config).
"""
import json
import os
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

import analytics_store as S

PAGE = 10_000
DEFAULT_HOST = "https://eu.posthog.com"          # the project is in PostHog's EU cloud (DATA_ARCHITECTURE.md §8.3)


def _post(url, body, key, timeout=120):
    request = urllib.request.Request(url, data=json.dumps(body).encode(), method="POST", headers={
        "Content-Type": "application/json", "Authorization": f"Bearer {key}"})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return json.loads(response.read())


def _query(host, project, key, hogql, post=_post):
    out = post(f"{host.rstrip('/')}/api/projects/{project}/query/", {"query": {"kind": "HogQLQuery", "query": hogql}}, key)
    return out.get("columns") or [], out.get("results") or []


def to_item(event, distinct_id, timestamp, properties):
    """One PostHog row in the game's wire format, with PostHog's own additions removed."""
    if isinstance(properties, str):
        try:
            properties = json.loads(properties)
        except json.JSONDecodeError:
            properties = {}
    properties = properties or {}
    country = properties.get("$geoip_country_code")
    kept = {k: v for k, v in properties.items() if not k.startswith("$") or k == "$set"}
    kept["distinct_id"] = kept.get("distinct_id") or distinct_id
    if isinstance(timestamp, datetime):
        timestamp = timestamp.astimezone(timezone.utc).isoformat()
    item = {"event": event, "properties": kept, "timestamp": str(timestamp)}
    if isinstance(country, str) and len(country) == 2:
        item["country"] = country.upper()
    return item


def run(store, project, key, events, days=365, host=DEFAULT_HOST, dry_run=False, post=_post, log=print,
        retention_days=395):
    names = sorted(set(events) | {"$identify"})
    quoted = ", ".join("'" + n.replace("'", "") + "'" for n in names)
    today = datetime.now(timezone.utc).date()
    totals = {"rows": 0, "accepted": 0, "near_duplicates": 0, "resent": 0, "rejected": 0, "erased": 0}
    for back in range(days, -1, -1):
        day = (today - timedelta(days=back)).isoformat()
        offset = 0
        while True:
            hogql = (f"SELECT event, distinct_id, timestamp, properties FROM events "
                     f"WHERE toDate(timestamp) = '{day}' AND event IN ({quoted}) "
                     f"ORDER BY timestamp, uuid LIMIT {PAGE} OFFSET {offset}")
            columns, rows = _query(host, project, key, hogql, post=post)
            if not rows:
                break
            index = {c: i for i, c in enumerate(columns)} or {"event": 0, "distinct_id": 1, "timestamp": 2,
                                                              "properties": 3}
            items = [to_item(r[index["event"]], r[index["distinct_id"]], r[index["timestamp"]], r[index["properties"]])
                     for r in rows if r[index["event"]] in names]
            totals["rows"] += len(items)
            if not dry_run:
                for i in range(0, len(items), 1000):
                    result = store.ingest(items[i:i + 1000], trusted=True, max_age_days=retention_days)
                    for k, v in result.items():
                        totals[k] = totals.get(k, 0) + v
            if len(rows) < PAGE:
                break
            offset += PAGE
        if totals["rows"] and back % 7 == 0:
            log(f"  through {day}: {totals['rows']} rows read")
    return totals


def main(store, config, args):
    key = os.environ.get("POSTHOG_PERSONAL_API_KEY", "").strip()
    if not key:
        print("Set POSTHOG_PERSONAL_API_KEY (a PostHog personal API key with the query:read scope) first.")
        return 2
    import analytics_server as A
    try:
        totals = run(store, args.project, key, A.schema_events(config["schema_path"]), days=args.days,
                     host=args.host, dry_run=args.dry_run, retention_days=config["retention_days"])
    except urllib.error.HTTPError as err:
        print(f"PostHog answered {err.code}: {err.read().decode(errors='replace')[:300]}")
        print("401/403: the key is wrong or lacks query:read. 404: the project id is wrong.")
        return 1
    print(json.dumps(totals, indent=1))
    if args.dry_run:
        print("Dry run: nothing was stored.")
    return 0
