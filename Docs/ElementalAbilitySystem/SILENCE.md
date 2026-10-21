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

**Every scored hit on a pilot SILENCES them for a time proportional to what the hit is worth - the
petals it is priced at, whether or not they had petals to lose. While silenced, every ability still works, but nothing it does can harm another pilot's vessel,
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

`seconds = the hit's points / 10 × 4 elements × secondsPerPetal`, added to whatever silence is left,
capped at `maxSeconds`. That is the hit's worth in petals at the fleet price (ten points to the petal,
on each of the four elements; `CombatHitDrain.PointsFor`) - exactly what the same hit takes from a
pilot who holds enough. A missile that upgrades its tier against the same pilot adds only the
difference, as its score does.

**Worth, not petals settled (Garrett, 2026-10-20).** The first cut silenced for the petals that
actually came loose. Every mode's pot starts empty (`Docs/ELEMENTAL_ECONOMY.md` §2.1), so in Garrett's
first Dog Fight run the AI scored well over 20 points and silenced nobody: a fresh pilot holds no
petals, so no hit takes any. Asked to choose between a starting stake in the combat modes, silencing
every scored hit by its worth, or leaving it, Garrett chose **silence every scored hit**.

| Knob (`VesselSilenceConfigSO`) | Shipped | Meaning |
|---|---|---|
| `secondsPerPetal` | **1** | one petal = one element level = one second |
| `maxSeconds` | **8** | the ceiling on one pilot's silence, however many hits land. 0 switches silence off |
| `silencingSources` | Explosion, VesselContact, Other | which hit classes silence (`ElementalDebuffSources`). A danger prism is not a hit (it burns), so it never silences. The Butterfly's wormhole toll scores but is left out: it is a price the rival chose by threading the mouth |
| `suppressDangerProduction` | on | a silenced pilot lays no new danger |

These are **starting values**. They are to be set by the plan in §6, not by feel.

Worked numbers at the shipped values:

| Hit | Points | Silence |
|---|---|---|
| a Sparrow bullet | 1 | 0.4 s (ten bullets: 4 s) |
| a Strike (Squirrel joust, Rhino sword, Thresher ball, Angler, Wrasse kite) | 8 | 3.2 s |
| a missile shockwave | 10 | 4 s |
| a Debuff (Dolphin cone, Scarab plate, Manta bloom, Serpent rifle) | 12 | 4.8 s |
| a missile blast | 20 | 8 s |
| a missile direct hit | 30 | 8 s (capped) |

**What limits it now.** It no longer runs dry with the victim's petals: a pilot at level 0 is still
silenced by every hit. What bounds it is the 8 s ceiling, the per-victim cooldown each reporter
already has (`VesselCombatHitLatch`), and the fact that a silenced pilot cannot score a hit, so cannot
silence anybody back. A warded pilot is not admitted, so is never silenced. A turn reset
(`VesselStatus.ResetForPlay`) lifts it. A pilot who keeps getting hit can be kept silenced for as long
as they stay hittable, and flying away is the answer the game gives them (every Time and Mass ability
still works). This is the first thing to watch in playtest and in the balancing plan (§6).

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

- **Decided where the hit is admitted**: `CombatHitDrain.TryAdmit` -> `VesselSilence.OnHitAdmitted`,
  on the machine that DECIDES the hit (the attacker's owner: a human's own client, the server for an
  AI; every reporter asks `ElementalTransfer.IsDecidedHere` first). So a hit silences once however
  many peers replay it, and silence and score are decided together.
- **Published to every peer** as an EXTENSION: prism damage is simulated on every peer, so every peer
  has to refuse the same hits. The decider sends the seconds it added through the victim hull's
  `NetworkVesselImpactor.PublishSilence` (`ISilenceRelay`; a ServerRpc any machine may call, then a
  ClientRpc that every peer but the sender applies with `VesselSilence.ExtendBy`). An extension rather
  than an absolute time, so two attackers on two machines landing hits inside one window both count.
  One hop of latency is noise against a window of seconds.
- **Keyed by player name**, the identity every harm site already carries (`Prism.Damage` knows only
  the attacker's name and domain; scoring is keyed the same way).
- **Wire change:** `NetworkConfig.ProtocolVersion` 9 → 10 (`Docs/claude/MULTIPLAYER_AND_SOCIAL.md`).

## 5. What the player sees, and what is not covered yet

- **The pilot who is silenced** feels it land and sees it hold (Garrett, 2026-10-20: "a very subtle
  camera shake on the onset and ... a subtle danger colored vignette that fits the game's aesthetics to
  signal the silenced state on all vessels"). `SilenceScreenFeedback` (`Controller/FX/`) gives the
  local human pilot, on every hull:
  - an **onset shake** (`CustomCameraController.Shake`, 0.45 for 0.35 s; between the Thresher's
    lightest crack, 0.35, and the Serpent's shot, 0.6) when a silence begins, and a lighter **per-hit
    shake** (0.2 for 0.15 s) on every further hit that extends it, so each hit is felt. The first cut
    shook once at 0.18 and never again: Garrett's second Dog Fight run (2026-10-21) "felt nothing from
    the enemy Sparrow hits", because the AI's rounds kept ONE window open for the whole fight and
    0.18 world units at chase distance is under a degree;
  - a **3D perimeter** for the whole window (Garrett, 2026-10-21: "instead of a 2d effect we just
    have a client side perimeter of 3d craziness on the perimeter where vignettes typically use 2d
    effects"; and then "more overlap, show only half the faceted object that is poking into the screen
    ... it doesn't read as crystals coming in from the edge it read reality trying to pull you somewhere
    you don't want to be").
    - **The shards:** 110 faceted crystal shards (6 to 10 sides, three rings, so 36 to 60 facets each)
      overlap in depth layers on the screen's edge. Each shard's centre sits ON the edge, so half of it is
      beyond the screen.
    - **The facets:** each one shows the frame behind it refracted, twisted, colour-split and smeared. Facets
      vary in how solid they are, and each shard fades toward its inward tip, so none ends in a point you
      could name.
    - **The pull:** a point wanders slowly around the screen (`perimeterPullDrift`). Near the edges, the
      frame and every facet's picture are dragged toward it in slow waves. The shards on its side grow
      longer and sink deeper: a maw that moves. The middle of the screen stays still.
    - **Hits:** each hit sinks the shards inward, tears their picture into sliding bands and sends a ripple
      through the edges (the hit flare, +0.35 decaying over 0.35 s).
    - **Timing:** the shards grow in as the silence lands, each a little after the last, and fade out
      over 0.6 s when it ends.
    - **The code:** `SilencePerimeter` (pure, deterministic) decides where the shards sit, how each is cut and
      where the pull is. `SilencePerimeterPass` draws on the LOCAL pilot's camera only, after URP's
      transparents: it copies the frame, warps it, then draws every shard far to near in ONE dynamic mesh
      (one draw call) from that copy (`SilencePerimeter.shader`, injected from script like the black hole
      lens, so no asset changes and no other camera or machine pays). The shards are drawn over everything:
      they belong to the screen.
  - **Depth follows the seconds** (Garrett, 2026-10-21: "the pull plus the 2d edge is very strong
    effect. lets try that on relatively thin edge around screen with high resolution. the more seconds
    of silence the deeper it goes in and bleeds back out as the seconds drain").
    - **How depth is measured:** depth = seconds of silence left / `depthFullSeconds` (8). It follows
      within `depthFollowSeconds` (0.25 s), so a hit's seconds push it in at once and the drain pulls it
      back out as it happens.
    - **With little silence left:** a thin, high-resolution band. 160 small shards (`perimeterSize` 0.1,
      sink 0.03) over a thin danger **edge**: a frame hugging the screen's border (faceted, 4 sides,
      starting at 0.93 with a 0.07 feather and a 0.015 rim line).
    - **At full depth:** the shards grow to `perimeterDeepSize` 0.38 and sink to 0.22, and the edge moves in
      to `vignetteDeepInner` 0.6.
    - **The edge sits under the shards:** with the perimeter on, the edge (strength 0.6) is drawn INTO the
      frame by the perimeter's own pass before the copy (`SilencePerimeter.shader` pass 2, the mask worked
      out per pixel, so it is sharp at any thickness and moves continuously). The pull therefore drags it
      and the facets refract it, as in the lab.
    - **The URP Vignette** is unused by default (`vignetteDarken` 0). The UI overlay edge is used only when
      the perimeter is off.
    - **The first cut of all was invisible.** It drove URP's Vignette in the danger colour at 0.26, and URP
      MULTIPLIES the frame by that colour, so on the arena's near-black sky red can only darken. Garrett
      saw nothing in two Dog Fight runs (2026-10-21).

  The look is explored in the **Silence Vignette Lab**:
  https://claude.ai/artifact/Hequw1VLdwXi2ztrDeuXQC (`Docs/Studios/SilenceVignetteLab.html`, write-up
  `Docs/Studios/SILENCE_VIGNETTE_LAB.md`). It draws the perimeter in WebGL the way the game does (its
  layout, geometry and pull are the game's code, under a parity gate). Round 4 (shipped) is the thin pull + edge
  that deepens with the seconds; rounds 1 to 3 are kept as looks to compare.

  Every number is on `VesselSilenceConfig.asset` under *Feedback*. It plays for the local human pilot
  only: a rival's or an AI's silence changes nothing on your screen. On top of that their hits still
  deflect: a rival prism wobbles instead of breaking, a rival pilot's flowers do not step down, and
  nothing scores. The ability row's cooldown veil was not used, because it says "you cannot use this"
  and every ability still works.
- **Fresh pilots are silenced too** (§2): Garrett's first Dog Fight run showed no vignette because
  the first cut silenced only for petals that came loose, and nobody in Dog Fight holds any at the
  start. Two console commands (`DiagnosticsHUD`, editor and dev builds) tell a silence that never
  began from one that did not draw:
  - `silence`: per pilot, the scored hits this machine decided against them, the windows that began,
    the takes settled and how many **took nothing**, petals lost, who is silenced now, and whether
    post-processing is there;
  - `silence test [seconds]`: silences your own pilot (default 4 s) so the shake and the vignette can
    be judged with no fight at all.
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
| The rules, the gates on vessels, the network split | `bash Tools/Build/elemental_transfer_harness/run.sh`, block **T11** (28 checks, real `VesselSilence` + `CombatHitDrain` + `VesselCombatHitLatch` + `ElementalTransfer` against fake hulls, including the Dog Fight case: a fresh pilot hit by a Strike is silenced 3.2 s) | ALL PASS. Negative controls: removing the silence call in `TryAdmit` fails 11 checks; removing the missile-tier netting fails 1 |
| Runtime C# binds against real Unity | `bash Tools/Build/unity_refcompile/run.sh` | OK: no errors in project code (80 assemblies) |
| The real game code, end to end, in Dog Fight (2026-10-21) | Amoebius headless (`Port/`, the game's own `Assets/_Scripts` compiled live): Menu_Main -> `arcade DogFight`, 2 seats (pilot + AI), `arcade ready`, the pilot flies nothing; then `console silence` and `get "CM PlayerCam" CustomCameraController _shakeTimeRemaining / _shakeIntensity` sampled every 3 frames | The AI's hits on a pilot holding NO petals are decided, admitted and silence them (29-40 hits, every take empty, one window held at the 8 s ceiling); the vignette driver reaches weight 1.00 with post-processing present; the camera receives the 0.45 onset shake and then 0.2 per-hit shakes. Amoebius draws no URP vignette, so the vignette's LOOK is still an editor check |
| The perimeter's layout and shards are the lab's, and its shader compiles | `bash Tools/Build/silence_vignette_parity/run.sh` (now also the `<perimeter-twin>` against `SilencePerimeter.cs`); `SilencePerimeterTests` (edit mode: hidden beyond the edge with no silence, in on the edge and out of the middle at full silence, a hit jolts inward, every shard a closed outward-wound volume and not a flat sheet, the shader's pass order); `python3 Tools/Shaders/verify_silence_perimeter.py` (glslang, HLSL mode, all three passes; the edge pass's mask run as C++ against the lab's twin; negative controls) | PASS (2026-10-21): 33 cases and 10366 values, worst difference 3e-6, 42 asset fields equal, six planted controls caught; both passes compile. Not run: the edit-mode tests (no Unity here) and the real URP library compile (the editor does it) |
| The vignette's shape is the lab's shape, and the lab's numbers are the asset's | `bash Tools/Build/silence_vignette_parity/run.sh`; `SilenceVignetteMaskTests` (edit mode); `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/SilenceVignetteLab.html` | PASS (2026-10-21): 5214 values, worst difference 4e-7, 24 asset fields equal, four planted controls caught; the lab scores the URP cut at 2.8 (about the just-noticeable difference) and the shipped overlay at 45 on open space |
| Static gates | `check_conditional_compilation`, `check_enum_member_references`, `check_switch_label_collisions`, `check_self_referential_locals`, `check_console_logging`, `check_using_directives`, `check_abstract_member_implementations`, `check_elemental_economy` | all OK |

The harness was already red on bleeding-edge before this change: its stubs had not followed
`CombatHitDrain.TryAdmit` (2026-10-10) or `CellConfigDataSO`'s warp field. Both are fixed here.

**Not run in the editor.** The prism and lifeform gates live on MonoBehaviours the harness cannot bind.
In the editor (no petals needed - any scored hit silences):

1. Thresher vs Thresher (Thresher Time, or freestyle with an AI): land a ball hit on a pilot. For about
   3 s the victim's ball and chain bounce off your prisms with a wobble and take
   nothing from you, while both triggers still work the flail. Then their hits land again.
2. Sparrow (Dog Fight): take a rocket hit (4-8 s of silence), then fire both guns and turret-stance prisms at a
   rival trail. Rounds deflect (wobble), turret prisms still land as your own mass, nothing scores.
3. Rhino with danger afterimages, or a Butterfly dusting its own prisms: while silenced, no new
   danger appears; danger prisms already out still burn on contact.
4. Kill a lifeform while silenced (joust, Stoat ripple): it lives. After the window: it dies.
5. MPPM host + client: silence the client's pilot from the host; on BOTH windows the client's shots
   deflect for the same window (within a frame or two), and the client's flowers agree.
6. Silence a human pilot (any hull; `silence test` in the console does it without a fight): a camera
   shake as it lands, a lighter one on every further hit while it lasts, a thin band of half-buried shards over a thin danger frame on the
   screen's border, all of it dragged toward a point that wanders around the screen (a maw on that side).
   `silence test 8` goes deep: the band grows in toward the middle, then bleeds back out as the seconds
   drain; hits sink and tear it. It fades out over about half a second when it ends. If they are missing, `silence` in the console says whether
   the perimeter is drawing, on which camera, or that its shader is missing. An AI or a rival being silenced shows nothing on your screen. Tune it on
   `VesselSilenceConfig.asset` > *Feedback*. In Dog Fight, the first AI hit on you should start it;
   `silence` in the console then shows the hits decided against you and the windows that began.
