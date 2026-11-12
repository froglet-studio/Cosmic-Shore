# The story flywheel: from today's playtests to today's video

Garrett, 2026-11-12: "lay the foundations so you can collect all the data you need to see where people are actually
having fun. What are they returning to? Use that footage to tell an honest story about what you measure the game to
be over weeks of play ... we are producing new videos from an optimized, informed, updated, and automated process
every day. If yesterday's story is the best we can do that is fine, but that should come from an analysis today."

Cosmic Shore is a new game with toy games (the Toybox) and voyages; the arcade is its party mode. The video tells
that story from what players actually do, not from what we hope they do.

The phased plan and its decisions: the Claude Doc linked from the `/story-flywheel` skill. This file is the how-to.

## The loop, once a day

```
playtests ──> analytics server (events) ──> measure.py ─┐
    │                                                    ├─> story.py ──> edl.json ──> trailer/cut.py ──> story-<day>.mp4
    └──> match replays (.cstheater) ──> footage.py ──────┘        │
                                          ▲                       └─> shoot.json ──> Unity TrailerShoot (Gbox) ──> clips/
                                          └──────────────────────────────────────────────────────────────────────┘
```

| Stage | Script | Reads | Writes |
|---|---|---|---|
| Measure | `Tools/Build/story/measure.py` | the analytics server's `aggregates.db` (no player ids): `activity_returns`, `plays`, `freestyle_vessels`, `sessions` | `measure.json`: per mode, vessel, the Toybox and each Toybox vessel, who came back on another day, a week later, rematches, quits, hours, trend; ranked by `pull` |
| Index | `Tools/Build/story/footage.py` | match replays (`theater.py`, moments by `moments.py`), staged Unity shots (`shots.json`), mined flight moments, re-filmed clips | `footage_index.json`: every piece of footage tagged real or staged, showable or needing a re-film; ratings and notes survive re-indexing |
| Decide | `Tools/Build/story/story.py` (+ `story_config.json`) | measure, index, yesterday's `story.json` | `story.json`, `edl.json`, `shoot.json`, `brief.md` |
| Cut | `Tools/Build/trailer/cut.py` | `edl.json`, shots | the day's video and contact sheet |
| All of it | `Tools/Build/story/daily.py` | the above | `<root>/days/<YYYY-MM-DD>/` |

Every script has `--self-test`.

## The honesty rules (enforced in `story.py`)

1. Every on-screen claim is filled from raw counts ("7 OF 12 PLAYTESTERS RETURNED ON ANOTHER DAY"), and `story.json`
   lists the numbers each one used. The ranking score never appears on screen.
2. A beat needs at least three players behind it.
3. Footage is labelled real or staged; real wins when both exist, and the brief says how much of the cut is staged.
   No footage, no beat: it goes on the shoot list instead.
4. The story is re-decided every day; yesterday's headline is kept only when today's numbers still put it first,
   and the brief says so.

## The fun signals the game sends

| Signal | Where | Since |
|---|---|---|
| Came back to an activity on 2+ days, 3+ days, a week apart | `activity_returns` on the analytics server (`analytics_store._rebuild_activity_returns`) | 2026-11-12 |
| Rematch within 10 minutes, finished, quit mid-match | `plays` (`game_started`, `game_completed`, `match_abandoned`) | 2026-11-07 builds |
| Toybox entries, minutes, vessel switches | `freestyle_vessels`, `freestyle_switches` | 2026-11-06 builds |
| Session length, cold start vs resume | `sessions` | 2026-11-07 builds |
| A replay's analytics match | `ReplaySummary.MatchId` (stamped by `ReplayService` from `GameDataSO.MatchId`) | 2026-11-12 |
| Voyages | **nothing yet** (plan phase 1) | |

## Running it

On Gbox (footage lives there; check for the overnight Claude instance first and keep to your own folders):

```
python3 Tools/Build/story/daily.py --root C:\Claude\story --shots C:\Claude\trailer\shots \
    --shots C:\Claude\trailer\shots-v2 --shots C:\Claude\trailer\shots-v3 \
    --flight-moments C:\Claude\reels\moments.json --replays <Recordings folders> \
    --api https://analytics.130-210-34-146.sslip.io --cut --cut-shots C:\Claude\trailer\shots
```

`--api` needs `FROGLET_ANALYTICS_KEY` (an admin key: `/v1/query` is admin-only). `--db aggregates.db` reads a copy of
the file instead; `--measure measure.json` reuses a measurement.

## What is not built yet

See the plan: voyage events, collecting replays from playtesters' machines, filming the shoot list in Unity without
a person, scoring the videos people rewatch, and the scheduled daily run.
