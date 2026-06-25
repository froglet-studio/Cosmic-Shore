# Quest Track + Breadcrumb — Engineering Handoff

**Owner:** Shombith
**Design visual (Claude design tool):** <https://claude.ai/design/p/e205f3d2-aa6a-41b6-985e-e4d8f8ee17b9?file=Quest+Track+%2B+Breadcrumb.dc.html>
**Canonical design doc:** [`ARCHITECTURE.md`](./ARCHITECTURE.md)

## 1. Intent

Fuse two systems that already exist in the repo into **one player-activation engine**:

- **Quest Track** — a chain of **unlocks**. Each unlock reveals a new app feature and is
  gated by a **quest** (a completion condition). Claiming a completed quest reveals the
  next unlock. This *replaces the XP track*.
- **Breadcrumb (Call-to-Action)** — a guidance layer. Any element of the app shell can be
  highlighted, and the system can light the **whole nested path** to it (dependency
  targets), so we can steer a player anywhere.

They compose: **the track decides what's next; the breadcrumb walks the player there.**

## 2. Locked invariants (do not violate)

- **C1 — XP is removed entirely.** No XP odometer, award, storage, display, or gating.
  Quest completion is the *only* progression currency.
- **C2 — Single guidance channel.** Every in-app "go here / do this" routes through the
  CTA/breadcrumb system. No bespoke arrows, highlights, or one-off tutorial overlays.
- **C3 — Continuity law.** Unlock reveals and CTA highlight indicators must
  bloom/grow/fade in and out over a visible transition — nothing pops instantly
  (platform-wide law).
- **C4 — Persistent & monotonic, one source of truth.** Once revealed, a feature stays
  revealed across sessions; progression is forward-only (except explicit debug/reset).
  Exactly one cloud-persisted progression record is authoritative.

## 3. Current code

**Spine — KEEP & generalize**

- `Assets/_Scripts/System/Progression/GameModeProgressionService.cs` — runtime
  (`DontDestroyOnLoad`), cloud-persisted via `UGSDataService.ProgressionRepo`. Events:
  `OnProgressionChanged`, `OnQuestCompleted`, `OnIntensityUnlocked`. Evaluates quests in
  `HandleGameEnd` → `ReportQuestStat` / `RecordIntensityPlay`; advances via
  `ClaimQuestAndUnlockNext`.
- `Assets/_Scripts/System/Progression/GameModeProgressionData.cs` — persisted state:
  `UnlockedModes`, `CompletedQuests`, `BestStats`, `MaxUnlockedIntensity`,
  `IntensityPlayCounts`.
- `Assets/_Scripts/ScriptableObjects/SO_GameModeQuestData.cs` + `SO_GameModeQuestList.cs`
  — authorable chain. `QuestTargetType` = {CrystalsCollected, RaceTimeUnder, JoustsWon,
  ScoreAbove, SurvivalTime, WinMatch, IntensityUnlocked, Placeholder}.
- `Assets/_Scripts/ScriptableObjects/SO_ProgressionConfig.cs` — tunable rules (asset:
  `_SO_Assets/GameModeQuest/ProgressionConfig.asset`).
- UI: `Assets/_Scripts/UI/Views/QuestTrackView.cs` +
  `Assets/_Scripts/UI/Elements/QuestItemCard.cs` (states Locked/Unlocked/ReadyToClaim/Claimed).

**Breadcrumb — KEEP & wire to spine**

- `Assets/_Scripts/System/CallToAction/CallToActionSystem.cs`, `CallToAction.cs`,
  `CallToActionTarget.cs`.
- `Assets/_Scripts/Data/Enums/CallToActionTargetType.cs` (ArcadeMenu, HangarMenu,
  HangarShip*, PlayGame*, StoreMenu, …), `UserActionType.cs`.
- `Assets/_Scripts/System/UserAction/` — `UserActionSystem.cs`, `UserAction.cs`,
  `UserActionTrigger.cs`.

**The gap to close:** today the breadcrumb is driven by an **older, mostly-stubbed**
stack — `QuestSystem.AddQuest()` is the only thing that calls
`CallToActionSystem.AddCallToAction()`, and it's fed by `UserJourneySystem` walking an
`SO_QuestChain` of plain `Quest` objects. The new `GameModeProgressionService` does **not**
raise any CTAs. We re-point the breadcrumb at the progression service and retire the old
stack.

**DEPRECATED — retire**

- XP: `Assets/_Scripts/System/Progression/ParticipationXpAwarder.cs`,
  `Assets/_Scripts/UI/Views/XPTrackView.cs`,
  `Assets/_Scripts/ScriptableObjects/SO_XPTrackData.cs` + `SO_XPTrackReward.cs`, and the XP
  members in `Assets/_Scripts/UI/Views/PlayerDataService.cs` (`GetXP`/`AddXP` ~L291/301,
  `UnlockReward` ~L347).
- Old quest stack: `Assets/_Scripts/System/Quest/Quest.cs` + `QuestSystem.cs`,
  `Assets/_Scripts/System/UserJourney/UserJourneySystem.cs`,
  `Assets/_Scripts/ScriptableObjects/SO_QuestChain.cs`.

## 4. Target architecture

- **Spine = `GameModeProgressionService`, generalized** so an *unlock* can reveal **any
  feature kind** — GameMode, Vessel, IntensityTier, Screen, Captain, Episode, UIElement —
  not just modes. Each unlock carries a **breadcrumb target id** + a **completion
  condition**.
- **New wire (the core deliverable):** when a quest becomes the *active frontier*, the
  progression service raises the breadcrumb for that unlock's target **plus its dependency
  path**; when the satisfying `UserAction` fires (or the player claims), the breadcrumb
  dismisses. **Recommend doing this wire as a SOAP `ScriptableEvent` channel** (house
  style is SOAP over singletons/static events for cross-system comms), not a direct
  `.Instance` call — decision point in § 6.
- **One cloud source of truth:** `UGSDataService.ProgressionRepo` (already in place). No
  second progression store.

## 5. Work breakdown (ordered)

1. **Generalize the unlock model.** Add a `FeatureKind` + `breadcrumbTargetId`
   (`CallToActionTargetType`) to the unlock data, and a `SpendCurrency` completion
   condition (so vessel/crystal unlocks fold in). Keep `SO_GameModeQuestList` as the
   ordered chain. *Acceptance:* a non-mode unlock (e.g. Vessel Hangar, already in the
   chain) authorable end-to-end with no code change.
2. **Close the breadcrumb wire.** Progression frontier change ⇒ raise CTA(target +
   dependency path); satisfying UserAction/claim ⇒ dismiss. *Acceptance:* completing a
   quest auto-lights the path to the next thing; no reference to
   `QuestSystem`/`UserJourneySystem`.
3. **Remove XP entirely (C1).** Delete the XP files in § 3, strip XP members from
   `PlayerDataService`, remove the XP bar from the Profile screen. *Acceptance:* no XP
   symbols remain; build is green.
4. **Retire the legacy quest stack.** Delete `Quest`/`QuestSystem`/`UserJourneySystem`/
   `SO_QuestChain`. *Acceptance:* the CTA system has exactly one driver (the progression
   service).
5. **Continuity-law pass (C3).** Unlock reveals + `CallToActionTarget.ActiveIndicator`
   animate in/out (no instant `SetActive`).
6. **Author the chain in data** (designer task): seed = Crystal Capture(free) → Hex Race →
   Joust → Wildlife Blitz → Party Game → Vessel Hangar, each with its breadcrumb target.

## 6. Decisions needed

- **SOAP-ify the cross-system events?** The current service uses C# `event Action` +
  `*.Instance`. House style prefers SOAP channels. OK to convert the
  progression↔breadcrumb wire (and ideally the progression events) to `ScriptableEvent`?
- **Captain XP scope.** "Remove XP" clearly covers the menu/profile participation XP
  track. `CaptainProgressCloudData` (captain leveling) also stores `xp` — **is captain XP
  in scope or out?** Default assumption: **out** (separate system) unless decided
  otherwise.
- **Chain shape:** strictly linear, or do we need branching unlocks now?

## 7. Conventions (per root `CLAUDE.md`)

SOAP for cross-system comms; single-writer to the progression record; Reflex DI;
fail-loud (no null-guards on SOAP event fields); continuity law everywhere; no
decay/timers as a fix.
