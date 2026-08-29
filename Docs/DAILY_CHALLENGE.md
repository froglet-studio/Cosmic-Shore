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
| **Seats** | Pinned to the card's `MinPlayersAllowed` (never below the humans actually present). |
| **Intensity** | Pinned to the challenge's, and **not** clamped to the player's unlocks. |

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
                      └─ gameData.IsDailyChallenge = true

  ── game scene ──
  OnMiniGameTurnStarted   → clock starts, local metric polled each frame
    target reached  →  record + gameData.InvokeGameTurnConditionsMet()
    time expired    →  record + gameData.InvokeGameTurnConditionsMet()
    mode ended first→  OnMiniGameTurnEnd → record whatever was achieved
  OnMiniGameEnd           → clear the attempt, IsDailyChallenge = false
  OnSessionEnded          → clear WITHOUT recording (an abandon is not a failed attempt)
```

The attempt ends the turn through **the mode's own end channel**
(`GameDataSO.InvokeGameTurnConditionsMet`, the one `TurnMonitorController` raises) rather than
tearing the scene down itself, so the scoreboard, stats reporting and replay flow are untouched.
The raise is gated on being the launch authority — a client raising it would end the turn on its
machine alone and desync the match.

---

## 4. Authoring the pool

**FrogletTools has no window for this yet** — edit `Assets/Resources/DailyChallengeCatalog.asset`
in the inspector. One entry per mode:

| Field | Meaning |
|---|---|
| `Mode` | The arcade mode. Must have a card in `SO_GameList` and a live scene. |
| `Metric` | The per-player stat counted. Normally **the mode's own scoring metric**. |
| `Target` | How much of it the local player must reach. |
| `TimeLimitSeconds` | Budget from the turn starting. `0` = no limit. |
| `Intensity` | Played at this intensity, for everyone. |
| `Verb` / `Noun` | Objective copy: `"Collect" 30 "crystals"` → *Collect 30 crystals in 1:00*. |

### Four rules for a target that can actually be met

1. **Keep the target UNDER the mode's own end condition.** A mode ends when a *domain* reaches its
   race target (`EndConditionOverridesSO`), and that ends the challenge run with it. Crystal
   Capture ends at **20** crystals per domain, so a "collect 30 crystals" challenge there is
   unreachable — the turn is over at 20. The shipped entry asks for **15**.
2. **The metric must be credited PER PLAYER.** `ScoringMetrics.Read` reads one player's stats.
   Nucleus Rush is deliberately **not** in the pool for this reason: it credits a domain's
   *representative* player (`NucleusRushController` line ~115), so a personal count there is not
   the player's own. Astro League and Scarab Scramble credit the actual scorer and are fine.
3. **Match the metric to what the mode surfaces.** A challenge counting something the mode's HUD
   does not show leaves the player with no readout of their own progress.
4. **Keep the intensity inside the mode's `MinIntensity`/`MaxIntensity`.** It is clamped to that
   range at open time, so an out-of-range value silently becomes a different challenge.

### What ships

11 entries: Crystal Capture (Scurry), Skim Race, Joust, Rampage, Ribcage, Salvo, Dog Fight, The
Bends, Astro League, Scarab Scramble, Wildlife Liberation — all at intensity 1, 60–120 s.
Reordering the list **re-rolls which date draws which mode**, because order is part of the draw;
append rather than insert if that matters.

---

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
- **No attempt limit.** `DailyChallengeService.DailyAttempts` is `0` (unlimited). The ticket
  plumbing is present and honoured if it is raised, but nothing sells more tickets, so a player
  who ran out would simply be locked out for the day.
- **The card prefab.** Only `GameTitle`, `TimeRemaining` and `BackgroundImage` are wired today.
  `ObjectiveText`, `StatusText` and `CompletedBadge` are optional and unwired — the card is
  readable without them, which is deliberate, but the objective line is the most useful thing it
  can say. The tile's `Button` also needs to be interactable again (it was hard-disabled while the
  feature was shelved).
- **Replay after completion.** `DailyChallengeCard.allowReplayAfterCompletion` is `false`: once the
  objective is met the tile stops accepting input and counts down to the next challenge. The MODE
  is still playable from its own card on the grid — only the daily objective is done for the day.

## 7. Superseded

`_Scripts/System/DailyChallengeSystem.cs`, `UI/Modals/DailyChallengeModal.cs` and
`UI/Views/DailyChallengeGameView.cs` are the PlayFab-era implementation: PlayerPrefs storage, a
`SO_TrainingGame` pool, a three-tier reward ladder, and an `Arcade` singleton that **is in no
scene**. They are inert (nothing here reads them, and `DailyChallengeSystem` is in no scene either)
and are left in the tree rather than deleted, because the reward ladder is the one idea in them
worth reviving. Do not wire both.
