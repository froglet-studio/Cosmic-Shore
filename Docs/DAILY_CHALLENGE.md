# The Daily Challenge

One curated objective per UTC day, the same one for every player, with each player's progress
against it synced to UGS Cloud Save. It is deliberately **not** a new game mode: it is an existing
arcade mode, launched through the existing arcade modal, with one personal objective and a time
budget attached.

---

## 1. The shape of it

| | |
|---|---|
| **Cycle** | 24h, keyed on the **UTC calendar day** (`yyyy-MM-dd`). Rolls at UTC midnight. |
| **Definition** | **Derived**, not stored — a pure function of the date over `DailyChallengeCatalogSO`. |
| **Progress** | **Stored** in UGS Cloud Save under `DAILY_CHALLENGE` (`DailyChallengeCloudData`). |
| **Objective** | `{metric, target}` read off the **local player's own** round stats. |
| **Time budget** | Seconds from the turn starting. Reaching the target **or** running out ends the turn. |
| **Size** | The run races to a **shortened** version of the mode's own end condition. |
| **Attempts** | **One per day**, spent at *launch*. |
| **Seats** | Pinned to the card's `MinPlayersAllowed`; **Add AI is unavailable**. |
| **Intensity** | Pinned to the challenge's, and **not** clamped to the player's unlocks. |
| **Domain** | Pinned to the challenge's (default **Jade**). |
| **Preview** | Plays under AI, but **tap-to-play is off** — look only. |
| **Authoring** | **FrogletTools ▸ Game Modes ▸ Daily Challenge**. |

### The one rule that makes it a daily challenge

> **The definition is derived; only the progress is stored.**

Today's challenge is `hash(UTC date) % pool.Count` over an authored pool
(`DailyChallengeCatalogSO.ForDate`). That is what buys, in one stroke:

- **No server round trip.** The card draws on a cold launch, and offline.
- **Everyone gets the same one.** Two clients on two platforms agree by construction. The hash is
  hand-written **FNV-1a**, not `System.Random` — `System.Random` is deterministic only *within one
  runtime's implementation*, which is not a promise two platforms can hold each other to.
- **Nothing to invalidate.** Cloud Save carries only what genuinely differs per player and
  genuinely has to survive a reinstall: best value, completed, attempt count.

### The daily run is a SMALLER version of the mode

A daily challenge is not the full match with an objective bolted on — it races to a shortened end
condition. Crystal Capture normally races a domain to **20** crystals; a daily run races to **12**,
and asks *you* for **8**.

`EndConditionOverridesSO` grew a **run-scoped override**: `SetRunOverride(mode, target)` /
`ClearRunOverride()`, consulted by every per-turn target accessor. It lives there rather than on
each monitor because the monitors already funnel through those accessors — one indirection covers
every mode, and a mode added later inherits it with nothing wired. It is `static` because the value
is set in the **menu** (at launch) and read in the **game scene** (`TurnMonitor.StartMonitor`),
which no serialized field spans; it is reset at `RuntimeInitializeOnLoadMethod` because statics
survive play-mode exit and a leaked override would silently shorten the next ordinary match.

**The race target is deliberately ABOVE the objective** (12 vs 8). The objective is *personal* while
an end condition is a *domain sum*, so a bot on your domain can push the domain total while you are
still working — leave them equal and the run can end on somebody else's crystal. Setting the race
target ~1.5× the objective means your own objective lands first in a normal run, and an opponent
racing ahead of you is a legitimate loss rather than an authoring bug.

`EndConditionOverridesSO.CanOverrideTurnTarget(mode)` says which modes this can reach.
**Astro League cannot be shortened** — its controller owns its own goal target — so a daily run of
it would be the full-length match with a clock on it. It is therefore **out of the shipped pool**;
the tool warns if anyone puts it (or a future mode like it) back in.

### Played once a day, and the attempt is spent at LAUNCH

`attemptsPerDay` (catalog, default **1**). The attempt is consumed in `BeginAttempt` and flushed
straight to Cloud Save rather than credited at the end — the one ordering that cannot be
save-scummed, because "played only once" has to survive an alt-F4 halfway through a bad run.
`DailyChallengeCloudData.RecordResult` therefore folds in the *result* only and never touches the
counter.

Running out does **not** lock the mode out: the card stops offering it as the day's objective and
counts down to the next one, while the mode stays on the arcade grid like any other. The card shows
three distinct states — `BEST n / target` (attempt available), `PLAYED — BEST n / target` (spent,
not met), `COMPLETE` — because a player who ran out without meeting the objective has **not**
completed it, and a card that said COMPLETE either way would be lying about their day.

### The launch panel states the CHALLENGE, not the mode

A daily challenge is a fixed ask with one attempt, not a lobby, and the panel is dressed to say so
(`ArcadeGameConfigureModal.ApplyDailyChallengePresentation`). Four things change, all from that one
fact:

| | Ordinary card | Daily challenge |
|---|---|---|
| **Objective box** | the mode's win condition | **the challenge** — *"Collect 8 crystals in 1:00"* |
| **Briefing** | the card's description + rotating tips | one line: **"Daily Challenge"** |
| **Add AI** | host may seat bots | **gone** — the seat count is the card's minimum, so there is no seat to take |
| **Preview** | tap to fly it | **live but look-only** — the arena still plays under AI |
| **Intensity / Domain tiles** | pickable | **pinned and dimmed**, because the choice is already made |

Two details are load-bearing:

- **The preview's own score must not tick the challenge's counter.** The box states the challenge;
  letting the AI's preview play move that number shows the player progress they have not made
  against an objective they have not started (`HandleObjectiveProgress` returns early).
- **A pinned domain other than Jade has to be REQUESTED, not just shown selected.** The tiles
  reflect `Player.NetDomain`, so highlighting one without the server round trip is exactly the "UI
  claims a domain the server never got" case `HandleDomainSelected` refuses to create. Jade needs no
  request — `MenuServerPlayerVesselInitializer` already resets every player to it on spawn, which is
  why it is the default.

Every one of these is **undressed again** when the next ordinary card opens: the panel is a shared
scene object, so an override applied here has to be handed back, and `SetSelectedGame` calls the
same method with the lock cleared.

### Rollover is a date comparison, never a timer

`DailyChallengeService` re-resolves `Today` whenever the UTC date key changes, checked once a
second. Nothing is scheduled for midnight. A device that slept across midnight, or whose clock
jumped, lands on the correct challenge at the next check — a pending timer would have been wrong in
both cases.

---

## 2. Files

| Role | File |
|---|---|
| The day's challenge (value type) | `_Scripts/Data/Structs/DailyChallenge.cs` |
| Authored pool + the draw | `_Scripts/ScriptableObjects/DailyChallengeCatalogSO.cs` |
| The pool asset | `Assets/Resources/DailyChallengeCatalog.asset` |
| The service (rollover, cloud, attempts) | `_Scripts/System/DailyChallenge/DailyChallengeService.cs` |
| Cloud record | `_Scripts/System/CloudData/Models/DailyChallengeCloudData.cs` |
| Cloud repository (pre-existing, now used) | `_Scripts/System/CloudData/Repositories/DailyChallengeRepository.cs` |
| The arcade tile | `_Scripts/UI/Elements/DailyChallengeCard.cs` |
| A "play today's challenge" shortcut | `_Scripts/UI/Elements/DailyChallengePlayButton.cs` |
| Launch route | `ArcadeExploreView.SelectDailyChallenge()` |
| Pinned launch config | `ArcadeGameConfigureModal.OpenForDailyChallenge()` |
| Run-scoped end condition | `_Scripts/ScriptableObjects/EndConditionOverridesSO.cs` (`SetRunOverride`) |
| **The editor tool** | `_Scripts/Editor/DailyChallengeWindow.cs` |
| Release gate for test mode | `_Scripts/Editor/Build/DailyChallengeTestModeBuildGuard.cs` |
| Tests | `_Scripts/Tests/Editor/DailyChallengeTests.cs` |

**Zero scene wiring for the service.** `DailyChallengeService` creates itself at
`RuntimeInitializeLoadType.AfterSceneLoad` on a `DontDestroyOnLoad` object — the shape
`VesselSpeedTunnel` uses — and is *handed* the `GameDataSO` it needs by whoever launches an
attempt, rather than hunting for one. There is no asset reference here that can drift.

---

## 3. The flow

```
DailyChallengeCard  (arcade grid)
  └─ ArcadeExploreView.SelectDailyChallenge()
      ├─ DailyChallengeService.Today            ← hash(UTC date) over the catalog
      ├─ FindGameByMode(challenge.GameMode)     ← the mode's SO_ArcadeGame
      └─ ArcadeGameConfigureModal.OpenForDailyChallenge(card, challenge)
          ├─ intensity pinned  (only that button active; HandleIntensitySelected refuses)
          ├─ seats pinned      (pcStepper min == max)
          └─ HandleAllPlayersReady
              └─ ArmDailyChallengeForLaunch()
                  └─ DailyChallengeService.BeginAttempt(gameData)
                      ├─ gameData.IsDailyChallenge = true
                      ├─ EndConditionOverridesSO.SetRunOverride(mode, raceTarget)   ← the SMALLER game
                      └─ spend the attempt + flush to cloud                          ← played once

  ── game scene ──
  OnMiniGameTurnStarted   → clock starts, local metric polled each frame
    target reached  →  record + gameData.InvokeGameTurnConditionsMet()
    time expired    →  record + gameData.InvokeGameTurnConditionsMet()
    mode ended first→  OnMiniGameTurnEnd → record whatever was achieved
  OnMiniGameEnd           → record, clear the attempt, clear the run override
  OnSessionEnded          → clear WITHOUT recording a RESULT (the attempt was already spent)
```

The attempt ends the turn through **the mode's own end channel**
(`GameDataSO.InvokeGameTurnConditionsMet`, the one `TurnMonitorController` raises) rather than
tearing the scene down itself, so the scoreboard, stats reporting and replay flow are untouched.
The raise is gated on being the launch authority — a client raising it would end the turn on its
machine alone and desync the match.

---

## 4. Authoring — FrogletTools ▸ Game Modes ▸ Daily Challenge

`DailyChallengeWindow` edits `Assets/Resources/DailyChallengeCatalog.asset` (created on first
open) and is the intended surface. The inspector still works; what the window adds is the
**validation**, because three of the four ways to author an unplayable challenge are invisible in a
plain field list and every one of them has been hit at least once.

**The layout is master/detail behind three tabs**, not a field list. Ten entries × nine fields is a
page and a half of scrolling to compare two numbers 400 px apart. The list on the left is one row
per entry carrying the three things you actually scan for — is it on, what does it ask, is it
broken (a coloured stripe plus an `OK` / `2 ⚠` / `1 ✕` pill) — and only the **selected** entry
spends vertical space on fields. *Pool*, *Next 7 days* and *Testing* are tabs rather than more
sections because they are different questions, and stacking them into one scroll makes every one of
them harder. The detail pane also carries a **Size** line — *"a normal match races to 20 — this
daily run races to 12"* — so the premise of the whole feature is not something you have to go and
look up.

### Per-entry fields

| Field | Meaning |
|---|---|
| `Enabled` | Park an entry without deleting it. **Re-rolls the rotation** — the draw indexes the *enabled* entries. |
| `Mode` | The arcade mode. Must have a card in `SO_GameList` and a scene. |
| `Metric` | The per-player stat counted. Normally the mode's own scoring metric. |
| `Target` | What the **local player** must reach. |
| `EndConditionOverride` | The mode's race target for a **daily** run — this is what makes it smaller. `0` = use `Target`. |
| `TimeLimitSeconds` | Budget from the turn starting. `0` = no limit. |
| `Intensity` | Played at this intensity, for everyone. |
| `Domain` | The colour the player flies. Jade is the default and the only one needing no server request. |
| `Verb` / `Noun` | Objective copy: `"Collect" 8 "crystals"` → *Collect 8 crystals in 1:00*. |

### What the tool checks, and why each check exists

| Check | Severity | Why |
|---|---|---|
| Mode has an `SO_ArcadeGame`, with a scene | error | Otherwise the tile does nothing on whichever date draws it. |
| Intensity inside the card's `Min`/`MaxIntensity` | error | It is silently clamped at launch, so the challenge quietly becomes a different one. |
| **Objective ≤ the run's race target** | error | The run ends when the race target is met, which ends the challenge with it. Crystal Capture races to 20, so *"collect 30"* there can never complete. |
| Race target < the mode's normal target | warning | If it is not smaller, it is the full-length match with a clock on it. |
| Mode's end condition is reachable by the override | warning | Astro League's controller owns its goal target — only the clock shortens it. |
| Metric credited **per player** | error | Nucleus Rush credits a domain's *representative*, so a personal objective there measures the wrong thing. It is out of the pool for this reason. |
| Domain is one a player flies | error | Blue is the "no team" sentinel, and `Domains` has no member at 0 — what a pre-field entry deserializes to. Both fall back to Jade. |
| Time limit ≥ 15 s | warning | Under that the run is over before the player has control. |
| Mode duplicated in the pool | warning | It comes up that much more often than the others. |

The window also previews **which mode the next 7 days draw**, so a reorder's effect on the rotation
is visible before it is committed, and draws the standard `FrogletToolShipPanel` — its output is
one asset in the working tree, and the panel is what makes "I edited it" and "the branch has it"
the same event.

### Two IMGUI traps this window paid for

**Never carve a pane out of `GUILayoutUtility.GetRect(0, 0, ExpandWidth, ExpandHeight)` and hand it
to `GUILayout.BeginArea`.** An expanding `GetRect` returns its *minimum* (0×0) during the **Layout**
event and the resolved rect only on **Repaint**, so every layout control inside the area lays itself
out against a zero-width viewport and draws nothing — while non-layout `GUI.*` calls in the same
area, which only need their rect on Repaint, keep working perfectly. That asymmetry is what makes it
present as *"the right-hand panel is broken"* rather than *"the container is wrong"*: the first
build of this window drew its entry list (explicit rects) and rendered the detail pane, Preview and
Testing tabs as empty boxes. Build panes out of **layout groups** — a horizontal group with a
fixed-width vertical on the left and scroll views that expand on their own — which resolve
identically in both passes. A **fixed**-height `GetRect` inside a layout group is fine and is how
the rows are drawn (the same shape `FrogletMasterToolWindow` uses).

**Queue structural edits; never mutate the list mid-`OnGUI`.** Add / remove / reorder / duplicate
change the control count between Layout and Repaint, which IMGUI reports as *"changed between
layout and repaint"* and draws through as a flickering, half-built pane. The window stores one
`Action _deferred` and runs it after the pass. It also fixes a second bug in the same place: the
first Remove button `return`ed out of `DrawEntryDetail` while still inside a `BeginHorizontal`,
leaving the group unbalanced.

### Test mode

Everything under **Testing** is inert unless the master switch is on *and* the game is running in
the editor or a development build (`DailyChallengeCatalogSO.TestActive`). On top of that,
`DailyChallengeTestModeBuildGuard` **fails a non-development build outright** while the switch is
set — the runtime gate already makes it harmless, and the guard makes it *loud*, because a flag
left set should never be silent.

| Setting | Effect |
|---|---|
| `forcedPoolIndex` | Pin the draw to one entry instead of hashing the date. Indexes the pool as the tool shows it. |
| `dayLengthMinutes` | A "day" becomes this many real minutes, so rollover is testable. |
| `ignoreAttemptLimit` | Replay the challenge while tuning it. |
| `timeLimitScale` | Multiplies every clock — `0.25` turns 60 s into 15 s. |

A shortened period's key is a **different shape** (`T4823…`) from a real date key (`2026-08-29`).
That is deliberate: a record written under a shrunken cycle can never be read as a real day's
progress, so switching back **wipes** it — the honest outcome rather than a blended one.

**Reset today's progress** clears this machine's cached record. In play mode it also rewrites the
live cloud record through `DailyChallengeService.ResetTodayForTesting()` (itself refused outside the
editor and development builds); outside play mode only the local snapshot goes, and the cloud copy
returns on the next sign-in.

### What ships

10 entries, all at intensity 1, 60–90 s, `attemptsPerDay = 1`, test mode off:

| Mode | Objective | Daily race target | Normal | Clock |
|---|---|---|---|---|
| Crystal Capture (Scurry) | 8 crystals | 12 | 20 | 1:00 |
| Skim Race | 10 crystals | 15 | auto (~39) | 1:30 |
| Joust | 1 joust | 2 | 3 | 1:00 |
| Rampage | 300 prisms | 450 | 2000 | 1:30 |
| Peel the Cage | 300 prisms | 450 | 2000 | 1:30 |
| Salvo | 150 prisms | 225 | 700 | 1:30 |
| Dog Fight | 20 points | 30 | 90 | 1:30 |
| The Bends | 1 bend | 2 | 3 | 1:30 |
| Scarab Scramble | 3 goals | 5 | 10 | 1:30 |
| Wildlife Liberation | 8 creatures | 12 | 30 | 1:30 |

Reordering the list **re-rolls which date draws which mode**; append rather than insert if that
matters.

## 5. Two design calls worth not re-litigating

**The objective is PERSONAL, never a domain sum.** "Score 30 crystals" is an ask of *you*. A domain
sum would let the AI seated beside you finish your challenge — which is exactly what would happen,
since the challenge seats the card's minimum and the rest are bots.

**Mode progression locks are IGNORED** (`respectModeProgression`, default `false`). The daily
challenge is a curated invitation into a mode you may not have reached yet. Honouring the lock
would mean skipping that entry *per player*, and two players would no longer share a date's
challenge — which is the one promise the whole design is built on. Flip the flag on the catalog
asset to change it.

---

## 6. What is still open

- **No in-game readout.** `DailyChallengeService.OnAttemptProgress(achieved, target, secondsLeft)`
  fires every frame of a live run and nothing subscribes to it yet. A HUD element binding that
  event is the natural next step; the toast feed was considered and rejected, because a toast
  situation shows nothing until every mode's `GameToastConfigSO` authors it.
- **The card prefab.** Only `GameTitle`, `TimeRemaining` and `BackgroundImage` are wired today.
  `ObjectiveText`, `StatusText` and `CompletedBadge` are optional and unwired — the card is
  readable without them, which is deliberate, but the objective line is the most useful thing it
  can say. The tile's `Button` also needs to be interactable again (it was hard-disabled while the
  feature was shelved).
- **Astro League is out of the pool** because its end condition cannot be shortened (§4). Giving
  it an entry in `EndConditionOverridesSO` would let it back in, but that changes a shipped mode's
  authoring surface, so it is left for a decision rather than done in passing.
- **No reward.** Completing the challenge records a completion and nothing else. The PlayFab-era
  three-tier ladder (§7) is the obvious thing to revive.

## 7. Superseded

`_Scripts/System/DailyChallengeSystem.cs`, `UI/Modals/DailyChallengeModal.cs` and
`UI/Views/DailyChallengeGameView.cs` are the PlayFab-era implementation: PlayerPrefs storage, a
`SO_TrainingGame` pool, a three-tier reward ladder, and an `Arcade` singleton that **is in no
scene**. They are inert (nothing here reads them, and `DailyChallengeSystem` is in no scene either)
and are left in the tree rather than deleted, because the reward ladder is the one idea in them
worth reviving. Do not wire both.
