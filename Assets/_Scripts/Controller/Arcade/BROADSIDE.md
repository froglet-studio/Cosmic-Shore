# Broadside — the arena brawl

`GameModes.Broadside = 57` · `MinigameBroadside.unity` · `BroadsideController`

## Overview

Regatta asked what every hull does with a racing line. **Broadside asks what every hull does with
a rival in front of it.** Seven hulls loose in Dog Fight's Boneyard, each fighting with the weapon
it actually has — a Sparrow's guns and rockets, an Urchin's chain spikes, a Rhino's energised
sword, a Squirrel's joust, a Dolphin's cone, a Scarab's plate, a Manta's bloom — and the first
**DOMAIN** to the point target wins on `ScoringMetric.CombatPoints`.

**A hit is priced by its VERB, never by its hull.** That is the whole design, and it is what lets
a mixed fleet share one score:

| verb | who lands it | price | why |
|---|---|---|---|
| **Round** (`Bullet`) | Sparrow tracer, Urchin spike | **1** | landed from range, and it repeats |
| **Strike** (`Strike`, new) | Rhino sword, Squirrel joust | **8** | you had to fly into them, inside their reach |
| **Debuff** (`Debuff`) | Dolphin cone, Scarab plate, Manta bloom | **12** | the only verb that leaves its victim measurably worse |
| **Rocket** (three tiers) | Sparrow skyburst | **10 / 20 / 30** | Dog Fight's prices — it is the same bay |

No hull is named anywhere in the scoring path, so the card can gain a hull without the rule
learning anything.

## What it had to fix before a brawl was possible

**Measured on the shipped containers, only four of the eight playable hulls could land a scoreable
hit** (Sparrow, Manta, Dolphin, Scarab). Everything below is counted platform-wide and **paid only
here** — the split Dog Fight established.

1. **`CombatHitClass.Strike`** — a contact hit, the fourth verb, unranked like `Bullet` and
   `Debuff`. It is a **verb, not a hull**: the Rhino's sword and the Squirrel's joust share it
   because both answer *"I flew into them."* Adding it is provably inert everywhere else — the
   base `PointsForCombatHit` returns 0, Dog Fight's switch is exhaustive with `_ => 0`, and
   Bends/Undertow's `? a : b` both default to `gunneryPoints`, authored 0.
2. **`VesselCombatHitBySkimmerEffectSO`** — the skimmer sibling of the projectile and explosion
   reporters. It had to solve **authority** differently: a projectile is a pooled local object and
   may raise unconditionally, but a skimmer overlap is observed on **every peer**, so an
   unconditional raise double-credits (the server records its own view *and* the shooter's client
   forwards the same contact, and the latch is per-machine). It gates on the machine that owns the
   striker — `IsNetworkOwner`, not `IsLocalUser`, or every AI goes un-scored.
3. **The Urchin's spike container had `projectileShipEffects: []`** — a spike passed straight
   through a rival pilot and did nothing at all. The same empty-container gap Dog Fight found in
   the skyburst and The Bends found in the Dolphin's cone, a third time. It gains the report **and
   a spin**, because a score with no felt cause on screen is the complaint every unwired weapon in
   this family has produced.
4. **`CombatHitScoring`'s raw tally lost its else-arm**, which had been filing a Rhino's **sword**
   as a *bullet* — a raw count, on a hull with no gun, on the scoreboard breakdown. A new
   `IRoundStats.StrikeHitsLanded` carries it. *An else-arm in a tally is the same trap the enum's
   own doc already records for pricing.*

**The Serpent is deliberately not on the card.** It has no anti-vessel verb authored at all (0/4
abilities). Listing it would seat a pilot who cannot score; giving it one is a `/vessel` job.

## The fleet table

Measured off the shipped prefabs and containers, 2026-09-16.

| hull | verb | wired before? | AI can fight? |
|---|---|---|---|
| Sparrow | guns + skyburst | ✅ 4 classes | yes — fires on its own timer once someone is ahead |
| Manta | Sting bomb → bloom | ✅ CrystalBlast | yes — planting is grazing |
| Dolphin | crystal cone | ✅ CrystalBlast | **opportunistically** — the cone needs a crystal |
| Scarab | cavitation plate | ✅ Cavitation | yes — `TryAutopilotDash` |
| Squirrel | joust | ❌ paid `Jousts` only | yes — the joust is landed by arriving |
| Rhino | energised sword | ❌ damaged + spun, reported nothing | yes — same |
| Urchin | chain spikes | ❌ **container empty** | yes — a replicated trigger tap |
| ~~Serpent~~ | — | — | **not on the card** |

## Balance

`Tools/Build/broadside_balance.py` is the offline authority; the generator **imports** it rather
than transcribing it, and asserts its claims.

**The insight the model is built on:** a weapon's fire rate is almost never what bounds its
scoring. `VesselCombatHitLatch` admits one hit per (shooter, victim, class) per that effect's own
`sameVictimCooldownSeconds`, so against one rival a hull's ceiling is
`points_per_hit / latch_window`. The Sparrow's 90-rounds-per-second full-auto is capped by a
**0.05 s** window long before its trigger is.

That makes **the windows the real dial**, and they are authored **per asset**, which is the mode's
finest lever — two hulls sharing the `Strike` class are still separated by how often the class
admits, and the separation is grounded in the mechanism:

- **Sword 1.4 s** — a swung blade is in contact *continuously* while the Rhino is alongside, so
  without the longer window it would score every second of a pass it never had to re-earn.
- **Joust 1.0 s** — a joust is *discrete*: it requires a fresh overtake at a closing speed the
  Squirrel has to win, so it re-earns its window and is allowed a shorter one. Its asset alone
  sets `requireFasterThanVictim`, which is the mode honouring what a joust already means.
- **Spike 0.12 s** — a volley is ~10 projectiles inside ~0.1 s; the Sparrow's 0.05 s would let one
  volley score ten times.

**Result: 1.91× spread at rest → 1.33× tuned**, every hull reaching the target in **3.6–4.8
minutes**. That is far tighter than Regatta's 6.0× residual, and the reason generalises:

> **A brawl is more balanceable than a race, because the mode owns the windows.** A race's rate is
> bounded by vessel speed, which a card cannot touch; a brawl's is bounded by latch windows the
> mode itself authors.

**Stated honestly — what the model cannot do.** The per-hull **connect fraction** (how much of an
engagement a hull spends with its weapon on a rival) is an **estimate, not a measurement**. It is
the one term no static read can supply, and it is where the model is weakest; each value carries
its reasoning in the source so a playtest can correct it by argument. **Time reaches nothing on
the Urchin, the Rhino or the Squirrel**, so those three get no handicap row — the residual is
structural, reported rather than faked.

## AI

**Every AI hunts through the platform; only the trigger is per hull.** Broadside joins Joust and
Dog Fight in `ServerPlayerVesselInitializerWithAI`'s seek-players set, so every AI chases a chosen
opponent whatever hull it drew — which is right here, because most of this roster's weapons are
landed by **arriving**. Two hulls need a trigger pulled, because their weapon is a stick gesture
inert under autopilot (the Tollway rule — an all-AI domain that cannot play is a defect):

- **Scarab** — `ScarabJukeController.TryAutopilotDash`.
- **Urchin** — a short **tap** through `PerformShipControllerActionsReplicated`. It must be the
  replicated path: an AI runs server-only, so a local press would fire spikes on one machine and
  show them to nobody. A tap is the aimed shotgun; a long hold would charge the omnidirectional
  burst instead.

The scene's four AI templates are `vesselClass: 0` (Random), so `PickAIVesselType` draws each bot's
hull **from the card** — a bot grid is a mixed grid too.

## Arena, reused not forked

Dog Fight's **Boneyard**, referenced verbatim (the cell is per-arena, not per-mode — Salvo does the
same). It happens to feed every hull's economy, which is why a mixed fleet can live in it: a
Squirrel skims the wreckage for boost, an Urchin grinds it, a Dolphin skims it for seed energy, a
Sparrow's rounds pay ammo off it, a Rhino's sword has mass to cut, a Scarab forges balls from its
crystals. Its intensity ladder and its `noNucleusSpawnRadius` come with it.

## Assets

Everything is authored by `Tools/Build/author_broadside_assets.py` (`--check`). Never hand-edit a
generated asset; re-run the generator.

**Gotcha:** the generator `import`s the balance model, so a fast edit→run cycle can read a stale
`Tools/Build/__pycache__` copy. `rm -rf Tools/Build/__pycache__` if a mutation seems not to take.

## Verification status

**Authored headless. Nothing has been run in the Unity editor.** All eight offline gates pass
(`author_broadside_assets.py --check`, switch-label collisions, enum references, conditional
compilation, self-referential locals, console logging, using directives, gamelist scenes,
`author_preview_spawns.py --check`), and the generator's asserts were negative-controlled — the
price-ordering, spread and time-to-target gates were each watched to fail and restored.

What stays editor-only: whether each hull's weapon actually reaches a rival at the ranges the model
assumed, whether the three newly-wired containers fire in play, whether the AI's spike tap reads as
a volley, and everything about feel.

## Known limitations

- **Connect fractions are estimates** (above). The first playtest should correct them.
- **The Dolphin is the weakest seat** at 4.8 min to target: its cone is gated on a crystal run
  rather than on the fight. Its Time row already buys it the maximum the platform allows.
- **No Serpent.**
- The **milestone toasts and the objective arrow** are shared with Dog Fight's; the arrow
  deliberately does not try to name a weapon, because seven hulls answer *"what do I do when I get
  there"* differently.
