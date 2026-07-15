# Cloud Save Export Tool

`export_cloud_save.py` dumps **every player's Cloud Save data** (all 12 keys —
`player_profile`, `PLAYER_STATS_PROFILE`, `VESSEL_STATS`, `GAME_MODE_PROGRESSION`, …) to
JSONL files you can query locally with DuckDB, open in a spreadsheet, or join against
PostHog exports. Python 3.8+, standard library only — no pip installs.

It follows Unity's official bulk-export guidance (support article
[47770905934740](https://support.unity.com/hc/en-us/articles/47770905934740), verified
2026-07): enumerate players with data via the Cloud Save Admin API's *Get Players*
endpoint, then page each player's items. The APIs are free to call; the binding
constraint is the admin rate limit (**1,000 requests per 30 minutes** — the tool waits
and resumes automatically on 429).

## One-time setup (~5 minutes)

1. **Create a service account**: [Unity Cloud](https://cloud.unity.com) →
   **Administration → Service Accounts → New**. Name it e.g. `cloudsave-export`.
2. **Give it read access**: on the service account → *Project roles → Add project role* →
   select the Cosmic Shore project → role **Cloud Save Viewer** (read-only is all it needs).
3. **Create a key**: on the service account → *Keys → Create key*. Copy the **Key ID** and
   **Secret Key** (secret is shown once). Treat these like passwords — never commit them,
   never put them in the game client.
4. **Find the project ID**: Unity Cloud → project → Settings (a UUID, same one in
   `ProjectSettings/UnityConnectSettings.asset`).

## Usage

```bash
export UGS_KEY_ID=xxxxxxxx
export UGS_SECRET_KEY=xxxxxxxx

# See the project's environments (usually just 'production')
python3 export_cloud_save.py --project-id <PROJECT_ID> --list-environments

# Full export of every player's default-class data
python3 export_cloud_save.py --project-id <PROJECT_ID> --environment production --out export_$(date +%Y_%m_%d)

# Just specific players (comma list, or @file with one ID per line)
python3 export_cloud_save.py --project-id <PROJECT_ID> --players 7bKp...,9dQz...
```

Output:

- `players.jsonl` — one line per player: `player_id` + per-access-class `{numKeys, totalSize}` metadata.
- `items.jsonl` — one line per stored item: `player_id`, `access_class`, `key`,
  `value` (the saved object as a JSON string), `write_lock`, `modified`, `created`.

**How long it takes:** requests ≈ `players/100` (enumeration) + ~1 per player per 20 keys
(item pages). With the 1,000-per-30-min cap that's roughly **900 players per half-hour**;
small player counts finish in seconds. Player IDs come back alphabetically, so players who
join mid-export can be missed in one pass — re-run for a clean cut (the official caveat).

## Querying with DuckDB

[DuckDB](https://duckdb.org) is a single binary; `duckdb` in the export folder gives you a
SQL shell over the files:

```sql
-- Everything, flattened
SELECT * FROM read_json_auto('items.jsonl') LIMIT 10;

-- XP / crystal-balance leaderboard from player_profile
SELECT player_id,
       json_extract_string(value, '$.displayName')            AS name,
       CAST(json_extract(value, '$.xp') AS INT)               AS xp,
       CAST(json_extract(value, '$.crystalBalance') AS INT)   AS crystals
FROM read_json_auto('items.jsonl')
WHERE key = 'player_profile'
ORDER BY xp DESC LIMIT 25;

-- Install-date cohort sizes (firstSeenUtc is Unix epoch ms)
SELECT date_trunc('week', to_timestamp(CAST(json_extract(value, '$.firstSeenUtc') AS BIGINT) / 1000)) AS cohort_week,
       count(*) AS players
FROM read_json_auto('items.jsonl')
WHERE key = 'player_profile'
GROUP BY 1 ORDER BY 1;

-- Games played per vessel across the whole population (VESSEL_STATS)
SELECT v.key AS vessel,
       sum(CAST(json_extract(v.value, '$.GamesPlayed') AS INT)) AS games
FROM (
    SELECT player_id, je.key, je.value
    FROM read_json_auto('items.jsonl'),
         json_each(json_extract(value, '$.Vessels')) AS je
    WHERE key = 'VESSEL_STATS'
) v
GROUP BY 1 ORDER BY games DESC;

-- Join against a PostHog CSV export (distinct_id == player_id)
SELECT p.player_id, ph.event, ph.timestamp
FROM read_json_auto('items.jsonl') p
JOIN read_csv_auto('posthog_events.csv') ph ON ph.distinct_id = p.player_id
WHERE p.key = 'player_profile';
```

The key inventory and each key's JSON shape are documented in
`Docs/Analytics/DATA_INVENTORY.md` §1.

## Notes

- Only the **default** access class is exported by default — all Cosmic Shore keys live
  there. `--all-access-classes` adds public/protected/private where the metadata shows data.
- Reads may count against the Cloud Save free tier's 1M reads/month meter (undocumented
  either way); a full export of a few thousand players is thousands of reads — negligible.
- The credentials work for any environment in the project; exports are per-environment.
