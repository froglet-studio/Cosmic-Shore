# The Gopher: the burrowing vessel (design studio, round 1, 2026-10-28)

**Studio:** `Docs/Studios/VesselStudio/gopher.html` (its own source of truth; no copy script), a page of the one Vessel
Studio artifact (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa). Test hook `window.__gopherStudio` (and `__lab`).
**Owner:** Yash (the Vessel Studio). **Status:** design only. There is no Gopher in the game, and none of its numbers
are in Unity.

## Where it came from

Yash, 2026-10-28, after riding the Meerkat's tree tunnel (`MEERKAT.md` rounds 13-15): *"I also want another version
where it's burrowing through a subterranean underground. Like a real burrowing animal ... the branches will be actually
a network of channel. Through the soil ... I should be able to choose which branch I wanna go. While navigating the
channels, in the tree I cannot navigate the other branches. It's just a one-way path. But in case of the burrowing, I
want the channels to be there ... it will not be just a single wormhole, it will more of be multiple networks of few
channels with two entrants in front, three four entrants in front and three-four on the other side, spread throughout a
big span of area. Where you can take one channel, your speed will increase and you will kind of slide into the channels
and with your left thumbstick ... if you are near any branch distributor, then you can choose left or right ... make that
a new vessel, name it something."*

- **The name:** the Gopher. Pocket gophers dig real multi-entrance burrow networks. The names in use were checked
  first: the Mole is Garrett's concept studio (`CONCEPT_FLEET.md`, a different mechanic: fast inside mass), and the
  honey badger is the Ratel half of the Honeyguide duo.
- **The page:** a copy of `meerkat.html` (round 15), with the single tunnel turned into a network. The Meerkat keeps
  its one-way tree tunnel.

## How it plays

- **One thumb flies**, as the Meerkat does: the left stick at full throttle. The right thumb is free for the burrow.
- **The reticle is the whole burrow RT would dig now**, as a wireframe `reticleAhead` u ahead of the nose:
  - `burrowEntrances` (3) mouths in front, spread `burrowEntranceSpan` (110 u). Yours is the one on your nose line (the
    middle one, the left of the two middle ones for an even count), drawn brighter.
  - `burrowForkLevels` (2) rows of forks. Every fork splits into a left and a right channel to the next row.
  - `burrowExits` (4) mouths on the far side, spread `burrowExitSpan` (320 u).
  - Depth: `burrowLengthMin` (260 u) with LT released, up to `burrowLengthMax` (760 u) with LT squeezed.
  - The right stick turns the whole burrow toward where it is pushed, up to `burrowBendDegrees` (70°) by the far side.
  - The channels rise and fall by `burrowRelief` (26 u), so the network has depth rather than lying flat. They are
    `burrowDiameter` (12 u) wide, flare to `burrowMouthDiameter` (24 u) at the mouths, and are dug rough
    (`burrowRoughness` 0.18).
- **RT digs it.** The burrow digs itself out from the entrances to the exits over `formSeconds` (0.6 s), and you dive
  into your entrance.
- **The slide:** your speed before the burrow × `rideEntryMultiplier` (1.2) going in, `rideMidMultiplier` (2.6) through
  the forks, and `rideExitMultiplier` (1.3) on the last channel. Each fork you take adds `forkBoost` (0.08 × your entry
  speed).
- **The forks:** riding, the left stick does not steer. Lean it left or right past `forkLean` (0.25) to take that
  channel at the next fork; a lean held at any time on the way to the fork counts. With no lean you take the straighter
  one.
  - `forkHintSeconds` (0.9 s) before a fork, the screen calls it out (◀ LEFT / RIGHT ▶) with the side you would take
    lit.
  - The channel you would take is also lit in the 3D.
  - Through the fork, the side you took flashes up.
- **Out of an exit**, you leave at your entry speed × `rideExitMultiplier`. The burrow stays `staySeconds` (10 s), then
  falls in over `collapseSeconds` (1 s). One burrow at a time: digging a new one fills in the last.
- **Everyone sees the dug channels:** the same wireframe as the reticle (rings every `burrowRingSpacing` u of
  `burrowRingSides` spindles, `burrowRibs` ribs) over a glass skin, in the digger's domain colour.

## The soil (everyone's, the rider's too)

- **A wall of earth round every channel:** `soilWall` (1.5) × its radius, as rough as the wireframe.
  - Like the Meerkat's walls, it is see-through face-on (`soilGlass`) and bright where it turns away (`soilRim`,
    `soilFresnel`).
  - Its colour is **layered strata**: loam, clay, sand and stone, one layer every `soilStrataThickness` (7 u). The
    layers lie level with the burrow's up, undulate gently and have dark seams between them (`soilStrata` 0.85).
  - It has grain and grit, with stones caught in it (`soilGrain`).
- **Stones:** `soilPebbles` (700) lit, faceted stones (`soilPebbleSize` 1.3 u) lie in the soil round the walls.
- **Roots:** `soilRoots` (30) roots hang down from above onto the channels.
- **Formed with the burrow:** all of it shows only where its channel has been dug, so the soil is dug out with the
  burrow and falls in with it.
- **Riding:** the strata stream past on the walls and the other channels show through them.
- **Placement is seeded** from where the burrow is, so every peer's soil matches.

## How it is built (for whoever takes it to Unity)

- **Each channel is its own tunnel**: a Meerkat pair with its own `wh`. Its centreline is a sampled Hermite curve
  (`chanPath`: it leaves and arrives along the burrow's spine, is evened out by length, and carries a twist-free frame).
  - `whAt`, `whFrame` and `whRadius` dispatch to it (`polyAt`, `polyFrame`, `chanRadius`).
  - So the Meerkat's drawing, glass, bent space, lens dents (mouths only) and collapse all work per channel.
- **The layout:** `netLayout` builds it in the hull's frame. The rows lie at 15-85% of the depth, their widths grow
  from the entrance span to the exit span, and the entrance on the nose line sits at x 0. Each fork links to the
  next-row node to its left and the one to its right, and nodes nothing reaches are dropped.
- **Into the world:** `netBuild` places it, the spine an arc of `burrowBendDegrees` × the right stick.
- **The ride:** `gateTransit` slides along the current channel. At its end it takes `forkPick` (the lean, else the
  straighter channel) and carries the hull's turn across the join (`q0`, `tIn`). The camera stays behind on the
  channel you came from until it is past the fork.
- **The AI is input-only:**
  - It digs when its ring is at least `aiMinDistance` (220 u) away and within reach of the burrow's turn.
  - It turns the burrow so the spine's chord points at the ring (twice the ring's angle off the nose) and digs it
    about as deep as the ring is far.
  - At each fork it leans toward the channel whose exits lie nearest the ring. A rookie (`aiNoise`) sometimes leans
    the wrong way.
- **Cost:**
  - A default burrow is 17 channels.
  - Wireframe: about 2,000 spindles in the shared instanced mesh (`BR_MAX` raised to 30,000).
  - Soil: one wall mesh per channel, about 700 vertices each, built once.
  - Stones: one instanced mesh, up to 3,000.
- **Not carried over:** the Meerkat's four-ability kit is on the page but off (lab-only, as on the Meerkat). Chain Dig
  is gone.

## Measured (round 1, headless)

- **The ride:** with the left stick at 0, it went entry → fork → the straighter channel → exit. Leaned right (+1), it
  took the right channel at both forks. Every channel and its soil digs out within the formation time, with 700 stones
  and 30 roots.
- **The AI level scorecard (seed 11), with the burrow against without (`aiBurrow` 0):**

  | Course | Easy | Medium | Hard | No burrow | Burrows (Hard) |
  |---|---|---|---|---|---|
  | 1 | 87.1 s | 81.3 s | 75.7 s | 110.9 s | 9 |
  | 2 | 79.4 s | 73.1 s | 70.9 s | 110.6 s | 9 |
  | 3 | 81.6 s | 78.8 s | 80.4 s | 122.0 s | 10 |
  | 4 | 86.7 s | 75.4 s | 75.5 s | 111.5 s | 11 |

  Every level finishes every course. The burrow saves about 30%. On course 3, Hard is slower than Medium: a finding,
  not tuned away.
- **The studio gate (`check_studio.cjs --hook __gopherStudio`):** all checks pass.

## Open

- Whether the Gopher keeps the Meerkat's kit, or gets its own four abilities (the `/vessel` §3 design gate).
- Whether a pilot may enter by another entrance than the one on the nose line, for example by aiming the right stick at
  it.
- A hull of its own (`/vessel-model`): the stand-in is the Stoat's model, stockier and short-tailed, with buck teeth and
  claws.
