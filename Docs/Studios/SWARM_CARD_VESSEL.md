# Swarm Card Vessel: the 80 cards (revision 3, 2026-10-20)

**Review page:** https://claude.ai/artifact/QB2oYkL8P8Cv7KPGyhdHp2 (filter by element or phase, drag the level slider, mark each card Keep / Tweak / Cut with a note; the next revision reads those marks).
**Data:** `Docs/Studios/SwarmCardVessel/cards.py` is the single source; `python3 Docs/Studios/SwarmCardVessel/build.py` writes this file, `cards.json` and the page (`Docs/Studios/SwarmDeck.html`).
**Status:** design list only. Nothing is in the lab or in Unity yet. Revision 3 follows Garrett's second brief (timed objective cycle, passive swarm); he reviews again.

## The vessel (Garrett's brief, 2026-10-20, revised the same day; not built yet)

The second swarm vessel after the Shepherd ([SWARM_VESSEL_LAB.md](SWARM_VESSEL_LAB.md)): a **mobile-first, single-player** vessel that mixes card play with a roguelike run. The fun is a huge number of wacky ways to change the swarm: morph into a dragonfly, give every member a laser, split into two swarms. **Picking a card should never feel tense.**

- **The swarm plays itself.** It roams the whole cell in 3D on its own. Nothing the player does steers it.
- **The player watches.** The player controls only a viewing plane through the cell, drawn the way the Wrasse draws 2.5D. A touch is a joystick that rotates that view on one or both of its relative axes, resetting after each turn. The view follows the swarm's approximate centroid, from a sample of members. Rotating it changes what you see, never what the swarm does.
- **The objective cycles on a timer.** The swarm works four objectives in turn: **Omnicrystal → Elemental crystals → Opposing vessels → Opposing mass**, then round again. A phase ends when its timer runs out or its goal is met, whichever comes first:

  | Phase | Goal that ends it early | Proposed timer and goal (for Garrett to set) |
  |---|---|---|
  | Omnicrystal | Collect an omnicrystal | 30 s |
  | Elemental crystals | Collect Y elemental crystals | 25 s, Y = 5 |
  | Opposing vessels | Debuff a vessel (silence, slow, petal, stripped crystal) | 25 s |
  | Opposing mass | Reduce opposing mass by X | 25 s, X = 30 prisms |

- **Cards.** The end of the Omnicrystal phase deals four cards, one per element, whether or not an omnicrystal was collected. The player picks one; the other three go. The phase bar and timer show when the next hand comes.
- **Minimum size.** Like every vessel, the swarm has a floor: below its minimum size (proposed 6 members) nothing can harm it, so a swarm is never wiped out.
- **Levels.** Each element has 20 cards, and each card goes from level 1 to 15. Upgrading an element means its cards arrive at that level. Drawing a card you already hold adds its level to yours.
- **What the player does:** watch the swarm, rotate the view, pick one of four cards, wait for the next hand. That is all.

Each card names the **phase it shines in**: the phase whose goal it helps meet sooner, which brings the next hand sooner.

## Element jobs (Garrett, 2026-10-20, from revision 2)

Each element owns a job, and no card of another element does that job:

| Element | Its job (no other element does this) |
|---|---|
| Mass | Prism production (danger or base), shielding, supershielding |
| Time | Your maneuverability, speed, positioning and movement |
| Space | More range or more prisms you can destroy, steal or shrink |
| Charge | Silencing other vessels' Charge and Space abilities, killing fauna, stripping elemental crystals from other vessels |

- At least half of each element's cards do its job (the **Job** column).
- At least half lean on the element's real physics (the **Physics** column): gravity, inertia and density for Mass; dilation, rewinds and phase for Time; folded, curved and expanding space for Space; static, arcs and magnetism for Charge.
- **No stuns (Garrett, 2026-10-20).** No card stuns another vessel or takes its control away: no stun, hijack, blind or shove. That rule protects the fun economy. Silencing Charge and Space abilities, slowing, taking petals and stripping crystals are all allowed.
- Exotic effects outside these jobs are welcome, as long as they do not take another element's job. `build.py` fails if an element drops under 10 in either column.
- Revision 3 removed everything that leaned on the player steering the swarm: plane flicks, the camera line, the objective toggle, the card reroll. The list of changes is at the end.

## How to read the scaling

- Every card has one or two numbers. Each is linear (`a + b × (level − 1)`) or a steady percentage step (`a × r^(level − 1)`), so the curve from 1 to 15 has no jumps. Counts round down.
- **Milestones** are the roguelike jolts: at some levels a card changes what it does, not only how much. Levels 5, 10 and 15 carry most of them, matching the fleet's level-5 upgrades.
- **Forms** (Dragonfly, Golem, Snowball, Hummingbird, Manta, Whale, Wasp, Thunderhead, Dragon, Neutron Star) are the swarm turning into one creature for a few seconds on a fixed cooldown.
- Baselines assumed by the numbers: a swarm of about 24 members with a cap of 40 and a protected minimum of 6, cruising at about 60 u/s, and a 1,400 u view (the Shepherd's). A later revision tunes these once the list is settled.

## Questions for Garrett (readings this revision chose)

1. **"Upgrade an element"** is read as a between-runs upgrade (roguelike meta progress), so a level-6 Time unlock makes every Time card you draw arrive at level 6. Or is it earned inside a run, for example from elemental crystals?
2. **Over the cap.** When a duplicate would push a card past 15, the extra levels are lost here. They could convert into something instead (a reroll, members).
3. **Forms.** Can the swarm hold two forms (say Dragonfly and Wasp)? This revision assumes they take turns, one form at a time.
4. **Phase numbers.** The timers and the X and Y goals above are proposals. Should meeting a non-omnicrystal goal early pay anything beyond moving the cycle on?

## Mass (20 cards)

*Weight, gravity and matter: the swarm lays your prisms, shields them and turns heavy.*

**Mass's job:** Prism production (danger or base), shielding, supershielding. 14 of 20 cards do it; 11 of 20 play on its physics.

| # | Card | Kind | Shines in phase | What it does | Scales: L1 / L5 / L10 / L15 | Milestones | Job | Physics |
|---|---|---|---|---|---|---|---|---|
| 1 | **Mound Builders** | Build | Any | The swarm lays your team's prisms along its path. | Prisms laid: 0.50/s / 1.50/s / 2.75/s / 4.00/s | L5: Laid prisms join into a solid wall with no gaps<br>L15: Laid prisms are shielded | Prism production | - |
| 2 | **Danger Drip** | Build | Opposing vessels | Members drip danger prisms behind them. A rival that flies through one is slowed and loses a petal, as any danger prism does. | Danger prisms: 0.30/s / 0.90/s / 1.65/s / 2.40/s | L10: Drips fall in short lines across the swarm's wake | Danger prism production | - |
| 3 | **Conservation of Mass** | Build | Any | Mass is never lost. A member that dies becomes your prisms where it fell. | Prisms per death: 1 / 3 / 5 / 8 | L10: The prisms are shielded | Prism production | Conservation of mass |
| 4 | **Inertial Wall** | Build | Opposing vessels | Every few seconds the swarm drops a straight wall of your prisms along its line of travel. The faster it flies, the longer the wall, because momentum carries the bricks. | Wall length at cruise: 4 prisms / 8 prisms / 13 prisms / 18 prisms<br>Every: 10.0 s / 8.1 s / 6.3 s / 4.9 s | L10: The wall is laid as danger prisms | Prism production | Inertia and momentum |
| 5 | **Accretion Disk** | Build | Omnicrystal | Members circling the objective lay a ring of your prisms round it, the way matter collects in orbit round a heavy body. | Ring size: 8 prisms / 16 prisms / 26 prisms / 36 prisms<br>Build time: 4.0 s / 2.6 s / 1.5 s / 0.9 s | L10: The ring is shielded<br>L15: The ring is supershielded | Prism production and shielding | Accretion in orbit |
| 6 | **Snowball** | Form | Any | Every 20 s the swarm rolls into one ball. It packs on a layer of your prisms each second, and when it breaks they land as a mound. | Ball lasts: 3.0 s / 5.0 s / 7.5 s / 10.0 s<br>Prisms per second: 2 / 6 / 11 / 16 | L10: The mound lands shielded | Prism production | Accretion |
| 7 | **Density** | Build | Any | Your prisms laid close together compress into fewer, bigger prisms, so the same mass holds more volume and takes more to break. | Merge radius: 10 u / 22 u / 37 u / 52 u<br>Volume per merge: 1.2× / 1.6× / 2.1× / 2.6× | L15: Merged prisms are supershielded | Prism production | Density and compression |
| 8 | **Neutron Star** | Form | Any | Every 30 s the swarm crushes into one dense star that cannot be hurt. When it ends it leaves a supershielded core of your prisms. | Star lasts: 1.5 s / 2.9 s / 4.7 s / 6.4 s<br>Core size: 3 prisms / 7 prisms / 12 prisms / 17 prisms | - | Supershielding | Degenerate matter |
| 9 | **Golem** | Form | Opposing vessels | Every 25 s the swarm stacks into a giant walking golem. Each stomp leaves a footprint of your prisms, and its own body is shielded. | Golem lasts: 4.0 s / 6.0 s / 8.5 s / 11.0 s<br>Prisms per footprint: 2 / 4 / 6 / 9 | L10: Footprints are danger prisms<br>L15: The golem is supershielded | Prism production and shielding | Weight |
| 10 | **Supershield Core** | Shield | Any | Your prisms near the swarm's centroid are supershielded while the swarm stays close. | Core radius: 40 u / 80 u / 130 u / 180 u | L10: The shield lasts a few seconds after the swarm leaves | Supershielding | - |
| 11 | **Carapace** | Shield | Opposing vessels | Members grow shells. A shell soaks one hit, then regrows. | Shell regrows in: 12.0 s / 7.2 s / 3.8 s / 2.0 s | L8: Two shells per member<br>L15: Three shells per member | Shielding | - |
| 12 | **Gravity Well** | Pull | Elemental crystals | The centroid's gravity pulls loose crystals toward the swarm, and your prisms inside the well pack into a shielded clump. | Pull radius: 50 u / 110 u / 185 u / 260 u | L10: Enemy vessels inside the well are dragged (25% slow) | Shielding | Gravity |
| 13 | **Bulk Up** | Body | Opposing vessels | Members grow bigger and heavier. Heavy members take more to kill and do not get knocked about. | Member size: 1.05× / 1.25× / 1.50× / 1.75×<br>Toughness: 1.0× / 1.8× / 2.8× / 3.8× | L10: A heavy member that rams a vessel takes a third of a petal | - | Inertia |
| 14 | **Mass Tug** | Hook | Opposing vessels | Members latch onto enemy vessels and weigh them down. | Most slow on one vessel: 15% / 31% / 51% / 71% | L10: A weighed-down vessel drops a petal every 5 s | - | Added mass |
| 15 | **Brood** | Growth | Any | The swarm breeds: a new member hatches at the centroid every few seconds, up to the swarm cap. | Hatch every: 6.0 s / 3.6 s / 1.9 s / 1.0 s | L10: Hatchlings arrive full size<br>L15: Twins: two members per hatch | - | - |
| 16 | **Big Family** | Growth | Any | Raises the swarm cap and the protected minimum size. | Swarm cap: 46 / 70 / 100 / 130<br>Minimum size: 6 / 8 / 10 / 13 | L10: Members over the cap wait in a reserve and refill losses at once | - | - |
| 17 | **Queen** | Growth | Any | One member is crowned. While she lives the swarm breeds faster and her guards are shielded. If she falls, the crown passes after a delay. | Breeding bonus: 20% / 60% / 110% / 160%<br>Crown passes in: 15.0 s / 7.8 s / 3.5 s / 1.5 s | L15: Two queens | Shielding | - |
| 18 | **Clutch** | Growth | Elemental crystals | Each crystal the swarm collects lays eggs that hatch a few seconds later. | Eggs per crystal: 1 / 3 / 5 / 8 | L10: Eggs hatch at once | - | - |
| 19 | **Pack Mules** | Economy | Elemental crystals | Elemental crystals the swarm carries home count for more element. | Extra element per crystal: 10% / 50% / 100% / 150% | - | - | - |
| 20 | **Critical Mass** | Burst | Opposing mass | When the swarm passes a size, it throws off a shell of your prisms all round it in one go, then has to grow back past the size to do it again. | Swarm size needed: 36 / 29 / 22 / 17<br>Prisms thrown: 8 / 20 / 35 / 50 | L10: The shell is shielded | Prism production | Critical mass |

## Time (20 cards)

*Speed, tempo and second chances: the swarm moves faster, turns sharper and gets where it needs to be.*

**Time's job:** Your maneuverability, speed, positioning and movement. 16 of 20 cards do it; 12 of 20 play on its physics.

| # | Card | Kind | Shines in phase | What it does | Scales: L1 / L5 / L10 / L15 | Milestones | Job | Physics |
|---|---|---|---|---|---|---|---|---|
| 1 | **Dragonfly** | Form | Elemental crystals | Every 15 s the swarm morphs into a giant dragonfly and darts at the objective in fast straight hops, collecting crystals on the way. | Dart speed: 2.00× / 2.60× / 3.35× / 4.10×<br>Dragonfly lasts: 3.0 s / 4.6 s / 6.6 s / 8.6 s | L10: Each hop leaves a slipstream lane | Speed and movement | - |
| 2 | **Quickening** | Speed | Any | The swarm cruises faster. | Cruise speed: 4% / 20% / 40% / 60% | - | Speed | - |
| 3 | **Slipstream** | Speed | Any | Members drafting behind other members fly faster, so a long thin swarm is a fast one. | Draft bonus: 5% / 17% / 32% / 47% | - | Speed | - |
| 4 | **Skim Rush** | Speed | Opposing mass | Members that skim close past enemy prisms gain speed, like a vessel's skim. | Speed per skim: 3% / 7% / 12% / 17% | L10: Skims also refill card cooldowns a little | Speed | - |
| 5 | **Agility** | Turn | Any | The swarm turns tighter, so it whips round to a new target instead of looping wide. | Turn rate: 90°/s / 150°/s / 225°/s / 300°/s | L10: Members bank like starlings: the whole swarm turns as one | Maneuverability | - |
| 6 | **Hummingbird** | Form | Elemental crystals | At the objective the swarm becomes a hummingbird: it holds dead still, turns on the spot and sips nearby crystals in. | Sip radius: 60 u / 92 u / 132 u / 172 u<br>Turn rate: 180°/s / 300°/s / 450°/s / 600°/s | - | Maneuverability and positioning | - |
| 7 | **Lead Time** | Aim | Opposing vessels | The swarm flies to where its objective will be in a moment, not where it is now, so it meets moving targets head on. | Looks ahead: 0.2 s / 0.6 s / 1.1 s / 1.6 s | L10: Sets off toward the next phase's objective in the last seconds of the timer | Positioning | Prediction along a worldline |
| 8 | **Skip Ahead** | Teleport | Omnicrystal | Every few seconds the swarm skips a moment of travel and arrives further along its path toward the objective. | Cooldown: 24.0 s / 15.1 s / 8.4 s / 4.7 s<br>Distance skipped: 300 u / 500 u / 750 u / 1000 u | - | Positioning | Skipping time |
| 9 | **Blink Step** | Dodge | Opposing vessels | A member about to be hit skips a fraction of a second ahead and is not there when the shot lands. | Chance: 10% / 26% / 46% / 66% | - | Movement | Skipping time |
| 10 | **Out of Phase** | Pass | Any | Every 15 s the swarm slips out of phase with the world and passes through prisms and vessels. | Phase lasts: 1.0 s / 2.2 s / 3.7 s / 5.2 s | - | Movement | Phase |
| 11 | **Slingshot** | Launch | Omnicrystal | When the objective is far, the swarm draws back and flings itself at it. | Fling distance: 200 u / 360 u / 560 u / 760 u<br>Cooldown: 12.0 s / 9.0 s / 6.2 s / 4.3 s | - | Movement | - |
| 12 | **Save Point** | Rewind | Any | Once per cycle, when the swarm is knocked down to its minimum size, it rewinds to where it was and how big it was a few seconds ago. | Rewinds: 3.0 s / 5.0 s / 7.5 s / 10.0 s | L10: The rewind also returns the petals you lost | Positioning | Rewind |
| 13 | **Fast Forward** | Speed | Any | Every 20 s the swarm runs on double time (speed, breeding, cooldowns) for a few seconds. | Double time lasts: 2.0 s / 3.6 s / 5.6 s / 7.6 s | - | Speed | Faster clock |
| 14 | **Metronome** | Rhythm | Any | The swarm surges forward on a beat, then glides. The beat quickens as the card levels. | Beat every: 2.00 s / 1.63 s / 1.26 s / 0.98 s<br>Surge speed: 1.5× / 1.9× / 2.4× / 2.9× | L10: A big surge as each new phase starts | Speed | Period and frequency |
| 15 | **Synchrony** | Rhythm | Opposing vessels | Members fall into sync like fireflies. Each synced flash bursts the whole swarm forward at once. | Flash every: 6.0 s / 4.3 s / 2.8 s / 1.9 s<br>Burst speed: 1.30× / 1.50× / 1.75× / 2.00× | - | Movement | Coupled oscillators |
| 16 | **Mayfly Couriers** | Fetch | Elemental crystals | Short-lived mayflies hatch, race to the nearest crystals and bring them back. | Couriers: 1 / 3 / 5 / 8<br>Courier speed: 150 u/s / 210 u/s / 285 u/s / 360 u/s | - | Speed | - |
| 17 | **Rewind** | Survive | Opposing vessels | A member that is killed may rewind 2 s and come back. | Chance: 10% / 30% / 55% / 80% | - | - | Rewind |
| 18 | **Echo Swarm** | Copy | Elemental crystals | A ghost of the swarm replays its path a moment behind it, collecting crystals the real swarm missed. | Ghost strength: 20% / 36% / 56% / 76% | L8: Two ghosts<br>L15: Three ghosts | - | Echo of the past |
| 19 | **Stopwatch** | Field | Opposing vessels | Shots and vessels near the swarm move slower in its time bubble, so it can slip past them. | Slow: 10% / 22% / 37% / 52%<br>Radius: 80 u / 120 u / 170 u / 220 u | - | - | Time dilation |
| 20 | **Hurry Up** | Phase | Any | Every phase timer runs shorter, so the cycle comes round and new hands arrive sooner. | Phase time cut: 3% / 11% / 21% / 31% | L15: A phase whose goal is met early gives its leftover time to the next phase | - | A faster clock |

## Space (20 cards)

*Reach and geometry: the swarm reaches further and takes apart, steals or shrinks more of the enemy's prisms.*

**Space's job:** More range or more prisms you can destroy, steal or shrink. 16 of 20 cards do it; 10 of 20 play on its physics.

| # | Card | Kind | Shines in phase | What it does | Scales: L1 / L5 / L10 / L15 | Milestones | Job | Physics |
|---|---|---|---|---|---|---|---|---|
| 1 | **Annex** | Steal | Opposing mass | Enemy prisms a member touches change hands and become yours. | Steals per member: 0.10/s / 0.30/s / 0.55/s / 0.80/s | L10: Shielded prisms can be stolen and keep their shield | Steal | - |
| 2 | **Tesseract** | Steal | Opposing mass | The swarm folds space and steals enemy prisms from a distance, as if they were right next to it. | Steal reach: 30 u / 70 u / 120 u / 170 u | L10: Two prisms per fold | Steal range | Folded space |
| 3 | **Hole Punch** | Breaker | Opposing mass | Each member bursts enemy prisms it flies through. | Prisms per member: 0.2/s / 0.6/s / 1.1/s / 1.6/s | - | Destroy | - |
| 4 | **Wide Bite** | Reach | Opposing mass | Members reach further when they break, steal or shrink a prism. | Member reach: 106% / 130% / 160% / 190% | - | Destroy and steal range | - |
| 5 | **Shield Eaters** | Breaker | Opposing mass | Members can bite through shielded prisms. | Bites per shield: 6 / 3 / 1 / 1 | L10: One bite breaks a whole cluster of shielded prisms | Destroy | - |
| 6 | **Shrink Ray** | Shrink | Opposing mass | Enemy prisms the swarm passes shrink, and so do enemy vessels it touches. | Shrink: 5% / 15% / 27% / 40%<br>Vessel shrink lasts: 2.0 s / 4.0 s / 6.5 s / 9.0 s | - | Shrink | Scale |
| 7 | **Hubble Flow** | Shrink | Opposing mass | Space expands round the swarm. Nearby enemy prisms drift apart and shrink the longer they stay close. | Radius: 80 u / 128 u / 188 u / 248 u<br>Shrink rate: 2%/s / 6%/s / 11%/s / 16%/s | - | Shrink | Expanding space |
| 8 | **Curvature** | Territory | Omnicrystal | Where the swarm lingers, space curves. Enemy prisms inside slide toward the centre and shrink. | Zone radius: 100 u / 180 u / 280 u / 380 u<br>Shrink rate: 1.0%/s / 3.0%/s / 5.5%/s / 8.0%/s | - | Shrink range | Curved spacetime |
| 9 | **Black Hole** | Ultimate | Any | Once per omnicrystal the swarm collapses into a black hole that pulls in and eats every enemy prism near it. | Black hole lasts: 2.0 s / 3.6 s / 5.6 s / 7.6 s<br>Pull radius: 150 u / 250 u / 375 u / 500 u | - | Destroy range | Event horizon |
| 10 | **Meteor Drop** | Slam | Opposing mass | Every few seconds one member climbs high above the swarm and slams down on the objective, bursting enemy prisms round where it lands. | Every: 8.0 s / 5.7 s / 3.8 s / 2.5 s<br>Blast radius: 30 u / 54 u / 84 u / 114 u | L10: Two meteors at a time | Destroy range | Impact |
| 11 | **Shockwave** | Burst | Opposing vessels | Every 10 s the swarm flares into an expanding ring that bursts enemy prisms. | Ring radius: 80 u / 140 u / 215 u / 290 u | - | Destroy range | Pressure wave |
| 12 | **Orbital Scythe** | Orbit | Opposing mass | A share of members orbit the centroid at speed and cut every enemy prism on their circle. | Members in orbit: 10% / 18% / 28% / 38%<br>Orbit radius: 60 u / 84 u / 114 u / 144 u | - | Destroy range | Orbits |
| 13 | **Manta Sheet** | Form | Opposing mass | Every 20 s the swarm flattens into one wide manta that sweeps through the cell, bursting every enemy prism under its wings. | Wingspan: 150 u / 250 u / 375 u / 500 u<br>Manta lasts: 3.0 s / 4.2 s / 5.7 s / 7.2 s | - | Destroy range | - |
| 14 | **Whale Gulp** | Form | Opposing mass | Every 20 s the swarm becomes a whale mouth and gulps a cone of enemy prisms in one bite. Gulped prisms feed breeding. | Cone length: 120 u / 200 u / 300 u / 400 u<br>Most prisms per gulp: 10 / 34 / 64 / 94 | - | Destroy amount | - |
| 15 | **Spaghettify** | Breaker | Opposing mass | Near the swarm's densest point, tidal pull stretches enemy prisms long and thin until they snap. | Tidal radius: 40 u / 100 u / 175 u / 250 u<br>Snap after: 3.0 s / 2.0 s / 1.2 s / 0.7 s | - | Destroy range | Tidal forces |
| 16 | **Mirror Swarm** | Copy | Opposing mass | A mirror-image swarm flies point-reflected through the real swarm's centroid and breaks enemy prisms for real. | Mirror strength: 25% / 45% / 70% / 95% | - | Destroy amount | Reflection |
| 17 | **Split** | Split | Any | The swarm splits. Each extra swarm works the next objective in the cycle at the same time, so a later phase can be met before its timer starts. | Swarms: 2 for 8 s, every 30 s / 2, for good / 3 / 4 (one per objective) | L5: The split is permanent<br>L10: Three swarms<br>L15: Four swarms, one per objective | - | - |
| 18 | **Murmuration** | Decoy | Opposing vessels | The swarm swirls a huge shifting ghost cloud round itself. Enemy aim and AI lock onto the cloud instead of the swarm. | Shots fooled: 10% / 26% / 46% / 66% | - | - | - |
| 19 | **Scouts** | Vision | Elemental crystals | Scouts fly ahead and mark crystals and enemies on the map. | Reveal radius: 300 u / 500 u / 750 u / 1000 u | L10: Marked crystals pull the swarm a little even off-objective | - | - |
| 20 | **Wide Lens** | Camera | Any | Your view pulls back so you see more of the cell round the swarm. | View span: 1460 u / 1700 u / 2000 u / 2300 u | L10: An edge arrow points to the current objective | - | - |

## Charge (20 cards)

*Current, fields and lightning: the swarm silences rivals, kills fauna and knocks the crystals out of other vessels.*

**Charge's job:** Silencing other vessels' Charge and Space abilities, killing fauna, stripping elemental crystals from other vessels. 16 of 20 cards do it; 13 of 20 play on its physics.

| # | Card | Kind | Shines in phase | What it does | Scales: L1 / L5 / L10 / L15 | Milestones | Job | Physics |
|---|---|---|---|---|---|---|---|---|
| 1 | **EMP** | Silence | Opposing vessels | Every 20 s the swarm pulses an EMP that silences enemy Charge and Space abilities in range. | Silenced for: 0.5 s / 1.3 s / 2.3 s / 3.3 s<br>Radius: 120 u / 180 u / 255 u / 330 u | - | Silence | Electromagnetic pulse |
| 2 | **Lightning Rod** | Silence | Opposing vessels | One member stands as a lightning rod. Enemy Charge abilities fired near the swarm are drawn into it and fizzle. | Draw radius: 60 u / 108 u / 168 u / 228 u<br>Rods: 1 / 1 / 2 / 3 | - | Silence | Grounding |
| 3 | **Plasma Net** | Silence | Opposing vessels | Members link into a plasma net. An enemy that crosses a strand keeps flying, but its Charge and Space abilities go quiet. | Silenced for: 0.5 s / 1.3 s / 2.3 s / 3.3 s<br>Strand length: 40 u / 72 u / 112 u / 152 u | - | Silence | Plasma |
| 4 | **Thorns** | Silence | Opposing vessels | An enemy that kills a member may get a shock back that silences its Charge and Space abilities for a moment. | Chance: 5% / 25% / 50% / 75%<br>Silenced for: 0.5 s / 0.9 s / 1.4 s / 1.9 s | - | Silence | Static discharge |
| 5 | **Static Members** | Drain | Opposing vessels | Members carry static. A rival that brushes one has its Charge element drained. | Charge drained per touch: 2% / 6% / 11% / 16% | L10: The drained charge feeds your other Charge cards | Strip elements | Static charge |
| 6 | **Short Circuit** | Strip | Opposing vessels | A vessel swarmed by enough members sparks and drops elemental crystals, which your swarm can collect. It keeps full control of its flight. | Members needed: 16 / 11 / 7 / 4<br>Crystals knocked loose: 1 / 2 / 3 / 4 | - | Strip crystals | Short circuit |
| 7 | **Polarity** | Strip | Opposing vessels | The swarm's magnetic field tugs elemental crystals off enemy vessels that fly near it, without pushing the vessels themselves. | Field radius: 60 u / 100 u / 150 u / 200 u<br>Crystals tugged off: 0.2/s / 0.6/s / 1.1/s / 1.6/s | - | Strip crystals | Magnetism |
| 8 | **Dragon** | Form | Opposing vessels | Every 25 s the swarm becomes a long serpent dragon that breathes a cone of plasma. Vessels it scorches lose elemental crystals; fauna in it burn. | Breath length: 100 u / 180 u / 280 u / 380 u<br>Dragon lasts: 3.0 s / 4.6 s / 6.6 s / 8.6 s | L15: The breath takes a petal a second | Strip crystals and kill fauna | - |
| 9 | **Chain Lightning** | Weapon | Any | The swarm's hits arc on from target to target through fauna and vessels. | Jumps: 1 / 3 / 5 / 8 | - | Kill fauna | Arc discharge |
| 10 | **Tesla Coil** | Weapon | Any | Every few seconds the centroid zaps the nearest fauna. | Zaps per pulse: 1 / 3 / 5 / 8<br>Every: 3.0 s / 2.2 s / 1.6 s / 1.1 s | - | Kill fauna | Resonant coil |
| 11 | **Thunderhead** | Form | Any | Every 25 s the swarm puffs into a storm cloud that rains lightning on the fauna and vessels under it. | Cloud lasts: 3.0 s / 4.6 s / 6.6 s / 8.6 s<br>Strikes: 2.0/s / 4.4/s / 7.4/s / 10.4/s | - | Kill fauna | Storm charge separation |
| 12 | **Lasers** | Weapon | Opposing vessels | Members fire lasers at the nearest fauna or enemy vessel. | Members armed: 9% / 35% / 67% / 100%<br>Range: 120 u / 180 u / 255 u / 330 u | L10: Beams pierce<br>L15: Beams on one vessel merge into one big beam | Kill fauna | - |
| 13 | **Overcharge** | Burst | Elemental crystals | Each crystal collected charges the swarm. At full charge it lets out a blast that kills fauna round it and silences vessels. | Crystals to full: 10 / 5 / 3 / 1<br>Blast radius: 100 u / 180 u / 280 u / 380 u | - | Kill fauna and silence | Capacitor |
| 14 | **Spark Trail** | Trap | Opposing vessels | The swarm leaves a crackling trail. An enemy that crosses it is slowed and its abilities sputter. | Trail lasts: 2.0 s / 4.0 s / 6.5 s / 9.0 s<br>Slow: 10% / 22% / 37% / 52% | - | Silence | Live current |
| 15 | **Kamikaze** | Burst | Opposing vessels | Members that hit an enemy vessel or fauna explode in a spark that kills fauna and silences a vessel's Charge and Space abilities. Members never explode below the swarm's minimum size. | Blast radius: 20 u / 40 u / 65 u / 90 u<br>Chance the member lives: 0% / 20% / 45% / 70% | - | Silence and kill fauna | - |
| 16 | **Sting** | Weapon | Opposing vessels | Members within reach dive at enemy vessels. Enough stings take a petal. | Stings per petal: 12 / 6 / 3 / 1<br>Reach: 80 u / 120 u / 170 u / 220 u | - | - | - |
| 17 | **Wasp** | Form | Opposing vessels | Every 20 s the swarm fuses into a giant wasp that lunges at the nearest enemy vessel with its stinger. | Lunge range: 200 u / 320 u / 470 u / 620 u | L1: Lunge takes 1 petal<br>L8: Lunge takes 2 petals<br>L15: Lunge takes 3 petals | - | - |
| 18 | **Missiles** | Weapon | Opposing vessels | Every few seconds members launch as homing missiles at enemy vessels. | Missiles per volley: 1 / 3 / 5 / 8<br>Every: 8.0 s / 6.5 s / 5.0 s / 3.9 s | - | - | - |
| 19 | **Frenzy** | Hunt | Opposing vessels | When an enemy vessel is under half its petals, the swarm smells it and hits harder near it. | Bonus: 10% / 30% / 55% / 80% | - | - | - |
| 20 | **Electroreception** | Hunt | Any | Like sharks, members sense the electric fields of living things and hunt fauna anywhere in range, killing it. | Sensing range: 100 u / 200 u / 325 u / 450 u<br>Members hunting: 10% / 22% / 37% / 52% | L10: A killed fauna drops an elemental crystal | Kill fauna | Bioelectric fields |

## What changed since revision 2

| Card | Change |
|---|---|
| Mass: **Bulk Up** | No stuns: its L10 shoved vessels aside; it now takes a third of a petal on a ram. |
| Mass: **Big Family** | Also raises the new minimum size, below which nothing can harm the swarm. |
| Time: **Agility** | Was Reflexes, which settled the swarm onto your turned plane. You no longer steer, so it now sharpens the swarm's own turns. |
| Time: **Lead Time** | Its L10 led your plane turns; it now leads the phase change. |
| Time: **Save Point** | It fired when the swarm was nearly wiped; with the minimum size the swarm cannot be wiped, so it fires at the floor. |
| Time: **Metronome** | Its L10 followed your plane flicks; it now fires on each phase change. |
| Time: **Synchrony** | No stuns: the flash no longer blinds enemies. |
| Time: **Hurry Up** | Was Second Look (reroll a hand). A reroll adds a choice with stakes; you asked for no tension when picking. |
| Space: **Meteor Drop** | Now climbs above the swarm instead of leaving your plane. |
| Space: **Shockwave** | No stuns: the ring no longer shoves vessels. |
| Space: **Spaghettify** | New. Replaces Depth Dive: the swarm already roams the whole cell, so leaving the plane meant nothing. |
| Space: **Mirror Swarm** | It reflected across your camera's centre line; the camera no longer touches the swarm, so it reflects through the centroid. |
| Space: **Split** | Extra swarms follow the phase cycle instead of the toggle. |
| Space: **Wide Lens** | Reworded for the free view. |
| Charge: **Plasma Net** | No stuns: crossing a strand silences abilities instead of stunning the vessel. |
| Charge: **Thorns** | No stuns: the shock silences abilities and the vessel keeps flying. |
| Charge: **Polarity** | No stuns: it shoved vessels off their path, taking control from the pilot. The field now pulls crystals, not hulls. |
| Charge: **Kamikaze** | Stops at the minimum size. |
| Charge: **Electroreception** | New. Replaces Mind Control: a hijack takes the pilot's control away, which the fun economy rules out. |
