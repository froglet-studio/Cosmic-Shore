# Pilots and Artifacts (DRAFT, 2026-10-26)

Design draft from Shombith's request of 2026-10-26, built from the Notion pilot table. Not approved, nothing implemented.
Page with the Hangar loadout mockup: https://claude.ai/artifact/KQgwvDGzKwPnYum5xW6NX7

Note: "artifact" here is the in-game item. `Docs/Artifacts/` is the library of claude.ai pages, a different thing.

## The split

- **A pilot changes HOW a vessel plays** (qualitative). One pilot per match, each with a home vessel. It rewrites ONE ability: a new move, a new shape, a new cause and effect, never a bare number. It is **not tied to elements** (works at any petal level; element scaling still applies underneath), must not repeat a level-5 upgrade the same vessel already has, obeys PvP is petals only (`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`), and ships with an AI behaviour line.
- **An artifact changes HOW MUCH** (quantitative). One number per artifact (count, size, duration, cooldown). Three slots per vessel (proposal); hull artifacts fit one vessel, fleet artifacts fit any. Ranks I-III, no self-stacking. It moves the BASE value; elements scale on top in the match.
- Test: if it can be written as "+X" or "xY", it is an artifact.

This replaces the old model in `Assets/_SO_Assets/Captains/Elemental/` (four captains per vessel, one per element, each a "most X in class" stat line). The Hangar Pilots tab header (`Tools/Build/author_menu_hangar.py`, "Each pilot leans the vessel toward one element.") should change once this is decided.

## Pilots (26, from the Notion table)

kept = the Notion idea as written; reshaped = it was a number, broke the PvP rule, or repeated a level-5 upgrade.

| Pilot | Vessel | Ability rewritten | Rule | AI in the seat | Notion | Source note |
|---|---|---|---|---|---|---|
| Feist | Sparrow | Afterburner | Lighting the afterburner leaves a decoy Sparrow flying on your old heading for 2 s. AI pilots and homing Skybursts chase the decoy. | Burns when a rival sits on its tail. | reshaped | Notion line was empty; the name now means a feint. |
| Shrapnel | Sparrow | Skyburst | Skyburst blasts throw a ring of small prisms in your domain outward, so every missile breaks rival mass and builds yours. | Fires into the densest rival mass. | kept | Destruction effects also create mini-prisms in all directions. |
| Longshift | Sparrow | Turret Stance | Turret Stance becomes a drop turret: press A and the turret stays where you were, firing at rival prisms for 8 s while you fly on. It never targets pilots. | Drops the turret beside a rival wall, then leaves. | reshaped | Range and velocity moved to the Long Barrel artifact. |
| Flux | Sparrow | Pulsefire | Pulsefire rounds are waves: a widening arc that shoves prisms aside instead of popping them. Clear a lane, or push rival mass into a pile. | Opens a lane ahead when boxed in. | kept | Projectiles become waves of energy that push blocks. |
| Timebomb | Serpent | Sniper Shot | The round grows the longer it flies. A short shot makes a pinhole; a cross-map shot blows a crater. | Takes the longest clear line it can find. | kept | Projectiles grow larger the longer they are alive. Tag: Projectile user. |
| Siphon | Serpent | Sniper Shot + Seed Wall | Prisms your round destroys are stolen: they fly back and lock into your Seed Wall. | Snipes rival mass close to its own wall. | reshaped | Was: stolen volume grants temporary Mass (an element). Now the stolen volume builds the wall. |
| Scales | Serpent | Seed Wall | The Seed Wall grows as overlapping scales. A brick that is hit hands its shield to the brick behind it instead of falling, so the wall wears down from the front. | Plants walls across lanes rivals use. | reshaped | Was: all grown volume is shielded, which repeats the Mass level-5 shape. |
| Comet | Serpent | Solid Fuel Pellets | A pellet burn turns the Serpent into a comet: it passes through prisms, breaking them, instead of hitting them. | Burns through a wall to escape when cornered. | reshaped | Crystal effects doubled moved to the Comet Shard artifact. |
| Tomahawk | Rhino | Sword · special move | Throw the sword: double-tap the trigger and the energy blade spins out in an arc that follows your turn, cuts everything on its way, and comes back to the hull. | Throws at walls it cannot reach. | reshaped | Was: speed while turning. The throw's arc follows your turn. A thrown blade that hits a rival scores through the Sword Steal hit. |
| Krush | Rhino | Sword | Rampage: every prism the sword cuts kicks the Rhino forward. Cut along a wall and you tear through it at speed. | Lines up along a wall before it cuts. | kept | Destruction effects give you a speed boost. |
| Dozer | Rhino | Plough | The plough shovels instead of breaking: prisms pile up ahead of the nose and drop where you stop, as your own wall. | Pushes rival mass into a pile in front of its lane. | reshaped | Was: destruction ignores shields, which is the Rhino's Breaker level-5 upgrade. |
| Nul | Rhino | Sword | While the blade is lit, the arena cannot touch you: danger prisms, slow prisms and walls are cut through, not felt. | Lights the blade before flying into danger fields. | reshaped | Was: ignore all negative effects. Narrowed to arena effects, because ignoring rival hits would break the petals-only rule. |
| Tenacious | Squirrel | Drift trail | Your drift lays danger prisms instead of plain ones. The line you carve through a corner is a hazard for whoever follows. | Drifts on the racing line when a rival is close behind. | kept | Produce dangerous prisms instead of shielded ones. Tag: Shielded producer. |
| Bushy | Squirrel | Trail | Your trail has a third strand: three rails in a Y, the flux-capacitor shape, so the tube you leave can be ridden from three sides. | Nothing special; flies as usual. | kept | Trail has a third segment, arranged like a flux capacitor. Tag: split trail. |
| Strider | Squirrel | Tube | Ride any trail, yours or a rival's: hug it and it pulls you along like your own tube. | Jumps onto rival trails heading its way. | reshaped | The 20% / 50% numbers moved to the Tailwind artifact. |
| Trace | Squirrel | Skimming | Chase a rival's trail and you overwrite it: the trail you lay on top of theirs turns into your domain. | Tails the leader. | reshaped | Was: double charge from following. The chase is kept; the charge number is dropped. |
| Mason | Scarab | Switch · the wall | Your switches are mortared. The bricks are bonded, a broken brick regrows from its neighbours after a few seconds, and a ball that hits the frame bounces off instead of breaking it. | Places switches across the ball's lanes. | reshaped | Was: skimming shields friendly prisms. Rebuilt around your Scarab example. |
| Echo | Scarab | Ball forge | Every ball you forge has an echo: a smaller twin, about 60% of its size, forged beside it, that follows the first ball's path a beat behind. | Forges near switches. | kept | For each volume produced, produce another slightly smaller (40-80%). Moved from the Dolphin. |
| Heist | Manta | Kabloom | Your blooms steal instead of destroying: rival prisms caught in a bloom turn into your domain. | Bombs the biggest rival structure. | kept | Your destructive abilities steal. Tags: Destructive, Cultivator. |
| Reaper | Manta | Sting skim | Rival prisms you skim rot: they wither into danger prisms over a few seconds, and the rot spreads once to the prisms they touch. | Skims rival walls along their length. | kept | Skimming corrupts enemy prisms. |
| Trapper | Manta | Yastri turn trails | Hard-turn trails are tar: sticky prisms that slow whoever flies through them, you included. | Turns hard across a chaser's line. | reshaped | Was: prisms pull and slow opposing ships. Pulling a vessel breaks the PvP rule; a prism that slows anyone who touches it is an arena effect. |
| Astrofoil | Manta | Soar | While soaring, a blast that hits your prisms reflects: the wave runs back along the shot and breaks the attacker's prisms at the source. | Soars when it is under fire. | kept | Reflects destruction effects back at attackers. Aimed at their prisms, not their vessel. |
| Slingshot | Dolphin | Charge and release | Pull both sticks together to wind up, throw them apart to fire: the release launches you at twice top speed for a few seconds. | Winds up on straights before a crystal. | kept | Slow (sticks together) to fast (sticks apart) boosts to x2 max speed. Tag: Double-Joystick. |
| Riptide | Dolphin | Trail | Your prisms are mines: when a rival breaks one, it bursts and takes their nearby prisms with it. | Lays trail where rivals fight. | kept | Blocks that, when destroyed, explode and destroy opposing blocks. |
| Ironclad | Grizzly | Dig In | Digging in raises a bunker: a ring of shielded prisms grows around you, and it stays standing after you rush out. | Digs in beside crystals. | reshaped | Was: auto-shield your trail, which repeats several Mass level-5 upgrades. The Grizzly gets a fort instead. |
| Maestro | Butterfly | Piano keys | Play on the beat: a press in time with the music makes the keys you lay ring out a wave of extra volume. | Presses on the beat; the AI reads the music clock. | kept | Press on beat to produce extra rings of volume. Tag: Single-Joystick. **Check:** Notion tags Maestro Single-Joystick; the Butterfly is two-thumb, so the press would be a trigger tap. Moved from the Dolphin. |

## Artifacts (starting set; steps are proposals)

| Artifact | Fits | Changes | I | II | III | From |
|---|---|---|---|---|---|---|
| Tailwind | Fleet | Speed bonuses: strength / duration | +10% / +20% | +15% / +35% | +20% / +50% | Strider |
| Comet Shard | Fleet | Crystal effect strength | x1.25 | x1.5 | x2 | Comet |
| Ballast | Fleet | Trail prism size | +10% | +20% | +30% | new |
| Long Reach | Fleet | Skimmer reach | +10% | +20% | +30% | new |
| Quick Hands | Fleet | Ability cooldowns | -5% | -10% | -15% | new |
| Extra Bay | Sparrow | Skyburst missiles the bay holds | +1 | +1 | +2 | your example |
| Long Barrel | Sparrow | Pulsefire range and velocity | +10% | +20% | +30% | Longshift |
| Fifth Pellet | Serpent | Fuel pellets in the tank (holds 4) | 4 → 5 | 4 → 5 | 4 → 6 | new |
| Steady Bolt | Serpent | Sniper Shot recovery time | -10% | -20% | -30% | new |
| Long Edge | Rhino | Blade length (24 u at rest) | +2 u | +4 u | +6 u | new |
| Lingering Image | Rhino | Afterimage window (5 s) | +1 s | +2 s | +3 s | new |
| Spare Ring | Squirrel | Boost ring charges | +1 | +1 | +2 | new |
| Fourth Switch | Scarab | Standing switches (3) | 3 → 4 | 3 → 4 | 3 → 5 | new |
| Heavy Forge | Scarab | Forged ball size | +10% | +20% | +30% | new |
| Bomb Rack | Manta | Bombs the bay holds (3 at rest) | +1 | +1 | +2 | new |
| Wide Bloom | Manta | Bloom size | +10% | +20% | +30% | new |
| Seed Pod | Dolphin | Crystals seeded per cycle | +1 | +1 | +2 | new |
| Deep Lungs | Dolphin | Drift charge rate | +10% | +20% | +30% | new |
| Shell Rack | Grizzly | Trigger bombs held | +1 | +1 | +2 | new |
| Wide Keys | Butterfly | Key slab width | +10% | +20% | +30% | new |

## Open decisions (Shombith)

1. Old captains not on the Notion list (Jugger on the Rhino, Tender and Damsel on the Manta): retire or seat?
1. Home moves: Echo Dolphin -> Scarab (twin ball), Maestro Dolphin -> Butterfly (piano keys). The Dolphin keeps Slingshot and Riptide.
1. Do Notion's Required Vessel Tags let a pilot fly a second hull, or does each pilot keep one home?
1. Artifacts in competitive play: off in ranked/arena, or a fixed power budget per match?
1. How artifacts are found: cell drops, quest rewards, the Elemental Converter (the captain-upgrade economy is de-scoped).
1. Vessels with no pilot yet: Urchin, Thresher, Stoat, Shrike, Brittle Star, Angler, the crew vessels.

Shombith said an older design chat is coming; fold it in here when it arrives.
