# Salvo — Technical Documentation

> **Naming.** `GameModes.Salvo = 44` is the code/data/enum identity, and the player-facing
> `DisplayName` on `ArcadeGameSalvo.asset` is **"Salvo"** too. Do not rename the enum, the
> controller, the scene, or this file (the Maelstrom/"Maelstrom" precedent covers a display
> split if one is ever wanted).
>
> **ID history:** authored as 42, renumbered to **44** when this branch merged
> bleeding-edge — upstream had already taken 42 (`Bends`) and 43 (`ScarabScramble`).
> The number is set in `Tools/Build/author_salvo_assets.py`; re-run the generator
> rather than hand-editing the card.

## Overview

Salvo is the **Sparrow-only demolition race** — Dog Fight's inverse in the same Boneyard.
There, the wreckage is cover and a pilot who spends the match demolishing scenery loses; here
**tearing the wreck apart IS the score**. Two to four pilots race by DOMAIN to destroy the
hostile-prism target (default **700**): guns chip one prism at a time for free, skyburst
missiles level whole hulks, and the missiles run on a **crystal economy** — the tank does not
regenerate, a rocket costs half of it, and the only refuel is an omni crystal.

**The reason to play it together: the WINGMAN RELOAD.** Every omni crystal collected reloads
the missile bays of **every pilot on the collector's domain**, not just the collector. One
pilot flies the crystal line while a wingman camps the densest wreckage and fires every reload
the runner buys — a real division of labour on top of the domain-pooled score, instead of
parallel solo demolition.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameSalvo.unity` — cloned from
  `MinigameDogFight.unity`, so the arena, spawn sphere, AI templates and cell wiring are the
  Boneyard's, verbatim
- **GameMode enum**: `GameModes.Salvo = 44`
- **Controller**: `SalvoController : MultiplayerDomainGamesController` — a structural sibling
  of `RampageController` (1 round / 1 turn, `HasEndGame=false`, server winner detection in
  `OnTurnEndedCustom`, snapshot `SyncFinalScores_ClientRpc`), plus the wingman-reload RPC
- **Scoring**: `SalvoScoringRule.asset` (`SalvoScoringRuleSO : RampageScoringRuleSO` —
  metric `ScoringMetric.PrismsDestroyed`, golf-timed; the subclass only changes the end-game
  reveal label to "SALVO TIME")
- **Turn monitor**: `SalvoPrismTurnMonitor` — resolves the target from
  `EndConditionOverridesSO.GetSalvoPrismTarget()` (default **700**, FrogletTools ▸ Game Modes
  ▸ End Game Conditions — never a per-scene field), syncs via NetworkVariable →
  `GameDataSO.PrismTargetCount`
- **Players**: **2–4** with AI backfill. `MinDomainsAllowed = 2` (a race needs a rival),
  `MaxDomainsAllowed = 3`
- **Vessels**: **Sparrow only** — the standard three platform clamp layers, fed by the single
  `Vessels` entry on `ArcadeGameSalvo.asset`
- **Crystals**: `CrystalCountMode.PlayerCountPlusExtra` **+5** (7 omni crystals in a 2-player
  lobby, 9 in a full one) **plus** 14 scattered elemental pickups (seed 42)
- **Comeback**: `ScoreDifferenceSource.PrismsDestroyed`, rate **0.013** (a quarter-of-target
  deficit ≈ 2.3 element levels — clears the "must buy a whole level" floor, well under
  Rampage's ~4.9-level footing since the target came down and the rate did not)
- **Environment**: the Boneyard cell assets **reused verbatim** (`Boneyard Cell Config
  {1..4}`, spawn profiles, scavengers, `SpawnableBoneyard{1..4}`) — 9,043 → 34,654 prisms by
  intensity
- **Config**: `_SO_Assets/Games/ArcadeGameSalvo.asset` (registered in
  `GameLists/OrganicRematchGames.asset`, `ProgressionConfig.alwaysUnlockedModes`)

## The loop (why the pieces already existed)

The whole missile economy is the Sparrow's shipped wiring, not this mode's invention:

| Piece | Where it lives |
|---|---|
| Missile tank: max **1**, starts full, **no regeneration** | `Sparrow.prefab` ResourceSystem, resource 0 ("Missiles") |
| A skyburst costs **0.5** of the tank → 2 rockets per refuel | `SkyBurstGunAction.asset` (`ammoCost`) |
| Full-auto guns cost **0** — always available, chip damage | `FullAutoAction.asset` |
| An omni crystal **sets the tank full** on collect | `SparrowVesselChangeResourceByCrystalEffect.asset` (the Sparrow's one `vesselCrystalEffects` entry), replayed on the collector's own machine by `CrystalManager.ReplayVesselCrystalEffects` |

Salvo's job was to build a mode where that loop is the game: stock the arena with crystals,
make destruction the score, and extend the refuel to the domain.

## The wingman reload (the one new mechanism)

```
OmniCrystalImpactor.AcceptImpactee            [SERVER only - clients early-out]
  └─ raises EventOnCrystalCollected (SOAP ScriptableEventCrystalStats,
     payload = collector's PlayerName)                 ← StatsManager already listens here
        └─ SalvoController.HandleOmniCrystalCollected  [server]
            ├─ resolve domain: gameData.TryGetRoundStats(name).Domain
            └─ RefuelDomainMissiles_ClientRpc(domain)
                └─ on EVERY peer (host included): for each player of that domain,
                   ResourceSystem.SetResourceAmount(missileResourceIndex, MaxAmount)
```

Why this shape:

- **The signal originates server-side** because omni collection resolves server-only
  (`OmniCrystalImpactor.IsNetworkClient()` early-out) — the SOAP raise simply never happens on
  a client, and the handler re-guards on `IsServer` anyway.
- **Ammo is deliberately LOCAL state.** Each machine simulates its own vessel's firing
  (projectiles are local objects — see DOGFIGHT.md "Multiplayer"), so a broadcast set-to-full
  on every peer is exactly as authoritative as the ammo system itself. The write that matters
  lands on each vessel's OWNER machine; the same write on replicas is a harmless idempotent
  set.
- **The collector is covered twice**: the platform crystal effect refills them (replayed on
  their owner machine), and the RPC's set-to-full is idempotent on top.
- **A blast-consumed crystal refuels nobody** — `ConsumeByBlast` raises with an empty
  `PlayerName` and the handler skips it; there is no pilot to credit a reload to.
- **Elemental crystals do NOT refuel.** They raise a different channel
  (`ElementalCrystalImpactor.OnCrystalCollected`, the static event) and pay element levels.
  Omni = ammo, elemental = progression — the same split Dog Fight uses.

## Scoring (nothing new)

`ScoringMetric.PrismsDestroyed` against `GameDataSO.PrismTargetCount` — the exact Rampage /
PeelTheCage machinery. The destruction stat auto-increments through `StatsManager.PrismDestroyed`
(server-side for trails, and via `Player.ReportEnvironmentPrismDestroyed_ServerRpc` for the
client-simulated environment — the Boneyard's wreckage is environment-owned `Domains.Blue`
mass, hostile to every domain, so all of it scores). Fauna bodies count too (a scavenger is
prisms); teammates' trails never score, by the roster domain check.

The two Sparrow-vs-Sparrow combat-hit effects still run here (they are wired on the shared
weapon containers), but `PointsForCombatHit` is 0 in this rule — shooting a rival pilot
suppresses them (spin + skimmer shrink), it does not score. That is deliberate: interference
is free, the quarry is the arena.

## Crystals

- **Omni**: `crystalCountMode: 1` (`PlayerCountPlusExtra`), `extraCrystalsToSpawnBeyondPlayerCount: 5`,
  `noNucleusSpawnRadius: 420` kept from the donor (the Boneyard has no nucleus; without the
  fallback every crystal stacks on the arena's exact centre). Rampage's inversion: there the
  crystal count IS the scarcity dial; here abundance is the point, because a Sparrow with an
  empty tank and no crystal in reach is a pilot with nothing to do but plink.
- **Elemental**: 14, scattered by `SalvoController.SpawnElementalCrystals` — the same
  deterministic per-peer recipe as Dog Fight (seed 42), same standing caveat: collection is
  per-peer, tolerable only because they score nothing.

## AI

**Platform default, deliberately** — the same reasoning as Rampage: an AI pilot already seeks
the nearest collectible cell item (here: the omni crystals, i.e. its own ammo line), and the
Sparrow prefab's `AIPilot` already fires FullAuto and SkyBurst on their own cooldowns at
whatever the wreck field puts in front of it. Salvo is **not** in
`ServerPlayerVesselInitializerWithAI`'s seek-players set (hunting pilots is Dog Fight's game),
and the controller installs **no** `SetExternalTargetProvider` — which is the one thing that
must not override crystal seeking (the Rampage lesson). AI missile ammo refuels through the
same wingman-reload RPC as everyone else (ClientRpcs execute on the host's client half, which
owns every AI).

## Objective marker

`MiniGameHUD.CreateObjectiveProviderForGameMode` maps Salvo to **`RampageObjectiveProvider`**
— the nearest managed, collectable omni crystal. Right for the same reason it is right in
Rampage: the crystal is the thing the match is played around (there the blast trigger, here
the reload), and the arena rains lifeform hearts + elemental pickups that must not be pointed
at.

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.salvoPrismTarget`, 0 = default **700**) — the hostile prisms **one
domain** must destroy. Live/Build split + build auto-restore work like every other mode.

700 vs Rampage's 2000: the Sparrow's destruction comes in crystal-rationed bursts (a rocket
costs half the tank) rather than the Dolphin's continuous graze-and-blast loop, and the
Boneyard's intensity-1 arena is 9,043 prisms against Rampage's 9,830 seeded forest. Retuned
down from the launch value of 1500 for a shorter match; neither number has a measured match
behind it yet.

## Assets

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameSalvo.asset` |
| Scoring rule | `_SO_Assets/Scoring Rules/SalvoScoringRule.asset` |
| Scene | `_Scenes/Multiplayer Scenes/MinigameSalvo.unity` (in `EditorBuildSettings`) |
| End conditions | `Assets/Resources/EndConditionOverrides.asset` (`salvoPrismTarget`) |
| Boneyard cell/profiles/arena | Dog Fight's `Boneyard *` assets, referenced verbatim — **not** forked |

Every Salvo-owned asset is authored by `Tools/Build/author_salvo_assets.py` (deterministic
GUIDs, idempotent, validates before writing). **Re-tune there and re-run** rather than
hand-editing YAML. The generator clones `MinigameDogFight.unity` as its donor — if the Dog
Fight scene is reworked, the asserts here will fail against the moved donor; that is the
expected end state of a migration generator (see the Dog Fight generator's §9 note), not a
break to repair.

## Shared-code touchpoints (added for this mode)

| Site | Change |
|---|---|
| `GameModes` | `Salvo = 44` |
| `SalvoController` | new controller (Rampage shape + wingman reload + elemental scatter) |
| `SalvoPrismTurnMonitor` | new turn monitor reading `GetSalvoPrismTarget()` |
| `SalvoScoringRuleSO` | new rule subclass (`RampageScoringRuleSO` + "SALVO TIME" reveal) |
| `EndConditionOverridesSO` (+ window + asset) | `salvoPrismTarget` live/build/getter, default 700 |
| `ElementalComebackSystem.DefaultSourceFor` | Salvo → `PrismsDestroyed` |
| `MiniGameHUD.CreateObjectiveProviderForGameMode` | Salvo → `RampageObjectiveProvider` |

Nothing else moved: no new stats, no new metrics, no new impact effects, no vessel or cell
edits. The mode is deliberately a composition of shipped systems.

## In-editor verification (authored headless — NOT yet run)

1. **Open** `MinigameSalvo.unity`. Every script reference resolves; the controller's
   inspector shows `rule` = SalvoScoringRule, `onOmniCrystalCollected` =
   EventOnCrystalCollected, `missileResourceIndex` = 0, elemental scatter 14/400/42; the
   Cell shows the four Boneyard configs on Intensity Wise.
2. **DESTRUCTION SCORES — the load-bearing check.** Shoot wreckage with the full-auto: the
   domain score ticks per prism destroyed. Land a skyburst on a hulk: the score jumps by the
   blast's whole harvest. Shooting a TEAMMATE's trail scores nothing.
3. **THE WINGMAN RELOAD — the headline check.** In a 2v2 (or 1 human + AI teammate), empty
   your missile tank (two rockets), then have your TEAMMATE collect an omni crystal while you
   touch nothing. Your missile gauge must refill on your own machine, and a rocket must fire.
   An OPPONENT's collect must NOT refill you.
4. **Client refuel.** In a real lobby, the CLIENT empties its tank and the HOST collects (same
   domain): the client's gauge refills. Reverse it: host empties, client collects — host
   refills. If either direction fails, the RPC path is broken.
5. **Crystal abundance.** Count the omni crystals at match start: players + 5 (e.g. 9 in a
   4-player lobby), scattered inside r≈420, none stacked at the centre. Collect one and
   confirm it respawns somewhere else.
6. **Blast-consumed crystals refuel nobody** (if a Scarab forge or equivalent can run here:
   a blast spending a crystal must not reload anyone — the empty-name guard).
7. **Elementals don't refuel.** Skim an elemental crystal with an empty tank: element level
   rises, missile gauge stays empty.
8. **Win + scoreboard.** First domain to 700 ends the turn; winners show "SALVO TIME" +
   time, losers "N Prisms Left" with individual "N Prisms" secondary lines. Teammates share
   the win. Replay (scene reload) resets everything to 0.
9. **Comeback.** Let one domain fall ~175 prisms behind: the trailing pilots' element flowers
   fill ~2 levels; their turret prisms grow (Mass). Closing the gap drains it.
10. **AI participates.** AI Sparrows fly at crystals, fire guns/rockets into wreckage, and
    their domain's score climbs. They must NOT orbit enemy pilots (that is Dog Fight's brain;
    Salvo must not be in the seek-players set).
11. **Objective arrow** points at the nearest omni crystal, never at a lifeform heart or an
    elemental pickup.
12. **Regression — Dog Fight unchanged.** Launch Dog Fight: 4 fixed omni crystals (not
    players+5), gunnery scores, wreckage doesn't. The two modes share the Boneyard assets, so
    a Salvo retune must not have touched them.
13. **Pacing.** Time a full match at intensity 1 — if it runs long, the target
    (`salvoPrismTarget`) is one editor field; note how much destruction came from rockets vs
    guns.

## Known limitations / follow-ups

- **700 is still unmeasured** — retuned down from the launch value of 1500 on request, not from
  a playtest. The intended match length is 3–6 minutes; the target is the dial.
- **No refuel FEEDBACK beyond the gauge.** The wingman reload lands silently (the ammo gauge
  fills). A `GameToastSituation` ("WINGMAN RELOAD — <name>") + a small SFX would sell the
  cooperation; the toast enum + config authoring was deliberately left out of v1 (the same
  unauthored-toast state Dog Fight ships in).
- **No milestones.** Rampage ships without them too; Dog Fight's quarter/half rungs would
  port trivially if the mode needs mid-match drama.
- **Shared-arena coupling is deliberate but real**: retuning the Boneyard for Dog Fight
  (structure counts, danger, PhaseThresholds) retunes Salvo's quarry too. If the two modes
  ever need to diverge, fork the cell configs then — not preemptively.
- **The elemental scatter is per-peer** (Dog Fight's standing caveat) — fine while they score
  nothing.
