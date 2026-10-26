# Pilots and Artifacts (DRAFT rev 2, 2026-10-26)

Design draft from Shombith's requests of 2026-10-26, built from the Notion pilot table. Not approved, nothing implemented.
Page with portraits and the Hangar loadout mockup: https://claude.ai/artifact/KQgwvDGzKwPnYum5xW6NX7

Note: "artifact" here is the in-game item. `Docs/Artifacts/` is the library of claude.ai pages, a different thing.

## The split

- **A pilot changes HOW a vessel plays** (qualitative). Two pilots per vessel, one in the seat per match. It rewrites ONE ability: a new move, a new shape, a new cause and effect, never a bare number. It is **not tied to elements** (works at any petal level; element scaling still applies underneath), must not repeat a level-5 upgrade the same vessel already has, obeys PvP is petals only (`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`), and ships with an AI behaviour line.
- **An artifact changes HOW MUCH** (quantitative). One number per artifact (count, size, duration, cooldown). Three slots per vessel (proposal); two hull artifacts per vessel plus five fleet artifacts. Ranks I-III, no self-stacking. It moves the BASE value; elements scale on top in the match.
- Test: if it can be written as "+X" or "xY", it is an artifact.

This replaces the old model in `Assets/_SO_Assets/Captains/Elemental/` (four captains per vessel, one per element, each a "most X in class" stat line). The Hangar Pilots tab header (`Tools/Build/author_menu_hangar.py`, "Each pilot leans the vessel toward one element.") should change once this is decided.

## Art on hand (measured 2026-10-26)

- **24 named portraits** in `Assets/_Graphics/Pilots/` (`Portrait_<Name>_Placeholder.png`, 220x280 finished full-body paintings despite the file name; Feist's file is spelled `Fiest`), all wired to `SO_Captain` assets. No other branch adds pilot art (Scurious, PilotPortrait_Placeholder08 and RawPortrait_Mason_Large were deleted in 2024).
- **21 of the 26 Notion pilots have art.** Without art: Heist, Nul, Timebomb, Ironclad, Trapper. With art but not in Notion: Jugger, Tender, Damsel. Feist's portrait was Timebomb's before the 2024 rename ("rename timebomb to fiest").
- **8 unnamed spare portraits** (`PilotPortrait_01`, `PilotPortrait_Placeholder01-07`); four sit on the Grizzly, Termite and Urchin placeholder captains.
- **This plan: 40 pilots, 24 with art, 16 without** (Heist, Trapper, Snap, Nul, Timebomb, Ironclad, Glimmer, Pack, Lariat, Pod, Murmur, Roost, Pincer, Shoal, Comb, Raid).

## The 20 vessels

The Hangar class list (`SO_Classlist_Classes`) without the three omega crews (Warden, Bulwark, Tempest). The Grizzly is playable but not on that list.

## Pilots (40)

kept = the Notion idea as written; reshaped = it was a number, broke the PvP rule, or repeated a level-5 upgrade; new = a game-asset-only pilot or a new name.

| Vessel | Pilot | Art | Ability rewritten | Rule | AI in the seat | Kind | Source |
|---|---|---|---|---|---|---|---|
| Sparrow | Shrapnel | yes | Skyburst | Skyburst blasts throw a ring of small prisms in your domain outward, so every missile breaks rival mass and builds yours. | Fires into the densest rival mass. | kept | Destruction effects also create mini-prisms in all directions. |
| Sparrow | Flux | yes | Pulsefire | Pulsefire rounds are waves: a widening arc that shoves prisms aside instead of popping them. Clear a lane, or push rival mass into a pile. | Opens a lane ahead when boxed in. | kept | Projectiles become waves of energy that push blocks. |
| Manta | Reaper | yes | Sting skim | Rival prisms you skim rot: they wither into danger prisms over a few seconds, and the rot spreads once to the prisms they touch. | Skims rival walls along their length. | kept | Skimming corrupts enemy prisms. |
| Manta | Astrofoil | yes | Soar | While soaring, a blast that hits your prisms reflects: the wave runs back along the shot and breaks the attacker's prisms at the source. | Soars when it is under fire. | kept | Reflects destruction effects back at attackers. Aimed at their prisms, not their vessel. |
| Serpent | Scales | yes | Seed Wall | The Seed Wall grows as overlapping scales. A brick that is hit hands its shield to the brick behind it instead of falling, so the wall wears down from the front. | Plants walls across lanes rivals use. | reshaped | Was: all grown volume is shielded, which repeats the Mass level-5 shape. |
| Serpent | Siphon | yes | Sniper Shot + Seed Wall | Prisms your round destroys are stolen: they fly back and lock into your Seed Wall. | Snipes rival mass close to its own wall. | reshaped | Was: stolen volume grants temporary Mass (an element). Now the stolen volume builds the wall. |
| Rhino | Tomahawk | yes | Sword · special move | Throw the sword: double-tap the trigger and the energy blade spins out in an arc that follows your turn, cuts everything on its way, and comes back to the hull. | Throws at walls it cannot reach. | reshaped | Was: speed while turning. The arc follows your turn. A thrown blade that hits a rival scores through the Sword Steal hit. |
| Rhino | Dozer | yes | Plough | The plough shovels instead of breaking: prisms pile up ahead of the nose and drop where you stop, as your own wall. | Pushes rival mass into a pile in front of its lane. | reshaped | Was: destruction ignores shields, which is the Rhino's Breaker level-5 upgrade. |
| Squirrel | Tenacious | yes | Drift trail | Your drift lays danger prisms instead of plain ones. The line you carve through a corner is a hazard for whoever follows. | Drifts on the racing line when a rival is close behind. | kept | Produce dangerous prisms instead of shielded ones. Tag: Shielded producer. |
| Squirrel | Trace | yes | Skimming | Chase a rival's trail and you overwrite it: the trail you lay on top of theirs turns into your domain. | Tails the leader. | reshaped | Was: double charge from following. The chase is kept; the charge number is dropped. |
| Dolphin | Slingshot | yes | Charge and release | Pull both sticks together to wind up, throw them apart to fire: the release launches you at twice top speed for a few seconds. | Winds up on straights before a crystal. | kept | Slow (sticks together) to fast (sticks apart) boosts to x2 max speed. Tag: Double-Joystick. |
| Dolphin | Riptide | yes | Trail | Your prisms are mines: when a rival breaks one, it bursts and takes their nearby prisms with it. | Lays trail where rivals fight. | kept | Blocks that, when destroyed, explode and destroy opposing blocks. |
| Urchin | Bushy | yes | Track Projector | The projected rail is three rails in a Y, the flux-capacitor shape: three lanes to ride, and the chain spikes cascade along all three. | Projects the rail toward rival mass, then rides it. | kept | Trail has a third segment, arranged like a flux capacitor. Tag: split trail. Moved from the Squirrel. |
| Urchin | Heist | **no** | Chain Spikes | Chain spikes steal: a cascade turns rival prisms into your domain instead of breaking them, so every chain grows the prismscape you ride. | Fires into rival mass that joins its own. | kept | Your destructive abilities steal. Tags: Destructive, Cultivator. |
| Butterfly | Maestro | yes | Piano keys | Play on the beat: a press in time with the music makes the keys you lay ring out a wave of extra volume. | Presses on the beat; the AI reads the music clock. | kept | Press on beat to produce extra rings of volume. Tag: Single-Joystick. **Check:** Notion tags Maestro Single-Joystick; the Butterfly is two-thumb, so the press would be a trigger tap. Moved from the Dolphin. |
| Butterfly | Damsel | yes | Dust mode | The dust settles: leave Dust mode and the dust capsule stays where it was, drifting down for 6 s and still working the mass below it. | Leaves dust over its own mass before it switches mode. | new | Game asset only (old line: biggest blocks in open space). Moved from the Manta. |
| Thresher | Krush | yes | Wrecking ball | Every rival prism the ball smashes kicks it faster, so a run of smashes keeps the ball red without spinning up again. | Chains smashes along rival walls. | kept | Destruction effects give you a speed boost. Moved from the Rhino. |
| Thresher | Trapper | **no** | Plant | Planting the ball lays a ring of tar prisms around the anchor: sticky prisms that slow whoever flies through them, you included. | Plants across a chaser's line. | reshaped | Was: prisms pull and slow opposing ships. Pulling a vessel breaks the PvP rule; a prism that slows anyone who touches it is an arena effect. |
| Scarab | Mason | yes | Switch · the wall | Your switches are mortared. The bricks are bonded, a broken brick regrows from its neighbours after a few seconds, and a ball that hits the frame bounces off instead of breaking it. | Places switches across the ball's lanes. | reshaped | Was: skimming shields friendly prisms. Rebuilt around your Scarab example. |
| Scarab | Echo | yes | Ball forge | Every ball you forge has an echo: a smaller twin, about 60% of its size, forged beside it, that follows the first ball's path a beat behind. | Forges near switches. | kept | For each volume produced, produce another slightly smaller (40-80%). Moved from the Dolphin. |
| Stoat | Strider | yes | Pathfinder | The paths you boost down stay behind as a lane: for 10 s anyone in your domain rides it at Pathfinder speed. | Boosts down paths toward its team's objective. | reshaped | The 20% / 50% numbers moved to the Tailwind artifact. Moved from the Squirrel. |
| Stoat | Comet | yes | Time mode | The orbit slingshot makes you a comet: for the length of the kick you pass through prisms and break them instead of hitting them. | Slings through rival walls. | reshaped | Crystal effects doubled moved to the Comet Shard artifact. Moved from the Serpent. |
| Goby | Tender | yes | Dens | Dens tend themselves: while the Pistol is within reach, a den regrows any prism taken from it. | Keeps the Pistol near its best den. | new | Game asset only (old line: longest debuff from crystal decoy). Moved from the Manta. |
| Goby | Snap | **no** | Vortex rings | Hold to charge the Pistol's snap: the vortex ring grows while held, and on release carves a tunnel through prisms on its way to the locked rival. | Charges a ring when the lock sits behind a wall. | new | New name (a pistol shrimp's snap). |
| Brittle Star | Jugger | yes | Ring throw · special move | Catch the thrown ring: fly through it before it fuses and it is back on the hull with the fuse reset, and your spare is kept. | Throws, then catches when no target is in reach. | new | Game asset only (old line: biggest max shield size). Moved from the Rhino. |
| Brittle Star | Nul | **no** | Speed mode | In Speed mode the arena cannot touch you: danger and slow prisms in your path are cut by the ring before you feel them. | Switches to Speed before crossing danger fields. | reshaped | Was: ignore all negative effects. Narrowed to arena effects, because ignoring rival hits would break the petals-only rule. |
| Shrike | Timebomb | **no** | Void Lance | The Void Lance grows the longer it flies. A close shot pins one block; a long shot hollows out a whole wall. | Takes the longest clear line it can find. | kept | Projectiles grow larger the longer they are alive. Tag: Projectile user. |
| Shrike | Ironclad | **no** | Nests | Your nests are forts: while a nest's flora lives, the nest regrows its super-shielded blocks. | Nests beside contested crystals. | reshaped | Was: auto-shield your trail, which repeats several Mass level-5 upgrades. |
| Angler | Glimmer | **no** | Paint | Paint sweeps: holding RT paints a wide fan instead of one point, so the half-school spreads across a whole wall instead of piling onto one prism. | Sweeps long rival walls. | new | New name. |
| Angler | Pack | **no** | Lock-on | A lock-on sends a pack of three that share the target and come back to the school after it falls, instead of one creature that is spent. | Locks the rival with the most petals. | new | New name. **Check:** Changes what happens to creatures the school spends: check it against the locked ecosystem principles. |
| Wrasse | Feist | yes | Afterburner | Lighting the afterburner leaves a decoy: a ghost hull and kite fly on your old heading for 2 s, and AI pilots and homing missiles chase it. | Burns when a rival sits on its tail. | reshaped | Notion line was empty; the name now means a feint. Moved from the Sparrow (the Wrasse is its hull). |
| Wrasse | Lariat | **no** | Kite tail | The tail lassos prisms too: close a loop around a cluster of rival prisms and the whole cluster is stolen. | Loops clusters the pilot flies past. | new | New name. |
| Orca | Longshift | yes | Gunner outrigger | Drop an outrigger: one gunner turret detaches and keeps firing at rival prisms for 8 s, and re-docks when the hull flies back through it. | Drops a turret by a rival wall, then leaves. | reshaped | Range and velocity moved to the Long Barrel artifact. Moved from the Sparrow. |
| Orca | Pod | **no** | Gunner missiles | The gunners' missiles fly as a pod: if all three fire within a second they merge into one large missile that any gunner steers. | AI gunners fire together when a human gunner fires. | new | New name (an orca pod). |
| Starling | Murmur | **no** | The swarm | Two murmurations: the swarm splits into two flocks that chase different objectives at once, and the next card you pick goes to the flock you are looking at. | Looks at the flock nearer the objective. | new | New name. |
| Starling | Roost | **no** | Between objectives | Between objectives the flock roosts on your mass: the prisms it lands on are shielded until it takes off again. | Turns the view toward its own mass between phases. | new | New name. |
| Jacks | Pincer | **no** | Herding | Pull the thumbs apart while herding and the jacks fan out to the herd's flanks, driving it through a gap from both sides. | Flanks herds aimed at a gap. | new | New name. |
| Jacks | Shoal | **no** | Herding | A herd you drive packs into a bait ball: a tight ball that rolls through rival prisms and breaks them. | Drives the ball through rival walls. | new | New name. **Check:** Changes how real fauna moves: check it against the locked ecosystem principles. |
| Honeyguide | Comb | **no** | The Dynamo | Patterns lock in: a pattern made three times is no longer replaced; it stays on the Dynamo and pays a slip charge every spin. | The AI Ratel repeats its best pattern. | new | New name (a honeycomb). |
| Honeyguide | Raid | **no** | The Slip | Zips steal: slip down a rival trail and it turns to your domain behind you as you pass. | Slips along rival trails. | new | New name (the ratel raids the hive). |

## Artifacts (45; steps are proposals)

| Fits | Artifact | Changes | I | II | III | From |
|---|---|---|---|---|---|---|
| Fleet | Tailwind | Speed bonuses: strength / duration | +10% / +20% | +15% / +35% | +20% / +50% | Strider |
| Fleet | Comet Shard | Crystal effect strength | x1.25 | x1.5 | x2 | Comet |
| Fleet | Ballast | Trail prism size | +10% | +20% | +30% | new |
| Fleet | Long Reach | Skimmer reach | +10% | +20% | +30% | new |
| Fleet | Quick Hands | Ability cooldowns | -5% | -10% | -15% | new |
| Sparrow | Extra Bay | Skyburst missiles the bay holds | +1 | +1 | +2 | your example |
| Sparrow | Long Barrel | Pulsefire range and velocity | +10% | +20% | +30% | Longshift |
| Manta | Bomb Rack | Bombs the bay holds (3 at rest) | +1 | +1 | +2 | new |
| Manta | Wide Bloom | Bloom size | +10% | +20% | +30% | new |
| Serpent | Fifth Pellet | Fuel pellets in the tank (holds 4) | 4 → 5 | 4 → 5 | 4 → 6 | new |
| Serpent | Steady Bolt | Sniper Shot recovery time | -10% | -20% | -30% | new |
| Rhino | Long Edge | Blade length (24 u at rest) | +2 u | +4 u | +6 u | new |
| Rhino | Lingering Image | Afterimage window (5 s) | +1 s | +2 s | +3 s | new |
| Squirrel | Ring Stock | Boost ring charges | +1 | +1 | +2 | new |
| Squirrel | Hot Rings | Boost ring speed | +10% | +20% | +30% | new |
| Dolphin | Seed Pod | Crystals seeded per cycle | +1 | +1 | +2 | new |
| Dolphin | Deep Lungs | Drift charge rate | +10% | +20% | +30% | new |
| Urchin | Long Rail | Projected track length (100 u) | +15 u | +30 u | +45 u | new |
| Urchin | Deep Chain | Spike cascade generations | +1 | +1 | +2 | new |
| Butterfly | Wide Keys | Mass-mode key width | +10% | +20% | +30% | new |
| Butterfly | Long Dust | Dust capsule length | +10% | +20% | +30% | new |
| Thresher | Long Chain | Chain length | +10% | +20% | +30% | new |
| Thresher | Heavy Ball | Wrecking ball size | +10% | +20% | +30% | new |
| Scarab | Fourth Switch | Standing switches (3) | 3 → 4 | 3 → 4 | 3 → 5 | new |
| Scarab | Heavy Forge | Forged ball size | +10% | +20% | +30% | new |
| Stoat | Wide Poles | Dipole pole size | +10% | +20% | +30% | new |
| Stoat | Long Ripple | Ripple reach | +10% | +20% | +30% | new |
| Goby | Deep Den | Prisms a den holds | +10% | +20% | +30% | new |
| Goby | Quick Dart | Dart cooldown | -10% | -20% | -30% | new |
| Brittle Star | Spare Ring | Spare rings held (2) | 2 → 3 | 2 → 3 | 2 → 4 | new |
| Brittle Star | Cool Guns | Heat per round | -10% | -20% | -30% | new |
| Shrike | Missile Nest | Missiles per nest flora | +1 | +1 | +2 | new |
| Shrike | Quick Dock | Docked cooldown penalty (x1.6) | x1.5 | x1.4 | x1.25 | new |
| Angler | Big School | Creatures in the school | +2 | +4 | +6 | new |
| Angler | Long Beam | Paint reach (3200 u) | +10% | +20% | +30% | new |
| Wrasse | Long Tail | Kite tail length | +10% | +20% | +30% | new |
| Wrasse | Wide Head | Kite head size | +10% | +20% | +30% | new |
| Orca | Boost Tank | Helm boost tank | +10% | +20% | +30% | new |
| Orca | Gunner Rack | Missiles per gunner | +1 | +1 | +2 | new |
| Starling | Big Flock | Flock cap (200) | +10 | +20 | +30 | new |
| Starling | Card Hand | Cards offered per pick | +1 | +1 | +2 | new |
| Jacks | Wide Herd | Herding reach | +10% | +20% | +30% | new |
| Jacks | Fast Jacks | Jack speed | +10% | +20% | +30% | new |
| Honeyguide | Battery Cell | Slip charges the battery holds | +1 | +1 | +2 | new |
| Honeyguide | Long Zip | Slip reach | +10% | +20% | +30% | new |

## Open decisions (Shombith)

1. Two pilots per vessel: the six legacy vessels had four each (one per element). Keep two, or grow the flagships back to three or four (the Hangar has four card slots)?
1. Which 20: swap the Grizzly in for one of these? (Ironclad moved to the Shrike because the Grizzly is not on the list.)
1. Art for 16 pilots: use the 8 spare portraits as stand-ins for 8 of them now?
1. New names (Snap, Glimmer, Pack, Lariat, Pod, Murmur, Roost, Pincer, Shoal, Comb, Raid) are working names: keep, rename,?
1. Artifacts in competitive play: off in ranked/arena, or a fixed power budget per match?
1. How artifacts are found: cell drops, quest rewards, the Elemental Converter (the captain-upgrade economy is de-scoped).
1. Pack (Angler) and Shoal (Jacks) change how creatures behave: check them against Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md before building.

