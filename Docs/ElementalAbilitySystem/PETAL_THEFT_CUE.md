# The petal theft cue: a PvP hit you can follow with the sound off

Footage critique, 2026-11-13, weak link 3 ("PvP does not read on screen", approved by Garrett the same day): a scored
hit drew a small star near the victim, its petals left as tiny lime free-for-all crystals, and nothing on screen tied
the victim to the pilot who scored. A muted replay could not say who hit whom. PvP is petals only (LOCKED,
`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`), so every scored hit is a theft, and the theft has to be visible.

**Done when** someone watching a muted replay can say who hit whom.

## What a hit now looks like, in order

| Beat | What you see | Colour, from the worn colour set |
|---|---|---|
| Flash | a real point light on the victim's hull: the hull and the mass round it light up | the set's danger signal colour |
| Tear-off | faceted shards tear off the victim's skin, lean away from the attacker and hang for 0.3 s, tumbling | the victim's domain ship material (the facing two-tone of its own domain band) |
| Stream | the shards turn and stream to the attacker, stretching with speed, steering onto it wherever it flies (0.55 to 1.4 s) | as above: pieces of the victim, in the victim's colour |
| Pulse | each shard sinks into the attacker's hull; the attacker's own light pulses as they land | the attacker's domain signal colour |
| Silence | while the victim is silenced, shards orbit its hull, fewer as the silence drains | the victim domain's danger block |

The count follows what the hit is worth (ten points to the petal on four elements, the price silence uses): a bullet
(0.4 petal) is one shard, so a Sparrow's stream of rounds draws a stream of petals; a missile shockwave (4 petals) is
six; a direct missile (12) is the cap, 18. Every size is a share of the hull's measured radius, every time is game
time, so the cue holds in a 0.25x shot. Every number is on `Resources/PetalTheftCueConfig.asset`
(`PetalTheftCueConfigSO`), with two empty FMOD slots (`tearOffEvent`, `landEvent`).

## What it is, and what it is not

- **A picture of the score, not a rule.** Ranged hits still EJECT the petals as free-for-all crystals and contact
  hits still STEAL them (`ElementalTransfer`). The shards stand for what the hit was worth, which is what the attacker
  gained; a fresh pilot holding no petals is still visibly hit. Nothing reads the cue to decide anything.
- **One event with the score.** `CombatHitDrain.TryAdmit` and `TryAdmitPetals` announce every hit they admit through
  `CombatHitCue.OnAdmitted`. A warded victim, a silenced attacker and a latched repeat score nothing and draw nothing.
  Proved by `Tools/Build/elemental_transfer_harness` T12, negative-controlled (removing the call fails 7 checks).
- **On every screen.** The decider (the attacker's owner) draws its own cue and sends it through the victim hull's
  `ICombatHitCueRelay` (`NetworkVesselImpactor.PublishHitCue`, an unreliable RPC pair, protocol version 31) to every
  other peer, the same shape as silence's publish.
- **In replays with nothing recorded.** A replay plays the match in its real scene and re-runs every recorded press,
  so its rounds land and its hits are admitted again (`Docs/REPLAYS.md` §6.3).
- **In the world, in 3D** (both LOCKED look rules): the shards are flat-shaded hexagonal bipyramids
  (`PetalTheftShapes.BuildShardMesh`, `PetalTheftShapesTests.Shard_IsAFacetedVolume_NotAFlatSheet` with a flatten
  control), the flash is a light on the hull, nothing is drawn on the screen.

## Code

| File | Job |
|---|---|
| `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/CombatHitCue.cs` | the fact: `Landed(victim, attacker, petals)` on every machine, and `ICombatHitCueRelay` |
| `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/CombatHitDrain.cs` | raises it for every admitted hit |
| `Assets/_Scripts/Controller/ImpactEffects/Impactors/NetworkVesselImpactor.cs` | the publish to other peers |
| `Assets/_Scripts/Controller/FX/PetalTheftFeedback.cs` | draws it: self-installing, pooled shards, one light per hull |
| `Assets/_Scripts/Controller/FX/PetalTheftShapes.cs` | the pure half: counts, sizes, travel, steering, the mesh |
| `Assets/_Scripts/ScriptableObjects/PetalTheftCueConfigSO.cs`, `Assets/Resources/PetalTheftCueConfig.asset` | every number |

## Left for others

- The small 2D hit stars (`critique/stills/01`, `02`) belong to the explosion pass (weak link 2), which replaces them;
  this cue does not touch them.
- The ejected crystals still wear the lime collect-me signal. That is the economy's look, not this cue's.

## Verification

Runtime compile: `Tools/Build/unity_refcompile`. The cue's wiring: the elemental transfer harness, T12. The look:
before and after clips filmed with the trailer director's replay command on Gbox (in the PR). Not yet opened in the
Unity editor by a person; `Docs/UNITY_VERIFICATION_CHECKLIST.md` carries the hand test.
