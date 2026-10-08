# Branch archive: `cece/quirky-gates-1wqijw`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-25 by Claude
- **Unmerged commits:** 1
- **Forked from:** `2f2a9e55b` (2026-06-25, Merge pull request #564 from froglet-studio/claude/bold-noether-sli4g9)
- **Tip:** `4e8ad870f`
- **Files touched (5):**
  - `CLAUDE.md`
  - `Docs/MENU_PROGRESSION_AND_IAP.md`
  - `Docs/QuestSystem/ARCHITECTURE.md`
  - `Docs/QuestSystem/HANDOFF.md`
  - `Docs/README.md`

### `4e8ad870f` — docs(quest): canonical Quest Track + Breadcrumb activation design + handoff

_Claude, 2026-06-25 18:26:11 +0000_

```text
Add Docs/QuestSystem/ARCHITECTURE.md (design of record, current-vs-target
tagged) and HANDOFF.md (engineer work plan + XP/legacy-stack retirement
checklist) capturing the unified player-activation engine: a chain of
unlocks (any app feature, quest-gated) wired to the Call-to-Action
breadcrumb. Locks constraints C1-C4 (XP removed entirely; single guidance
channel; continuity law on reveals; persistent & monotonic single source
of truth).

Index the new docs in Docs/README.md and CLAUDE.md (+ a short section), and
update Docs/MENU_PROGRESSION_AND_IAP.md so the XP track is marked removed
(was "cosmetic") with pointers to the QuestSystem docs.
```

```text
 CLAUDE.md                        |  34 ++++++++++
 Docs/MENU_PROGRESSION_AND_IAP.md |  22 ++++++-
 Docs/QuestSystem/ARCHITECTURE.md | 242 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Docs/QuestSystem/HANDOFF.md      | 132 ++++++++++++++++++++++++++++++++++++++
 Docs/README.md                   |   7 ++
 5 files changed, 435 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 504 lines)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 9b768985d..a5f11f730 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -296,6 +296,7 @@ MiniGameControllerBase (abstract, NetworkBehaviour)
 | `ScoringSystem/` | `Docs/` | Scoring system (in-game score HUD + final scoreboard): `ARCHITECTURE.md` (shared data layer, event dispatch, per-mode override table, target = one unified networked scoring path), `REFACTOR.md` (sequenced backlog + ground rules: SOAP/observer/SOLID/DRY/KISS, retire `IsMultiplayerMode`), `BUGS.md`, `TESTS.md`. |
 | `TournamentSystem/` | `Docs/` | Tournament mode (`GameModes.Tournament = 36`): `ARCHITECTURE.md` — session-level meta chaining the three domain minigames (HexRace → Joust → Crystal Capture) via sequential `Single` loads; network-free standings folded from the synced `GameDataSO.Results` by the persistent `TournamentController`; host-only Continue→hub→Summary end-game flow (summary-vs-hub keyed off the authoritative `IsShuffleComplete`, race-to-6); `TournamentDataSO` data + file index. |
 | `ShuffleSystem/` | `Docs/` | **"Maelstrom" is the player-facing display name of Tournament mode** (the docs folder keeps the legacy "Shuffle" name) — the `ArcadeGameTournament.asset` card carries `DisplayName = "Maelstrom"`. It is **not** a separate mode: code/data/enum stay **Tournament** (`GameModes.Tournament = 36`); the scene file was renamed to `Maelstrom.unity` in the v2 rework. `ARCHITECTURE.md` is a **pointer** to `TournamentSystem/ARCHITECTURE.md`; the former Shuffle-specific behavior deltas (randomized lineup, per-domain `{2,1,0}` scoring + crystal-wallet credit, race-to-6) are now **shipped**. |
+| `QuestSystem/` | `Docs/` | **Quest Track + Breadcrumb player-activation engine** (formerly the "XP track"): `ARCHITECTURE.md` (the chain-of-unlocks design — each unlock reveals any app feature gated by a quest; the Call-to-Action breadcrumb guides the player to it; current-vs-target tags; locked constraints C1–C4) + `HANDOFF.md` (engineer work plan / migration + retirement checklist). **XP is removed entirely** — quest completion is the only progression currency. Spine = `GameModeProgressionService`; breadcrumb = `CallToActionSystem`; the legacy `Quest`/`QuestSystem`/`UserJourneySystem`/`SO_QuestChain` stack + all XP code are retire-targets. |
 | `CameraMigrationReview.md` | `Docs/` | Camera system migration tracking |
 | `BOOTSTRAP_AUDIT.md` | `_Scripts/System/Bootstrap/` | Bootstrap scene audit, execution order, DI registration |
 | `HEXRACE.md` | `_Scripts/Controller/Arcade/` | HexRace game mode technical reference |
@@ -1743,6 +1744,39 @@ At any total at most two adjacent colours show (e.g. +8 → 3 blue + 2 white). P
 - **Rolling out to another vessel**: add an `ElementalBarsView` to that vessel's HUD (or run the wirer), then assign it to the vessel's `SilhouetteController.elementBars`. No code changes.
 - **Performance**: petals render at ~88px — keep `maxTextureSize` small (128). One `Image` per petal (20 total), `raycastTarget` off, event-driven (no `Update`), `SetLevel`/`RefreshBar` early-out when nothing changed, tweens `SetLink`ed and killed + snapped to rest on `OnDisable` for pooled/toggled HUDs.
 
+### Quest Track & Breadcrumb (Player Activation)
+
+The **Quest Track** (formerly the "XP track") and the **Breadcrumb / Call-to-Action**
+system compose into one player-activation engine. The quest track is a **chain of
+unlocks** — each unlock reveals a new app feature and is gated by a **quest** (a
+completion condition); claiming a completed quest reveals the next one. The breadcrumb
+can highlight **any** element of the app shell (and the whole nested path to it via
+dependency targets), so the two systems together can guide a player anywhere. **The
+track decides what's next; the breadcrumb walks the player there.**
+
+Canonical design + engineer work plan: **`Docs/QuestSystem/ARCHITECTURE.md`** (design of
+record, current-vs-target tagged) and **`Docs/QuestSystem/HANDOFF.md`** (migration +
+retirement checklist). Visual aid:
+<https://claude.ai/design/p/e205f3d2-aa6a-41b6-985e-e4d8f8ee17b9?file=Quest+Track+%2B+Breadcrumb.dc.html>.
+
+- **Spine (keep, generalize):** `GameModeProgressionService` + `GameModeProgressionData`
+  (cloud-persisted via `UGSDataService.ProgressionRepo`) + `SO_GameModeQuestData` /
+  `SO_GameModeQuestList` + `SO_ProgressionConfig`, rendered by `QuestTrackView` /
+  `QuestItemCard`. Generalize so an unlock can reveal **any feature kind** (GameMode,
+  Vessel, IntensityTier, Screen, Captain, Episode, UIElement), not just game modes.
+- **Breadcrumb (keep, wire to spine):** `CallToActionSystem` / `CallToAction` /
+  `CallToActionTarget` (`CallToActionTargetType`), driven by `UserActionSystem` /
+  `UserActionType`. Today it is wired to the legacy quest stack, **not** the progression
+  service — closing that wire is the core deliverable.
+- **Locked constraints (do not violate):** **C1** XP removed entirely — quest completion
+  is the only progression currency; **C2** single guidance channel — all in-app guidance
+  routes through the CTA/breadcrumb (no bespoke highlights/tutorials); **C3** continuity
+  law — unlock reveals and CTA indicators bloom/fade, never pop; **C4** unlocks persistent
+  & monotonic with exactly one cloud-persisted source of truth.
+- **Retire-targets:** all XP code (`SO_XPTrackData`, `SO_XPTrackReward`, `XPTrackView`,
+  `ParticipationXpAwarder`, `PlayerDataService` XP members) and the legacy
+  `Quest` / `QuestSystem` / `UserJourneySystem` / `SO_QuestChain` stack.
+
 ### Namespace Convention
 
 All game code lives under `CosmicShore.*` with 8 primary namespaces:
diff --git a/Docs/MENU_PROGRESSION_AND_IAP.md b/Docs/MENU_PROGRESSION_AND_IAP.md
index fbeb978de..b0d1dd434 100644
--- a/Docs/MENU_PROGRESSION_AND_IAP.md
+++ b/Docs/MENU_PROGRESSION_AND_IAP.md
@@ -45,6 +45,13 @@ Do **not** remove `HangarScreen : IScreen` or the `_lastLoadFrame` double-load g
 > participation odometer (+25 XP/game, win or lose) that currently grants nothing.
 > Games unlock through a **quest chain**; vessels unlock by **spending crystals**;
 > intensity tiers unlock by **playing**. All three are independent of XP.
+>
+> **⚠️ Decision (XP removed entirely):** XP is being **deleted**, not kept as a cosmetic
+> odometer. Quest completion is the only progression currency going forward. The quest
+> chain + the Call-to-Action breadcrumb are being unified into one player-activation
+> engine — see **`Docs/QuestSystem/ARCHITECTURE.md`** (design of record) and
+> **`Docs/QuestSystem/HANDOFF.md`** (migration + retirement checklist). § 2d below is
+> retained only to describe what is being removed.
 
 ### 2a. Arcade game modes — quest chain
 
@@ -83,9 +90,20 @@ The locked intensity buttons live in `ArcadeGameConfigureModal` (`IsIntensityUnl
 - **Access to the Hangar feature** is gated by the quest chain: `IsVesselHangarUnlocked()` returns true once every quest *before* the quest named `SO_ProgressionConfig.vesselHangarQuestDisplayName` ("VESSEL HANGAR") is completed.
 - **Individual vessels** are gated by **crystals**, not XP or quests. Lock state + price live on the `SO_Vessel` asset (`isLocked`, `UnlockCost`, default 100). `VesselUnlockSystem.TryPurchaseVessel` spends crystals via `PlayerDataService.TrySpendCrystals` and persists the unlock to the Hangar cloud repo.
 
-### 2d. XP track (cosmetic)
+### 2d. XP track (REMOVED — see `Docs/QuestSystem/`)
 
-`ParticipationXpAwarder` awards a flat `participationXpPerGame` (default 25) to the local player each game, feeding the menu XP bar via `PlayerDataService.AddXP`. The `XPTrackView` renders milestones from `SO_XPTrackData`, but **its milestone rewards grant nothing** — `PlayerDataService.UnlockReward` has no callers. If you want XP to actually grant content later, wire `SO_XPTrackReward.unlockType` / `unlockReferenceId` to a grant call; the seam exists but is unused.
+> **Status: retire-target.** Per the decision above, XP is being removed entirely. Quest
+> completion replaces it as the only progression currency. The files described here are
+> the deletion targets — see `Docs/QuestSystem/HANDOFF.md` § 3 for the full retirement
+> checklist. Do **not** add new XP grants or wire `SO_XPTrackReward` to content.
+
+What exists today (all to be deleted): `ParticipationXpAwarder` awards a flat
+`participationXpPerGame` (default 25) to the local player each game, feeding the menu XP
+bar via `PlayerDataService.AddXP`. The `XPTrackView` renders milestones from
+`SO_XPTrackData`, but **its milestone rewards grant nothing** — `PlayerDataService.UnlockReward`
+has no callers. Retire-targets: `SO_XPTrackData`, `SO_XPTrackReward`, `XPTrackView`,
+`ParticipationXpAwarder`, and the XP members in `PlayerDataService`
+(`GetXP` / `AddXP` / `UnlockReward`).
 
 ---
 
diff --git a/Docs/QuestSystem/ARCHITECTURE.md b/Docs/QuestSystem/ARCHITECTURE.md
new file mode 100644
index 000000000..1a22724a6
--- /dev/null
+++ b/Docs/QuestSystem/ARCHITECTURE.md
@@ -0,0 +1,242 @@
+# Quest Track + Breadcrumb — Player-Activation Engine
+
+_Cosmic Shore · Unity 6 / C# · Froglet Inc._
+
+**Design visual (Claude design tool):** <https://claude.ai/design/p/e205f3d2-aa6a-41b6-985e-e4d8f8ee17b9?file=Quest+Track+%2B+Breadcrumb.dc.html>
+**Engineer handoff / work plan:** [`HANDOFF.md`](./HANDOFF.md)
+
+Two systems compose into one activation engine. The **Quest Track** is a chain of
+**Unlocks**; each Unlock reveals a new app feature and is gated by a **Quest** (a
+completion condition). The **Breadcrumb** (Call-to-Action) system can highlight ANY
+element of the app shell — including the whole nested path to it — to guide the player
+to whatever they need to do next. **The quest track decides WHAT'S NEXT; the breadcrumb
+guides the player THERE.**
+
+This doc is the canonical home for the design. It records both the **current** code
+reality and the **target** unified design. Each item below is tagged:
+
+- **[SHIPPED]** — exists and works today.
+- **[TARGET]** — part of this design, not yet built.
+- **[RETIRE]** — exists today but is being deleted/replaced by this design.
+
+> This is the design of record, authored from the visual aid above. The work to reach
+> the target state is sequenced in [`HANDOFF.md`](./HANDOFF.md).
+
+---
+
+## Locked constraints (invariants — do not violate)
+
+- **C1 — XP REMOVED ENTIRELY.** Quest completion is the only progression currency. No
+  XP storage, display, award, or gating anywhere. (Earlier the track was the "XP track";
+  XP is now gone, not made cosmetic.)
+- **C2 — SINGLE GUIDANCE CHANNEL.** Every in-app "go here / do this" hint routes through
+  the CTA/breadcrumb system. No bespoke per-feature arrows, highlights, or one-off
+  tutorial overlays.
+- **C3 — CONTINUITY LAW.** Unlock reveals and CTA highlight indicators must
+  bloom / grow / fade over a visible transition; nothing pops instantly into or out of
+  existence (this is the platform-wide continuity law — see root `CLAUDE.md`).
+- **C4 — UNLOCKS PERSISTENT & MONOTONIC.** Once revealed, a feature stays unlocked across
+  sessions (cloud-persisted). Progression is forward-only except via explicit debug/reset.
+  Exactly ONE authoritative, cloud-persisted source of truth.
+
+---
+
+## Legend (both layers)
+
+| Glyph | Meaning |
+|---|---|
+| ⬡ Unlock node | a single `{ feature, gating Quest, breadcrumb Target(s) }` |
+| ⬢ Quest gate / asset | completion condition / authorable ScriptableObject |
+| ◎ Breadcrumb target | app-shell element the CTA lights (blooms/fades — C3) |
+| ▤ Persisted state | cloud-saved record |
```

</details>
