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

> **A per-entry TIME LIMIT survived that retirement and was the same mistake in a smaller costume.**
> Every shipped entry carried one (60–90 s), `TickAttempt` ended the TURN when it expired, and the
> attempt had already been spent at launch — so a player who ran out of that clock lost their one
> weekly attempt and submitted **nothing**. That is exactly what it looked like from outside: *"they
> can't play again, and no entry went in."* The field is deleted from the entry, the challenge
> struct, the objective copy, the editor tool, the test-mode scale and the shipped asset.
>
> What remains is the rule this section always stated: **the challenge OBSERVES the match, it never
> ends it.** Reaching the target stamps the completion and its time — the leaderboard score — and
> the match carries on to the mode's own end condition exactly as it would have without the
> challenge. There is now no code path by which a weekly run differs from an ordinary one.


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

**Giving an attempt BACK: `attemptResetToken` on the catalog.** Spending at launch is the right
ordering and it has one cost — a bug between launch and submit takes the attempt with it, and there
is no per-player remedy that scales. Bumping the token makes every record written before the bump
read as belonging to an earlier period, so the staleness path that already handles a week rollover
resets it: attempt back, best value and completion cleared with it (they are one record).

Two things make it safe. It reuses the EXISTING staleness reset rather than adding a second way to
clear a record — a remedy with its own code path is a remedy nobody has tested. And the token is
kept OUT of the draw key: the mode is chosen by hashing the period, so folding the token in would
silently re-roll the week into a different game, and a player mid-week would find the challenge had
changed. `PeriodKeyFor` drives the draw; `RecordKeyFor` (period + token) is what progress is filed
under.

It is a NUMBER, not a button, because a button would have to reach every player's cloud record one
at a time — a server job nobody has. A number travels with the build and each client applies it to
itself on next launch, offline included. It cannot be undone: lowering it re-issues the challenge a
second time rather than restoring anything.


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

## 6. The leaderboard

**Who completed this week's objective fastest.** Every weekly challenge is *"reach N of
something"*, so the one thing left worth ranking is how long it took — and the board reads exactly
the way the mock-up does: rank · avatar · name · time, lowest at the top.

### Only a COMPLETION earns an entry

A player who never reached the target has **no time**, not a slow one. Submitting a sentinel for
them would either rank people who never finished above people who did, or bury the real times under
a wall of identical placeholders — and either way the reward tiers at the end of the week would be
computed off a list that is mostly not a ranking. So: complete it and you are on the board; don't
and you are not.

`WeeklyChallengeService.FinishAttempt` is the single site that can produce an entry, submitting
`_attemptElapsed` when `achieved >= target`. There is deliberately no second submit path.

### ONE leaderboard, reset weekly by UGS — not one per week

The SDK cannot create leaderboards, so a per-week id would need a server job minting them forever.
The dashboard's own **reset schedule** does it, and its **archive on reset** is what a reward pass
reads once the week has closed.

**Three settings live in the UGS dashboard and nothing in this code can enforce them:**

| Setting | Value | Why |
|---|---|---|
| **Sort order** | **Ascending** | The score is a TIME, so the fastest run is the smallest number. |
| **Update strategy** | **Keep best** | "Best" is relative to sort order, so ascending keeps the fastest. Almost moot at one attempt a week; under test mode's unlimited attempts it stops a practice run overwriting a good one. |
| **Reset** | **Weekly, on the same UTC Monday boundary, ARCHIVING ON** | The archive is the only record of who won a week once the board has rolled over. |

**The sort order is the one that fails silently**, so it is checked at runtime instead: UGS returns
rows in rank order, so a correct board hands back non-decreasing times.
`WeeklyChallengeLeaderboardService.WarnIfSortedWrong` screams **once per session** if it doesn't —
because a wrongly-sorted board looks completely normal. The rows are real, the names are real, the
times are real; they are simply upside down, with the slowest run in the world at rank 1.

> A code-side workaround — submitting `BIG - time` so a descending board ranks correctly — was
> **considered and rejected**. It makes every raw score in the dashboard, in every export, and in
> the archive the reward pass reads a number nobody can interpret, to save one dashboard setting.

### The id, and the per-mode boards it replaced

The shipped id is **`weekly_challenge`** (`WeeklyChallengeCatalogSO.leaderboardId`, authored in
**FrogletTools > Game Modes > Weekly Challenge** on the Testing tab). One board, because the score
is a completion TIME for every entry in the pool — same unit and same direction whichever mode the
week draws, which is exactly what lets a single board span all ten.

**This replaced a second, older leaderboard system, now deleted.** `LeaderboardConfigSO` mapped
every mode × intensity to its own board (`mp_joust_intensity_1`, `sp_wildlifeblitz_intensity_3`, …) and
`UGSStatsManager.SubmitScoreInternal` submitted to it at *every* arcade game end. It is gone —
config SO, its inspector, `ActiveGameModesWindow`, the asset and the submit path — because
leaderboards are a weekly-challenge feature and two systems answering "what is a leaderboard here"
is how one of them ends up wrong. Three things it was carrying are worth recording:

- **Four of its twenty mappings could never fire.** `sp_hexrace_intensity_1‑4` were keyed to
  `GameMode: 31`, a permanently reserved never-assigned enum ID, and the `ProtectMission` rows
  point at a mode with no scene and no reporter.
- **Crystal Capture had no mapping at all**, so every score it reported hit *"No leaderboard
  mapping"* and was dropped.
- **Per-mode bests are unaffected.** They live in Cloud Save `MODE_STATS` and still do; what
  disappeared is the global ranking of them, not the record.

The read path was never wired either way: `LeaderboardsMenu` (the Port screen) still fetches
through the deprecated PlayFab `LeaderboardManager` and is unrelated to both systems.

### The time format

`WeeklyChallengeRanking.FormatSeconds` → `mm:ss.cc`. Centiseconds rather than whole seconds because
this is a race: a 60-second challenge at whole-second resolution has 60 possible scores, so a full
board would be mostly ties broken by submission order, which reads as arbitrary.

**It converts the whole value to centiseconds in ONE step and rounds.** The obvious implementation —
whole seconds, then `floor((seconds - whole) * 100)` — prints `47.3` as **`0:47.29`**, because the
double nearest 47.3 is a hair below it and the subtraction keeps the whole error. (That bug was
written, then caught by evaluating the formatter numerically before the test was believed.) The
0.005 s a round can add displays a time marginally *slower* than the run, which is the safe
direction — a floor would print times the player did not achieve.

### Rewards are NOT here

A reward system is being built separately; it reads the archive. This layer ranks and nothing else,
and the reward tooltip in the mock-up is that system's surface, not this one's.

### Files

| Role | File |
|---|---|
| Submit + fetch (all UGS contact) | `_Scripts/System/WeeklyChallenge/WeeklyChallengeLeaderboardService.cs` |
| One row, resolved for a UI | `_Scripts/Data/Structs/WeeklyChallengeRanking.cs` |
| Which population a tab ranks | `_Scripts/Data/Enums/LeaderboardScope.cs` |
| Which regional board this player is on | `_Scripts/System/WeeklyChallenge/WeeklyChallengeRegion.cs` |
| The ROW LIST | `_Scripts/UI/Views/WeeklyChallengeLeaderboardPanel.cs` |
| The WINDOW (tabs, countdown, reward tooltip, close) | `_Scripts/UI/Modals/WeeklyChallengeLeaderboardModal.cs` |
| Wiring it up | `_Scripts/Editor/FrogletTools/WeeklyChallengeLeaderboardWirer.cs` |
| The ids | `WeeklyChallengeCatalogSO.leaderboardId` + `.regionalLeaderboards` (authored in the tool) |

**`WeeklyChallengeRanking` is a project type, not the UGS entry it is built from.** A view taking a
`Unity.Services.Leaderboards.Models.LeaderboardEntry` would drag the SDK into the UI layer and break
the day the package renames a field — and this project already has *two* types called
`LeaderboardEntry` (the PlayFab one and `CosmicShore.Data.LeaderboardEntry`), so a third would be
three names for one idea.

**That warning fires on the SERVICE too, and heeding half of it is not enough.** Keeping the SDK
type out of the *view* does nothing about the file that must name it: the service sits in
`CosmicShore.Core` and imports `CosmicShore.Data`, so a plain
`using Unity.Services.Leaderboards.Models;` made the bare name `LeaderboardEntry` ambiguous the
moment the file used it (`CS0104`). The fix is an **alias**, not an import —
`using UgsLeaderboardEntry = Unity.Services.Leaderboards.Models.LeaderboardEntry;` — which names the
one type this file means and leaves every other name in that namespace alone. The options types
(`AddPlayerScoreOptions`, `GetScoresOptions`, …) live in `Unity.Services.Leaderboards` itself and
collide with nothing, so that import stays. *General rule: when a type name is known to be
duplicated, import nothing from its namespace — alias the one member you need.*

**The panel adopts its row template by name**, the same way the connecting panel's pilot roster
does and for the same reason: the row count is not known until the fetch answers, so a serialized
reference per row is impossible. Wire `rowContainer` and a template; the rank / avatar / name /
score inside it are found by name unless wired explicitly.

**No avatar travels with a leaderboard entry *on its own*, so the submit puts one there.** UGS holds
a player id, a name, a rank and a score — not a profile. The one field a score can carry with it is
its **metadata**, so `SubmitCompletionAsync` stamps the local profile's icon id into it
(`{"a":<id>}`, `WeeklyChallengeRanking.AvatarMetadataKey`) and the fetch reads it back with
`ReadAvatarIdFromMetadata`. That closes the follow-up this section used to record, with one honest
limit: **an entry submitted before this shipped has no metadata**, so it resolves to
`WeeklyChallengeRanking.NoAvatar` and keeps the template's art. That is the normal case for old
rows, not a failure.

Three details are each a test (`WeeklyChallengeLeaderboardTests`):

- **`NoAvatar` is `-1`, never `0`.** Icon 0 is a real icon, so a zero sentinel silently shows *that
  face* on every row that carries no avatar.
- **The scan looks for the QUOTED key**, so a payload with an unrelated `"area"` field does not
  match on the letter `a`.
- **A row with no avatar keeps the template's sprite rather than clearing it.** An `Image` with no
  sprite draws a solid white rectangle, so "no avatar" would read as a rendering bug.

The parser is a hand-rolled scan rather than a JSON parse — the payload is one integer under a
one-character key, and it runs once per row per fetch — and it lives on the *struct*, because the
struct owns the field, and because a hand-rolled parser is exactly the kind of thing that fails
silently and therefore has to be testable.

### THREE SCOPES, and only one of them is a filter over anything

The window has World / Regional / Friends tabs. **They are three different questions asked of UGS,
not three filters over one answer** — which is the whole reason `LeaderboardScope` exists:

| Scope | What it actually is | Needs |
|---|---|---|
| **World** | A page of the board | the `leaderboardId` |
| **Regional** | A page of a **different board** | a row in `regionalLeaderboards` matching the player's region |
| **Friends** | A lookup of specific player ids **on the world board** | the Friends service initialised |

**Regional has to be its own board.** Unity Gaming Services has no notion of a player's region — a
board is a board and every score on it is global. So "regional" can only mean *a second board that
only that region submits to*, and a completion is submitted to the world board **and** the player's
regional board. The tempting alternative — fetch the world page and filter it client-side — looks
equivalent and silently produces an empty board: the page is the *global* top N, so a region with
nobody in it sees nothing and reads it as broken.

`WeeklyChallengeRegion` resolves the key, first answer wins: a region **published** by the
networking layer (`WeeklyChallengeRegion.Publish` — nothing calls it today; the hook exists because
the Relay session picks its region by measured latency, which is the *right* answer), else the
device's two-letter ISO country, else nothing. Nothing means the tab reports no board rather than
guessing — putting a player on the wrong region's board is worse than showing none. The country is
deliberately **not** mapped to a coarse continent in code: which countries share a board is a
business decision, so the table is authored (one row per country, several rows may share an id).

**Friends re-numbers its ranks 1..n.** A friends list showing 1st, 4th, 812th is a world board with
most of its rows missing, not a friends board. The world rank is not lost — it is simply not what
that tab is answering. The local player is always included, because a friends board you are not on
cannot tell you whether you are beating your friends.

**A tab whose scope has nothing configured is DIMMED, never hidden.** A tab that vanishes changes
the row's layout whenever the answer changes, and a player who saw three tabs yesterday reads two as
a broken build. The state is resolved *before* the fetch (`IsScopeAvailable`), because an
unconfigured board and a board nobody has finished both come back empty and the player deserves to
know which one they are looking at.

The Friends tab additionally has its own switch (`friendsTabEnabled`, **off**): "we cannot ask" and
"we are not shipping this yet" are different facts and only one of them changes at runtime. The code
path is complete — turning the switch on is all it takes.

### The countdown is a CLOCK and stays a clock

`WeeklyChallengeLeaderboardModal.FormatHoursMinutesSeconds` → `HH:MM:SS`, with hours running past 24
rather than rolling over, so the top of a week reads `167:59:59`. Deliberately *not*
`WeeklyChallengeCard.FormatCountdown`, which switches units as the week runs down (`6d 3h` →
`7:12:33` → `1:04`) because it is glanced at on a card. Hours are padded to two so the string never
changes width within an hour — that is what stops the label jittering under a proportional font.

### The animation is on channels a layout group does not own

Rows fade and **swell**; they deliberately do not rise. The rows live under a `VerticalLayoutGroup`,
which owns `anchoredPosition` and rewrites it on every layout rebuild — so a position tween is a
second writer to a value the layout considers its own, and the rows snap the first time anything
dirties the layout. Alpha and `localScale` are untouched by a layout group, which makes them the two
channels a row can safely animate wherever it is parented. *General rule: before animating a UI
transform, ask what else writes to that field.*

The stagger is **divided down** rather than truncated when a list is long enough to exceed
`maxStaggerTotal` — truncating leaves the tail of a long board arriving all at once, which reads as
the animation giving up. Every tween is `SetLink`ed and killed-and-snapped on disable, or a panel
closed 40 ms into its cascade re-opens with half its rows transparent and undersized.

---

### Reaching the board: a spent challenge still OPENS

**The card no longer goes dead when the attempt is spent.** It used to — `CanAttempt` gated the
card's own `interactable` — and that also made the week's LEADERBOARD unreachable, because the board
lives behind that card. A player who finishes the challenge on Monday could not see where their run
placed for the other six days. So the card (and the `WeeklyChallengePlayButton` shortcut, which must
not disagree with the card it is a shortcut to) is gated only on the challenge EXISTING, and the
launch panel greys its own Start button instead.

`CanAttempt` is still the single authority for whether the challenge can be **played**. It is simply
no longer the authority for whether it can be **looked at**.

**Start is greyed, never hidden.** A missing Start button reads as a broken modal; a dead one with
`ALREADY PLAYED THIS WEEK` beside it explains itself — and that matters precisely because the point
of still opening a spent challenge is to reach the leaderboard, and a window that looks broken is
one nobody explores. `ArcadeLaunchPanel.SetStartAvailable` drives `interactable` rather than active
state, because the modal's ready-up path owns the button's ACTIVE state and toggles it freely, so a
hide would be undone on the next redraw. Nothing else in the project writes Start's `interactable`,
which is what makes it a channel the panel can own outright. The availability is held as panel STATE
and re-asserted from `SetReadyUpState`, for the same reason `_addAIAvailable` is.

**A disabled button is not the whole gate.** `OnStartGameClicked` is public, a prefab may carry its
own onClick to it, and the modal's gamepad path drives focus ROWS rather than the button — so the
refusal lives in the handler (`CanStartWeeklyChallenge`), not on the control. The reason text
deliberately carries **no countdown**: it is written once when the card opens, a modal can sit open
for minutes, and a ticking value that does not tick is worse than none. The card in the grid behind
it already counts the week down.

**The leaderboard button lives on the launch panel and the modal opens the window.** The panel
raises `OnLeaderboardRequested` and does not know a leaderboard window exists; the modal resolves
and opens it. Like `SetAddAIAvailable`, `SetLeaderboardAvailable` is passed **either way** rather
than only switched on — the panel is a shared scene object, so a button shown for a challenge has to
be taken back when the next ordinary card opens. The arcade modal is **not** closed underneath the
board: the player is mid-decision about a card, so the board is a detour, not a destination.

---

### Scene wiring — run the tool

**FrogletTools ▸ Interface ▸ Wire Weekly Challenge Leaderboard** resolves every reference below
from the window's own hierarchy and reports what it could not find. **Report only** shows what it
would do without writing. It is a repair tool as well as a bring-up tool, because it never
overwrites a reference set by hand unless you tick the box — so re-running after re-laying the art
is always safe.

It exists because the window is ~16 references and every one of them fails *silently*: a tab
pointed at the wrong button switches the wrong scope, an unwired backdrop leaves a tooltip that
opens and never closes, and a missed `rowContainer` spawns every row on the modal root behind the
artwork. None of those throw.

Two lookups in it are worth knowing about. **Three objects in this window are called `RankBG`** —
the tooltip's backdrop, the tier table's own background inside it, and the leaderboard row's rank
badge — so the backdrop is resolved by path from a known parent, never by a name search over the
window. And **a `rowTemplate` that is already a PREFAB ASSET is kept**, not re-pointed at the
leftover scene copy: extracting the row is the direction this is going, and re-resolving it every
run would quietly undo that.

The tool's `Validate & Push` gate fails on only the things without which the window is *broken*
rather than merely plainer: no row panel, no close button, a reward panel wired without its
backdrop, or a `RankRewardPanel` left ACTIVE in the scene (it would be on screen the moment the
window opens). Everything else is legitimately optional and must not fail a push.

Everything below is OPTIONAL — a modal with only a close button opens and closes, one with only the
panel lists the week. Nothing logs about a field left empty, because "this window does not have that
piece" is a layout, not a misconfiguration.

| Component | Field | Wire to |
|---|---|---|
| `WeeklyChallengeLeaderboardModal` (on `LeaderboardConfigureModal`) | `panel` | the `WeeklyChallengeLeaderboardPanel` (found in children if empty) |
| | `timeLeftText` | `Content/Time` |
| | `challengeTitleText` | `Content/LeaderboardHeader` (optional) |
| | `worldTab` / `regionalTab` / `friendsTab` | the three `ButtonTabs` children |
| | `rankRewardButton` | `Content/RankRewardButton` |
| | `rankRewardPanel` | `Content/RankRewardPanel` |
| | `rankRewardBackdrop` | `RankRewardPanel/RankBG` |
| | `closeButton` | `Content/CloseButton` |
| | `contentRoot` | `Content` (found by name if empty) |
| | `ModalType` | `WEEKLY_CHALLENGE_LEADERBOARD` |
| `ArcadeGameConfigureModal` | `leaderboardModal` | this window (found in the scene if empty) |
| `ArcadeLaunchPanel` (each one) | `leaderboardButton` | the panel's own leaderboard button |
| | `startUnavailableLabel` | optional line beside Start; without it the button just greys |
| | `screenSwitcher` | the scene's `ScreenSwitcher` |
| `WeeklyChallengeLeaderboardPanel` (on `LeaderboardScrollView/Viewport/Content`) | `rowContainer` | that same `Content` (its own transform if empty) |
| | `rowTemplate` | the `LeaderboardContent` **prefab** (or the container's first child) |
| | `templateBackground` | `LeaderboardContent`'s own `Image` — the one the podium tints |
| | `profileIcons` | the project's `SO_ProfileIconList` |

The row's rank / avatar / username / score are found **by name** inside the template, so they need
wiring only if the names change. `RankRewardPanel` must start **inactive**; the backdrop gets its
`Button` and `raycastTarget` added at runtime rather than asked of the layout, because "the tooltip
would not close" is a bug nobody can see in the hierarchy.

**Two components, and where each one sits.** The split is real — the modal owns the window's
decisions, the panel owns the rows — but *where* the panel sits is free: it draws into
`rowContainer`, which is an explicit field. Both on the modal root works; the tool offers to put
the panel on the scroll `Content` instead, so the component sits on the object it draws into. It
will not MOVE an existing one, because moving a MonoBehaviour drops its serialized values.

**`LeaderboardContent` is a prefab asset, and `rowTemplate` on the PANEL is where it goes.** The
modal has no prefab field at all — it owns the window's decisions (tabs, countdown, tooltip, close)
and never touches a row, so a row prefab on it would be a reference nothing reads.

The tool finds it on its own: when the scene has no template left to clone, it searches the project
and accepts a prefab only if it is **unambiguous AND has the shape of a row** (a `RankText`, a
`Username`, a `ScoreText`). Name alone would happily wire someone else's `LeaderboardContent`, and
that failure — rows that draw but stay blank — reads as the fetch being broken rather than as the
wrong prefab. An already-assigned prefab is always kept over anything the search would find.

`rowTemplate` takes either a prefab asset or an in-scene object, and the panel only
hides an **in-scene** template: `SetActive(false)` on a prefab asset writes to the asset on disk —
a permanent edit to a shared file, made by opening a menu. `gameObject.scene.IsValid()` is the test
that tells the two apart, and it is used rather than `PrefabUtility` because this runs at `Awake`
in a build. A prefab template is also instantiated ACTIVE regardless of how the asset is authored,
since the asset's own active state is about the asset, not about a row.

---

## 7. What is still open

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
  three-tier ladder (§7) is the obvious thing to revive. The window's reward tooltip is authored
  ART today — it lists tiers, and nothing reads or pays them.
- **Nothing publishes the connection region.** `WeeklyChallengeRegion.Publish` exists and no caller
  does. Until one does, Regional is resolved from the device's country, which is wrong for anyone on
  a VPN or an imported console. The fix is one call from the party/Relay layer once a session's
  region is known.
- **The Friends tab ships OFF** (`friendsTabEnabled`). The code path is complete and tested against
  the SDK surface; the switch exists so the board can ship before the friends flow is finished.
- **Nothing opens the window yet.** `WeeklyChallengeLeaderboardModal.Open()` is the entry point;
  the weekly card is the natural caller.

## 8. Superseded — and why it keeps its old NAME

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

### …and they now own their own data

The rename took the shared `DailyChallenge` **struct** with it and the dead system stopped
compiling — which is the useful half of the discovery: those two features were sharing a value type,
so *"the legacy cluster is separate"* was true of its names and not of its data.

`Data/Structs/DailyChallenge.cs` is therefore re-created as the **legacy** type, carrying only what
`DailyChallengeSystem` actually reads (`GameMode`, `Intensity`). It is deliberately not a copy of
`WeeklyChallenge`: a dead feature's data should shrink to what it uses rather than track a live
one's shape. Pointing the dead system at `WeeklyChallenge` instead would have re-tied a retired
feature to a live one through the type system, which is exactly the coupling this section exists to
prevent.

**General:** *renaming a feature is a compile-time test of whether it was ever actually separate
from the one it superseded.* What the compiler names is where they were still joined.

### …and the compiler is blind to the half of a rename that lives in the SCENE

That compile-time test has an exact blind spot, and this rename fell into it. It renamed one
**serialized field** — `ArcadeExploreView.DailyChallengeCard` → `WeeklyChallengeCard` — and Unity
keys serialized data by field **name**. `Menu_Main.unity` still said `DailyChallengeCard:`, so the
reference the scene had correctly wired deserialized **null**, and nothing anywhere said so.

What that cost is out of all proportion to a null reference, because of where it landed:
`PopulateGameSelectionList` dereferenced the card on its **first** line, and that method is the one
thing that gives every arcade card its mode, its click listener and its lock state. So the whole
grid died — cards left inactive and unclickable ("all arcade game lists are locked"), and every card
warning `No SO_ArcadeGame found for mode BlockBandit`, which is `GameCard.Start` coercing its
unassigned default (`GameModes.Random`) to a legacy mode that the cards' own list
(`OrganicRematchGames`) does not carry. **Neither symptom names the field**, and the arcade is three
systems away from the weekly challenge.

Fixed three ways, deliberately overlapping:

1. **`[FormerlySerializedAs("DailyChallengeCard")]`** on the field — Unity's own migration, and the
   only one that reaches a scene, prefab or local copy nobody thought to re-save.
2. The **scene key migrated** in `Menu_Main.unity`, so the shipped data is honest rather than
   relying on an attribute a later cleanup might remove.
3. The card made **genuinely optional** in `PopulateGameSelectionList`. It already was eight lines
   later (`if (WeeklyChallengeCard) …Bind(this)`), so optional was the intent all along and the
   unguarded line was simply the bug. It now takes the grid's first dpad row when it is present and
   **no row at all** when it is not — an empty row is not equivalent, because `ArcadeDPadNav` clamps
   a column into `row.Count - 1`, which is `-1` on an empty row and throws the moment the dpad walks
   into it. The game rows are consequently **counted**, not `i + 1`.

**General:** *renaming a serialized field is a data migration, not a refactor* — `FormerlySerializedAs`
is the migration, and a `git mv` of the `.cs` + `.meta` protects only the guid, never the field
names inside it. And its corollary: *an optional dependency has to be optional on every line that
touches it* — one unguarded dereference is enough, and it fails hardest when it sits at the top of
the method everything else in the screen depends on.
