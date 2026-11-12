# Story flywheel: two proposals for Shombith (2026-11-12)

The daily story (`README.md` here) measures what playtesters come back to. Two things it cannot see are Shombith's to
decide: voyages send no analytics, and playtesters' match replays never leave their machines. Both are proposals; no
code for either is on any branch. Garrett asked for the flywheel; nothing here changes the live backend.

## 1. Voyage events

Voyages are half of what the game is (toy games and voyages; the arcade is the party mode), and today the analytics
server sees a voyage as nothing at all. Four events would let the story say "they came back to their voyage".

| Event | When (hook) | Properties | What the story reads from it |
|---|---|---|---|
| `voyage_started` | `VoyageRun`, when a run starts (the squad from `SquadLaunchIntent`, ~line 284) | `voyage_id` (GUID per run), `resumed` (bool), `squad_humans`, `squad_ai`, `vessel_classes` (comma list), `saga_id` | voyages started, how many resumed a saved one (a return), crew size |
| `voyage_job_finished` | where a job's work is judged (`VoyageRules.WorkDone` callers) | `voyage_id`, `verb` (one of the nine work verbs), `faction_type`, `outcome` (done, partial, failed), `seconds` | which verbs people finish and which they abandon |
| `voyage_relic_found` | `VoyageRun` relic banner (`Lines.Relic`) | `voyage_id`, `relic_id`, `saga_id` | the moment worth filming |
| `voyage_ended` | `Dock` (home), `EndAtStop` (anchored), `GiveUp` (reason) | `voyage_id`, `ended` (docked, anchored, lost_at_sea, left), `jobs_done`, `relics`, `seconds` | completion, and whether people come back the next day |

The server would add `voyage` as a `kind` in `activity_returns` (players who sailed on 2+ days, a week apart) and a
`voyages` table beside `plays`. Each event needs its `UGSKeys` constant, the facade call, an `EVENT_SCHEMA.json`
entry and a row in `event-taxonomy.md`, as every event does.

## 2. Collecting playtesters' replays

Every match is already recorded (`ReplayService`, the last 10 kept on the device), and since 2026-11-12 each replay
carries its analytics `match_id`. What is missing is getting the good ones to Gbox, where they are re-filmed in Unity
with the trailer's look (`Tools/Build/story/shoot.py`, TrailerShoot's `replay` verb).

Proposal: an opt-in setting "Share my match replays to help make Cosmic Shore's videos" (off by default, its own
line in the privacy overlay, its words an authored field). When it is on, a finished match's `.cstheater` and
`.replay.json` (typically 0.3-7 MB, measured on Gbox's ten) are uploaded in the background over Wi-Fi to a bucket
(the R2 bucket the Toybox already publishes to, or the analytics VM), keyed by `match_id`. Player names in the
summary are replaced by the analytics pseudonym before upload. The daily run pulls new replays from the bucket into
the footage index.

Decisions this needs: whether to ask at all during the Steam playtest; the bucket; how long replays are kept; and
whether pilot names may appear on screen in a public video (proposed: never without that player's yes).
