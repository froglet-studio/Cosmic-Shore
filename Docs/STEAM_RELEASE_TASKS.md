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

A ✅ on an ID means that item has landed. The row stays on the board rather than being deleted, so what was done — and what it deliberately did *not* cover — stays readable.

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
| **R16** ✅ | **Closed the multiplayer session lifecycle — the SESSION half.** Landed 11 Sep 2026; the editor half is **H10** and is deliberately not covered here. Of the four items: **B2 closed** (fixed since 2026-08-20 — the semaphores are deliberately never disposed; the tracker was stale). **B11 closed** as superseded by B16, with the reverted recycle explicitly barred from returning and the evidence that would reopen it written down. **Presence B4** — a real cause found and fixed (`ConvergeToCanonicalAsync` released a lobby it hosts by DELETING it, evicting every other occupant into a frozen online list and a false `ForceReset`), commit `0c1f747b`. **Party B5** — every named cause traced closed in source, plus two the entry never named; its repro predates all of them AND the MPPM unique-tag prerequisite, so the record was stale rather than the bug fixed. Both are 🟡, not 🟢: **nothing was run in an editor** (no Unity in that session), so the two retests are real work and they belong to **H10**. **Second pass, 2026-09-11, on an owner report from live play that outranked this tracker** (*"once you get off the happy path the game is bugged… connecting is a challenge, leaving is a challenge"*, 4 US players, not latency): two structural client defects found and fixed — **B18**, a client could not leave a match at all and could not leave a Maelstrom until the whole race-to-N ended (every exit gated on `IsServer`; the correct pattern existed on exactly one screen), and **B19**, nothing watched a client's scene transition so a lost one was a permanent black screen. Both 🟡 — not playtested. Return (rejoin-in-progress, host migration) stays deliberately cut. **Sibling of R13** — that one is whether the experience is pleasant, this is whether it holds together; the join splash showing nothing for up to ~60s is logged there. | Session ✅ + editor ⬜ | DoD #2, DoD #3, H10 | [`MULTIPLAYER_LIFECYCLE_PROMPT.md`](prompts/MULTIPLAYER_LIFECYCLE_PROMPT.md) |

---

## P1 — required for a build outsiders see

| ID | Item | Lane | Blocks | Prompt |
|---|---|---|---|---|
| **R4** ✅ | **De-scoped the commerce surfaces (C4).** Landed 11 Sep 2026. Every surface that would take money now carries a deliberate coming-soon state from ONE authority, `Resources/CommerceAvailability` — the existing `MenuAvailability` / `MenuAvailabilityView` pattern extended, not a second locked look (asserted by `CommerceDeScopeTests`). Both paths to `PurchaseConfirmationModal` are gated, and `IAPManager.OpenCheckout` declines at the choke point its two entry points share. The config **fails closed** — its code defaults are the de-scoped state, so a missing asset cannot re-open a money surface. The UGS crystal loop (earning, vessel unlocks) is untouched. Full record: [`MENU_PROGRESSION_AND_IAP.md` §6](MENU_PROGRESSION_AND_IAP.md). **Code + assets only — nothing was opened in Unity**, so the asset import, the one new scene component and the on-screen dimming are unverified; that pass is R1/R3. ⚠ **Three findings the audit did not have:** the live path this closed was **PROFILE → `UnlockVesselButton` → episode panel → Support Us → `Application.OpenURL`**, reachable from a cold boot — and that button, despite its name, never unlocked a vessel; **`MenuScreens.STORE` has no entry in `ScreenSwitcher.screens` at all**, so the store is the `ArkScreen` (already locked) and `NavigateTo(STORE)` was silently landing the player on the Hangar; and the Hangar's **captain upgrade** is PlayFab-catalog commerce, not the live crystal loop, so it was already refusing with a bare sting and no reason. | Session + editor | — | [`DESCOPE_COMMERCE_SURFACES_PROMPT.md`](prompts/DESCOPE_COMMERCE_SURFACES_PROMPT.md) |
| **R5** ✅ | **Closed the PC platform defaults (B2).** Landed 11 Sep 2026: `defaultScreenWidth/Height` 1024 × 768 → 1920 × 1080, `resizableWindow` 0 → 1, Standalone bundle id → `com.FrogletGames.CosmicShore`. **Code half only** — the manual PC sanity pass (pads, alt-tab, focus, quit path, no mobile-only prompts) is R1/R3 and is what proves this landed. ⚠ **B2's other half has a wrong premise: its audio step says "Wwise audio init", and the middleware is FMOD** — 17 first-party files use `FMODUnity`, **zero** reference `AkSoundEngine`/`AkAudioListener`, and `Assets/Wwise/` is inert. A pass written against Wwise tests nothing. What the audio step should actually check is in [`Docs/AudioSystem/FMOD_AUDIT.md` §0](AudioSystem/FMOD_AUDIT.md); **it is R1/R3 that must run it**, per this row's own note. (Found by R8.) | Session | R1 quality | [`PC_PLATFORM_DEFAULTS_PROMPT.md`](prompts/PC_PLATFORM_DEFAULTS_PROMPT.md) |
| **R6** | **Playtest child app: runbook + dual-appID upload (A4, A5, A6, B4).** The Steam runbook is written for Revision 1 — a paid release on one app with `beta`/`default` branches. `Tools/Steam/upload.sh` takes one `STEAM_APPID`/`STEAM_DEPOTID` pair. Revision 2 needs the base app *and* the Playtest depot. | Session | A4, A5, E7 | [`STEAM_PLAYTEST_DUAL_APP_PROMPT.md`](prompts/STEAM_PLAYTEST_DUAL_APP_PROMPT.md) |
| **R7** | **Publish a load-time target, then measure (D2, 8.0 person-days).** The target is D2's actual deliverable and does not exist. The matrix is now **19 modes × 4 intensities = 76 combinations**, not the ~28 the estimate assumed. Authoring the target and the harness is session work; running it is not. | Session + editor | DoD #5 | [`LOAD_TIME_TARGET_PROMPT.md`](prompts/LOAD_TIME_TARGET_PROMPT.md) |
| **R8** ✅ | **Corrected the drifted documentation.** Landed 11 Sep 2026. Rev 1 carries a supersession sheet (insertion-only); the **Rev 2 gap is recorded, not papered over** ([`STEAM_CHECKPOINT_SERIES.md`](STEAM_CHECKPOINT_SERIES.md) — it has *never* been committed); UI redesign T1 + T3 moved to DONE via the tracker skill and **T2 re-checked and deliberately left TODO**; the perf doc carries a dated staleness note; the Wwise→FMOD correction is recorded at R5 and in the FMOD audit. **Two findings the audit's drift table did not have:** T2 has *not* shipped (SplashScreen and Loadout Container are still un-migrated), and `UNITY_VERIFICATION_CHECKLIST.md` claimed two open items while carrying **46** — all of them predating the QA backlog's scan, so **R2 is now load-bearing for more than it looked**. | Session | — | [`DOC_DRIFT_SWEEP_PROMPT.md`](prompts/DOC_DRIFT_SWEEP_PROMPT.md) |
| **R13** | **Multiplayer quality of life.** The growth model is players inviting friends, and **a player currently cannot send a friend request at all** — the facade methods are referenced by nothing and both UI surfaces that called them were retired. Alongside it: an invite popup that auto-hides in **3 s**, is latest-wins and has no inbox; **no recently-played-with**, so the social graph cannot grow from play; a join-failure toast documented as best-effort and *"may be suppressed during the scene reload"*; and ready lights that are a count, so nobody can see who is holding up a launch. | Session + editor | the invite loop itself | [`MULTIPLAYER_QOL_PROMPT.md`](prompts/MULTIPLAYER_QOL_PROMPT.md) |
| **R14** ✅ | **Indexed the launch blockers and registered the third-party licences.** Landed 11 Sep 2026 — [`THIRD_PARTY_REGISTER.md`](THIRD_PARTY_REGISTER.md) (every component in `Assets/` + `manifest.json`, with a ships/editor-only column derived from Unity's actual inclusion rules, and 8 entitlement questions for the Asset Store account holder) and [`LAUNCH_BLOCKER_INDEX.md`](LAUNCH_BLOCKER_INDEX.md) (19 candidates, each with a measured inbound-reference check and a `remove`/`keep`/`salvage-first`/`needs-a-human` verdict). **Nothing deleted, no licence status asserted without a document behind it.** ⚠ **One licence exposure to raise now, not at the next checkpoint (index §B1):** first-party UI draws art out of **NiceVibrations' *demo* folder**, and one of those references is in `Menu_Main.unity`, an enabled build scene — Asset Store EULAs treat demo content separately from the plugin, and if the answer is no the fix is re-authoring four sprites, which has an art lead time. **Four premises in this row were wrong and are corrected there:** `Unity Assests` is not an unattributed grab-bag but **TextMesh Pro's essential resources** (deleting it breaks every text component — `TMP Settings.asset` is loaded by *name*, so it measures **zero** guid references), NiceVibrations' two "licence files" are a Rust crate list and a haptic-sample CC notice rather than the **product EULA** (still absent), `Effects Library` contains only `Froglet Stuff/` and may be first-party (**unknown** — the clone is shallow, so provenance is not recoverable here), and the 20 orphan arcade cards are **not** all inert — `check_gamelist_scenes.py` validates `SO_GameList` rosters only, and **6 are still referenced from vessel class SOs**. **Two defects found that ship today:** 3.5 MB of TMP *example* content and 1.8 MB of QuickScene Pro *editor-tool icons* are both packed into every player because a `Resources/` folder ships whether or not anything references it, and NiceVibrations' demo asmdef is not editor-only, so 30 demo scripts compile into the player. **ParrelSync:** the `Assets/` side is **confirmed** non-shipping (0 referrers, not in `Resources/` or `Editor/`); the **package** side is **flagged, not confirmed** — `Library/` is not committed so its asmdef cannot be read here, and unlike `com.unity.pipeline` (guarded by `UnityPipelineReleaseGuard`) **no build guard exists for it**. Also corrected: **CLAUDE.md:745 is wrong** that `_Scripts/Game` is vestigial — it holds 3 `.cs`, two of them live, plus a material on `Rhino.prefab` and a config on `Manta.prefab`. | Session ✅ → human (entitlement) | legal exposure at ship | [`LAUNCH_BLOCKER_INVENTORY_PROMPT.md`](prompts/LAUNCH_BLOCKER_INVENTORY_PROMPT.md) |

---

## P2 — needed before the paid-EA gate, not before the invite build

| ID | Item | Lane | Blocks | Prompt |
|---|---|---|---|---|
| **R9** ✅ | **Closed wave cohorting in instrumentation (C7).** Landed 11 Sep 2026: every session now sends `invite_wave` — the UTC-Monday week key of the player's cloud-backed `first_seen_utc_ms` — plus that raw timestamp, as **person properties** via `Identify`, so an analyst can re-bucket to any wave definition without a client change. **Zero new UGS event-schema rows**, which was the stated constraint. Three defects were found on the way and fixed: person properties were never actually refreshed on profile change (so a session ending in a **crash** could carry no wave — biasing the crashiest players out of the very number the stability gate reads), an unknown first-seen was *written* rather than omitted over a good value, and the shared week-key formatter rendered through the device locale's **calendar** (measured: `1448-03-25` on ar-SA, `2569-09-07` on th-TH), which had already been silently giving those players a different weekly challenge. ⚠ **Client half only — the dashboards do not exist.** The PostHog retention, funnel and crash-free insights broken down by `invite_wave` still have to be built; DoD #6 has a data source, not a chart. ⚠ **`invite_wave` is a PROXY for a Steam grant batch, not the batch** — no Steam SDK exists to read one, so a tester who requested in week 1 and installed in week 3 is in wave 3, and a reinstall under anonymous auth lands in a later wave ([`DATA_ARCHITECTURE.md` §7.3.3](Analytics/DATA_ARCHITECTURE.md)). | Session ✅ → off-machine (dashboards) | the paid-EA gate, DoD #6 | [`ANALYTICS_WAVE_COHORT_PROMPT.md`](prompts/ANALYTICS_WAVE_COHORT_PROMPT.md) |
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
| **H10** | Multiplayer regression, 4-player disconnect/rejoin (D4) | Human | **Now also covers B18/B19 (2026-09-11), and those want a REAL two-machine pass rather than MPPM** — B19's live call site is a ClientRpc that MPPM cannot exercise faithfully, because MPPM virtual players share one process and one `GameDataSO`. The party and presence suites exist: `Docs/PartySystem/TESTS.md`, `Docs/PresenceSystem/TESTS.md`. **Inherits from R16 (2026-09-11):** B2 and B11 closed, so four open lifecycle bugs are now two. Party B5 and Presence B4 are both 🟡 and both need a retest this pass CANNOT skip — B4 carries a code fix verified only by syntax parse, and B5 carries no fix at all, only a source audit saying every named cause is already closed. **Use uniquely tagged VPs** (`PartySystem/TESTS.md` § "MPPM prerequisites"): the historical repro for BOTH bugs predates that requirement, and untagged clones share one UGS `PlayerId`, which reproduces both symptoms on its own. A failure now is a NEW root cause — capture both `Player.log`s and open a fresh entry rather than re-walking the closed tables. Details: `MPPM_SESSION_LOG.md` Session 4. |
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
| 6 | Crash + funnel telemetry in dashboards, cohorted by wave | R9 ✅ *(client emits `invite_wave`; the dashboards themselves are still to build in PostHog)* |
| 7 | Zero public review surface | structural — Playtest configuration, already true |
| 8 | Paid-EA gate published, exit checklist signed | H12, R9 ✅ *(the gate can now be measured; publishing it is still H12)* |
