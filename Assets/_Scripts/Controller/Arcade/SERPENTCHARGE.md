# Serpent Charge (`GameModes.SerpentCharge = 67`)

The Serpent's **CHARGE** game: a **sniper duel**. A Charge game is about hitting other pilots with
your abilities; the Space games are the ones about destroying prisms (Garrett, 2026-10-16). So the
only thing that scores here is a **snipe**: a Sniper Shot that strips a rival pilot. On the
Serpent, **Charge is the Sniper Shot's recovery**. The rifle waits 12 s between rounds at rest and
5.4 s at Charge 10. The round is the same at every level: a 1.5° hitscan cone, 3,000 u long. It
pierces everything hostile it contains, and it strips any rival pilot inside its reach. So the mode
works like this:

1. Hold the scope.
2. Put the reticle on a rival pilot and fire.
3. Lay a seed to vanish from their sights, and use the forest as cover.
4. Spend the cooldown collecting Charge so the next round comes sooner.

The first **DOMAIN** to **8 snipes** wins, golf-timed (Dustup's shape).

It follows the `<Hull><Element>` family: Sirocco (Butterfly · Space), Grizzly Charge, Grizzly Time
and Warpline (Stoat · Time).

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameSerpentCharge.unity` (cloned from `MinigameSirocco.unity`) |
| Controller | `SerpentChargeController : MultiplayerDomainGamesController` |
| AI | `SerpentMarksmanAI`, a mode-local server driver that hunts pilots (below) |
| Metric | `ScoringMetric.CombatPoints` (8), reused. A snipe is a `Debuff`-class combat hit |
| Rule | `SerpentChargeScoringRuleSO : ScoringRuleSO`. `snipePoints` **1**, every other hit class **0** |
| Turn monitor | `SerpentChargeSnipeTurnMonitor : CombatPointTurnMonitorBase` (Dustup's shape, its own getter) |
| Target | `EndConditionOverridesSO.serpentChargeSnipeTarget` (**8**) |
| Comeback | rate **0.6** (Dustup's): a quarter behind (2 snipes) buys 1.2 element levels |
| Arena | **Rampage Cell Config 4 only**, referenced read-only. The forest is cover, not the score |
| Economy | waves of **Charge crystals**, 10 every 40 s |
| Objective arrow | `BendsObjectiveProvider`: the nearest visible rival pilot (cloaked pilots are skipped) |
| Genre petal | **Charge**, from the metric alone (no `ModeGenre` row) |
| Generator | `Tools/Build/author_serpent_charge_assets.py` (`--check`) |

## The loop

1. **Scope** (hold the left trigger). The eyepiece opens and the right trigger becomes the rifle.
   Unscoped, the right trigger is the cloak (`SERPENT_SNIPER_SCOPE.md`).
2. **Find a rival.** The arrow points at the nearest rival pilot you can see. The forest is cover:
   a round reaches only as far as the farthest prism it kills (the full 3,000 u if it kills none),
   so a pilot tucked behind a stand is out of reach of a round that dies in the stand.
3. **Fire** (right trigger, while scoped). A rival pilot inside the round's reach is stripped, and
   that is **one snipe**. Prisms in the cone die too, and they pay nothing.
4. **Cloak** (right trigger, unscoped). The seed wall goes down and your hull, its tail, its jets
   and its trail vanish from every other player. No AI tracks a cloaked pilot, and the arrow skips
   one.
5. **Collect Charge while you reload.** Each Charge crystal shortens the next cooldown, and the
   comeback hands a trailing domain extra element levels on top.

## Reused, and why

- **The metric and the turn monitor are Dustup's shape.** The pilot strip was **already** a landed
  combat hit of class `Debuff` (`SniperShotAction.asset` `hitClass: 2`), so `CombatHitScoring.Credit`
  already paid it into `DebuffHitsLanded` (the raw count) and `CombatPoints` (at the rule's price).
  Nothing new is counted. The rule prices `Debuff` at 1 and every other class at 0 explicitly per
  class, so a mis-authored vessel roster cannot hide behind a working scoreboard. The same-victim
  window (0.5 s) sits far under the rifle's 5.4-12 s cooldown, so every round that connects pays.
- **The arena is Rampage's fourth config, alone** (`cellTypeChoiceOptions: 0` over one config).
  It was the four-config intensity ladder while prisms were the score. Now the forest is only
  cover, and config 4 is the rung measured at **58.7 FPS in a build** (`RAMPAGE.md`). Intensity no
  longer changes the forest. It changes only the AI ladder below.
- **The scene** is a small delta from Sirocco's: identity guids, the four AI hulls going from
  Butterfly (13) to Serpent (7), and the cell narrowed to config 4. The spawn ring is Rampage's own,
  500 outside the nucleus. The scene is committed, so the clone stands down. The redesign's two
  scene edits (the monitor's script guid and the cell block) are an idempotent patch the generator
  applies to the committed scene.
- **The forest PLANTS IN over the opening ~45 s** (2026-10-13, Rampage's own change, inherited).

## Charge crystals (the mode's economy)

`chargeCrystalsPerWave` (10) elemental **Charge** crystals are scattered through a 320-1000 u shell
around the arena centre. Each wave clears the previous one and lays a fresh scatter every
`chargeCrystalWaveSeconds` (40).

They are **local objects**, laid identically on every peer. The server broadcasts only a wave
index (`LayChargeWave_ClientRpc`), and each peer derives the layout from `(seed, wave)`. This is
the Salvo / Dog Fight recipe with one change for a match-long economy: a wave REPLACES the last
one. Collection is per-peer, as it is in those modes, so the per-peer state never has to agree and
never accumulates drift. That is tolerable for the same reason it is there: a crystal scores
nothing. It only shortens a cooldown, and the cooldown is owner-authoritative anyway
(`SniperShotActionExecutor`'s documented limitation).

## AI: the pilot hunter (`SerpentMarksmanAI`)

`AIPilot` writes a stick and a throttle and nothing else. On the Serpent prefab its generic ability
cycler toggles the cloak on a timer and never touches the scope, and the rifle only fires while the
scope is up. Without a driver every AI Serpent would fly the whole match unable to fire, which is
the Tollway rule's defect: an all-AI domain that cannot play. So every AI Serpent becomes a hunter.

- **It holds the scope** with one replicated LT press (the scope is a latch). It re-presses only if
  the latch did not take, for example if input was muted for the countdown.
- **It owns the right trigger, so it cloaks deliberately.** RT is the cloak unscoped and the rifle
  scoped, so AIPilot's ability cycler is told to keep off the cloak
  (`AIPilot.SuppressCycledAbility<CloakSeedWallActionSO>`, restored on disarm). Otherwise a cycler
  press while the scope was up would fire the rifle. Once per reload, and only when at least
  `aiCloakMinCooldownSeconds` (5) of it remains, the hunter does three things: it lowers its scope,
  presses the cloak **through the replicated input**, and raises the scope again a second later.
  The hull, its tail, its jets and its trail vanish on every machine.
- **It hunts the rival it can bring to bear most cheaply.** Every re-plan it takes the visible
  rival with the smallest distance divided by a turn factor (1 dead ahead, falling to
  `aiTurnPenaltyFloor` straight behind), within its hunt range. Past that range it flies at the
  nearest visible rival. It steers at that pilot plus a fixed aim error for the plan's life
  (`SetExternalTargetProvider`). Inside `aiMinAimDistance` it pulls away to `aiStandoffDistance` and
  turns back in, because a sniper cannot line up a pilot it is about to ram.
- **It cannot see a cloaked pilot.** A rival whose hull is cloaked
  (`CloakSeedWallActionExecutor.IsCloaked`) is not a target. A rival it was hunting is dropped the
  moment they cloak, and the bot flies to where it last saw them.
- **It fires only when the round would strike a rival.** The fire gate first checks, cheaply,
  whether any visible rival is inside the rifle's cone. Only then does it ask the rifle's own
  `QueryCone` (the rifle's range, half-angle, radius floor and target filter) for the round's
  reach, and fire if the rival is inside it. The trigger is a replicated RT press, because the
  round resolves on every peer. The hunter makes **no forest-wide line searches**. The prism-race
  design's per-candidate full-range cone queries are gone with it.
- **On a long cooldown it detours to Charge crystals**, but only when at least
  `aiDetourMinCooldownSeconds` of reload remain, so a detour never costs a shot.

**Difficulty is the intensity row.** Every strength is a four-rung ladder on
`SerpentChargeSettings.asset`. The generator asserts each one is monotone in the hard direction,
that intensity 4's aim error is inside the cone and intensity 1's outside it, and that the C#
initializers match the asset:

| Ladder | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| re-plan every (s) | 1.2 | 0.9 | 0.6 | 0.4 |
| aim error (°, vs a 1.5° cone) | 3.0 | 2.0 | 1.2 | 0.5 |
| reaction to a ready rifle (s) | 1.0 | 0.6 | 0.3 | 0.12 |
| pilot-hunt range (u) | 1,500 | 2,000 | 2,500 | 3,000 |
| Charge-crystal detour range (u) | — | 450 | 700 | 1,000 |

At intensity 1 the bot aims outside the cone, so it misses more than it hits, reacts in a second
and never detours for Charge. At intensity 4 it re-plans every 0.4 s, aims within half a degree,
reacts in about a tenth of a second, hunts you across the rifle's whole range and keeps its Charge
topped up. The only thing that beats it is cover and the cloak.

## Platform changes (and the rule each one records)

- **`GameModes.SerpentCharge = 67`**, and `EnumIntegrityTests` moves to 63 members (ThresherTime took 66 on bleeding-edge first).
- **`EndConditionOverridesSO.serpentChargeSnipeTarget`** gets the full row set (field, build twin,
  getter, authored-target row, live/build compare, apply, capture) plus the editor window's field,
  "effective now" and build-summary rows. It replaced `serpentChargePrismTarget` with the prism
  race. The generator removes the retired keys from `EndConditionOverrides.asset`, because Unity
  never prunes a serialized key the class lost.
- **`GameToastSituation.SerpentChargeScopeHint = 137`**, an idle hint. The rest is reuse: `82`
  (`DebuffHitsLanded` rose) **is** a snipe in this mode, and `128-131` are the race beats from
  `DomainRaceToasts`. The prism milestone (`83`) is gone.
- **`MiniGameHUD`**: SerpentCharge → `BendsObjectiveProvider`. That provider now also **skips a
  cloaked pilot** in every mode that uses it, because an arrow that points at an invisible pilot
  gives the cloak away.
- **AI cloaks reach every player** (2026-10-13). `AIAbility.ReplicateThroughInput` is a new
  opt-in on AIPilot's ability cycler. When it is set, the entry presses the vessel's own control
  through `PerformShipControllerActionsReplicated` instead of starting the ability locally on the
  server. It is ticked on the Serpent's cloak entry, and it fixes the cloak in **every** mode that
  flies AI Serpents. Before, an AI cloak hid the hull, faded the trail and laid the seed on the
  server only. `R_VesselActionHandler.TryGetInputForAsset` answers which control runs a given
  authored asset. General rule: an AI press whose output rides no other replicated channel must
  be replicated (that method's own doc states it).
- **A cloak hides the WHOLE vessel from observers** (2026-10-16). It used to disable only the hull
  renderer, so the **VesselTail** (the model's tail TrailRenderer, not the jets,
  `Docs/VESSEL_TAIL_AND_JETS.md`), the jets and every other renderer under the vessel root stayed
  visible on an "invisible" pilot. `CloakSeedWallActionExecutor` now sets `forceRenderingOff` on
  every renderer under the vessel root for non-local viewers (prisms excluded; `enabled` is left
  to the owners that toggle it, as `VesselTailAndJets.HideTails` does) and lifts exactly those at
  the reveal, clearing a TrailRenderer first so it does not draw the route flown under the cloak.
- **AIs do not track a cloaked pilot** (2026-10-13). `CloakSeedWallActionExecutor.IsCloaked` is
  one predicate, true from cloak to reveal. AIPilot's platform player seeking (Joust, Dog Fight,
  Broadside) skips a cloaked candidate and breaks an existing lock when its target cloaks, and the
  hunter does the same. `AIPilot.SuppressCycledAbility<T>` lets a mode drive an ability itself.
- **`SniperShotActionExecutor.MinPathRadius`** is a new read-only accessor, so a caller that
  *predicts* a shot asks exactly the cone the shot fires. General rule: an AI that scores its own
  options should score them with the weapon's own geometry, never a copy of its numbers.

## Verification status

**Authored headless; nothing has been opened in the editor.**

Proved:
- The generator's `--check`, watched failing on three negative controls for the snipe redesign:
  a drifted `DefaultSerpentChargeSnipeTarget`, a settings-SO ladder initializer that disagreed with
  the asset, and the scene's cell reverted to the intensity ladder. All passed once restored.
- The stand-down path, watched by drifting the Sirocco donor's AI templates.
- The standing out-of-editor gates.
- `/cardart`'s `--check` / `--strict`.
- **The C# compiles against real Unity references** (`Tools/Build/unity_refcompile`): the player
  config reports no errors in project code. The editor config reports none in files this branch
  changed, gating the test and the end-conditions window. (It reports three pre-existing errors in
  upstream UI files, an `OnValidate` override the editor references do not declare.) The negative
  control works: a misspelled member injected into `SerpentMarksmanAI` failed the player compile in
  its own method body.

Not proved:
- That it plays, or that it runs at a playable frame rate. **The 2 FPS report has not been
  re-measured.** Config 4 is the measured-good rung, and the hunter no longer runs forest queries,
  but the number that matters is a capture from this mode.
- That the AI's aim settles on the Serpent's turn rate.
- That 8 is the right length.
- That `SerpentChargeScoringRuleTests` passes: it compiles, but EditMode tests need the editor.

`/verify-unity` was not available in this session (no editor, no `unity` binary), so the change is
filed in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Known limitations

- **The target is reasoned, not measured.** It assumes a pilot connects with about one round in
  three. Play-test it.
- **An AI cloaks at most once per reload**, and only in a reload of 5 s or more, because it must
  lower its scope to do it. At rest that is every other shot or so (the cloak itself runs 15 s).
- **Charge-crystal collection is per-peer** (see above). A client can see a crystal still standing
  that a remote pilot has already collected on its own machine, until the next wave replaces it.
- **Rampage's creation queue polls every pending prism every frame** while the forest plants in.
  It is inherited, not added, and it is the leading suspect for the opening seconds' cost. It is
  recorded as a follow-up, not fixed here.
