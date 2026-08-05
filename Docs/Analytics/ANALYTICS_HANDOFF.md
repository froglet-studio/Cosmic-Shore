# Analytics & Data — Status and Action List

> **Audience:** Shombith (and Ian, for the dashboard half).
> **Scope:** everything done to the player data layer and the analytics pipeline, plus the
> ordered list of what only you can do — accounts, keys, dashboards, legal.
> **Maps to:** `STEAM_EA_INVESTOR_CHECKPOINT` rows **C7** (instrumentation buildout, 5.0 d,
> Shombith + Ian) and **F4** (GDPR/COPPA consent review, 1.0 d, Shombith).
>
> Companions: `DATA_ARCHITECTURE.md` (the authority on schema + design), `EVENT_SCHEMA.json`
> (machine-readable event contract), `POSTHOG_SETUP.md` (the click-by-click PostHog guide),
> `../../Tools/Analytics/README.md` (export + backfill scripts).

---

## 1. What changed — the short version

Two things were wrong and both are now fixed.

**The save data had drifted.** Five Cloud Save keys, written at different times by different
people, disagreed on nearly everything: key casing, field casing, what a timestamp is (three
different formats), how a composite dictionary key is spelled (two separators for one concept).
Nothing carried a schema version, so nothing could ever be migrated — only broken.

**Three of the fields the instrumentation email asked for did not exist and could not be
produced.** Not "weren't wired up" — the game had no clock that excluded pause, no per-match
identifier, and no replicated UGS player id at all.

Both are shipped and merged into `bleeding-edge` (PR #625, four commits), plus a follow-up that
salvaged the useful half of the older attribution branch (PR #592, now closed).

---

## 2. Cloud Save — before and after

### 2.1 The keys

| Before | After | What happened |
|---|---|---|
| `player_profile` | `PLAYER_PROFILE` | Renamed to match the others; restructured into groups; absorbed `LastLoginTick` |
| `VESSEL_STATS` | *deleted* | Merged into `HANGAR_DATA` — one record per vessel |
| `HANGAR_DATA` | `HANGAR_DATA` | Restructured; absorbs vessel stats; three write-dead fields now actually written |
| `PLAYER_STATS_PROFILE` | `MODE_STATS` | Four bespoke per-mode models collapsed into one uniform record |
| `GAME_MODE_PROGRESSION` | `GAME_MODE_PROGRESSION` | Untouched by request, except a `SchemaVersion` field |

**Five keys became four.** One fewer round-trip at sign-in, and the vessel join stopped being a
client-side correlation across two payloads on a bare string key.

### 2.2 The format standard (was violated by every model)

- Cloud Save keys: `SCREAMING_SNAKE_CASE`
- JSON fields: `PascalCase`
- Timestamps: `long`, **Unix epoch milliseconds UTC**, always suffixed `UtcMs`
- Durations: `float`, **seconds**, always suffixed `Seconds`
- Composite dictionary keys: `"{Mode}:{Intensity}"` — one separator, everywhere
- Every root model carries `SchemaVersion`

`SchemaVersion` was added while breaking was still free, specifically so the *next* change is a
migration rather than another break. **It has already paid for itself once:** a later branch
removed player XP, and `PLAYER_PROFILE` went to v2 — old saves still carry the dead
`Progression` key and it is ignored on load, which is exactly the no-op migration the field
exists to enable.

### 2.3 Three real bugs found and fixed

1. **`SelectedVessel` had no writer at all.** The only reference outside the model was a debug
   read in the editor window. That is why it was always `""`. It now writes on vessel-select
   confirm, so it genuinely means "last vessel the player chose".
2. **`VesselPreferences` was only ever `.Clear()`ed** — never written, by anything. It is
   replaced by `PreferredVessel` (singular, as you asked), *derived* as the most-flown vessel
   rather than stored. That required tracking per-vessel flight time, which nothing did.
3. **`UnlockedVessels: [""]`** was a real data bug, not a display artifact: one `SO_Vessel`
   asset ships with a blank `Name`, and the unlock writer persisted it verbatim. The flat list
   is now a keyed map with an explicit `Unlocked` flag, and blank names are rejected at the
   writer — so the shape no longer permits the bug. **The underlying asset still needs
   fixing** (see §6, E2).

### 2.4 Why `MODE_STATS` matters more than it looks

Before, `WildlifeBlitz`, `HexRace`, `Joust` and `CrystalCapture` each had their own class
holding the same idea under four different field names — `HighScores`,
`BestMultiplayerRaceTimes`, `BestRaceTimes`, `HighScores` — two of them `int` and two `float`.

Adding Astro League or Brood Rush meant: a fifth class, a fifth field on the root model, a fifth
null-check in the repository, and a fifth branch in the high-score evaluator.

**Adding a mode is now data, not code.** The record also gained `GamesPlayed` / `GamesWon`,
which is the denominator the "rematch rate by game mode" analysis needs.

---

## 3. Analytics — what the instrumentation email asked for

### 3.1 `flight_time_seconds` — done

Measured from **`GameDataSO.StartTurn`**, the instant after the countdown when players are
activated. That is precisely "when the player is given control", and it already existed on every
controller path, single-player and multiplayer.

You flagged pause exclusion as the uncertain part. **It was easy** — `PauseSystem` already
exposed the flag and both events. Three lines.

The part that was *not* obvious: both natural implementations are wrong.

| Candidate | Why it fails |
|---|---|
| `realtimeSinceStartup` (what the old `duration_seconds` uses) | counts paused and backgrounded time |
| scaled `Time.time` | excludes pause correctly, but Astro League drives `Time.timeScale` for hitstop and goal slow-mo, so seconds spent in slow-mo get under-counted |

It uses `Time.unscaledTime`, gated on turn-running + not-paused + not-backgrounded, and
integrates on state transitions only — **no per-frame cost at all**.

**`duration_seconds` was deliberately kept alongside it.** The difference between the two is
pause + AFK + between-round time — a churn signal you get for free instead of instrumenting
separately.

### 3.2 Timestamps — done

`timestamp_utc_ms` (for arithmetic) and `timestamp_utc_iso` (for day/week/month bucketing with
no per-query conversion). Both are the *client's* clock at completion, which is what the email
meant; UGS and PostHog each stamp their own ingest time separately.

### 3.3 Grouping players — done, with one change to the ask

The email asked for a single `lobby_id`. **A single identifier does not work**, and it is worth
knowing why before anyone queries this data.

Under the locked eager-Relay design the party session id genuinely is shared by everyone in the
session — but a party that stays together and plays three games back-to-back keeps **one**
session id. Grouping on it alone collapses three games into one. Grouping on a per-match id
alone loses "the same four people stayed together all evening".

So `game_started` carries **both**:

| Field | Grain |
|---|---|
| `match_id` | one game instance — the "same game instance" key the email wanted |
| `party_id` | one sitting; stable across consecutive matches by the same party |

The organic-rematch query then reads: *same `player_ids` + same `game_mode` +
`invite_triggered = false` + a **different** `party_id` + within X days.* The different-`party_id`
clause is what stops one evening of three games registering as two rematches.

**`player_ids` could not be produced at all before.** `Player` replicated no UGS id, and
`IPlayer.PlayerUUID` is the *display name* (two players can pick the same one). A new
`Player.NetUgsPlayerId` NetworkVariable fixes that. The list is derived on each peer from
replicated network objects and sorted, so every client computes the identical set. AI is
excluded and reported separately as `player_count_ai`.

**`invite_triggered`** is party-level state — the joiner knows they accepted, the host knows
they sent, and someone who arrived through presence knows neither. It is set on both sides of
the handshake and cleared when the party empties. That clearing is load-bearing: without it, a
party that formed by invite, dissolved, and re-formed organically the next day would still
report `true` and be excluded from exactly the organic cohort you are trying to measure.

### 3.4 PostHog — done

`AnalyticsServiceFacade.RecordEvent` was already the single funnel every event passed through,
so this was an interface extraction rather than a rewrite. Two sinks now receive an identical
payload, so UGS and PostHog cannot drift apart.

**Identity uses both keys, deliberately.** `distinct_id` is the UGS player id — immutable, and
the same key as Cloud Save and Leaderboards, so PostHog joins to both with no mapping table.
`display_name` rides as a **person property**, so you can find a person by either. Keying the
event graph on display name would split one player into several the first time they rename.

As a person property rather than an event property there is one mutable copy per person instead
of one immutable copy per event — so a rename corrects history, and erasure is a single-object
operation.

Person properties mirrored from Cloud Save: display name, avatar, crystal balance, lifetime
crystals earned/spent, first-seen, session count, games completed, total flight time, preferred
vessel, selected vessel, unlocked vessel count, unlocked mode count.

**Region is locked to EU Cloud** — see §5.

---

## 4. Salvaged from the old attribution branch (PR #592, closed)

That branch built an equivalent sink layer independently, against a base 5,379 commits old. Its
sink class was named `PostHogSink` while the shipped one is `PostHogAnalyticsSink` — different
filenames, so merging it would **not** have conflicted. It would have silently landed a second
PostHog sink beside the first and double-sent every event.

**Kept** (none of it existed on `bleeding-edge`):

- `Tools/Analytics/export_cloud_save.py` — bulk Cloud Save export via a read-only service
  account, standard-library Python only
- `Tools/Analytics/import_snapshot_to_posthog.py` — backfills existing players into PostHog
  People/cohorts, so your whole player base is visible rather than only post-launch players
- `POSTHOG_SETUP.md`, `viability-report.md`, `event-taxonomy.md`, `utm-conventions.md`,
  `implementation-plan.md` — each with a provenance banner

**Ported into the shipped sink** — genuine improvements it lacked:

- **HTTP 4xx batches are dropped, not retried.** This was the worst gap: with a wrong or empty
  API key, the old code would re-send the same rejected batch forever until the queue cap
  silently discarded every *newer* event.
- 10-second request timeout, so a stalled upload cannot hang the quit flush
- Warn-once-per-failure-episode instead of one warning per batch
- `excludedEvents` filter — the free-tier budget lever
- `install_id` — device GUID surviving sign-out

---

## 5. The EU vs US question — settled

**Keep EU Cloud.** The client host is `https://eu.i.posthog.com`.

The premise behind switching to US — *"Froglet is a Delaware C-corp"* — does not apply, and it
is worth writing down because it will come up again:

- **GDPR scope follows where the players are, not where the company is.** Article 3(2) is
  explicitly extraterritorial: a US entity offering a service to people in the EU is in scope.
  A game on Steam and mobile stores has EEA/UK players from day one.
- **No US law requires US residency for game telemetry.** Data-residency mandates of that kind
  attach to government and defense work.
- **US Cloud would cost real work:** every EEA/UK player's events become a restricted transfer
  needing Standard Contractual Clauses plus a transfer impact assessment, or reliance on the
  EU–US Data Privacy Framework — whose two predecessors were both struck down in court.
- **EU Cloud costs nothing.** Same price, same features; batched uploads make the latency
  difference irrelevant.

**This also caught a live bug.** The shipped config pointed at `us.i.posthog.com` while the
project is EU. Once the key was pasted, events would have gone to a host that accepts none of
them — silently, with no errors. Fixed.

PostHog regions **cannot be changed after project creation**, so this is effectively permanent.

---

## 6. Your action list

Ordered. Everything in **A** is required before any data arrives at all.

### A · Turn the pipeline on — ~45 min, unblocks everything else

| # | Action | Why it matters |
|---|---|---|
| **A1** | Open `Assets/Resources/PostHogConfig.asset` in Unity, paste the **Project API key** (starts with `phc_`) into `Project Api Key`. Confirm `Host` reads `https://eu.i.posthog.com`. Save, commit, push. | The sink is **inert** until this key exists. This is the single blocking step. |
| **A2** | PostHog → *Organization settings → Billing → Product analytics → Edit billing limit → **$0***. | Makes free-tier overage a hard drop instead of a surprise bill. |
| **A3** | **Declare every event and every parameter in the UGS dashboard Event Manager**, from `Docs/Analytics/EVENT_SCHEMA.json`. | UGS **silently discards** any event or parameter not declared there. This is the most likely reason so little data has been arriving. |
| **A4** | Play one full game. Check PostHog → *Activity* for `game_started` / `game_completed`, and the UGS dashboard for the same. | End-to-end proof. If PostHog is empty but UGS is not, the key or host is wrong. |

### B · Backfill the players you already have — ~1 hr, once

Without this, PostHog only knows players who play *after* A1. The scripts are plain Python,
no `pip install`.

| # | Action |
|---|---|
| **B1** | Unity Cloud → *Administration → Service Accounts* → new account `cloudsave-export`, project role **Cloud Save Viewer**. Create a key; note Key ID, Secret, and the Project ID. |
| **B2** | Run `python3 Tools/Analytics/export_cloud_save.py` → JSONL of every player's save data. |
| **B3** | Run `python3 Tools/Analytics/import_snapshot_to_posthog.py` → one snapshot per player into PostHog People. |

Full instructions and ready-made DuckDB queries: `Tools/Analytics/README.md`.

### C · Verify the new fields — ~1 hr, needs two clients

These are the email's actual asks; they should be confirmed working before anyone builds a
dashboard on them.

| # | Check | Pass condition |
|---|---|---|
| **C1** | Run an MPPM two-client game. Compare `game_started` on both clients. | `match_id`, `party_id`, `player_ids`, `invite_triggered` are **byte-identical** on both. If `player_ids` differs, stop and tell me. |
| **C2** | Play once joining by **invite**, once joining through **presence**. | `invite_triggered` is `true` then `false`. |
| **C3** | Play a game, pause for ~30 s mid-match, finish. | `flight_time_seconds` is ~30 s **less** than `duration_seconds`. |
| **C4** | Play two back-to-back games with the same party. | Same `party_id`, **different** `match_id`. |

### D · Legal — blocking for public release, not for the invite playtest

Maps to checkpoint row **F4**. Consent gating is already implemented, so most of this is review
and paperwork, not engineering.

| # | Action | Notes |
|---|---|---|
| **D1** | Execute the **PostHog DPA** and review their sub-processor list. | Article 28 requirement. PostHog publishes one; it has to actually be signed. |
| **D2** | Add **PostHog (EU)** by name to `Docs/Legal/PRIVACY_POLICY_TEMPLATE.md` and the consent dialog copy, listing the categories sent. | The opt-in gate is already correct mechanically. Consent that does not name the recipient is not informed consent. |
| **D3** | Update **Apple Privacy Nutrition Labels** and **Google Play Data Safety** in the same release the sink ships in. | Because display name is sent, declare *user content* as linked to identity. A truthful form describing the previous build is still a false declaration. |
| **D4** | Build **server-side PostHog person deletion**. | See §7 — this is the one item that is a code gap, not paperwork. |
| **D5** | Confirm the store age rating / Families-policy classification. | If the game is ever listed as child-directed, third-party analytics restrictions apply — Apple's Kids Category prohibits it outright. |

### E · Housekeeping — not urgent

| # | Action |
|---|---|
| **E1** | **Delete the branch `claude/analytics-attribution-viability-vwkunw`.** PR #592 is closed, but the git proxy in my environment refuses ref deletions (`send-pack: unexpected disconnect`), so I could not remove it. One click in the GitHub UI. |
| **E2** | Find the `SO_Vessel` asset with a blank `Name` and fix it. The new schema stops it *persisting*, it does not fix the asset. |
| **E3** | Set `Excluded Events` on `PostHogConfig.asset` to `ui_action` if you ever approach the 1M/month free tier. UGS keeps receiving it, so nothing leaves the system of record. |

---

## 7. Known gaps — stated plainly

**Erasure is only partial.** Deleting a PostHog *person* requires an admin API key that must
never ship in a client build. The in-game button stops collection, drops the pending queue, and
sets `gdpr_deletion_requested` on the person — but the deletion itself is currently a manual
dashboard step (*People → search the UGS PlayerId → Delete person → include events*). **Until
D4 exists, the button reports a deletion that has not fully happened**, which is worse than no
button. It logs a warning saying so.

**UGS SDK deprecation.** `Start/StopDataCollection` are deprecated on Unity 6.2+. They are
suppressed rather than migrated, deliberately: the replacement is the engine-level Developer
Data consent framework, and per Unity's own changelog, mixing the two throws at runtime. It has
to be an all-at-once cutover reconciled with the existing consent/age gate — its own change,
with in-editor verification.

**Scoring direction lives in one table** in `UGSStatsManager` rather than in config.
`LeaderboardConfigSO` has no direction field and the controller's `UseGolfRules` is not
reachable at report time. This is consolidation, not new duplication — the old code hardcoded
exactly the same branching — but the real fix is publishing `SO_Game.GolfScoring` onto
`GameDataSO` so both read one value.

**`IPlayer.PlayerUUID` is still the display name.** It is load-bearing for AOE block ownership
strings, so repointing it at the real id would be a gameplay change smuggled into an analytics
commit. `UgsPlayerId` was added alongside it; retiring `PlayerUUID` is a separate job.

---

## 8. When the quest branch lands

`claude/ftue-editor-tool-69acq5` adds a new Cloud Save key, **`QUEST_GRAPH_PROGRESS`**
(`QuestProgressCloudData`): per-quest records keyed by quest id, holding a completion flag, a
resume cursor (phase index + current node), and the full list of completed node ids as
`"{phase}/{nodeId}"`. The per-node list is genuinely useful — it gives an onboarding funnel and
exact mid-phase resume from the same record.

**Two things to reconcile when it merges** (do not merge it now — this is a heads-up):

1. **It versions with `Version`, not `SchemaVersion`.** Same idea, different name, which is the
   exact drift the format standard exists to prevent. Rename on merge.
2. **It creates a second progression key.** `GAME_MODE_PROGRESSION` was deliberately left alone
   "pending the quest-system pass" — and this *is* that pass, but it adds a parallel key rather
   than folding in. There is also a known duplication already flagged:
   `MODE_STATS[mode:i].GamesPlayed` and `GAME_MODE_PROGRESSION.IntensityPlayCounts["mode:i"]`
   are the same number in two places. The merge is the moment to decide whether
   `GAME_MODE_PROGRESSION` survives at all.

Its `UGSKeys.cs` change is additive and should auto-merge cleanly against the rewritten key
list.

---

## 9. Checkpoint mapping

| Row | Was | Now |
|---|---|---|
| **C7** — Instrumentation and analytics buildout (5.0 d, Shombith + Ian) | event coverage, dashboard construction, end-to-end validation | **Event coverage is done** — schema locked, envelope + match grouping + flight time shipped, `EVENT_SCHEMA.json` is the contract. Remaining C7 work is §6 **A** (turn it on), **B** (backfill), **C** (validate), and Ian's dashboard construction on top. |
| **F4** — GDPR/COPPA consent review (1.0 d, Shombith) | review, not build | Still a review, **plus one build**: D4, server-side deletion. The rest is D1/D2/D3/D5 paperwork. Region is settled (§5), which removes the SCC question entirely. |
