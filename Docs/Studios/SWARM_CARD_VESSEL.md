# Swarm Card Vessel: the 80 cards (revision 1, 2026-10-20)

**Review page:** https://claude.ai/artifact/QB2oYkL8P8Cv7KPGyhdHp2 (filter by element or objective, drag the level slider, mark each card Keep / Tweak / Cut with a note; the next revision reads those marks).
**Data:** `Docs/Studios/SwarmCardVessel/cards.py` is the single source; `python3 Docs/Studios/SwarmCardVessel/build.py` writes this file, `cards.json` and the page (`Docs/Studios/SwarmDeck.html`).
**Status:** design list only. Nothing is in the lab or in Unity yet. Garrett reviews this list, then revision 2.

## The vessel (Garrett's brief, 2026-10-20, captured as context; not built yet)

The second swarm vessel after the Shepherd ([SWARM_VESSEL_LAB.md](SWARM_VESSEL_LAB.md)): a **mobile-first, single-player** vessel that mixes card play with a roguelike run. The fun is a huge number of wacky ways to change the swarm: morph into a dragonfly, give every member a laser, split into two swarms.

- **The plane.** The swarm is pulled onto a 2D plane, like the Shepherd's flock. It is drawn the way the Wrasse draws 2.5D planar play.
- **Camera.** Centred on the swarm's approximate centroid, from a sample of members rather than all of them.
- **Plane control.** A touch on the map is a joystick that rotates the plane on either or both of its two relative rotation axes. The rotation resets after each turn, so each drag is a fresh relative turn.
- **Objective toggle.** The swarm travels in the plane toward one of: **Omnicrystal**, **Elemental crystals**, **Opposing vessels**, **Opposing mass**. Each card below names the objective it works best with.
- **Card draw.** Collecting an omnicrystal draws four cards, one per element. Playing one discards the other three. The next omnicrystal draws four new cards.
- **Levels.** Each element has 20 cards, and each card goes from level 1 to 15. Upgrading an element means the cards you draw for it already arrive at that level. Drawing a card you already have adds its level to the one you hold.

## How to read the scaling

- Every card has one or two numbers. Each is linear (`a + b × (level − 1)`) or a steady percentage step (`a × r^(level − 1)`), so the curve from 1 to 15 has no jumps. Counts round down.
- **Milestones** are the roguelike jolts: at some levels a card changes what it does, not only how much. Levels 5, 10 and 15 carry most of them, matching the fleet's level-5 upgrades.
- **Forms** (Dragonfly, Golem, Snowball, Hummingbird, Manta, Whale, Wasp, Thunderhead, Dragon, Neutron Star) are the swarm turning into one creature for a few seconds on a fixed cooldown.
- Baselines assumed by the numbers: a swarm of about 24 members with a cap of 40, cruising at about 60 u/s, and a 1,400 u view (the Shepherd's). Revision 2 can tune these once the list itself is settled.

## Questions for Garrett (readings this revision chose)

1. **"Upgrade an element"** is read as a between-runs upgrade (roguelike meta progress), so a level-6 Time unlock makes every Time card you draw arrive at level 6. Or is it earned inside a run, for example from elemental crystals?
2. **Over the cap.** When a duplicate would push a card past 15, the extra levels are lost here. They could convert into something instead (a reroll, members).
3. **Forms.** Can the swarm hold two forms (say Dragonfly and Wasp)? This revision assumes they take turns, one form at a time.
4. **Passive or played?** Every card here works by itself once picked; the player only steers the plane and the objective. Should some cards be tap-to-fire instead?

## Mass (20 cards)

*Bodies, building and weight: the swarm grows, breeds, lays your prisms and turns into heavy things.*

| # | Card | Kind | Best with | What it does | Scales: L1 / L5 / L10 / L15 | Milestones |
|---|---|---|---|---|---|---|
| 1 | **Brood** | Growth | Any | The swarm breeds: a new member hatches at the centroid every few seconds, up to the swarm cap. | Hatch every: 6.0 s / 3.6 s / 1.9 s / 1.0 s | L10: Hatchlings arrive full size<br>L15: Twins: two members per hatch |
| 2 | **Big Family** | Growth | Any | Raises the swarm cap. | Swarm cap: 46 / 70 / 100 / 130 | L10: Members over the cap wait in a reserve and refill losses at once |
| 3 | **Bulk Up** | Body | Opposing mass | Members grow bigger and heavier. Bigger members hit harder and take more to kill. | Member size: 1.05× / 1.25× / 1.50× / 1.75×<br>Toughness: 1.0× / 1.8× / 2.8× / 3.8× | L10: Heavy members shove enemy prisms aside instead of bouncing off |
| 4 | **Mound Builders** | Build | Any | The swarm lays your team's prisms along its path. | Prisms laid: 0.50/s / 1.50/s / 2.75/s / 4.00/s | L5: Laid prisms join into a solid wall with no gaps<br>L15: Laid prisms are shielded |
| 5 | **Snowball** | Form | Opposing mass | Every 20 s the swarm rolls into one ball for a few seconds. It rolls up every enemy prism it touches and grows with each one. | Ball lasts: 3.0 s / 5.0 s / 7.5 s / 10.0 s<br>Prisms it can hold: 10 / 50 / 100 / 150 | L10: When the ball breaks, the rolled-up prisms land as yours |
| 6 | **Golem** | Form | Opposing vessels | Every 25 s the swarm stacks into a giant walking golem. Each stomp bursts prisms and knocks vessels back in a ring. | Golem lasts: 4.0 s / 6.0 s / 8.5 s / 11.0 s<br>Stomp radius: 60 u / 100 u / 150 u / 200 u | L15: The golem takes no damage |
| 7 | **Gravity Well** | Pull | Elemental crystals | The centroid pulls loose crystals and enemy prisms toward the swarm. | Pull radius: 50 u / 110 u / 185 u / 260 u | L10: Also drags enemy vessels (25% slow inside the well) |
| 8 | **Mitosis Feast** | Growth | Opposing mass | A member that eats enough enemy prisms splits into two. | Prisms per split: 8 / 5 / 3 / 1 | - |
| 9 | **Carapace** | Defense | Opposing vessels | Members grow shells. A shell soaks one hit and regrows. | Shell regrows in: 12.0 s / 7.2 s / 3.8 s / 2.0 s | L8: Two shells per member<br>L15: Three shells per member |
| 10 | **Pack Mules** | Economy | Elemental crystals | Elemental crystals the swarm carries home count for more element. | Extra element per crystal: 10% / 50% / 100% / 150% | - |
| 11 | **Mass Tug** | Hook | Opposing vessels | Members latch onto enemy vessels and weigh them down. | Most slow on one vessel: 15% / 31% / 51% / 71% | L10: A weighed-down vessel drops a petal every 5 s |
| 12 | **Martyr Bricks** | Build | Any | A member that dies leaves a small block of your prisms where it fell. | Prisms per death: 1 / 3 / 5 / 8 | - |
| 13 | **Wall Weavers** | Build | Opposing vessels | Every few seconds the swarm drops a straight wall of your prisms across its wake to block chasers. | Wall length: 4 prisms / 8 prisms / 13 prisms / 18 prisms<br>Every: 10.0 s / 8.1 s / 6.3 s / 4.9 s | - |
| 14 | **Fortress** | Build | Omnicrystal | When the swarm reaches its objective and holds there, it builds a ring of shielded prisms round itself. | Ring size: 8 prisms / 16 prisms / 26 prisms / 36 prisms<br>Build time: 4.0 s / 2.6 s / 1.5 s / 0.9 s | - |
| 15 | **Stampede** | Charge-in | Opposing mass | Against enemy mass the swarm lines up and charges. Each member flattens the prisms in its lane. | Prisms per member per charge: 1 / 3 / 5 / 8 | L10: The charge also knocks vessels out of the lane |
| 16 | **Clutch** | Growth | Elemental crystals | Each crystal the swarm collects lays eggs that hatch a few seconds later. | Eggs per crystal: 1 / 3 / 5 / 8 | L10: Eggs hatch at once |
| 17 | **Neutron Star** | Form | Opposing mass | Every 30 s the swarm crushes into one dense star that ploughs through anything and cannot be hurt. | Star lasts: 1.5 s / 2.9 s / 4.7 s / 6.4 s | - |
| 18 | **Queen** | Growth | Any | One member is crowned. While she lives the swarm breeds faster and members near her are tougher. If she falls, the crown passes after a delay. | Breeding bonus: 20% / 60% / 110% / 160%<br>Crown passes in: 15.0 s / 7.8 s / 3.5 s / 1.5 s | L15: Two queens |
| 19 | **Sticky Tar** | Trap | Opposing vessels | Members drip tar prisms. An enemy that flies through tar is slowed. | Tar slow: 10% / 22% / 37% / 52% | - |
| 20 | **Meteor Drop** | Slam | Opposing mass | Every few seconds one member leaves the plane, climbs, and slams down on the objective, bursting prisms round where it lands. | Every: 8.0 s / 5.7 s / 3.8 s / 2.5 s<br>Blast radius: 30 u / 54 u / 84 u / 114 u | L10: Two meteors at a time |

## Time (20 cards)

*Speed, tempo and second chances: the swarm moves faster, rewinds, repeats and bends time round it.*

| # | Card | Kind | Best with | What it does | Scales: L1 / L5 / L10 / L15 | Milestones |
|---|---|---|---|---|---|---|
| 1 | **Dragonfly** | Form | Elemental crystals | Every 15 s the swarm morphs into a giant dragonfly and darts at the objective in fast straight hops, collecting everything it passes. | Dart speed: 2.00× / 2.60× / 3.35× / 4.10×<br>Dragonfly lasts: 3.0 s / 4.6 s / 6.6 s / 8.6 s | L10: Each hop leaves a slipstream lane |
| 2 | **Quickening** | Speed | Any | The swarm cruises faster. | Cruise speed: 4% / 20% / 40% / 60% | - |
| 3 | **Rewind** | Survive | Opposing vessels | A member that is killed may rewind 2 s and come back. | Chance: 10% / 30% / 55% / 80% | - |
| 4 | **Echo Swarm** | Copy | Any | A ghost of the swarm replays its path a moment behind it. Ghosts collect crystals and hit like the real swarm. | Ghost strength: 20% / 36% / 56% / 76% | L8: Two ghosts<br>L15: Three ghosts |
| 5 | **Second Look** | Cards | Omnicrystal | Reroll a hand of four cards. Rerolls recharge as you collect omnicrystals. | Omnicrystals per reroll: 5 / 3 / 1 / 1 | L15: Keep two cards from a hand instead of one |
| 6 | **Slipstream** | Speed | Any | Members drafting behind other members fly faster, so a long thin swarm is a fast one. | Draft bonus: 5% / 17% / 32% / 47% | - |
| 7 | **Stopwatch** | Field | Opposing vessels | Enemy vessels and shots near the swarm run slow. | Slow: 10% / 22% / 37% / 52%<br>Radius: 80 u / 120 u / 170 u / 220 u | - |
| 8 | **Hourglass** | Freeze | Any | Every so often time stops for everything except your swarm. | Freeze: 0.30 s / 0.90 s / 1.65 s / 2.40 s<br>Every: 25.0 s / 19.5 s / 14.3 s / 10.5 s | - |
| 9 | **Metronome** | Rhythm | Opposing mass | The swarm surges on a beat. On each beat members hit and collect for more. | Beat bonus: 1.5× / 1.9× / 2.4× / 2.9× | L10: The beat speeds up as the swarm grows |
| 10 | **Fast Forward** | Speed | Any | Every 20 s the swarm runs on double time (speed, breeding, cooldowns) for a few seconds. | Double time lasts: 2.0 s / 3.6 s / 5.6 s / 7.6 s | - |
| 11 | **Rapid Molt** | Cooldown | Any | Every card with a cooldown recharges faster. | Cooldown cut: 3% / 15% / 30% / 45% | - |
| 12 | **Hummingbird** | Form | Elemental crystals | At the objective the swarm becomes a hummingbird that hovers and sips. Crystals and prisms within reach are drawn in fast. | Sip radius: 60 u / 92 u / 132 u / 172 u<br>Sips: 2.0/s / 4.0/s / 6.5/s / 9.0/s | - |
| 13 | **Mayfly Couriers** | Fetch | Elemental crystals | Short-lived mayflies hatch, race to the nearest crystals and bring them back. | Couriers: 1 / 3 / 5 / 8<br>Courier speed: 150 u/s / 210 u/s / 285 u/s / 360 u/s | - |
| 14 | **Déjà Vu** | Economy | Elemental crystals | A collected crystal may count twice. | Chance: 5% / 17% / 32% / 47% | L15: Omnicrystals can repeat too (an extra hand) |
| 15 | **Skim Rush** | Speed | Opposing mass | Members that skim close past enemy prisms gain speed, like a vessel's skim. | Speed per skim: 3% / 7% / 12% / 17% | L10: Skims also refill card cooldowns a little |
| 16 | **Age Ray** | Breaker | Opposing mass | Enemy prisms the swarm touches age and crumble a moment later. | Crumble after: 6.0 s / 3.6 s / 1.9 s / 1.0 s | L10: A crumbling prism ages its neighbours |
| 17 | **Second Wind** | Survive | Any | When the swarm falls under a third of its cap, it surges: faster and breeding hard for a few seconds. | Surge lasts: 3.0 s / 5.0 s / 7.5 s / 10.0 s<br>Cooldown: 40 s / 29 s / 20 s / 14 s | - |
| 18 | **Save Point** | Survive | Any | Once per omnicrystal, if the swarm is nearly wiped, it rewinds to where it was and how big it was a few seconds ago. | Rewinds: 3.0 s / 5.0 s / 7.5 s / 10.0 s | L10: The rewind also returns the petals you lost |
| 19 | **Synchrony** | Rhythm | Opposing vessels | Members fall into sync like fireflies. Each synced flash blinds nearby enemies and bursts the swarm forward. | Flash every: 6.0 s / 4.3 s / 2.8 s / 1.9 s<br>Blind: 0.5 s / 0.9 s / 1.4 s / 1.9 s | - |
| 20 | **Reflexes** | Control | Any | The swarm settles onto a newly turned plane faster, so plane flicks feel snappy. | Settle time: 1.50 s / 0.86 s / 0.43 s / 0.21 s | - |

## Space (20 cards)

*Reach, shape and breaking: the swarm splits, spreads, teleports, eats through shields and bends the plane.*

| # | Card | Kind | Best with | What it does | Scales: L1 / L5 / L10 / L15 | Milestones |
|---|---|---|---|---|---|---|
| 1 | **Split** | Split | Any | The swarm splits. Each extra swarm chases the next objective on the toggle, and the camera frames them all. | Swarms: 2 for 8 s, every 30 s / 2, for good / 3 / 4 (one per objective) | L5: The split is permanent<br>L10: Three swarms<br>L15: Four swarms, one per objective |
| 2 | **Wide Spread** | Reach | Opposing mass | Members spread out and cover more ground. | Swarm radius: 106% / 130% / 160% / 190% | - |
| 3 | **Portal Hop** | Teleport | Omnicrystal | Every few seconds a portal opens at the objective and the swarm pours through it. | Cooldown: 24.0 s / 15.1 s / 8.4 s / 4.7 s<br>Range: 300 u / 500 u / 750 u / 1000 u | - |
| 4 | **Phase Fold** | Pass | Any | Every 15 s the swarm folds out of phase and passes through prisms and vessels. | Fold lasts: 1.0 s / 2.2 s / 3.7 s / 5.2 s | - |
| 5 | **Shield Eaters** | Breaker | Opposing mass | Members can bite shielded prisms. | Bites per shield: 6 / 3 / 1 / 1 | L10: Eaten shields come back as your shielded prisms |
| 6 | **Hole Punch** | Breaker | Opposing mass | Each member bursts enemy prisms it flies through. | Prisms per member: 0.2/s / 0.6/s / 1.1/s / 1.6/s | - |
| 7 | **Shockwave** | Burst | Opposing vessels | Every 10 s the swarm flares into an expanding ring that shoves vessels and bursts prisms. | Ring radius: 80 u / 140 u / 215 u / 290 u<br>Shove: 50 u/s / 110 u/s / 185 u/s / 260 u/s | - |
| 8 | **Murmuration** | Decoy | Opposing vessels | The swarm swirls a huge shifting ghost cloud round itself. Enemy aim and AI lock onto the cloud instead of the swarm. | Shots fooled: 10% / 26% / 46% / 66% | - |
| 9 | **Manta Sheet** | Form | Opposing mass | Every 20 s the swarm flattens into one wide manta that sweeps the plane, bursting every prism under its wings. | Wingspan: 150 u / 250 u / 375 u / 500 u<br>Manta lasts: 3.0 s / 4.2 s / 5.7 s / 7.2 s | - |
| 10 | **Orbit Guard** | Defense | Opposing vessels | A share of the members orbit the centroid as a spinning ring that blocks shots. | Members on guard: 10% / 18% / 28% / 38%<br>Ring radius: 60 u / 84 u / 114 u / 144 u | - |
| 11 | **Depth Dive** | 2.5D | Elemental crystals | Members may leave the plane to reach crystals and prisms above and below it. | Reach off the plane: 40 u / 100 u / 175 u / 250 u | - |
| 12 | **Scouts** | Vision | Elemental crystals | Scouts fly ahead and mark crystals and enemies on the map. | Reveal radius: 300 u / 500 u / 750 u / 1000 u | L10: Marked crystals pull the swarm a little even off-objective |
| 13 | **Wide Lens** | Camera | Any | The camera pulls back so you see more of the plane. | View span: 1460 u / 1700 u / 2000 u / 2300 u | L10: An edge arrow points to the objective |
| 14 | **Shrink Ray** | Weird | Opposing vessels | Enemy vessels the swarm touches shrink: smaller hull, shorter trail, slower. | Shrink: 5% / 15% / 27% / 40%<br>Lasts: 2.0 s / 4.0 s / 6.5 s / 9.0 s | - |
| 15 | **Mirror Swarm** | Copy | Any | A mirror-image swarm flies reflected across the camera's centre line, and it collects and hits for real. | Mirror strength: 25% / 45% / 70% / 95% | - |
| 16 | **Slingshot** | Launch | Omnicrystal | When the objective is far, the swarm draws back and flings itself at it. | Fling distance: 200 u / 360 u / 560 u / 760 u<br>Cooldown: 12.0 s / 9.0 s / 6.2 s / 4.3 s | - |
| 17 | **Whale Gulp** | Form | Opposing mass | Every 20 s the swarm becomes a whale mouth and gulps a cone of prisms in one bite. Gulped prisms feed breeding. | Cone length: 120 u / 200 u / 300 u / 400 u<br>Most prisms per gulp: 10 / 34 / 64 / 94 | - |
| 18 | **Black Hole** | Ultimate | Any | Once per omnicrystal the swarm collapses into a black hole that pulls in and eats everything near it. | Black hole lasts: 2.0 s / 3.6 s / 5.6 s / 7.6 s<br>Pull radius: 150 u / 250 u / 375 u / 500 u | - |
| 19 | **Blink Dodge** | Defense | Opposing vessels | A member about to be hit blinks a short hop aside. | Chance: 10% / 26% / 46% / 66% | - |
| 20 | **Claim Zone** | Territory | Omnicrystal | Where the swarm lingers it claims a zone. Enemy prisms inside decay and yours grow. | Zone radius: 100 u / 180 u / 280 u / 380 u<br>Decay: 1.0 prisms/s / 3.0 prisms/s / 5.5 prisms/s / 8.0 prisms/s | - |

## Charge (20 cards)

*Fighting and petals: the swarm shoots, stings, shocks and turns into weapons.*

| # | Card | Kind | Best with | What it does | Scales: L1 / L5 / L10 / L15 | Milestones |
|---|---|---|---|---|---|---|
| 1 | **Lasers** | Weapon | Opposing vessels | Members fire lasers at the nearest enemy prism or vessel. | Members armed: 9% / 35% / 67% / 100%<br>Range: 120 u / 180 u / 255 u / 330 u | L10: Beams pierce<br>L15: Beams on one vessel merge into one big beam |
| 2 | **Danger Swarm** | Danger | Opposing vessels | A share of members are danger prisms. A rival that touches one is slowed and loses a petal. | Danger members: 7% / 33% / 66% / 100% | - |
| 3 | **Chain Lightning** | Weapon | Opposing mass | Every hit arcs on to nearby targets. | Jumps: 1 / 3 / 5 / 8 | - |
| 4 | **Sting** | Weapon | Opposing vessels | Members within reach dive at enemy vessels. Enough stings take a petal. | Stings per petal: 12 / 6 / 3 / 1<br>Reach: 80 u / 120 u / 170 u / 220 u | - |
| 5 | **Kamikaze** | Burst | Opposing mass | Members that hit an enemy explode, bursting prisms round them. | Blast radius: 20 u / 40 u / 65 u / 90 u<br>Chance the member lives: 0% / 20% / 45% / 70% | - |
| 6 | **Plasma Net** | Trap | Opposing vessels | Members link into a plasma net. An enemy that crosses a strand is stunned. | Stun: 0.3 s / 0.7 s / 1.2 s / 1.7 s<br>Strand length: 40 u / 72 u / 112 u / 152 u | - |
| 7 | **Wasp** | Form | Opposing vessels | Every 20 s the swarm fuses into a giant wasp that lunges at the nearest enemy vessel with its stinger. | Lunge range: 200 u / 320 u / 470 u / 620 u | L1: Lunge takes 1 petal<br>L8: Lunge takes 2 petals<br>L15: Lunge takes 3 petals |
| 8 | **Static Shield** | Defense | Opposing vessels | Hits on the swarm are soaked up as static, and the stored charge powers your other Charge cards. | Hits soaked: 10% / 26% / 46% / 66% | - |
| 9 | **Tesla Coil** | Weapon | Opposing mass | Every few seconds the centroid zaps the nearest enemy prisms. | Zaps per pulse: 1 / 3 / 5 / 8<br>Every: 3.0 s / 2.2 s / 1.6 s / 1.1 s | - |
| 10 | **Polarity** | Push | Opposing vessels | The swarm repels enemy vessels like a matching magnet pole, shoving them off its path. | Push radius: 60 u / 100 u / 150 u / 200 u | L10: Repelled vessels drop crystals |
| 11 | **Frenzy** | Hunt | Opposing vessels | When an enemy vessel is under half its petals, the swarm smells it and gets faster and harder-hitting near it. | Bonus: 10% / 30% / 55% / 80% | - |
| 12 | **Spark Trail** | Trap | Opposing vessels | The swarm leaves a crackling trail. An enemy that crosses it is slowed. | Trail lasts: 2.0 s / 4.0 s / 6.5 s / 9.0 s<br>Slow: 10% / 22% / 37% / 52% | - |
| 13 | **Missiles** | Weapon | Opposing vessels | Every few seconds members launch as homing missiles at enemy vessels. | Missiles per volley: 1 / 3 / 5 / 8<br>Every: 8.0 s / 6.5 s / 5.0 s / 3.9 s | - |
| 14 | **Overcharge** | Burst | Elemental crystals | Each crystal collected charges the swarm. At full charge it lets out a big blast round the centroid. | Crystals to full: 10 / 5 / 3 / 1<br>Blast radius: 100 u / 180 u / 280 u / 380 u | - |
| 15 | **Mind Control** | Weird | Opposing vessels | A vessel swarmed by enough members is hijacked and flies where your plane points for a moment. | Hijack lasts: 0.5 s / 1.3 s / 2.3 s / 3.3 s<br>Members needed: 20 / 14 / 9 / 6 | - |
| 16 | **Thunderhead** | Form | Opposing mass | Every 25 s the swarm puffs into a storm cloud that rains lightning on everything under it. | Cloud lasts: 3.0 s / 4.6 s / 6.6 s / 8.6 s<br>Strikes: 2.0/s / 4.4/s / 7.4/s / 10.4/s | - |
| 17 | **Bounty** | Economy | Opposing vessels | Each petal the swarm takes hatches new members. | Members per petal: 1 / 3 / 5 / 8 | L10: A knockout drops an omnicrystal |
| 18 | **EMP** | Disable | Opposing vessels | Every 20 s the swarm pulses an EMP that switches off enemy abilities in range. | Abilities off for: 0.5 s / 1.3 s / 2.3 s / 3.3 s<br>Radius: 120 u / 180 u / 255 u / 330 u | - |
| 19 | **Thorns** | Defense | Opposing vessels | An enemy that kills a member may get a shock back that stuns it for half a second. | Chance: 5% / 25% / 50% / 75% | - |
| 20 | **Dragon** | Form | Opposing mass | Every 25 s the swarm becomes a long serpent dragon that breathes a cone of plasma, bursting prisms and scorching vessels. | Breath length: 100 u / 180 u / 280 u / 380 u<br>Dragon lasts: 3.0 s / 4.6 s / 6.6 s / 8.6 s | L15: The breath takes a petal a second |
