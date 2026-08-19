# The Bends — Technical Documentation

**Mode:** `GameModes.Bends = 42`  ·  **Display name:** "The Bends"
**Scene:** `Assets/_Scenes/Multiplayer Scenes/MinigameBends.unity`
**Controller:** `BendsController` (`MultiplayerDomainGamesController`)
**Vessel:** Dolphin, only
**Players:** 2–4  ·  **Domains:** 2–3  ·  **Intensity:** 1–4
**Metric:** `ScoringMetric.CombatPoints` — first DOMAIN to the bend target (default **3**) wins

---

## Overview

The Bends is a dogfight with no guns in it.

Every pilot flies a Dolphin, and the Dolphin has exactly one offensive act: it banks blast
energy **only by skimming**, discharges it **only on a crystal**, and what comes out is a cone
(`DOLPHIN_ENERGY_ECONOMY.md` §1). Rampage built an arena around that loop and paid you for
aiming the cone at a forest. This mode changes nothing whatsoever about the vessel and changes
the target: **the only thing that scores is catching an opposing pilot in the blast.**

A caught pilot takes the blast's all-element debuff — every element down 0.5, decaying over 4
seconds. That is one **bend**, worth 1 point, and **three bends win the match** — a race to 3,
like Joust's three collisions. Nothing is destroyed and nobody is removed; the
victim is simply *worse at the mode* for four seconds: a narrower cone (Charge), a shorter one
(Space), slower crystal seeding (Mass), a weaker boost (Time). The whole fight is therefore
about that window — landing a bend, then using the four seconds it buys.

The loop per pilot:

1. **Graze the forest** to fill the energy meter (150 skims).
2. **Race a rival to a crystal** — the arena carries few, and fewer at higher intensity.
3. **Put the cone on a person**, not on the trees.

Steps 1 and 2 are Rampage's, verbatim and deliberately (see *The arena*). Step 3 is the mode.

**A bend is a whole-match event, not a tick.** At a target of 3 each hit is worth a third of the
race, which is what makes the four-second debuff window matter: you are not chipping someone
down, you are taking a third of the match and simultaneously making them worse at contesting the
next one. It also keeps the HUD number small enough to hold in your head, and makes a blast that
catches two opponents at once a genuine match-swinging moment rather than a bigger number.

---

## Why this needed no new weapon, resource, or ability

Because the Dolphin already had all three, and the platform already had everywhere to put the
result:

| Need | What already existed |
|---|---|
| A weapon | `AOEConicExplosion.prefab` — the Dolphin's crystal blast |
| A debuff | `VesselElementalDebuffByExplosionEffectSO` (asset: `ScarabCavitationDebuffByExplosionEffect`) — authored, and **never wired to anything** |
| A way to record a vessel-vs-vessel hit | `VesselCombatHitByExplosionEffectSO` → `GameDataSO.OnCombatHitLanded` → `StatsManager.CombatHitLanded` → `CombatHitScoring.Credit` (Dog Fight's pipeline) |
| A way to weight it per mode | `ScoringRuleSO.PointsForCombatHit` — 0 in every mode but the one that pays |
| Somewhere to keep the score | `IRoundStats.CombatPoints`, already replicated and already summed by domain |

So the mode is one new scoring rule, one new controller, one new asset, and **one wiring edit**
— section below. There is no per-event listener in the controller at all, exactly like Rampage,
Ribcage and Dog Fight.

---

## The platform change: the Dolphin's blast can now touch a pilot

`AOEConicExplosionImpactorDataContainer` shipped with `vesselExplosionEffects` **empty**. The
Dolphin's blast has therefore always destroyed every prism it engulfed and done *nothing at all*
to a pilot standing in the same volume — a weapon that swallows a ship and leaves it untouched.

That is a platform gap, not a mode setting, and it is fixed as one. Two sibling effects now hang
on that container, dispatched from the one contact:

1. **`ScarabCavitationDebuffByExplosionEffect`** — the ELEMENTAL expression of "the blast weakens
   you". Elementals are the platform's single buff/debuff system, so a blast that wants to weaken
   a pilot reaches for that fundamental rather than inventing a per-blast status.
2. **`VesselCombatHitByCrystalBlast`** — the scoring report (`CombatHitClass.Debuff`).

**Both land in every mode; only this mode's rule pays for them.** That is the same split Dog
Fight established for gunnery — counts are a platform fact, points are a mode's opinion — and it
means a Rampage Dolphin now also debuffs a rival it catches, and scores nothing for it, which is
correct.

Scoped to the **conic** prefab deliberately. The same shape of edit on the shared
`AOEExplosion.prefab` would label every vessel's crystal blast a bend in every mode.

---

## The two flags this mode added to the combat-hit effect

Both are on `VesselCombatHitByExplosionEffectSO`, both default **off** (so Dog Fight's authored
missile-blast asset is byte-unchanged), both **on** for the crystal blast.

### `requireDebuffableVictim` — the score must follow the effect

A pilot warded against `Explosion`-class elemental debuffs eats the cone and keeps their levels:
`ApplyElementalEffect` drops negative magnitudes whose source class the victim is immune to.
Scoring their attacker would pay for something that provably did not happen — and the two effects,
siblings in one container dispatched from one contact, would disagree about whether anything
occurred. The gate reads `victimStatus.IsImmuneToElementalDebuff(ElementalDebuffSources.Explosion)`,
i.e. it asks about *this blast's own* debuff class rather than about a broader state.

Off for a missile, because a rocket that hits you hit you whatever your immunity state.

### The Dolphin's own Time 5 collided with this — RESOLVED, and worth reading

When this flag was written, elemental immunity was somebody else's vessel: the Sparrow while
boosting, the Serpent while stopped. **`bleeding-edge` then landed "Drift Ward"** — Dolphin
Time 5, `VesselElementalImmunity(condition: WhileDrifting, upgradeGate: Time)` on
`Dolphin.prefab` — and in this mode *every pilot is a Dolphin*. The mode's only vessel therefore
held a hard counter to the mode's only scoring event, and worse, the counter was handed to
whoever was **losing**:

- **Drift is a HELD action.** `DriftActionSO` sets `IsDrifting` in `StartAction` and clears it in
  `StopAction`; there is no timer. Immunity lasted as long as the input was held.
- **A one-bend deficit unlocks it.** `ElementalComebackSystem` feeds `SetComebackModifier`, which
  composites into `GetEffectiveLevel`, which is what `R_VesselElementalAbilityHandler` reads to
  flip an unlock. Dolphin Time authors `UnlockLevel: 5`, `RelockBelowLevel: 4`,
  `LatchPolicy: Relock`. At the shipped rate: base level 1 (0.1) + one bend behind
  (1 × 4.0 ⁄ 10 = 0.4) = 0.5 → **level 5 → Drift Ward**. Falling one bend behind out of three
  handed the trailing pilot immunity.
- **`Relock` was the only brake**, and it only releases when the deficit closes — which in a 1v1
  required the leader to land a bend on a pilot who was currently immune to bends.

**The resolution was a platform change, not a mode setting** — this doc's own lever 3, which then
read "no mechanism exists for that today". The mechanism is that an elemental debuff now carries
its **source class** (`ElementalDebuffSources`) and a ward is held against a **mask** of classes.
The Drift Ward was authored to answer *danger prisms* — drifting through your own hazardous arena —
so it now wards `DangerPrism` and nothing else (`Dolphin.prefab` → `wardedSources: 1`). Danger
ribs still can't drain a drifting Dolphin; another pilot's cone always can. The two other levers
were rejected on the record: lowering the comeback rate trades one dead dial for another, and
turning `requireDebuffableVictim` off breaks the "score must not disagree with the effect"
property the flag exists for.

Full rationale and the two invariants that keep the scope honest (`All` is `~0`; an unclassified
debuff falls in `Other`): `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md` §1.1.

**Still a playtest question** (and never claimed here): the *cost* of holding a drift. CLAUDE.md's
"Locked freezes the velocity vector outright" describes the VECTOR flight model, and
`Dolphin.prefab` authors `vectorFlightModel: 0` — the scalar model, where throttle pushes along
`Course`. Whether a permanently drifting Dolphin can manoeuvre at all is unchanged by this fix and
still unmeasured; it now only affects how the Dolphin flies, not whether it can be scored on.

### `requireOwningMachine` — a networking fix, not a design choice

A crystal collection **resolves server-side**, and `NetworkCrystalManager.ReplayVesselCrystalEffects`
then re-runs the vessel effects on the **owning client** so the pilot sees their own blast. So a
client's single blast genuinely exists on **two machines** — unlike a Sparrow rocket, which is a
pooled local object that only ever exists on one.

Without the flag the server would credit its own copy *and* accept the client's forwarded RPC for
the same bend: **every client hit scored twice.** `VesselCombatHitLatch` cannot help — it is
per-machine and cannot see across the wire.

The test is **`IPlayer.IsNetworkOwner`**, not `IsLocalUser`, because an AI's vessel is
server-owned and its hits must still be recorded:

| shooter | server copy | owner copy | recorded |
|---|---|---|---|
| host's human | owner → raises | (same machine) | once, directly |
| AI | owner → raises | — | once, directly |
| client's human | not owner → **skipped** | owner → raises | once, via RPC |

---

## The bug this mode found in the client→server path

`Player.ReportCombatHit_ServerRpc` re-validated the wire value like this:

```csharp
var resolved = hitClass == (int)CombatHitClass.Missile
    ? CombatHitClass.Missile
    : CombatHitClass.Bullet;      // ← everything else collapses here
```

That was correct while exactly two classes existed and became a **silent un-scoring bug** the
moment a third did. A client's bend arrived as `Bullet`: it landed in the wrong raw counter and
was paid at this mode's *gunnery* rate — which is deliberately **zero**. A client could fight a
whole match and score nothing while the host scored normally.

It now validates against the declared set (`Enum.IsDefined`, already the idiom two methods down
in the same file) and still falls back to `Bullet` for a genuinely out-of-range value, which is
the point of re-validating rather than trusting the wire.

**General lesson:** a "check for the one special member, else the default" validator encodes the
current size of an enum. It does not fail when the enum grows — it mis-files.

---

## Where the point VALUES live

`BendsScoringRuleSO`, and nowhere else:

| class | points | why |
|---|---|---|
| `Debuff` | **1** | a point IS a bend — the scored metric, the raw count and the HUD number are all the same thing, with no scale factor to divide out when reading a scoreboard |
| `Bullet` / `Missile` | **0** | the Dolphin has no guns |

Guns pay zero rather than simply never happening, and that is deliberate. The vessel restriction
is **data** (the `Vessels` list on `ArcadeGameBends`); a rule that silently paid for gunnery
would turn a mis-authored roster into a *scoring* bug rather than a *roster* bug. Zero says what
the mode means.

`CombatHitScoring.Credit` applies whichever rule is live, server-side, at the instant of the hit
— which is what keeps `ScoringMetric.CombatPoints` a plain cumulative int that sums by domain,
drives the HUD, feeds the comeback system and orders the scoreboard through the shared machinery,
with no per-metric weighting table anywhere.

---

## Counting once per blast per victim

The cone **grows through** its victim over many frames, so both effects need a per-victim window
or one detonation would debuff and pay on every frame it overlaps. Two independent windows,
authored to the same **1 s**:

- the debuff effect's own `cooldown` (its private `ResourceSystem`-keyed table);
- `VesselCombatHitLatch.TryAdmit(shooter, victim, Debuff, 1)` for the score.

They are separate tables by design — the same arrangement Dog Fight's direct-hit and blast
effects use — and **must be kept equal**. If the score window were shorter than the debuff
window, a blast would pay for a debuff it did not apply.

A blast that catches **two** enemies pays **twice**: the latch is keyed per victim. Against a
3-bend target that single blast is two thirds of the match, which is the mode's biggest play.

---

## Why it is a TEAM race and not a free-for-all

Structural, not a scoring preference: `ExplosionImpactor.AcceptImpactee` declines own-domain
vessels unless the blast authors friendly fire (`AOEConicExplosion.prefab` authors
`affectSelf: 0`). **You cannot bend a teammate at all**, so a same-domain pair with an individual
win condition would simply be unable to fight each other. Domains ARE the sides.

Wildlife Liberation already tried and reverted an individual winner on the weaker version of this
argument (four seats, three domains, so a full lobby always has teammates). Do not re-derive it.

This is also why `MinDomainsAllowed` is 2 and `MinPlayersAllowed` is 2 — stricter than Rampage,
which can be played solo because its target is a forest. This mode's target is a person, so a
lobby launched solo or all on one domain would have nothing legal to score against.

---

## The arena — Rampage's, on purpose and read-only

`MinigameBends.unity` is a clone of `MinigameRampage.unity` with **two** things changed: the
controller and the turn monitor. Everything else is inherited verbatim, references intact:

- the four per-intensity **cactus-forest cell configs** (`CellTypeChoiceOptions.IntensityWise`),
  including their authored volume ladders — a cactus leaf is 5×5×3 = 75 volume, ~4.7× nominal, so
  those thresholds are hand-authored and must not be re-derived (`RAMPAGE.md`);
- the **crystal counts** per intensity: 2× players / players / players−1 (min 1) / exactly 1;
- the **cell-relative spawn ring**, every pilot facing the cell;
- the four **AI templates**, already `vesselClass: 2` (Dolphin).

Referencing those configs rather than forking them is CLAUDE.md's "the Cell owns the environment
— minigames don't build parallel systems" applied to a whole arena. The two modes want the same
world for the same reason (it is the same vessel economy); they differ in what you aim the cone
at, which is a scoring rule, not a world.

**Intensity therefore means the same thing it means in Rampage: scarcity.** The forest is
identical at all four levels; the crystals get rarer and the wildlife heavier. Here that reads
even more directly than it does there — the crystal is your *only* trigger, so its scarcity is
exactly how contested it is to get a shot off at all.

The generator asserts the inheritance rather than assuming it (`cellTypeChoiceOptions: 1` present,
four Dolphin AI templates present), so if the donor scene is re-authored this fails loudly instead
of quietly producing an empty cell with nothing to skim.

---

## AI — the drift aim, and the hook it needed

The AI installs **no** `AIPilot.SetExternalTargetProvider`. That override replaces crystal seeking
outright, and in a mode whose weapon is *fired by* a crystal it would disarm every AI in the
arena — the exact lesson Rampage recorded after removing such a provider.

What the AI needed instead was narrower, and the seam already existed. `AIPilot` already drifts
once a crystal is lined up — swinging its nose off its course so the cone comes out somewhere
other than straight ahead — and already resolves *where to point* through
`ResolveDriftLookDirection`, which by default aims at the densest cluster of hostile mass
(`Cell.GetExplosionTarget`, the same Burst density query aggression-1 fauna hunt with). That
default is right in Rampage, where the forest IS the score, and wrong here.

So `AIPilot` grew **`SetDriftLookTargetProvider`** — a general, opt-in hook at exactly that seam:

- returns a world **position**, or `null` to fall through;
- sampled only on the drift path, so a null or unresolvable provider costs nothing;
- runs the same "would this drift actually turn the vessel?" test (`dot < 0.9`) as the mass
  cluster, so an aim point already lying along the objective falls through rather than producing
  a drift that does nothing;
- falls back to the mass cluster and then to the legacy 180° flip — the graceful chain the
  default already ran.

It is deliberately a **separate** hook from `SetExternalTargetProvider` because they answer
different questions: the steering hook decides where the AI **goes**, this one decides what it
**aims at** once it is already going somewhere.

`BendsController.ArmAimHooks` then gives each AI the nearest opposing pilot, with:

- **lead** (`aiAimLeadSeconds`, 0.35) along the rival's own course, because the cone has real
  length and a blast put where someone *was* is a miss;
- a **range gate** (`aiAimMaxRange`, 900) past which the provider returns `null` and the platform
  default resumes. This matters more than it looks: aiming at an unreachable pilot would stop the
  AI clearing forest, and clearing forest is how it banks the energy for the next shot.

`DisarmAimHooks` runs on despawn, on game end and on replay reset. It is not optional bookkeeping:
the provider closes over the controller and over an `IPlayer`, and AI players are spawned
`destroyWithScene: false`, so a hook left armed would outlive the match that installed it.

---

## Everyone starts at zero

`CombatPoints` and `DebuffHitsLanded` are zeroed on every roster entry at **two** moments, both
server-only (the setters push through server-write NetworkVariables, so a client zeroing its own
would just be overwritten and would desync until the next delta):

1. `OnNetworkSpawn` — cheap, and the moment every peer agrees the match has not started;
2. `OnCountdownTimerEnded`, before `base` — the last moment before anyone can score, by which
   time a late joiner (or a player whose `PrepareForNewScene` landed before their `RoundStats`
   had replicated its name) is on the roster. This is the sweep that actually guarantees it in a
   real lobby.

`VesselCombatHitLatch.Clear()` also runs at spawn and on replay reset, on **every** peer: the
latch is static, `Time.time` keeps running across a scene load, and the latch is consulted
wherever a blast is simulated — so a fast rematch could otherwise inherit a claimed window and
silently eat the first bend of the new match.

This is belt-and-braces against the Ribcage regression where players started a match on a
non-zero score. `RoundStats` lives on the **persistent** Player object, so a stat that survives
is worth zeroing twice rather than never.

---

## Comeback — and why it matters more here than anywhere else

`ComebackRatePerScoreDeficit = 4.0`, against a 3-bend target. **One bend behind buys 4 element
levels; two behind buys 8 and saturates near the level-10 sustained ceiling.**

The rate is a **function of the target** (`bonusLevels = deficit × rate`), so it only means
anything next to the scale of deficits the mode produces. This is the trap `DOGFIGHT.md` records
— a rate authored for a 500-point target quietly became worth 0.2 of a level when the target
became 120 — and dropping this mode's target from 60 to 3 was a 20× version of exactly it: the
0.4 that bought 6 levels at a 15-point deficit would buy **0.3 of a level** at the same
*fraction* of the race. A dead dial that reads as a working one.

`author_bends_assets.py` now **fails the build** if a quarter-of-target deficit stops buying a
whole element level, so the two numbers can never drift apart again without saying so. (Verified
by running the generator with the old 0.4 against the new target: it refuses.)

It carries more weight in this mode than in any other, because **the thing a bend takes is
element levels**. A player who is losing is, by construction, also debuffed — the losing
condition and the weakened condition are the same quantity. The comeback buff is what stops that
becoming a spiral. All four elements rise together per the platform law
(CLAUDE.md / `ElementalComebackSystem`: equal-elements), so this dial is the whole tuning surface;
a Charge-only weighting would be a fundamentals change, not a mode setting.

`ElementalComebackSystem` maps `GameModes.Bends → ScoreDifferenceSource.CombatPoints`, alongside
Dog Fight and for the same reason: `Score` lands only at game end, so points are the live stat.

---

## End condition

`BendsPointTurnMonitor` resolves the target **server-side** from `EndConditionOverridesSO`
(FrogletTools ▸ Game Modes ▸ End Game Conditions — never a per-scene field, per the
`/EndGameConditions` skill), syncs it via one NetworkVariable, and publishes it to
`GameDataSO.CombatPointTargetCount`. The turn ends when the mode's own
`BendsScoringRuleSO.IsObjectiveReached` reports a domain's summed `CombatPoints` has reached it.

The monitor shares `CombatPointTurnMonitorBase` with `DogFightPointTurnMonitor` — extracted for
this mode, because the two differ in exactly one thing (which target to read) and everything else
was identical. `DogFightPointTurnMonitor` keeps its class name and file, so its scene reference is
untouched.

Game end runs the family pattern: server winner detection in `OnTurnEndedCustom` → snapshot
`SyncFinalScores_ClientRpc` → `InvokeWinnerCalculated` + `InvokeMiniGameEnd`, with
`HasEndGame = false` and a `SetupNewRound` override so the Ready button never reappears.

Golf-timed: the winning domain's pilots carry their finish time, everyone else a
`GolfScoreSentinels` sentinel encoding their team's remaining points. Lower is better and winners
always sort first.

---

## Dolphin-only

Not enforced in this controller at all. Three independent platform layers, all reading the single
`Vessels` list on `ArcadeGameBends`:

1. `GameDataSO.SyncFromArcadeGame` clamps the launching machine's selection;
2. `ServerPlayerVesselInitializer.ResolveSpawnVesselType` re-clamps **server-side** at spawn —
   the one that catches a client whose owner-write `NetDefaultVesselType` still carries the hull
   it last flew;
3. `ServerPlayerVesselInitializerWithAI` clamps the AI's scene-authored class too (and the cloned
   scene already authors `vesselClass: 2`, so the clamp never has to fire).

---

## Assets

Authored by `Tools/Build/author_bends_assets.py` (idempotent, deterministic GUIDs, `--check` for
CI). Do not hand-edit these — re-tune the constant in the generator and re-run.

| Asset | What |
|---|---|
| `_SO_Assets/Games/ArcadeGameBends.asset` | the card: mode 42, Dolphin-only, 2–4 players, 2–3 domains, comeback 4.0 |
| `_SO_Assets/Scoring Rules/BendsScoringRule.asset` | metric 8 (CombatPoints), golf, bend 1 / gunnery 0 |
| `_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByCrystalBlast.asset` | `hitClass: 2`, both new flags on, 1 s window |
| `_SO_Assets/Effects/.../AOEConicExplosionImpactorDataContainer.asset` | **edited in place** — the debuff + the report |
| `_Scenes/Multiplayer Scenes/MinigameBends.unity` | Rampage clone, controller + monitor swapped |
| `_SO_Assets/Games/GameLists/OrganicRematchGames.asset` | card registered |
| `_SO_Assets/GameModeQuest/ProgressionConfig.asset` | mode 42 always unlocked |
| `Assets/Resources/EndConditionOverrides.asset` | `bendsPointTarget` / `…Build` = 3 |
| `ProjectSettings/EditorBuildSettings.asset` | scene added after Rampage |

## Shared-code touchpoints

| File | Change |
|---|---|
| `Data/Enums/GameModes.cs` | `Bends = 42` |
| `Data/Enums/CombatHitClass.cs` | `Debuff = 2` |
| `Data/Enums/GameToastSituation.cs` | `BendsQuarterBent` 60 / `BendsHalfBent` 61 / `BendsLeadChanged` 62 |
| `Data/Enums/IRoundStats.cs`, `RoundStats.cs` | `DebuffHitsLanded` + its event and NetworkVariable |
| `Controller/Arcade/Scoring/CombatHitScoring.cs` | three-way switch on hit class |
| `Controller/Player/Player.cs` | `Enum.IsDefined` validation (the bug above) |
| `Controller/AI/AIPilot.cs` | `SetDriftLookTargetProvider` / `ClearDriftLookTargetProvider` |
| `Controller/Arcade/ElementalComebackSystem.cs` | `Bends → CombatPoints` |
| `Controller/Arcade/TurnMonitors/CombatPointTurnMonitorBase.cs` | extracted from `DogFightPointTurnMonitor` |
| `ScriptableObjects/EndConditionOverridesSO.cs` + `Editor/EndConditionOverridesWindow.cs` | the Bends target, live + build baseline |

## Collider budget

**No change.** The mode adds no spawner, no environment prefab, no fauna and no pickup of its
own — it inherits Rampage's per-intensity cell configs unmodified, so the standing collider
budget for that arena is unchanged at every intensity. The only new runtime work is two effect
executions per blast-vs-vessel contact (a dictionary probe and a latch probe), on a contact path
that already ran.

---

## Verification — authored headless, NOT yet run in the editor

Everything below was validated out of editor: Roslyn parses every changed file with no
syntax/scope/duplicate errors, `check_conditional_compilation.py` passes, and
`author_bends_assets.py --check` reports byte-identical output. What remains needs the running
editor and a real lobby.

0. **Pace** — the very first thing to feel: at a 3-bend target a match is over in a handful of
   clean hits. If that reads as too abrupt, the target is one number in FrogletTools ▸ Game Modes
   ▸ End Game Conditions — but raise the comeback rate with it, or the generator's guard will
   (correctly) start complaining in the other direction.
1. **Import** — open the project; confirm no missing-script warnings on `MinigameBends.unity` and
   that `BendsController` / `BendsPointTurnMonitor` resolve on the Game object.
2. **The wiring, first and alone** — fly a Dolphin at an AI in ANY mode, blast it, and confirm the
   victim's element flowers drop and recover over ~4 s. If this does not happen, nothing else
   matters: it is the container edit.
3. **Scoring, host** — start a 2-player 2-domain match, bend the opponent once, confirm +1 on the
   HUD and the turn-monitor deficit dropping from 3 to 2.
4. **Scoring, client (MPPM)** — the same from a *client* Dolphin. Confirm **+1, not +2** (the
   `requireOwningMachine` gate) and **not +0** (the `Enum.IsDefined` fix). This is the single most
   important test on the branch — both bugs live on this path and neither is visible from the
   host, and at a 3-bend target a double-credit ends the match in two hits.
5. **Immunity** — confirm a bend on an elementally immune pilot scores nothing.
6. **Double catch** — a blast that engulfs two opponents scores 2, i.e. two thirds of the match.
7. **Growth window** — a cone that engulfs one opponent for a second or more scores 1, once.
8. **AI aim** — watch an AI collect a crystal with a rival within 900 u and confirm it drifts its
   nose toward the rival rather than toward the forest; beyond 900 u confirm it goes back to
   grazing.
9. **End + replay** — run a match to 3, confirm the scoreboard's secondary line reads `N bends`
   with the right counts on every peer, then replay and confirm everyone starts at 0.

## Known limitations / follow-ups

- **No toast copy.** `BendsQuarterBent` / `BendsHalfBent` / `BendsLeadChanged` are posted but no
  `GameToastConfigSO` defines them, so `TryGetDefinition` misses and the milestone toast is a
  silent no-op (the alert haptic still fires). This matches the shipped state of Dog Fight,
  Rampage, Ribcage and Wildlife Liberation, which all post situations with no authored copy. One
  config asset covering all five modes is the right fix, not five.
- **`BendsObjectiveProvider` is not wired into the scene**, exactly like `DogFightObjectiveProvider`
  — the objective-marker HUD element has no host in these scenes yet. The provider is correct and
  ready for whichever one lands first.
- ~~**The Drift Ward interaction is the branch's top open risk**~~ — **RESOLVED** by scoping the
  ward to `ElementalDebuffSources.DangerPrism`, so a drifting Dolphin is warded against the arena
  and never against another pilot's cone. See the section above. What remains open there is only
  the unrelated flight-model question (can a permanently drifting Dolphin manoeuvre at all), which
  one playtest answers.
- **The debuff magnitude is Rampage-era.** `-0.5` on every element over 4 s was authored for a
  blast that never touched a pilot; it has never been play-tested as a *scored* quantity. If a
  bend reads as too weak to be worth aiming for, that asset is the dial — not the point value,
  which sizes the race rather than the feel. At a 3-bend target this cuts both ways: the debuff
  now has to carry a whole third of the match, so if it reads as a shrug the mode reads as a
  coin flip.
