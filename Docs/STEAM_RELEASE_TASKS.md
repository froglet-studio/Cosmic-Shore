# Steam Release — remaining work board

Every open item between `bleeding-edge` and the invite-only Steam Playtest, derived from
**`Docs/STEAM_CHECKPOINT_REV3_READINESS_AUDIT.pdf`** (10 Sep 2026) measured against the
Revision 2 checkpoint of 31 July.

> **Revision 2 is not in this repository** — it exists but was never committed, so its item
> definitions (`A4`, `B2`, `C8`, `D2`, …) cited throughout this board cannot be looked up here.
> The series, the gap, and the one known error in Rev 2's text are recorded in
> **[`Docs/STEAM_CHECKPOINT_SERIES.md`](STEAM_CHECKPOINT_SERIES.md)**. Revision 1 is still in the
> repo and still describes a **paid Early Access** launch — it now carries a supersession notice,
> but do not plan from it.

**Owner of this file:** whoever is running the milestone. Hand-edit freely — unlike
`Docs/QA/QA_BACKLOG.md` this is not tool-generated.

---

## How to read the board

`LANE` says **who can actually do the item**, which is the single most useful thing to know
when planning a week:

| Lane | Meaning |
|---|---|
| **Session** | A Claude session can do it end to end from the repository. A prompt exists — paste it into a fresh session. |
| **Session + editor** | A session does the work; a human then opens Unity to verify or run a tool. |
| **Human** | Needs a running machine, a device, or a play session. No prompt can do it. |
| **Off-machine** | Steamworks, art, paperwork, hosting. Outside the repository entirely. |

`BLOCKS` names what cannot proceed until the item lands.

> **Deliberately excluded:** the quest and progression system (audit §02, action 01).
> Design is still working the chain and it is tracked separately. Everything below is
> independent of it — see *[The one dependency to watch](#the-one-dependency-to-watch)*.

---

## P0 — the milestone does not happen without these

| ID | Item | Lane | Blocks | Prompt |
|---|---|---|---|---|
| **R1** | **Prove a Windows IL2CPP player reaches the main menu.** `QA-BUILD-WINDOWS-PLAYER` is a stated P0 in the QA backlog and has never been run. Every defect in that chain — the `IL1005` linker failure, the `PauseMenu.Prewarm` crash — was invisible in the Editor. Build with `Tools/Build/build_windows.sh`, launch, sign in, reach `Menu_Main`, open and close the pause menu, read `Player.log` end to end. | Human | R2, R3, B7, every D item, DoD #3 | — |
| **R2** | **Regenerate the QA backlog.** `Docs/QA/QA_BACKLOG.md` was generated 13 Aug against PRs #583–#710; the repo is at #855. Roughly 145 PRs and nine game modes are absent from the list QA works from. Run the `/qa-backlog` skill. ⚠ **Scope is larger than the PR count suggests (found by R8):** `Docs/UNITY_VERIFICATION_CHECKLIST.md` holds **46 open items** (45 🔴 + 1 🟡), every dated one *later* than this backlog's 13 Aug scan, so none has ever been absorbed. The skill already sweeps that file by name — this run is what migrates them. | Session | R3, D5 | run `/qa-backlog` |
| **R3** | **Start the daily all-modes pass (C8).** Nineteen modes now, not seven. D5's bug bash needs a seed corpus and none exists — one submitted verdict in the backlog's entire history. Start before the bash, not at it. | Human | D5, D6 | — |
| **R16** | **Close the multiplayer session lifecycle.** Connect → play → drop → recover → return. Drop and recover are green and engine-verified; **connect is not** — the party-join path carries two open red bugs, both second-player failures: Party B5 (the second joiner fails to join) and Presence B4 (second invite not delivered), plus Party B2 (`ObjectDisposedException`) open and B11's fix reverted. A cohort that compounds through invites cannot compound through a broken join. Return (rejoin-in-progress, host migration) stays deliberately cut. **Sibling of R13** — that one is whether the experience is pleasant, this is whether it holds together. | Session + editor | DoD #2, DoD #3, H10 | [`MULTIPLAYER_LIFECYCLE_PROMPT.md`](prompts/MULTIPLAYER_LIFECYCLE_PROMPT.md) |

---

## P1 — required for a build outsiders see

| ID | Item | Lane | Blocks | Prompt |
|---|---|---|---|---|
| **R4** | **Finish the de-scope sweep (C4, 3.0 person-days).** `StoreScreen`, `EpisodeScreen`, `PurchaseCard` and `PurchaseConfirmationModal` are live surfaces with no coming-soon treatment. The `MenuAvailability` / `MenuAvailabilityView` pattern already exists and is written to be the one place this lives — extend it rather than growing a locked look per screen. | Session + editor | DoD #1 | [`DESCOPE_COMMERCE_SURFACES_PROMPT.md`](prompts/DESCOPE_COMMERCE_SURFACES_PROMPT.md) |
| **R5** | **Close the PC platform defaults (B2).** `defaultScreenWidth/Height` is 1024 × 768, `resizableWindow` is 0, and the Standalone bundle id is still `com.Froglet-Games.Tail-Glider`. Mobile-era defaults never re-pointed at PC. ⚠ **B2's audio step says "Wwise audio init" and that is wrong — the middleware is FMOD** (17 first-party files use `FMODUnity`; **zero** reference `AkSoundEngine`/`AkAudioListener`; `Assets/Wwise/` is inert). A pass written against Wwise tests nothing. What to check instead: [`Docs/AudioSystem/FMOD_AUDIT.md` §0](AudioSystem/FMOD_AUDIT.md). | Session | R1 quality | [`PC_PLATFORM_DEFAULTS_PROMPT.md`](prompts/PC_PLATFORM_DEFAULTS_PROMPT.md) |
| **R6** | **Playtest child app: runbook + dual-appID upload (A4, A5, A6, B4).** The Steam runbook is written for Revision 1 — a paid release on one app with `beta`/`default` branches. `Tools/Steam/upload.sh` takes one `STEAM_APPID`/`STEAM_DEPOTID` pair. Revision 2 needs the base app *and* the Playtest depot. | Session | A4, A5, E7 | [`STEAM_PLAYTEST_DUAL_APP_PROMPT.md`](prompts/STEAM_PLAYTEST_DUAL_APP_PROMPT.md) |
| **R7** | **Publish a load-time target, then measure (D2, 8.0 person-days).** The target is D2's actual deliverable and does not exist. The matrix is now **19 modes × 4 intensities = 76 combinations**, not the ~28 the estimate assumed. Authoring the target and the harness is session work; running it is not. | Session + editor | DoD #5 | [`LOAD_TIME_TARGET_PROMPT.md`](prompts/LOAD_TIME_TARGET_PROMPT.md) |
| ~~**R8**~~ | ✅ **DONE 2026-09-11 — the drifted documentation is corrected.** Rev 1 carries a supersession sheet (insertion-only); the **Rev 2 gap is recorded, not papered over** ([`STEAM_CHECKPOINT_SERIES.md`](STEAM_CHECKPOINT_SERIES.md) — it has *never* been committed); UI redesign T1 + T3 moved to DONE via the tracker skill and **T2 re-checked and deliberately left TODO**; the perf doc carries a dated staleness note; the Wwise→FMOD correction is recorded at R5 and in the FMOD audit. **Two findings the audit's drift table did not have:** T2 has *not* shipped (SplashScreen and Loadout Container are still un-migrated), and `UNITY_VERIFICATION_CHECKLIST.md` claimed two open items while carrying **46** — all of them predating the QA backlog's scan, so **R2 is now load-bearing for more than it looked**. | Session | — | [`DOC_DRIFT_SWEEP_PROMPT.md`](prompts/DOC_DRIFT_SWEEP_PROMPT.md) |
| **R13** | **Multiplayer quality of life.** The growth model is players inviting friends, and **a player currently cannot send a friend request at all** — the facade methods are referenced by nothing and both UI surfaces that called them were retired. Alongside it: an invite popup that auto-hides in **3 s**, is latest-wins and has no inbox; **no recently-played-with**, so the social graph cannot grow from play; a join-failure toast documented as best-effort and *"may be suppressed during the scene reload"*; and ready lights that are a count, so nobody can see who is holding up a launch. | Session + editor | the invite loop itself | [`MULTIPLAYER_QOL_PROMPT.md`](prompts/MULTIPLAYER_QOL_PROMPT.md) |
| **R14** | **Index the launch blockers and register the third-party licences.** Several of the largest folders in `Assets/` are commercial Asset Store products — NiceVibrations (47 MB), Shift Sci-Fi UI, Effects Library, PrimitivePlus, and a 19 MB folder named `Unity Assests` with 199 files and no licence file. The repository proves presence; only purchase records prove we may distribute. Produces an index and a register, deletes nothing. | Session → human | legal exposure at ship | [`LAUNCH_BLOCKER_INVENTORY_PROMPT.md`](prompts/LAUNCH_BLOCKER_INVENTORY_PROMPT.md) |

---

## P2 — needed before the paid-EA gate, not before the invite build

| ID | Item | Lane | Blocks | Prompt |
|---|---|---|---|---|
| **R9** | **Wave cohorting in instrumentation (C7).** The paid-EA gate is defined to read retention and stability *cohorted by invite wave*, and no cohort dimension exists in the analytics code. Note the constraint: it belongs on the person record via `Identify`, **not** stamped on every event — UGS schema rows are permanent and capped at 1,500. | Session | the paid-EA gate, DoD #6 | [`ANALYTICS_WAVE_COHORT_PROMPT.md`](prompts/ANALYTICS_WAVE_COHORT_PROMPT.md) |
| **R10** | **Author the Rhino's elemental ability map.** 1 of 4 slots designed, 0 of 4 level-5 upgrades authored — and the Rhino is the locked hull for **Astro League, Peel the Cage and Headlong**. Three shipped modes rest on a hull the fleet contract calls unfinished. Needs a design decision first; the prompt prepares the proposal against the shipped vessel, it does not invent the mapping. | Session → design | perceived polish of 3 modes | [`RHINO_ABILITY_MAP_PROMPT.md`](prompts/RHINO_ABILITY_MAP_PROMPT.md) |
| **R11** | **Author `UrchinHUDVariant.prefab`.** The Urchin's ability map is complete at 4/4 with all four upgrades, and it has no HUD prefab at all — `UrchinVesselHUDController` and its view are unreferenced code. It locks two modes (Hijack, Skein). | Session + editor | Hijack / Skein polish | [`URCHIN_HUD_PROMPT.md`](prompts/URCHIN_HUD_PROMPT.md) |
| **R12** | **Manta and Serpent ability maps.** Manta 3/4 named, 0/4 upgrades; Serpent 1/4 named, 0/4 upgrades. Neither locks a mode, so both sit behind the Rhino. | Session → design | — | reuse R10's prompt, retargeted |
| **R15** | **Clean the GitHub repository.** `.git` is **1.4 GB** against a 2.0 GB tree, because the FMOD Studio project is committed whole — cache, unsaved working state and ~180 MB of WAVs duplicated with `Assets/_Audio/Music` — while LFS covers only `*.so` and `*.bundle`. Also: no PR template, no `CODEOWNERS`, and PR #855 worth of branches never triaged. Stops the growth now; puts the history rewrite to a human as a decision rather than doing it. | Session → human | contributor and CI cost, daily | [`GITHUB_HYGIENE_PROMPT.md`](prompts/GITHUB_HYGIENE_PROMPT.md) |

---

## Human and off-machine — no prompt can move these

| ID | Item | Lane | Note |
|---|---|---|---|
| **H1** | Steamworks account, Direct fee, tax and banking (A1, A2) | Off-machine | The 30-day Steam Direct clock starts at payment and compresses for nobody. Runbook: `Docs/STEAM_BUSINESS_SETUP.md`. |
| **H2** | Content survey and mature-content questionnaire (A3) | Off-machine | |
| **H3** | Coming Soon page live with the Playtest signup section (A5) | Off-machine | Gated on R6's runbook and on E3 capsule art. |
| **H4** | Wave policy signed (A6) | Off-machine | Gated on R6. |
| **H5** | Store page + Playtest build submitted for review (A7) | Off-machine | Two Valve reviews, 1–5 business days each. |
| **H6** | Provision a CI runner | Human | Four workflows are authored; the static half of the landing guard runs. **The compile half needs a runner, and no workflow builds a Windows player today** — which is why R1 is manual. See `Docs/BUILD_AND_DELIVERY.md` §10. |
| **H7** | Steam overlay verification (B7) | Human | Blocked on R1 plus a real Steam build on a branch. |
| **H8** | Profiling on the GTX 1060 floor machine (D1) | Human | Locks the hardware floor and the frame/memory targets. |
| **H9** | Two-hour soak across freestyle and back-to-back tournaments (D3) | Human | |
| **H10** | Multiplayer regression, 4-player disconnect/rejoin (D4) | Human | The party and presence suites exist: `Docs/PartySystem/TESTS.md`, `Docs/PresenceSystem/TESTS.md`. |
| **H11** | Bug bash (D5) | Human | Needs R2 and R3 first — it is seeded from findings that do not yet exist. |
| **H12** | Signed exit checklist before Wave 1 (D6) | Human | Cannot be signed without D1–D5 evidence. |
| **H13** | Capsule and key art, five sizes (E3) | Off-machine | The checkpoint's own **HIGH** risk: 13 days on one contributor inside a two-week window. |
| **H14** | Trailer v1 + 30s social cut (E5) | Off-machine | Same contributor as H13. |
| **H15** | Screenshots, one per minigame (E4) | Off-machine | Scope grew: nineteen modes, not seven. Decide the subset deliberately. |
| **H16** | Steam copy pack, positioning, Discord (E1, E2, E6) | Off-machine | |
| **H17** | Wave 0 and Wave 1 operations (E7, E10) | Off-machine | Gated on H5 and R1. |
| **H18** | Host the privacy policy, support contact, EULA decision (F1–F3) | Off-machine | Templates drafted in `Docs/Legal/`. |
| **H19** | GDPR / COPPA posture review for a PC invite cohort (F4) | Human | Consent gating is implemented; this is a review. |

---

## The one dependency to watch

Everything above is independent of the quest chain **except through one decision**, and it is
worth making early because it changes what R2, R3 and H11 are testing.

`GameModeProgressionService` is not instantiated in any scene, so every unlock gate currently
**fails open** — the arcade renders fully unlocked and all intensities are available. That is a
survivable state for an invite build, and arguably the right one. But it means:

- **QA is currently testing an ungated game.** If the service is wired in later, every unlock
  path becomes untested again and R2/R3 have to re-run against it.
- **Wiring the service in without also assigning `ProgressionConfig.asset` would lock 15 of the
  19 live arcade modes**, because the built-in fallback config is `{ Maelstrom }` alone.

So decide the posture *before* R3 starts, not after. The audit's recommendation is to make the
chain a **guide rather than a gate** — have `IsGameModeUnlocked` return true for any mode outside
the chain, using the `IsGameModeInQuestChain` predicate that already exists and is not consulted
by the arcade view. That decouples the invite build from design's content permanently, and it
holds whether the chain ends up with six quests or twenty.

---

## Definition of done — what each item buys

Mapped to the checkpoint's eight exit criteria, so the board can be read backwards from the gate.

| # | Criterion | Turned on by |
|---|---|---|
| 1 | Steam page live with Playtest signup, review passed | H1, H3, H5, R6, H13 |
| 2 | Playtest build approved, Waves 0–1 granted, invites on | R1, R6, **R16**, H5, H17 |
| 3 | Fresh install → completed 4-player Tournament, zero P0/P1 | R1, R2, R3, R16, H11, H12 |
| 4 | Fresh account completes the quest chain, unlocks persist | *(quest work — excluded from this board)* |
| 5 | Cold boot to playable meets the load-time target, all modes | R7, H8 |
| 6 | Crash + funnel telemetry in dashboards, cohorted by wave | R9 |
| 7 | Zero public review surface | structural — Playtest configuration, already true |
| 8 | Paid-EA gate published, exit checklist signed | H12, R9 |
