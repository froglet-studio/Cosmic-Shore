# Scarab Scramble — Technical Documentation

## Overview

**Scarab Scramble** is the Scarab-only party game — the accessible sibling of Astro League,
and the platform's designated **beachhead mode**: the one a brand-new player can be handed
with a single sentence and be scoring inside their first minute.

> Fly through a bright crystal and it becomes YOUR ball. Roll it, bat it, bank it off the
> walls and through any glowing ring — first team to the goal target wins.

1–6 players, 2–3 domains, AI backfill for solo, through the same unified Netcode scene
pipeline as every other domain minigame. The design record it lands is
`R_VesselActions/SCARAB.md` — this mode answers its open question §15.11 as "**a second
mode** that shares the arena machinery", and ships the §4.2–§4.5 mode-side ball work
(permanent ownership, multi-ball, per-ball attribution, a forge cap).

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameScarabScramble.unity` (cloned from
  the Dog Fight scene — same `Game` GO component set, mode components swapped in place at
  the same fileIDs)
- **GameMode enum**: `GameModes.ScarabScramble = 42`
- **Controller**: `ScarabScrambleController : MultiplayerDomainGamesController`
  (1 round / 1 turn, `HasEndGame=false`, `UseSceneReloadForReplay=true`, server winner
  detection in `OnTurnEndedCustom` → snapshot `SyncFinalScores_ClientRpc` — the DogFight
  shape)
- **Scoring**: `ScarabScrambleScoringRuleSO : AstroLeagueScoringRuleSO`
  (`metric = Goals`, points not golf; race to `GameDataSO.GoalTargetCount` over domain
  sums). Goal target lives in **EndConditionOverridesSO** (FrogletTools ▸ Game Modes ▸
  End Game Conditions), default **10**, resolved by `ScarabScrambleGoalTurnMonitor`
- **Config**: every mode number lives in `ScarabScrambleSettingsSO`
  (`Assets/_SO_Assets/Games/ScarabScrambleSettings.asset`). Ball physics deliberately does
  NOT: the ball reads the settings asset serialized on `AstroLeagueBall.prefab`, so the
  payload behaves identically in every context it is forged in
- **Vessels**: **Scarab only** (`ArcadeGameScarabScramble.asset` `Vessels = [Scarab]`),
  enforced by the three platform layers (`SyncFromArcadeGame`, `ResolveSpawnVesselType`,
  the AI clamp). No mode-local vessel check
- **Intensity**: court radius (480→720), hoop count (4→1) and mouth (60→42) per intensity,
  resolved server-side from the settings lists and replicated as GEOMETRY NetworkVariables
  (`n_CourtRadius`/`n_HoopCount`/`n_HoopMouthRadius`) so late joiners and asset drift can
  never desync the court. Intensity 1 is the party setting; intensity 4 is one small hoop
  at the sphere's focus point

## The design, and why each rule points at accessibility

Astro League's skill floor is the sword (blade-point kinematics), its scarcity is one
contested ball, and its rhythm is stop-start (kickoffs, celebrations). Scramble inverts
all three, and a three-judge design panel (accessibility / beachhead-referral /
platform-law) shaped the final rule set:

1. **Everyone always has their own objective.** Every omni crystal forges a ball on
   SKIMMER contact (`ScarabBallForgeBySkimmerCrystalEffectSO` — the crystal becomes a ball in
   place, at rest, and the hull then strikes a real ball), so a new player is never locked out
   the way a single match ball locks them out.
2. **Ownership is permanent** (`AstroLeagueBall.SetOwnershipLockedServer`, SCARAB.md
   §4.2): a ball is its maker's domain colour from birth to death, so it always eats the
   enemy's trail and shields its own — the legibility that keeps a multi-ball arena
   readable, for players and spectators alike.
3. **The ARMING gate makes "no wrong way to touch anything" literally true.** A hoop
   crossing scores only when the ball's LAST TOUCH belongs to its owning domain (untouched
   since forge = the maker's launch, still scores). An enemy shoving your ball through a
   ring scores NOTHING — the ball sails on, disarmed, until your team re-touches it. This
   was the panel's headline accessibility fix: without it, the single most instinctive act
   (push ball through ring) was the mode's one trap.
4. **The juke is the STEAL.** The one enemy act that converts a ball is a strike delivered
   mid-juke-dash (`ScarabJukeController.IsJukeStrikeWindowOpen`, read by the ball's
   strike path): the committed skill move converts, the casual bump never does. This is
   the mode's player-vs-player verb — "stole it mid-flight and scored with YOUR ball" —
   restored after permanent ownership deleted last-touch stealing.
5. **Goals stop nothing** (§15.13's party answer): the scored ball detonates
   (`SpendServer` — the goal-mouth burst, continuity of existence) and play flows on. No
   kickoffs, no world-stop celebrations, nobody waits.
6. **The court is a sphere and walls REFLECT.** `AstroLeagueBoundary` Sphere is the
   centre-focusing shape: every carom returns the ball toward the middle, where the hoops
   ring the centre at `hoopRingRadiusFraction` (0.45) — a new player's wild shots are
   recycled into chances. §4.3's boundary-death idea is deliberately NOT used here (a
   resource you can waste is the wrong feel for a beachhead mode). The sphere physics also
   manufactures the mode's signature moment — the multi-carom **bank goal** — which the
   controller celebrates by bounce count (`ScarabScrambleBankGoal` toast, "BANK x{n}").
7. **The cap refuses, never culls, and never silently.** Live balls are capped per DOMAIN
   (`ballsPerPlayer × roster`); at the cap a crystal simply collects normally (§15.5's
   recommendation — an expiry would be an imposed clock) and the capped pilot gets a
   targeted toast, because a silent gate is indistinguishable from a broken feature.

## Class inventory

| Class | Role |
|---|---|
| `ScarabScrambleController` | Match director: court/hoop build on every peer (resolved-geometry NVs), forge policy hooks (`ScarabBallForge.ForgeGate` cap + `OnForged` adoption: ownership lock, boundary handoff, forger ledger), hoop scoring + arming gate + toast beats (goal / bank / match point / lead change / cap), AI rollers, fauna exclusion sweep, final-score snapshot |
| `ScarabScrambleHoop` | One scoring ring: ToyFactory ring body (the toy "portal you thread" shape language), bloom-in (continuity law; detection live at full radius from t=0 — state final at start, photons animate), per-ball segment-crossing detection vs `AstroLeagueBall.Live` (either direction, teleport-guarded), MPB flare on goals |
| `ScarabScrambleSettingsSO` | Court/hoop/cap/AI/fauna-exclusion tunables (per-intensity lists) |
| `ScarabScrambleScoringRuleSO` | Thin subclass of the AL rule so the mode owns its asset |
| `ScarabScrambleGoalTurnMonitor` | DogFight-shape monitor: resolves the goal target from `EndConditionOverridesSO`, NV-syncs it, publishes `GameDataSO.GoalTargetCount`, ends the turn via `rule.IsObjectiveReached`, shows the local DOMAIN's deficit |
| `ScarabScrambleObjectiveProvider` | HUD arrow: nearest own-domain live ball, else nearest FORGE-SOURCE crystal (omni only — `ScarabScrambleController.IsForgeSource`; pointing a new player at an elemental heart teaches the wrong first lesson). Wired via the `MiniGameHUD.CreateObjectiveProviderForGameMode` case |

## Shared-code touchpoints (why non-Scramble files are in this branch)

| Change | File |
|---|---|
| `ScarabScramble = 42` | `_Scripts/Data/Enums/GameModes.cs` (+ `EnumIntegrityTests` count → 41; that assertion had drifted to 33 while the enum grew to 40, so it was already failing before this branch) |
| Ownership lock + touch ledger (`LastTouchDomainServer` / `LastToucherNameServer` / `WallBouncesSinceTouchServer`) + juke-steal + `SpendServer` + birth bloom + **`n_SizeScale`** | `AstroLeague/AstroLeagueBall.cs` (+ `spawnBloomSeconds` on `AstroLeagueSettingsSO`) |
| `n_SizeScale` fixes a confirmed pre-existing bug this mode made live: `SetSizeScale` ran server-side after the spawn payload was built, so a SPACE-scaled forged ball rendered — and prism-scanned — at prefab size on every remote peer | same |
| `ForgeGate` (mode cap policy) + `OnForged` (mode adoption) + both forge paths routed through `Request` | `R_VesselActions/ScarabBallForge.cs`, `EffectsSO/Skimmer Crystal Effects/ScarabBallForgeBySkimmerCrystalEffectSO.cs` |
| `IsJukeStrikeWindowOpen` (the steal window) | `Vessel/ScarabJukeController.cs` |
| `scarabScrambleGoalTarget` live/build/getter, default 10 | `EndConditionOverridesSO` + window + `Resources/EndConditionOverrides.asset` |
| `GameToastSituation` 60–66 (goal, match point, lead change, forge hint, roll hint, bank goal, ball cap) | `_Scripts/Data/Enums/GameToastSituation.cs` |
| `case GameModes.ScarabScramble → Goals` | `ElementalComebackSystem.DefaultSourceFor` + `ElementalComebackSystemTests.LiveSourceCases` |
| Objective-provider case | `_Scripts/UI/MiniGameHUD.cs` |

## The Scarab's nucleus seeding, seen from the mode

The Scarab passively studs the nucleus with balls of its domain (`SCARAB.md §4.6`). That is a
**vessel ability, not mode content** — this controller installs nothing for it and can turn none of
it off — but because Scramble's court *is* the nucleus, the two meet:

- Balls knocked **inward** off the nucleus wall drop into the court as ordinary balls of
  consequence, so the mode gains a **second income stream** beside the crystal forge. They arm,
  score and detonate like any other ball.
- Balls knocked **outward** leave through the wall into the cytoplasm and bounce around out there
  for fun. They are outside the court, so they cannot reach a hoop and cannot score — which is the
  intended "just for fun" reading, achieved by geometry rather than by a rule.
- **Embedded balls are excluded from the mode's forge cap** (`CanForge` skips
  `IsEmbeddedOnNucleus`). Counting them would let a passive vessel behaviour quietly starve the
  crystal forge, refusing a pilot a ball because of balls they never made and cannot yet reach.
- The **overload** (one ball too many banked inside the nucleus) detonates every live ball,
  including the court's scoring balls. It is rare, player-caused and loud — but if playtest finds
  it too punishing for a beachhead mode, the dial is `detonateAllLiveBalls` on
  `Resources/ScarabNucleusFieldConfig`, which narrows it to the banked balls only. Do not add a
  mode-local suppression: the ability is platform behaviour and Scramble is not entitled to a
  private exception.

## Known limitations / follow-ups

- **The juke-steal works for remote clients** via `ScarabJukeController.NotifyJukeFired_ServerRpc`
  (the controller is now a `NetworkBehaviour`; a fire mirrors onto the server's replica, so
  `IsJukeStrikeWindowOpen` is true where the ball's strike path actually runs). The window
  opens one half-RTT late on the server — the same latency the dashed vessel's
  NetworkTransform pose arrives with, so the two travel together. Only the fire MOMENT
  travels; the window length is the server's own serialized `jukeDurationSeconds`, never a
  client number. The fuller `Juke_ServerRpc` (direction + Charge snapshot) remains SCARAB.md's
  Phase 2.5 item for the cavitation-blast half.
- **An offline Roslyn pass cannot see a `System`/`UnityEngine` name collision.** This branch
  was syntax-checked against .NET reference assemblies with no `UnityEngine.dll` available,
  so every Unity type was already unresolved — which means `Object` resolved *only* to
  `System.Object` and the CS0104 ambiguity that a real Unity compile raises
  (`ScarabBallForge`, after `using System;` was added for the forge gate's `Func`/`Action`)
  was **structurally invisible** to it. The offline check proves syntax and structure; it
  cannot prove name resolution. The collision set under `using System;` + `using UnityEngine;`
  is exactly `Object` and `Random` — grep for those two before trusting an offline pass, and
  treat the first real Editor compile as the authority.
- **AI cannot juke or blast** (`ScarabJukeController` is inert under autopilot), so AI
  play is fetch-and-escort only (`ArmRollers`: nearest crystal ↔ escort own ball behind
  the predicted position toward the nearest hoop, full throttle via the Scarab
  transformer's autopilot branch).
- **No disarmed-ball visual**: an enemy-touched ball looks identical to an armed one. A
  future flicker/dim needs a replicated arming bit; deferred.
- **No forge-exclusion zone around hoop mouths**: a crystal spawning at a hoop is a
  forge-through-the-ring jackpot. Deliberate for v1 — spawn luck is shared and the moment
  is a delight, not an exploit (recorded against SCARAB.md §4.1's anti-trivialisation
  reasoning; add a small no-forge radius if playtests disagree).
- **The forge prefab's `destroyedBySuperShielded: 1` is inert here** (no super-shielded
  mass exists in this arena) — but it and a super-shielded edge lining are **mutually
  exclusive**: the lining sits inside the analytic wall, so adding it back for looks
  would make every wall approach silently kill the ball. Do not add the lining without
  flipping the flag on a mode-owned ball prefab variant.
- **PhaseThresholds are an authored ESTIMATE** (Restless 12000/11000, Frenzy 36000/32000
  volume over a zero floor — Scarab trail is 10 at rest → 40 skim-grown per prism, one
  per spawn; the Astro League ladder rides a 30k lining floor this arena does not have
  and must never be copied). Run FrogletTools ▸ Ecology ▸ Measure Cell Environment
  Baselines and retune after the first playtest; Restless is the fauna-release gate and
  the mode's primary ecology pacing dial.
- **Toast copy claims "bumping enemy balls is always safe"** — true for scoring (the
  arming gate) and for ownership (bumps never recolor); a shielded own-ribbon deflects
  (`prismCaromRestitution 1`) rather than "eats momentum". Copy uses "deflect" where it
  matters.
- **Card art is unauthored** (`IconActive`/`IconInactive`/`CardBackground` = 0, the Astro
  League card's own current state).

## Cell ecosystem

The standard Cell owns the environment (`Scarab Scramble Cell Config` — cloned from the
Astro League trail-grazing template with per-mode fauna assets, per the shared-species
rule): no flora, three low-population foragers (tadpole/brittlestar/piranha clones), and
the **cleanup crew waits outside the court** until the volume ladder leaves Calm
(`Cell.FaunaExclusionRadius` swept by the controller — the Astro League pattern, reading
"the pitch is crowded" from the spine). The nucleus IS the court:
`SetNucleusWorldRadius(courtRadius)` + **`NucleusIsControlZone = false`** (play geometry,
not a claim — skip this and the whole pitch is inedible, ECOSYSTEM §25.1). Crystals spawn
inside the court by the platform's own rule (the omni respawn volume IS the nucleus),
count = players + 2 (`NetworkCrystalManager` PlayerCountPlusExtra), neutral domain.
Collider budget: hoops and court add ZERO colliders (analytic boundary + plane-crossing
detection); balls carry one SphereCollider each, capped by the forge gate; fauna bounded
by `MaxLivePopulation` (8+4+22).

## In-editor verification (authored headless — NOT yet run)

1. **Open** `MinigameScarabScramble.unity`: every script reference resolves (no Missing
   (Mono Script)); the Game GO carries ScarabScrambleController + ScarabScrambleGoalTurnMonitor.
2. **Enter play (solo + AI backfill, intensity 1)**: court sphere ≈480 blooms as the
   nucleus; four rings bloom in facing the centre; crystals appear inside the court.
3. **Forge**: fly through a bright crystal → a ball of your colour GROWS in ahead of you
   carrying your velocity (no pop). HUD arrow points at it.
4. **Score**: push it through any ring from either side → ring flares, ball detonates,
   toast `{name} rings one home - n/10`, HUD domain sums move, play continues.
5. **Arming gate**: push an ENEMY ball through a ring → nothing scores, ball sails on.
   Re-touch by its owner → it scores again for them.
6. **Juke-steal**: juke-dash into an enemy ball → it converts to your colour.
   Ordinary bump → it never converts. Works from a remote client too
   (`NotifyJukeFired_ServerRpc` opens the window on the server's replica).
7. **Bank**: score off 2+ wall caroms → `BANK x{n}` toast.
8. **Cap**: forge 2×roster balls → next crystal collects normally + targeted cap toast.
9. **Match end**: first domain to 10 → winner banner, scoreboard, Play Again reloads.
10. **MPPM two-client**: forged balls appear on the client at the correct SIZE (the
    `n_SizeScale` fix) and colour; goals sync; a client's juke-dash steals (the
    `NotifyJukeFired_ServerRpc` round-trip).
11. **Ecology**: idle until trail silts the court (~Restless 12000 volume) → foragers
    pour over the court wall and graze.
