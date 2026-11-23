# Analytics tools

| Folder or script | What it does |
|---|---|
| `server/` | **Froglet Analytics**, our own analytics backend: the game's events, the nightly numbers, the read API (`server/README.md`) |
| `console/` | The Froglet Analytics console page the server serves at `/` |
| `export_cloud_save.py` | Downloads every player's UGS Cloud Save data to files on your PC |

The rest of this page is the Cloud Save export. Plain Python, **standard library only, nothing to pip install**. It
talks to Unity's servers directly with an admin key: **no Unity editor, no game session, no consent dialog
involved.** The files it writes stay on your PC; query them with DuckDB (section 2).

---

## 0. One-time setup (~5 minutes of clicking)

**A. UGS service account** (the read-only admin credential):

1. Open [cloud.unity.com](https://cloud.unity.com) → **Administration → Service Accounts**
   → **New**. Name it `cloudsave-export`.
2. On the account → **Project roles → Add project role** → pick the Cosmic Shore project →
   role **Cloud Save Viewer**.
3. On the account → **Keys → Create key** → copy the **Key ID** and the **Secret Key**
   (the secret is shown only once). Treat both like passwords — never commit them, never
   put them in the game.
4. Also note the **Project ID** (a UUID): Unity Cloud → the project → Settings.

**B. Python on Windows** (skip if `py --version` already works): install from
[python.org/downloads](https://www.python.org/downloads/) — on the first installer
screen **tick "Add python.exe to PATH"**. Any Python 3.8+ is fine.

---

## 1. Windows: the whole flow, copy-paste

Open **Command Prompt** (Start menu → type `cmd` → Enter), then paste these blocks one at
a time. Replace only the three `YOUR_...` placeholders (no quotes needed unless the value
contains spaces — these never do).

**Go to the tools folder** (adjust the path to where the repo is on your disk):

```bat
cd /d C:\Projects\Cosmic-Shore\Tools\Analytics
```

**Set the credentials for this window** (`set` lasts until you close the window; nothing
is saved to disk):

```bat
set UGS_KEY_ID=YOUR_KEY_ID
set UGS_SECRET=YOUR_SECRET_KEY
set UGS_PROJECT_ID=YOUR_PROJECT_ID
```

**Sanity check — list the project's environments** (also proves the credentials work):

```bat
py export_cloud_save.py --list-environments
```

Expected output — one line per environment:

```
9f6e...-...-...   production
```

**Export everything** (uses the `production` environment by default; add
`--environment <name>` if your data lives elsewhere):

```bat
py export_cloud_save.py
```

Expected output:

```
  50 players, 214 items so far...
  100 players, 431 items so far...
Done. 137 players, 583 items.
  cloud_save_export\players.jsonl
  cloud_save_export\items.jsonl
```

If you see `rate limited (429): waiting 60s...` lines, that's normal — Unity's admin API
allows 1,000 requests per 30 minutes and the script waits and resumes by itself. A few
thousand players can take an hour or more; just leave the window open.

**Mac/Linux**: same commands with `python3` instead of `py` and `export VAR=value`
instead of `set VAR=value`.

---

## 2. Query the export locally with DuckDB

For the analyst: the full, unflattened data (per-intensity high scores, per-vessel
counters, …) is in `cloud_save_export/items.jsonl` (`value` is the saved object as a JSON
string). [DuckDB](https://duckdb.org) is a single free .exe — run it in the export folder:

```sql
-- XP / crystal leaderboard
SELECT player_id,
       json_extract_string(value, '$.displayName')          AS name,
       CAST(json_extract(value, '$.xp') AS INT)             AS xp,
       CAST(json_extract(value, '$.crystalBalance') AS INT) AS crystals
FROM read_json_auto('items.jsonl')
WHERE key = 'player_profile'
ORDER BY xp DESC LIMIT 25;

-- Weekly install cohorts (firstSeenUtc is epoch ms)
SELECT date_trunc('week', to_timestamp(CAST(json_extract(value, '$.firstSeenUtc') AS BIGINT) / 1000)) AS cohort_week,
       count(*) AS players
FROM read_json_auto('items.jsonl')
WHERE key = 'player_profile'
GROUP BY 1 ORDER BY 1;

-- Total games per vessel across everyone
SELECT v.key AS vessel,
       sum(CAST(json_extract(v.value, '$.GamesPlayed') AS INT)) AS games
FROM (
    SELECT player_id, je.key, je.value
    FROM read_json_auto('items.jsonl'),
         json_each(json_extract(value, '$.Vessels')) AS je
    WHERE key = 'VESSEL_STATS'
) v
GROUP BY 1 ORDER BY games DESC;
```

---

## 3. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `'py' is not recognized...` | Python isn't installed or not on PATH. Install from python.org with **"Add python.exe to PATH"** ticked, open a **new** cmd window, or try `python` instead of `py`. |
| `HTTP 401` / `HTTP 403` from the export | Wrong Key ID/Secret, or the service account doesn't have **Cloud Save Viewer** on *this* project (role is per-project!), or the secret was regenerated. Re-check §0A; `set` the vars again (typos count). |
| `Environment 'production' not found` | Run `py export_cloud_save.py --list-environments` and pass the right name via `--environment`. |
| Export finishes with `0 players` | Wrong environment (data usually lives in `production`, test data elsewhere), or wrong `--project-id` — verify the UUID against Unity Cloud → project → Settings. Players only appear here if they have saved data. |
| Lots of `rate limited (429)` waits | Normal. Unity caps the admin API at 1,000 requests/30 min. The script waits and continues; don't close the window. |

Security reminder: the Key ID/Secret pair grants read access to all player saves. Keep it
out of git, out of the game client, and out of screenshots. `set` variables vanish when
the cmd window closes — that's a feature.
