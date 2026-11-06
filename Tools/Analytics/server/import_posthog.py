"""Copy PostHog's history into the analytics server, once (Shombith, 2026-11-06: "what about the older data").

    sudo -u froglet-analytics env FROGLET_ANALYTICS_CONFIG=/var/lib/froglet-analytics/config.json \\
        POSTHOG_PERSONAL_API_KEY=phx_... \\
        /var/lib/froglet-analytics/venv/bin/python analytics_server.py import-posthog --project <id> [--dry-run]

What it does:
- asks PostHog's query API (HogQL) for every event the game sends (Docs/Analytics/EVENT_SCHEMA.json) and every
  $identify, one day at a time, oldest first, 10,000 rows a page (paged by timestamp: PostHog refuses OFFSET for
  personal API keys);
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
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

import analytics_store as S

PAGE = 10_000
DEFAULT_HOST = "https://eu.posthog.com"          # the project is in PostHog's EU cloud (DATA_ARCHITECTURE.md §8.3)


def _post(url, body, key, timeout=120, sleep=time.sleep, tries=12):
    """One query. PostHog rate-limits personal API keys per hour: on 429 it waits (Retry-After, else a minute) and
    asks again, so a long import slows down instead of stopping."""
    for attempt in range(tries):
        request = urllib.request.Request(url, data=json.dumps(body).encode(), method="POST", headers={
            "Content-Type": "application/json", "Authorization": f"Bearer {key}"})
        try:
            with urllib.request.urlopen(request, timeout=timeout) as response:
                return json.loads(response.read())
        except urllib.error.HTTPError as err:
            if err.code != 429 or attempt == tries - 1:
                raise
            wait = err.headers.get("Retry-After") if err.headers else None
            seconds = int(wait) if wait and wait.isdigit() else 60
            print(f"  PostHog asked to slow down; waiting {seconds} s")
            sleep(seconds)


def _query(host, project, key, hogql, post=_post):
    out = post(f"{host.rstrip('/')}/api/projects/{project}/query/", {"query": {"kind": "HogQLQuery", "query": hogql}}, key)
    return out.get("columns") or [], out.get("results") or []


INSTANT = 10_000                                 # the most rows read for events sharing one timestamp


def _at(timestamp):
    return f"toDateTime('{str(timestamp).replace(chr(39), '')}')"


def _rows(host, project, key, post, day, quoted, condition, limit):
    hogql = (f"SELECT event, distinct_id, timestamp, properties FROM events "
             f"WHERE toDate(timestamp) = '{day}' AND event IN ({quoted}) {condition}"
             f"ORDER BY timestamp LIMIT {limit}")
    return _query(host, project, key, hogql, post=post)[1]


def _store(store, rows, names, totals, dry_run, retention_days):
    items = [to_item(r[0], r[1], r[2], r[3]) for r in rows if r[0] in names]
    totals["rows"] += len(items)
    if dry_run:
        return
    for i in range(0, len(items), 1000):
        result = store.ingest(items[i:i + 1000], trusted=True, max_age_days=retention_days)
        for k, v in result.items():
            totals[k] = totals.get(k, 0) + v


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
    totals = {"rows": 0, "accepted": 0, "near_duplicates": 0, "resent": 0, "rejected": 0, "erased": 0,
              "days": 0, "days_short": 0}
    # One count per day first: empty days are never asked for, and each day read is checked against PostHog's count.
    first = (today - timedelta(days=days)).isoformat()
    _, counted = _query(host, project, key,
                        f"SELECT toDate(timestamp) AS day, count() FROM events WHERE toDate(timestamp) >= '{first}' "
                        f"AND event IN ({quoted}) GROUP BY day ORDER BY day LIMIT 1000",
                        post=post)
    expected = {str(d)[:10]: int(n) for d, n in counted}
    for day in sorted(expected):
        before = totals["rows"]
        # Keyset paging on the timestamp: PostHog refuses OFFSET on queries made with a personal API key. A page
        # takes the next PAGE rows after the last timestamp read; when a page is full, the instant it ends on may
        # go on past the page, so that instant is read whole (up to INSTANT rows) before stepping past it.
        last = None
        while True:
            rows = _rows(host, project, key, post, day, quoted, f"AND timestamp > {_at(last)} " if last else "", PAGE)
            if not rows:
                break
            full = len(rows) >= PAGE
            if full:
                edge = rows[-1][2]
                kept = [r for r in rows if r[2] != edge]
                whole = _rows(host, project, key, post, day, quoted, f"AND timestamp = {_at(edge)} ", INSTANT)
                if len(whole) >= INSTANT:
                    log(f"  {day}: {INSTANT} or more events share the timestamp {edge}; only {INSTANT} were read")
                rows = kept + (whole or [r for r in rows if r[2] == edge])
            _store(store, rows, names, totals, dry_run, retention_days)
            if not full:
                break
            last = rows[-1][2]
        totals["days"] += 1
        if totals["rows"] - before != expected[day]:
            totals["days_short"] += 1
            log(f"  {day}: read {totals['rows'] - before} of the {expected[day]} events PostHog counts")
        if totals["days"] % 7 == 0:
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
    if totals.get("days_short"):
        print(f"{totals['days_short']} day(s) read fewer events than PostHog counts (listed above). Running it again "
              "is safe: what is already stored is stored once.")
    if args.dry_run:
        print("Dry run: nothing was stored.")
    return 0
