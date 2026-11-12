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
| Film | `Tools/Build/story/shoot.py` | `shoot.json` (or `--best`: the best unfilmed moments), the footage index, `story_config.json` `camera` | a script for the Unity trailer director's inbox: per moment `replay FILE START`, `slowmo N`, that element's camera on the moment's own pilot, `record <item id>-<element>`; encoded clips with those names make the item showable for that element |
| Cut | `Tools/Build/trailer/cut.py` | `edl.json`, shots | the day's video and contact sheet |
| All of it | `Tools/Build/story/daily.py` | the above | `<root>/days/<YYYY-MM-DD>/` |

Every script has `--self-test`. `theater.py --write-enums` refreshes `enums.json`, which names vessels and modes when the scripts run outside a checkout (`C:\Claude\story\tools` on Gbox).

## The honesty rules (enforced in `story.py`)

1. Every on-screen claim is filled from raw counts ("7 OF 12 PLAYTESTERS RETURNED ON ANOTHER DAY"), and `story.json`
   lists the numbers each one used. The ranking score never appears on screen.
2. A beat needs at least three players behind it.
3. Footage is labelled real or staged; real wins when both exist, and the brief says how much of the cut is staged.
   No footage, no beat: it goes on the shoot list instead.
4. The story is re-decided every day; yesterday's headline is kept only when today's numbers still put it first,
   and the brief says so.
5. Every cut shows the four elements: PvP, maneuvering, destruction and creation. An element the beats miss is
   filled with its best clip (real first); one with no clip goes on the shoot list and the brief names it.

## The shot rules (Garrett, 2026-11-12, after the first real reel)

"The camera was way too close up so all I could see was a vessel. We need more slow motion, wide shots, etc. Show
PvP, maneuvering, destruction, and creation."

- **The four elements.** `moments.py` tags every moment: a near pass or a close finish is PvP; a turn-rate peak is
  maneuvering; prisms dying around a pilot is destruction (a raid, and PvP too, when they were a rival's); the
  pilot's own prisms going up behind it is creation. Destruction and creation come from the replay's prism stream.
  `shoot.py` picks round-robin across the four, so a list covers all of them before it repeats one.
- **Wide, never tight.** Each element has its own camera in `story_config.json` `camera`: a wide three-quarter for
  PvP (110 away) and destruction (90), a crane pulling up and back for creation so the trail is seen being drawn, a
  loose chase for maneuvering (55 back). `check_camera` refuses a chase closer than `min_chase_back` (40) or any
  other camera closer than `min_distance` (60).
- **Real slow motion.** The director's `slowmo N` steps 1/(60 N) s of game per filmed frame, so every frame is drawn:
  PvP x3, maneuvering x2, destruction x4, creation real time. The moment's peak is in the middle of the window.
- **The moment's own pilot.** The camera follows `local` for the recording's first human and `pilot:NAME` for any
  other, so a moment of the second pilot is not filmed on the first.

## The fun signals the game sends

| Signal | Where | Since |
|---|---|---|
| Came back to an activity on 2+ days, 3+ days, a week apart | `activity_returns` on the analytics server (`analytics_store._rebuild_activity_returns`) | 2026-11-12 |
| Rematch within 10 minutes, finished, quit mid-match | `plays` (`game_started`, `game_completed`, `match_abandoned`) | 2026-11-07 builds |
| Toybox entries, minutes, vessel switches | `freestyle_vessels`, `freestyle_switches` | 2026-11-06 builds |
| Session length, cold start vs resume | `sessions` | 2026-11-07 builds |
| A replay's analytics match | `ReplaySummary.MatchId` (stamped by `ReplayService` from `GameDataSO.MatchId`) | 2026-11-12 |
| Creation and destruction | the replay's prism stream (`TheaterWorld`: births and deaths with positions), read by `theater.py` | 2026-11-12 |
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

See the plan, and `PROPOSALS.md` here for the two that are Shombith's call (voyage events, collecting replays from playtesters' machines). Also not built: filming the shoot list in Unity without
a person, scoring the videos people rewatch, and the scheduled daily run.
