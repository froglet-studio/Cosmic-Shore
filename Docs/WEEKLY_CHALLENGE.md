# The Weekly Challenge

One curated objective per UTC week, the same one for every player, with each player's progress
against it synced to UGS Cloud Save. It is deliberately **not** a new game mode: it is an existing
arcade mode, launched through the existing arcade modal, with one personal objective and a time
budget attached.

> **Renamed from "daily challenge" (2026-09-01.)** The cycle is a week, the countdown reads in days,
> and every live type carries the `WeeklyChallenge` name. The inert PlayFab-era cluster
> (`DailyChallengeSystem`, `DailyChallengeModal`, `DailyChallengeGameView`,
> `DailyChallengeLeaderboardView`, `DailyChallengeRewardState`, the ticket/reward economy around
> them) keeps its old names on purpose — it is a *different, dead* feature, and renaming it would
> make two systems look like one. See §7.

---

## 1. The shape of it

| | |
|---|---|
| **Cycle** | One week, keyed on its **UTC Monday** (`yyyy-MM-dd`). Rolls at UTC Monday 00:00. |
| **Definition** | **Derived**, not stored — a pure function of the week over `WeeklyChallengeCatalogSO`. |
| **Progress** | **Stored** in UGS Cloud Save under `WEEKLY_CHALLENGE` (`WeeklyChallengeCloudData`). |
| **Objective** | `{metric, target}` read off the **local player's own** round stats. |
| **Time budget** | Seconds from the turn starting. Reaching the target **or** running out ends the turn. |
| **Size** | **The mode's own end conditions, untouched.** The run races to that mode's authored target. |
| **Attempts** | **One per week**, spent at *launch*. |
| **Seats** | Pinned to the card's `MinPlayersAllowed`; **Add AI is unavailable**. |
| **Intensity** | Pinned to the challenge's, and **not** clamped to the player's unlocks. |
| **Domain** | Pinned to the challenge's (default **Jade**). |
| **Preview** | Plays under AI, but **tap-to-play is off** — look only. |
| **Authoring** | **FrogletTools ▸ Game Modes ▸ Weekly Challenge**. |

### The one rule that makes it a weekly challenge

> **The definition is derived; only the progress is stored.**

This week's challenge is `hash(UTC Monday) % pool.Count` over an authored pool
(`WeeklyChallengeCatalogSO.ForDate`). That is what buys, in one stroke:

- **No server round trip.** The card draws on a cold launch, and offline.
- **Everyone gets the same one.** Two clients on two platforms agree by construction. The hash is
  hand-written **FNV-1a**, not `System.Random` — `System.Random` is deterministic only *within one
  runtime's implementation*, which is not a promise two platforms can hold each other to.
- **Nothing to invalidate.** Cloud Save carries only what genuinely differs per player and
  genuinely has to survive a reinstall: best value, completed, attempt count.

### The week starts on the UTC MONDAY

`WeeklyChallengeCatalogSO.WeekStartUtc` is the one function every other period answer is derived
from — the key, the rollover, the countdown.

**Monday because ISO-8601 says so**, and a week boundary is exactly the kind of thing that must not
be a matter of taste: a client that started weeks on Sunday would draw a different challenge from
its neighbour for one day in seven, and only for players in that day. **UTC** for the same reason
the day cycle used it — a local-time boundary makes the challenge change at a different moment in
every timezone.

The trap the implementation guards, and the reason it is tested: **`DayOfWeek` numbers Sunday `0`**,
so the naive `date.AddDays(-(int)date.DayOfWeek)` puts Sunday at the *start* of the following week.
`((int)DayOfWeek + 6) % 7` shifts Sunday to the end where ISO wants it.

### The run uses the MODE'S OWN end conditions

**A weekly run is an ordinary match of its mode.** Nothing in the challenge shortens, lengthens or
otherwise touches that mode's end condition — Crystal Capture races a domain to its authored 20, and
the challenge asks *you* for 8 of them along the way.

> **Reverted deliberately (2026-09-01).** An earlier pass gave each entry an `EndConditionOverride`
> and applied it through a run-scoped `EndConditionOverridesSO.SetRunOverride`, so that a weekly run
> was a *smaller* version of the mode. That whole mechanism is **removed** — the override fields, the
> static run-scoped target, and `CanOverrideTurnTarget`. Do not reintroduce it without a design call.
>
> **This is the ONLY thing that was reverted.** The launch panel's locks — intensity, domain, seat
> count, Add AI — are unchanged and deliberate: *the ASK is fixed, the MODE is not altered*. Those
> are two separate decisions and it is worth keeping them separate, because the natural reading of
> "the run is an ordinary match" is that the card should be configurable too, and it should not be.

**The one authoring rule that survives it** is the same trap in a new place: the turn ends when the
mode's own race target is met, so **an objective above what a match of that mode can produce is
unreachable by construction**. The editor tool errors on it, and `WeeklyChallengeTests` asserts it
over the shipped catalog. The objective should also sit meaningfully *below* that target, because
the objective is *personal* while an end condition is a *domain sum* — set them equal and a teammate
can end the run on somebody else's score.

### Played once a week, and the attempt is spent at LAUNCH

`attemptsPerPeriod` (catalog, default **1**). The attempt is consumed in `BeginAttempt` and flushed
straight to Cloud Save rather than credited at the end — the one ordering that cannot be
save-scummed, because "played only once" has to survive an alt-F4 halfway through a bad run.
`WeeklyChallengeCloudData.RecordResult` therefore folds in the *result* only and never touches the
counter.

Running out does **not** lock the mode out: the card stops offering it as the day's objective and
counts down to the next one, while the mode stays on the arcade grid like any other. The card shows
three distinct states — `BEST n / target` (attempt available), `PLAYED — BEST n / target` (spent,
not met), `COMPLETE` — because a player who ran out without meeting the objective has **not**
completed it, and a card that said COMPLETE either way would be lying about their day.

### The launch panel states the CHALLENGE, not the mode

A weekly challenge is a fixed ask with one attempt, not a lobby, and the panel is dressed to say so
(`ArcadeGameConfigureModal.ApplyWeeklyChallengePresentation`). Four things change, all from that one
fact:

| | Ordinary card | Weekly challenge |
|---|---|---|
| **Briefing** | the card's description + rotating tips | **the objective** — *"Score 20 combat points in 1:30"* |
| **Objective box** | the mode's win condition + a live counter | **hidden** |
| **Add AI** | host may seat bots | **gone** — the seat count is the card's minimum, so there is no seat to take |
| **Intensity / Domain tiles** | pickable | **pinned and dimmed**, because the choice is already made |
| **Preview** | tap to fly it | **live but look-only** — the arena still plays under AI |

**What is NOT pinned is the mode's end conditions** (above). The controls are locked because the
*ask* is fixed; the *mode* is played exactly as it always is. Keep those two ideas apart — the
natural reading of "the run is an ordinary match" is that the card should be configurable too, and
it should not be.

Two details are load-bearing:

- **The objective box is hidden rather than repurposed.** It exists to pair a mode's win condition
  with a live counter; here there is nothing to count until the run starts, so a box repeating the
  briefing beside a `0` says the same thing twice and one of them wrongly. The preview's own AI score
  is suppressed for the same reason (`HandleObjectiveProgress` returns early) — it would show the
  player progress they have not made against an objective they have not started.
- **A pinned domain other than Jade has to be REQUESTED, not just shown selected.** The tiles
  reflect `Player.NetDomain`, so highlighting one without the server round trip is exactly the "UI
  claims a domain the server never got" case `HandleDomainSelected` refuses to create. Jade needs no
  request — `MenuServerPlayerVesselInitializer` already resets every player to it on spawn, which is
  why it is the default.

Every one of these is **undressed again** when the next ordinary card opens: the panel is a shared
scene object, so an override applied here has to be handed back. `SetAddAIAvailable` is passed
`!weekly` rather than only switched off, for exactly that reason — a version that only turned it off
takes Add AI away from every card after it.

### Rollover is a period-key comparison, never a timer

`WeeklyChallengeService` re-resolves `Today` whenever the UTC date key changes, checked once a
second. Nothing is scheduled for midnight. A device that slept across midnight, or whose clock
jumped, lands on the correct challenge at the next check — a pending timer would have been wrong in
both cases.

---

## 2. Files

| Role | File |
|---|---|
| The week's challenge (value type) | `_Scripts/Data/Structs/WeeklyChallenge.cs` |
| Authored pool + the draw | `_Scripts/ScriptableObjects/WeeklyChallengeCatalogSO.cs` |
| The pool asset | `Assets/Resources/WeeklyChallengeCatalog.asset` |
| The service (rollover, cloud, attempts) | `_Scripts/System/WeeklyChallenge/WeeklyChallengeService.cs` |
| Cloud record | `_Scripts/System/CloudData/Models/WeeklyChallengeCloudData.cs` |
| Cloud repository (pre-existing, now used) | `_Scripts/System/CloudData/Repositories/WeeklyChallengeRepository.cs` |
| The arcade tile | `_Scripts/UI/Elements/WeeklyChallengeCard.cs` |
| A "play this week's challenge" shortcut | `_Scripts/UI/Elements/WeeklyChallengePlayButton.cs` |
| Launch route | `ArcadeExploreView.SelectWeeklyChallenge()` |
| Launch dressing (briefing only) | `ArcadeGameConfigureModal.OpenForWeeklyChallenge()` |
| **The editor tool** | `_Scripts/Editor/WeeklyChallengeWindow.cs` |
| Release gate for test mode | `_Scripts/Editor/Build/WeeklyChallengeTestModeBuildGuard.cs` |
| Tests | `_Scripts/Tests/Editor/WeeklyChallengeTests.cs` |

**Zero scene wiring for the service.** `WeeklyChallengeService` creates itself at
`RuntimeInitializeLoadType.AfterSceneLoad` on a `DontDestroyOnLoad` object — the shape
`VesselSpeedTunnel` uses — and is *handed* the `GameDataSO` it needs by whoever launches an
attempt, rather than hunting for one. There is no asset reference here that can drift.

---

## 3. The flow

```
WeeklyChallengeCard  (arcade grid)
  └─ ArcadeExploreView.SelectWeeklyChallenge()
      ├─ WeeklyChallengeService.Today            ← hash(UTC date) over the catalog
      ├─ FindGameByMode(challenge.GameMode)     ← the mode's SO_ArcadeGame
      └─ ArcadeGameConfigureModal.OpenForWeeklyChallenge(card, challenge)
          ├─ intensity pinned  (only that button active; HandleIntensitySelected refuses)
          ├─ seats pinned      (pcStepper min == max)
          └─ HandleAllPlayersReady
              └─ ArmWeeklyChallengeForLaunch()
                  └─ WeeklyChallengeService.BeginAttempt(gameData)
                      ├─ gameData.IsWeeklyChallenge = true
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

## 4. Authoring — FrogletTools ▸ Game Modes ▸ Weekly Challenge

`WeeklyChallengeWindow` edits `Assets/Resources/WeeklyChallengeCatalog.asset` (created on first
open) and is the intended surface. The inspector still works; what the window adds is the
**validation**, because three of the four ways to author an unplayable challenge are invisible in a
plain field list and every one of them has been hit at least once.

**The layout is master/detail behind three tabs**, not a field list. Ten entries × nine fields is a
page and a half of scrolling to compare two numbers 400 px apart. The list on the left is one row
per entry carrying the three things you actually scan for — is it on, what does it ask, is it
broken (a coloured stripe plus an `OK` / `2 ⚠` / `1 ✕` pill) — and only the **selected** entry
spends vertical space on fields. *Pool*, *Next 7 days* and *Testing* are tabs rather than more
sections because they are different questions, and stacking them into one scroll makes every one of
them harder. The detail pane also carries a **Mode races to** line — the mode's own end condition — because
that is the number the objective has to fit inside, and it is exactly the number nobody
remembers.

### Per-entry fields

| Field | Meaning |
|---|---|
| `Enabled` | Park an entry without deleting it. **Re-rolls the rotation** — the draw indexes the *enabled* entries. |
| `Mode` | The arcade mode. Must have a card in `SO_GameList` and a scene. |
| `Metric` | The per-player stat counted. Normally the mode's own scoring metric. |
| `Target` | What the **local player** must reach. |
| `TimeLimitSeconds` | Budget from the turn starting. `0` = no limit. |
| `Intensity` | Played at this intensity, for everyone. Pinned in the launch panel. |
| `Domain` | The colour the player flies. Pinned. Jade is the default and the only one needing no server request. |
| `Verb` / `Noun` | Objective copy: `"Collect" 8 "crystals"` → *Collect 8 crystals in 1:00*. |

### What the tool checks, and why each check exists

| Check | Severity | Why |
|---|---|---|
| Mode has an `SO_ArcadeGame`, with a scene | error | Otherwise the tile does nothing on whichever date draws it. |
| Intensity inside the card's `Min`/`MaxIntensity` | error | It is silently clamped at launch, so the challenge quietly becomes a different one. |
| **Objective ≤ the mode's own race target** | error | The turn ends when that target is met, which ends the challenge with it. Crystal Capture races to 20, so *"collect 30"* there can never complete. |
| Objective **equal** to it | warning | That target is a domain SUM while the objective is personal, so a teammate can end the run on somebody else's score. |
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
the editor or a development build (`WeeklyChallengeCatalogSO.TestActive`). On top of that,
`WeeklyChallengeTestModeBuildGuard` **fails a non-development build outright** while the switch is
set — the runtime gate already makes it harmless, and the guard makes it *loud*, because a flag
left set should never be silent.

| Setting | Effect |
|---|---|
| `forcedPoolIndex` | Pin the draw to one entry instead of hashing the date. Indexes the pool as the tool shows it. |
| `periodLengthMinutes` | A "week" becomes this many real minutes, so rollover is testable. |
| `ignoreAttemptLimit` | Replay the challenge while tuning it. |
| `timeLimitScale` | Multiplies every clock — `0.25` turns 60 s into 15 s. |

A shortened period's key is a **different shape** (`T4823…`) from a real week key (`2026-08-24`).
That is deliberate: a record written under a shrunken cycle can never be read as a real week's
progress, so switching back **wipes** it — the honest outcome rather than a blended one.

**Reset this week's progress** clears this machine's cached record. In play mode it also rewrites
the live cloud record through `WeeklyChallengeService.ResetPeriodForTesting()` (itself refused
outside the editor and development builds); outside play mode only the local snapshot goes, and the
cloud copy returns on the next sign-in.

### What ships

10 entries, all at intensity 1, 60–90 s, `attemptsPerPeriod = 1`, test mode off:

| Mode | Objective | The mode races to | Clock |
|---|---|---|---|
| Crystal Capture (Scurry) | 8 crystals | 20 | 1:00 |
| Skim Race | 10 crystals | auto (~39) | 1:30 |
| Joust | 1 joust | 3 | 1:00 |
| Rampage | 300 prisms | 2000 | 1:30 |
| Peel the Cage | 300 prisms | 2000 | 1:30 |
| Salvo | 150 prisms | 700 | 1:30 |
| Dog Fight | 20 points | 90 | 1:30 |
| The Bends | 1 bend | 3 | 1:30 |
| Scarab Scramble | 3 goals | 10 | 1:30 |
| Wildlife Liberation | 8 creatures | 30 | 1:30 |

Reordering the list **re-rolls which week draws which mode**; append rather than insert if that
matters.

## 5. Two design calls worth not re-litigating

**The objective is PERSONAL, never a domain sum.** "Score 30 crystals" is an ask of *you*. A domain
sum would let the AI seated beside you finish your challenge — which is exactly what would happen,
since the challenge seats the card's minimum and the rest are bots.

**Mode progression locks are IGNORED** (`respectModeProgression`, default `false`). The weekly
challenge is a curated invitation into a mode you may not have reached yet. Honouring the lock
would mean skipping that entry *per player*, and two players would no longer share a week's
challenge — which is the one promise the whole design is built on. Flip the flag on the catalog
asset to change it.

---

## 6. What is still open

- **No in-game readout.** `WeeklyChallengeService.OnAttemptProgress(achieved, target, secondsLeft)`
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

## 7. Superseded — and why it keeps its old NAME

`_Scripts/System/DailyChallengeSystem.cs`, `UI/Modals/DailyChallengeModal.cs`,
`UI/Views/DailyChallengeGameView.cs`, `UI/Views/DailyChallengeLeaderboardView.cs`,
`Data/Structs/DailyChallengeRewardState.cs` and the ticket/reward economy around them
(`CatalogManager.GetDailyChallengeTicket`, `DailyRewardHandler`, `LeaderboardManager`'s
`DAILY_CHALLENGE` PlayFab statistic) are the **PlayFab-era implementation**: PlayerPrefs storage, a
`SO_TrainingGame` pool, a three-tier reward ladder, and an `Arcade` singleton that is in no scene.

They are inert — nothing here reads them, and `DailyChallengeSystem` is in no scene either — and are
left in the tree because the reward ladder and the leaderboard view are the ideas in them worth
reviving. **They deliberately keep the `Daily` name.** Renaming a dead feature to match a live one
is how two systems come to look like one, and the next person to grep `WeeklyChallenge` should find
exactly the code that runs. Do not wire both.
