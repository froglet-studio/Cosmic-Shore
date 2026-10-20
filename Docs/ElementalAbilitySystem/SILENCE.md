# Silence: taking a pilot's elements also takes their teeth for a while

**Decided by Garrett, 2026-10-20.** Fleet-wide. Code: `VesselSilence`
(`Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/VesselSilence.cs`), tuned by
`Assets/Resources/VesselSilenceConfig.asset` (`VesselSilenceConfigSO`).

> "Right now [the Charge element] is tied to an ability [that] strictly does one of two things to other
> vessels: either steals or strips elements from them. This is a good deterrent, but it is not
> protection. Reducing their elements does not stop them from stripping/stealing your elements or
> destroying the health prisms of your ark etc. Let's have all charge based abilities that strip/steal
> elements also silence charge and space attacks for a duration proportional to the elements
> stripped/stolen. This will temporarily prevent retaliation and destruction. Mass and time abilities
> should remain untouched, so players remain fully maneuverable and can still cultivate new mass."
>
> "Danger prisms are never silenced but the ability to produce them might be. A Thresher should always
> be able to use both triggers to control their flail but the flail should not affect players or
> opposing prisms while silenced. We want vessels to still use their abilities, but those abilities
> can not harm vessels, fauna, or prisms while silenced."
>
> "So a Sparrow can still shoot prisms but those prism projectiles can't destroy prisms, etc."

## 1. The rule

**A pilot who loses petals to a strip or a steal is SILENCED for a time proportional to the petals they
lost. While silenced, every ability still works, but nothing it does can harm another pilot's vessel,
fauna or prisms, and nothing it lays comes out dangerous.**

| Silence takes away | Silence leaves alone |
|---|---|
| taking petals from a vessel (strip, steal), and scoring the hit | every control and every ability: they still fire, swing, lay and fly |
| destroying, consuming, stealing, shrinking or un-shielding any prism that is not the pilot's own domain's (rival trails, neutral flora, the health prisms of an ark) | flight: speed, boost, turning, drifting, dashing (the Time element) |
| killing or jousting a lifeform (flora or fauna) | laying your own trail and building your own mass (the Mass element's cultivation): your prisms are laid, grown and shielded as normal |
| making a prism dangerous: a prism laid or turned dangerous by a silenced pilot comes out plain | **danger prisms already standing.** A danger prism is the arena, not a weapon (`Docs/ELEMENTAL_ECONOMY.md` §9): it still burns whoever flies into it, whoever laid it |
| a Squirrel's joust point | collecting crystals: a silenced pilot can still win their petals back |

So the Sparrow still shoots, and its turret-stance prisms still fly and stay where they land as its
own mass, but a round that reaches a rival prism deflects off it. The Thresher still works its flail
with both triggers, but the ball and the chain deflect off rival prisms and take nothing from pilots.

**How the original wording maps to this.** The first sentence asked to silence "Charge and Space
attacks" and leave Mass and Time "untouched, so players remain fully maneuverable and can still
cultivate new mass". The two follow-ups made it concrete: no ability is switched off, and the harm is
what is removed, including the harm of a Mass ability (the Sparrow's prism rounds, the Thresher's Mass
Mode steal). What stays untouched about Mass and Time is the part the first sentence named:
maneuvering and cultivating new mass.

## 2. How long

`seconds = petals lost × secondsPerPetal`, added to whatever silence is left, capped at `maxSeconds`.

| Knob (`VesselSilenceConfigSO`) | Shipped | Meaning |
|---|---|---|
| `secondsPerPetal` | **1** | one petal = one element level = one second |
| `maxSeconds` | **8** | the ceiling on one pilot's silence, however many hits land. 0 switches silence off |
| `silencingSources` | Explosion, VesselContact, Other | which take classes silence (`ElementalDebuffSources`). A danger prism BURNS, so it never silences. The Butterfly's wormhole toll is left out: it is a price the rival chose by threading the mouth |
| `suppressDangerProduction` | on | a silenced pilot lays no new danger |

These are **starting values**. They are to be set by the plan in §6, not by feel.

Worked numbers at the shipped values: a Thresher hit at Charge 0 steals 1 petal on each of four
elements, so 4 s; a Sparrow rocket's direct hit (30 points = 3 petals per element) is 8 s (capped);
ten Sparrow bullets (0.1 petal each per element) settle one petal per element, so 4 s.

**Silence is self-limiting.** It only follows petals that truly came loose: the victim's own
`ResourceSystem.AccrueElementalLoss` clamps every take to what they hold. A pilot stripped to level 0
loses nothing more, so they are silenced no further, and their silence runs out. A warded pilot loses
nothing and is never silenced. A silenced pilot cannot strip or steal, so they cannot silence anybody
back. A turn reset (`VesselStatus.ResetForPlay`) lifts it.

## 3. Where it is enforced

Not in the abilities. Every way a pilot harms something already funnels through a handful of methods,
so the gate sits there once, and an ability added later is covered without knowing silence exists.

| What is protected | The gate | File |
|---|---|---|
| a vessel's petals and the score for hitting it | `CombatHitDrain.TryAdmit` (the one gate every scored hit passes) refuses a silenced attacker | `Helpers/CombatHitDrain.cs` |
| ...and any take that skips the reporter | `ElementalTransfer.Apply` / `ApplyAll` / `ApplyAuthoritative` take nothing for a silenced attacker (a burn is never refused) | `Helpers/ElementalTransfer.cs` |
| a prism: destroy, cut, consume, horizon-vanish, steal | `Prism.Damage` (and `Slice` / `DamageByCollision`, which route through it), `Consume`, `Vanish`, `Steal` ask `Prism.DeflectsSilenced` first. A refused hit plays the deflection wobble the super-shield already uses, so it reads as BLOCKED, not as a miss | `Controller/Vessel/Prism.cs` |
| a prism's shield, stripped directly before a kill | the eight sites that call `DeactivateShields` on a rival's prism on a pilot's behalf ask `DeflectsSilenced` first: the Rhino sword, the Serpent's rifle and crack round, the Thresher's Breaker, the Shrike, the Angler's squad, the Wrasse's kite, the Astro League ball | the executors and effects named |
| a prism shrunk or turned dangerous by the Butterfly's dust | `SkimmerScaleDustPrismEffectSO`: Blight deflects, the danger roll grows instead | `Skimmer Prism Effects/` |
| a lifeform | `LifeForm.Jousted` (flora) and `Fauna.Predated` (every creature death a pilot causes: the joust, the Stoat's ripple and black hole, the Wrasse's kite) | `Environment/FloraAndFauna/` |
| a joust point | `VesselExplosionBySkimmerEffectSO.Execute` | `Vessel Skimmer Effects/` |
| new danger | `Prism.Initialize` drops the danger request of a prism laid by a silenced pilot; `Prism.MakeDangerous` refuses to turn a plain prism dangerous for one (a prism already dangerous stays so) | `Controller/Vessel/Prism.cs` |

A silenced pilot can still touch **their own domain's** mass: own prisms are not protected, so a
Thresher's red ball still breaks its own trail and an explosion still shields its own domain's mass.
`BuilderPrismWorld.GiveBack` returns a prism to the pilot a builder colony took it from through
`Prism.ReturnTo`, the one ownership change silence does not refuse, because it is restitution.

## 4. Who decides, and how every peer agrees

- **Decided on the victim's owner**, where the petals settle: `ElementalTransfer.Apply` offline,
  `ElementalTransfer.SettleTake` on a networked victim's owner. So the window is exactly as long as
  the petals that really came loose.
- **Published to every peer**: prism damage is simulated on every peer, so every peer has to refuse the
  same hits. The owner sends the REMAINING seconds through `NetworkVesselImpactor.PublishSilence`
  (`ISilenceRelay`, a ServerRpc then a ClientRpc), and each peer counts down on its own clock. One hop
  of latency is noise against a window of seconds.
- **Keyed by player name**, the identity every harm site already carries (`Prism.Damage` knows only
  the attacker's name and domain; scoring is keyed the same way).
- **Wire change:** `NetworkConfig.ProtocolVersion` 9 → 10 (`Docs/claude/MULTIPLAYER_AND_SOCIAL.md`).

## 5. What the player sees, and what is not covered yet

- **The pilot who is silenced** feels it land and sees it hold (Garrett, 2026-10-20: "a very subtle
  camera shake on the onset and ... a subtle danger colored vignette that fits the game's aesthetics to
  signal the silenced state on all vessels"). `SilenceScreenFeedback` (`Controller/FX/`) gives the
  local human pilot, on every hull:
  - a **very subtle onset shake** (`CustomCameraController.Shake`, 0.18 for 0.3 s; the Serpent's shot
    is 0.6, the Thresher's lightest crack 0.35). Only at the onset, not when a live window is extended;
  - a **subtle danger-coloured vignette** for the whole window: the gameplay post-processing profile's
    own Vignette override, driven through `PostProcessingManager.SetSilenceVignette` (the speed
    tunnel's home/offset shape, so the profile is exactly restored after). Its colour is the active
    palette's DANGER signal colour (`SO_ColorSet.GetDangerSignalColor`, the rim every danger prism
    wears) darkened toward black (`vignetteColorStrength` 0.75), at intensity 0.26, smoothness 0.55;
    it rises in 0.2 s and drains over 0.6 s when the window runs out.

  Every number is on `VesselSilenceConfig.asset` under *Feedback*. It plays for the local human pilot
  only: a rival's or an AI's silence changes nothing on your screen. On top of that their hits still
  deflect: a rival prism wobbles instead of breaking, a rival pilot's flowers do not step down, and
  nothing scores. The ability row's cooldown veil was not used, because it says "you cannot use this"
  and every ability still works.
- **Autopilots do not know they are silenced.** An AI keeps attacking through its silence and wastes
  the window. Teaching it to retreat and collect crystals instead is part of the plan in §6.
- **Not covered:** a blast that MATERIALISES a virtual swarm member before it reaches `Prism.Damage`
  still pays the materialise for a hit that is then refused (performance only). A burning prism
  (`PrismBurnManager`) whose owner is silenced burns without the danger look.

## 6. The balancing plan: equal score plateaus

Silence makes the Charge verb do two things (take petals, buy a window), so a hull's strength against
other hulls is no longer readable from its numbers. The values in §2 and every hull's strip and steal
magnitudes are balanced by measurement, in this order:

**The score.** For each pilot *i*, **score(i) = the total petals (elemental crystals) every opposing
pilot loses that pilot *i* caused**: what *i* strips, what *i* steals, and what danger prisms *i*
laid burn off opponents who collide with them. One petal is one point. It is the economy's own unit
(`Docs/ELEMENTAL_ECONOMY.md` §1), and it counts silence's effect without a separate term: a window
in which the opponent cannot answer is worth exactly the petals it lets *i* take.

1. **Each hull fights itself in its Charge arcade game.** Optimize the hull's AI in mirror matches
   (the same hull on every seat) in that hull's Charge game, until its score per minute stops
   rising: the **plateau**. Each round of AI work must beat the last on score; when several rounds
   in a row improve it by less than the noise between seeds, that is the asymptote. A mirror match
   isolates the AI's skill from the hull's strength, because both sides have the same hull.
2. **Each hull fights the other hulls in the Charge arena game, Broadside.** Broadside is the arena
   brawl - a mixed fleet in one rumble, every pilot attacking every other (Garrett, 2026-10-20: "the
   charge arena game ... that is like a dogfight rumble where players attack each other";
   `Assets/_Scripts/Controller/Arcade/BROADSIDE.md`). Bring the hull into Broadside and keep
   optimizing its AI against the other hulls' AIs until its score plateaus there too. This is where
   hull-against-hull strength shows.
3. **Balance the hulls so every plateau is the same.** Adjust each hull's effects (its strip and steal
   magnitudes, its danger-prism burn, and only then the silence knobs) until every hull plateaus at
   the same score in Broadside. Then re-check stage 1: a change that moves a hull's arena
   plateau moves its mirror plateau too.

**Never balance against an AI that has not plateaued.** Before the asymptote, a low score measures the
AI's skill, not the hull's strength, and a balance pass would buff a hull for having a weak pilot.

**The levers, from most specific to most global:**

| Lever | Where | Reaches |
|---|---|---|
| a hull's take per hit | `ThresherConfigSO.vesselHitPetals`, `stealScale` on the overtake assets, `SniperShotAction.vesselStripPerElement`, the per-weapon Debuff/Strike assets (`Tools/Build/author_combat_debuff_magnitudes.py`) | one hull |
| a hit class's price | `CombatHitDrain.PointsFor` (ten points to the petal; Broadside authors the same list) | every hull firing that class |
| the danger burn | `VesselElementalDebuffByDangerPrismEffect` and the petal-burn switch (`Docs/ELEMENTAL_ECONOMY.md` §4.1) | every danger prism |
| silence | `VesselSilenceConfig.asset` | every hull at once: move it last |

**What exists today, and what each step still needs.**

| Hull | Its Charge ability | Its Charge arcade game (today's candidate) |
|---|---|---|
| Grizzly | Dig In | **Grizzly Charge** |
| Sparrow | Skyburst Rockets | Dog Fight (Sparrow-only, scored on combat hits) |
| Squirrel | Crystal Joust | Joust (Squirrel-only, scored on jousts) |
| Dolphin | Echo Sight | The Bends (scored on the crystal blast's debuff) |
| Scarab | Cavitation Blast | Undertow (scored on the cavitation plate's debuff) |
| Rhino, Manta, Butterfly, Urchin, Stoat, Thresher, Serpent, Shrike, Brittle Star, Angler, Wrasse | see `python3 Tools/Build/element_ability_table.py <Hull>` | **not named yet** (Cleave, Bloomrush, Dustup and Hijack are single-hull cards that could be) |

- **Broadside seats seven hulls today** (Manta, Dolphin, Rhino, Urchin, Squirrel, Sparrow, Scarab;
  `ArcadeGameBroadside.asset`). Every other hull - Grizzly, Serpent, Butterfly, Stoat, Thresher,
  Shrike, Brittle Star, Angler, Wrasse - has to join the card (`/arenagame`) before it can reach
  stage 2. Broadside's board scores combat-hit POINTS, which the drain table prices at ten points to
  the petal (`CombatHitDrain.PointsFor`), so it already tracks the plan's score closely; the ledger
  below makes it exact (a hit on a pilot with nothing to lose scores points and takes no petals).
- **The score has no counter yet.** Every take already settles in one place (`ElementalTransfer`, with
  the attacker named), and the danger burn knows the prism and therefore its layer, so a per-attacker
  ledger is a small addition there. It is the first dev task of the plan, because nothing in stages 1
  to 3 can be measured without it.
- **AI work happens on `Ys-bleeding-edge`** (`Docs/AI_SYSTEM/README.md`: run
  `python3 Tools/Build/ai_branch_sync.py` first). The AI has to learn silence both ways: to press the
  attack while an opponent is silenced, and to fly off and collect crystals while it is.

## 7. How this sits with the locked rules

- **PvP is petals only** (`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`, LOCKED 2026-10-10). Silence is a
  consequence of the petal theft, never a separate hit, and it takes no motion and no control from the
  victim. It removes only the HARM their abilities do; they fly and press exactly as before. Garrett,
  who locked that rule, made this change.
- **A vessel may not move an opposing vessel, nor take its controls** (`Docs/ELEMENTAL_ECONOMY.md` §9).
  Silence moves nothing and mutes no input. `check_vessel_on_vessel_motion.py` stays green for it (its
  one standing finding, `VesselImpulseByExplosionEffectSO`, is older than this change).
- **One-to-one between a scored hit and a petal theft.** Kept: `TryAdmit` refuses the silenced
  attacker, so the hit neither scores nor takes.

## 8. Verification

| What | How | Result (2026-10-20) |
|---|---|---|
| The rules, the gates on vessels, the network split | `bash Tools/Build/elemental_transfer_harness/run.sh`, block **T11** (25 checks, real `VesselSilence` + `ElementalTransfer` + `CombatHitDrain` against fake hulls) | ALL PASS. Negative control: removing the `TryAdmit` gate, the transfer gate and the owner's silence call fails 4 checks |
| Runtime C# binds against real Unity | `bash Tools/Build/unity_refcompile/run.sh` | OK: no errors in project code (80 assemblies) |
| Static gates | `check_conditional_compilation`, `check_enum_member_references`, `check_switch_label_collisions`, `check_self_referential_locals`, `check_console_logging`, `check_using_directives`, `check_abstract_member_implementations`, `check_elemental_economy` | all OK |

The harness was already red on bleeding-edge before this change: its stubs had not followed
`CombatHitDrain.TryAdmit` (2026-10-10) or `CellConfigDataSO`'s warp field. Both are fixed here.

**Not run in the editor.** The prism and lifeform gates live on MonoBehaviours the harness cannot bind.
In the editor, with a victim holding petals (`Docs/ELEMENTAL_ECONOMY.md` §8 step 0):

1. Thresher vs Thresher (Thresher Time, or freestyle with an AI): land a ball hit on a pilot holding
   petals. For about 4 s the victim's ball and chain bounce off your prisms with a wobble and take
   nothing from you, while both triggers still work the flail. Then their hits land again.
2. Sparrow (Dog Fight): get stripped by a rocket, then fire both guns and turret-stance prisms at a
   rival trail. Rounds deflect (wobble), turret prisms still land as your own mass, nothing scores.
3. Rhino with danger afterimages, or a Butterfly dusting its own prisms: while silenced, no new
   danger appears; danger prisms already out still burn on contact.
4. Kill a lifeform while silenced (joust, Stoat ripple): it lives. After the window: it dies.
5. MPPM host + client: silence the client's pilot from the host; on BOTH windows the client's shots
   deflect for the same window (within a frame or two), and the client's flowers agree.
6. Silence a human pilot (any hull): one small camera nudge as it lands (no shake again while it is
   extended), a soft danger-red edge on the screen for the window, draining over about half a second
   when it ends. An AI or a rival being silenced shows nothing on your screen. Tune it on
   `VesselSilenceConfig.asset` > *Feedback*.
