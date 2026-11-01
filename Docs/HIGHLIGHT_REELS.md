# Highlight reels from real play

Garrett, 2026-11-01: "we were still very unable to compose good gameplay. Let's start with what good and fun
gameplay looks like ... use people's replays to make highlight reels, then use the highlight reels we watch
multiple times as a scoring for how much we like them. Once you learn what highlights of real game footage
looks like you can start composing your own. Then you can learn how to make that work better with music."

The plan with its reasoning: https://claude.ai/artifact/SYifactoVUQBzDsXGmcGZV. The viewer:
**Reel Room**, https://claude.ai/artifact/XE5yXNJmsU5VK4YVwHmyDq (private to Garrett until shared).

## The loop

| Stage | What | Tool |
|---|---|---|
| 1. Mine | Find moments in real flights without rendering: near passes, overtakes, crystal snatches, stunts, close finishes. Hand weights to start. | `Tools/Build/highlights/mine_moments.py` |
| 2. Cut | Convert a flight to a match replay, play it in its real scene in Amoebius, film the moments at true game speed, join them with a cut list | `flight_to_replay.py`, `render_reel.py` |
| 3. Score | The Reel Room logs each viewer's plays, replays, rewinds, skips and per-second watch counts. Refit the weights from them. | Reel Room (`watch` collection), `refit_weights.py` |
| 4. Compose | Staged AI matches, filtered by the learned score, shown blind beside real reels. Only after the refit picker beats random. | not built |
| 5. Music | Peaks on downbeats, cuts on bars. Only after stage 4 passes. | not built |

## Where the real play comes from

Not the match replays (`Docs/REPLAYS.md`): those stay on the device that recorded them, and none were on Gbox
on 2026-11-01. The **flight records** are already shared: every race played in the editor is recorded by
`HumanFlightRecorder` (schema `cosmic-shore.flight/3`, 10 Hz: the pilot's pose, speed, presses and camera,
the rivals' positions, rings and velocities, the crystals, the race rings, the course seed) and pushed by
`BugHuntAutoSend` to `bugledger/<user>` under `BugLedger/shared/reports/<user>/flights/`.
`mine_moments.py --fetch-bugledger DIR` copies them all without checking anything out. On 2026-11-01: 269
flights, about 8 hours, from five people, 25 modes (Regatta 68, Switchback 51, Warpline 20, Skim Race 19...).

What a flight does not carry, so a re-filmed race differs from the original: the rivals' orientation (taken
from their velocity), their domains (the playable domains other than the pilot's), analog stick values, the
prism world and creatures (the replayed hulls lay their trails live; ecology rolls its own dice, REPLAYS 6.3).

## Commands

```
python3 Tools/Build/highlights/mine_moments.py --fetch-bugledger /tmp/flights
python3 Tools/Build/highlights/mine_moments.py /tmp/flights --out moments.json
python3 Tools/Build/highlights/flight_to_replay.py FLIGHT.json OUT_DIR       # .cstheater + .replay.json
python  Tools/Build/highlights/render_reel.py --moments moments.json --flights /tmp/flights \
        --recordings <the reels profile's Recordings folder> --out reels/ --count 6
python3 Tools/Build/highlights/refit_weights.py --watch watch.json --reels reels/ --out weights.json
python3 Tools/Build/highlights/mine_moments.py /tmp/flights --weights weights.json
```

Every script has `--self-test`. `render_reel.py` needs the Amoebius Debug player (the `replay` console command
lives in the DiagnosticsHUD registry, which Release strips) and ffmpeg; a cloud session can build Amoebius only
when its network allows the .NET download hosts, so reels are rendered on Gbox, in a worktree of their own.

When this checkout's Amoebius does not build (2026-11-01: the game had run ahead of the port engine since
2026-10-28), build the player in a second worktree at the last commit that builds and still has
`ReplayMatchDirector` (aeee5ad6b1 then), and pass `--game <that worktree>`: its player, assets and enums are used,
and moments in modes or hulls it lacks are skipped. Each picture costs about 5 game frames of control round-trips,
so filming runs at `--slow 0.4` (about 30 pictures a game second); the replay clock puts the clip back at true speed.
The player profile `reels` keeps its data in `%LOCALAPPDATA%/CosmicShore-reels` (Recordings under it).

## Rules

- **Reels rendered by Amoebius are for learning what to show.** Anything public is re-rendered from the same
  flight and cut list in Unity. Amoebius parity is measured on one explosion (loss 0.0199, 2026-11-01).
- **AI-only footage never counts as real play.** It is a separate pool in the viewer, and is scored apart.
- **The weights in `mine_moments.py` are a guess until the Reel Room has data.** `refit_weights.py` refuses to
  fit fewer than 8 watched moments.
- Reels and frames are never committed. They go to the project folder and the Reel Room's asset store.

## Ownership

Reads, never changes: the flight recorder and bug ledger (Yash), the Theater and replay format (Will Bogen,
Yash), Amoebius and its control port (Shombith). `flight_to_replay.py` writes the Theater's format from the
outside; if `TheaterRecording` changes version, its self-test is where that shows.
