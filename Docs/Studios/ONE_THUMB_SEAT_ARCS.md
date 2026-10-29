# The Ratel and the Wrasse: a whole arc for the one-thumb seats

**Proposal, 2026-10-29. Nothing here is decided or built.** Both seats are Garrett's: `who_works_where.py` shows no
other work in flight on `DynamoCore.cs` or `KiteNavigatorCore.cs`. The records these build on are
`Assets/_Scripts/Controller/Vessel/Crew/HONEYGUIDE.md` (the Ratel) and
`Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md` (the Wrasse).

Garrett, 2026-10-29:

> "every vessel must be fun the thousandth time. it should be main worthy. you should get lost in it and drawn to
> return. [...] the wrasse has three fun things to do: attack vessels, prisms, and lasso elements. but the constant
> looping gets very repetitive. [...] the ratel has the same problem. you can't escape the time pressure of fiddling
> with your next three moves. it is a fun puzzle but it needs something to break up the repetition of pure pattern
> building. they are both elegant in that they can be played on older mobile devices in portrait mode with one thumb
> and still contribute to a squad. i want to retain those goals while completing the experience to address their
> repetitiveness with a more well rounded arc to their gameplay loop."

## 1. What the fun loops share

Garrett's six examples, read as one table:

| Vessel | Build | Tension | Release | Rest and choose | What changes each time |
|---|---|---|---|---|---|
| Dolphin | skim | lining up the crystal | the shot at mass, fauna or a vessel | picking the next target | the targets the world offers |
| Pistol | riding the Goby's path | how far to ride before resting | the den grows by the distance ridden | rest at the den, pick a path | the islands the Goby lays |
| Squirrel | speed | pressing its luck | the collision | the restart | the course |
| Thresher | slicing | how big before it blows | the explosion | — | what it sliced |
| Tigermoth | dust | running out | mass made | the drop, to relocate | where it drops |
| Scarab | getting the ball | carrying it through | the goal | the next start | the environment |

Every one has a **peak and a valley**. It builds something, risks it, releases it, and then gets a breath and a
choice. The release lands in the world, so the world after it is different and the next cycle starts somewhere new.
That is why the loop feels like returning to the fun, not repeating it.

The two one-thumb seats, against the same columns:

| Seat | Build | Tension | Release | Rest and choose | What changes each time |
|---|---|---|---|---|---|
| **Wrasse** | each loop (2-3 motes) | none: a loop fires the moment it closes, and a wrong one only fizzles | instant and small, mostly on the pilot (boosts, a cluster round the loop) | none: the lab measured the thumb moving 2179 mm a minute on the shipped Sling (`WRASSE_LASSO_LAB.md`) | little: motes respawn evenly over the board, weighted by the hull's element levels |
| **Ratel** | patterns | constant: a 15 s spin, and `apBank` 3 makes every spin's AP use-it-or-lose-it | a charge goes to the battery for the pilot; the Ratel never sees its payoff land | 5.5 s every 4 patterns, showing a random draw (nothing to choose) | nothing: the board knows nothing of the world |

Both are flat lines: the same intensity and the same size of decision, every 15 seconds. Neither is broken. Each is
missing a peak, a valley and a world.

## 2. The Wrasse: Hunt, Haul, Sling, Clean

A cleaner wrasse keeps a cleaning station, and big fish come to it to be cleaned. The arc takes that.

```
   CLEAN (docked, thumb off the glass, read the board)
     │  drag out and let go
     ▼
   HUNT (pick a shoal) ──► HAUL (loop, loop, loop: heavier, riskier) ──► SLING (throw it where it counts)
     ▲                                                                       │
     └──────────────────────────── fly back into the Grouper ◄───────────────┘
```

### Hunt: shoals, not scattered motes

Motes come as **shoals** that move like their element. They already animate like it (round 4f); this makes them
behave like it too:

- **Time** shoals dart and scatter when the kite comes close: tight, quick loops.
- **Mass** shoals are big, slow and drift together: wide, easy loops and heavy hauls.
- **Space** shoals blink across the board in short hops: loop where they are going to be.
- **Charge** shoals shy from the kite and drift toward rivals: they are caught near danger.

Where they gather comes from the plane: a shoal forms round the real things the board's plane cuts (flora of that
element, crystals, fauna), and open water holds few. So the Grouper's flight picks the fishing ground, the two seats
plan together, and no two boards look alike. The element weights by the hull's levels stay.

### Haul: press your luck

A closed loop no longer fires. It **cinches**: its motes become a **haul** carried at the end of the tail.

- Each further loop of the same element adds to the haul. The growth stays `base × 2^(n−1)`, but n counts the whole
  haul, not one loop, so a haul of 6 is worth far more than two of 3.
- A pure loop of another element **braids** into the haul. A mixed loop still fizzles.
- **The risk**: the haul weighs the kite down. On the Sling's own physics it is more mass on the rope, so the kite
  swings wider and slower and is harder to place. A rival's hull, a danger prism, or the kite's own head crossing the
  haul **spills** it: the motes scatter back onto the board as a loose shoal, to be caught again. Spilt, not lost.

### Sling: the release, aimed

Lift the thumb while the kite is swinging fast (above a `slingThrowSpeed`) and the haul flies off along the swing and
lands where it lands. A slow lift lets the kite drift on with the haul, as today.

| Haul | Where it lands |
|---|---|
| Space | blasts there (today it blasts at the loop's centre). Throw it into a rival wall. |
| Mass | the cluster grows there: a wall across a rival's line, cover round an ally, a lane for the pilot. |
| Time | thrown into the Grouper (the board's centre), it banks boosts, as a Time loop does today. |
| Charge | is not thrown. A Charge haul is a **flail**: it hangs on the tail and rams whatever it swings through (a rival: the Strike gate and petals; a creature: jousted). The head's ram stays. |
| All four, braided | an **Omni sling**: every element at once, each stronger (×1.5 to start). |

The Wrasse now sees its own payoff land on its own board, and in the world.

### Clean: the rest, and the role

Fly the kite into the Grouper with no haul and it **docks**: it rides the hull and the thumb can leave the glass.
While docked it **cleans**: the Grouper's silence (`Docs/ElementalAbilitySystem/SILENCE.md`) runs down faster. A loop
of the tail round an ally's shadow on the board cleans that ally too. That is the cleaner fish's mutualism as a squad
role: after a fight, the squad wants a Wrasse.

Leaving the dock is a sling: drag out and let go, and the kite leaves along the drag. That throw is the first move of
the next hunt.

**One cycle runs about 20 to 40 seconds**: rest and read the board, launch at the shoal you picked, loop until you
dare not, throw, come home.

### What stays

- One thumb, no click, portrait. The only "button" is lifting the thumb, which already means let go.
- Very Low: a haul is a few of the mote meshes already drawn, a shoal is the same motes with a steering rule, and no
  camera is added.
- The head's pop and ram: the always-there verb, as the Dolphin's skim is.
- Wrasse Run: the kite still threads the rings.
- The L5s each still apply to their element; they now apply to its sling.

## 3. The Ratel: Breath, Build, Raid, Feast

In the wild the honeyguide finds the hive and the ratel breaks it open. The Dynamo is the ratel's engine; the hive
is out in the world.

```
   BUILD (careful, slow: patterns → charges → slips)
     │  the Honeyguide finds a hive; the Ratel chooses when
     ▼
   RAID (fast, physical: chain the hive's real prisms, break them) ──► FEAST (clock stopped: place the honey, pick one of three)
     ▲                                                                       │
     └───────────────────────────────────────────────────────────────────────┘
```

### Breath: two numbers first

- **`apBank` 3 → 9.** A spin still gives 3 AP, but what is not spent is kept. Planning three moves every 15 seconds
  stops being a must: the Ratel may skip a spin to think, or save for one big turn. The risk is already in the game:
  the ports keep turning, so the board you saved for turns away.
- **A pick of three, not a random draw.** Every 4 patterns the clock already stops to show a new ability. Show three
  and let the Ratel pick one. The stop gets a decision, as the Pistol's den gets a path.

Both are config changes (`HoneyguideConfigSO`: `apBank`, and the draw), the cheapest step in the order of preference
(`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md`). They should be tried first.

### Build: as it is

Patterns, charges, slips, the 39 cards and the two triggers stay unchanged.

### Raid: the second game

The Honeyguide finds a **hive**: a hostile structure that is a surface or a volume
(`PrismscapeTopology.DimensionOf` at Surface or above; a trail or loose prisms are not a hive) within `hiveReach` of
the hull. A Peck that hits one marks it (the honeyguide's call), and a **Hive** card lights on the Ratel's board.

The Ratel taps it **when it chooses**: finish the build first, or go now. The board becomes the hive. The spin clock
stops, and the 37 cells fill with the hive's real prisms, projected onto the hex (each cell takes the domain of the
prism nearest its spot). The raid plays nothing like the build:

- **Fast and tactile, no AP.** Drag a chain through touching cells of one domain with the thumb. Letting go breaks
  the chain, and every prism in it shatters in the world, scored to the hull through the normal destruction path.
- **Long chains pay honey.** A chain of 3 or more gives a free prism of its domain for the build board; longer chains
  bank AP and, past 6, a charge (numbers to tune).
- **Stings.** A shielded cell takes two passes. A danger cell stings: a chain through it breaks. A super-shielded cell
  cannot be chained (super-shielded mass is never destroyed, `Docs/PRISM_MOTION.md`).
- **It ends** when the comb is cleared, after `raidSeconds` (12 to start), or when the Honeyguide flies out of
  `hiveReach`. So the pilot gets a new beat too: **hold the hive**, circling it and fending off rivals while the Ratel
  breaks it.

What the raid breaks is real hostile mass gone. The Ratel's squad contribution lands in the world, and the Ratel
watches it go: the comb crumbles on the board as the prisms break in the world.

### Feast: the rest and the pick

When the raid ends, the clock stays stopped. The honey rides the cursor until placed (as the Translator's free prisms
already do), and the Ratel picks one of three: a new ability, a new port engine, or a card swapped. Then the spin
restarts.

**One cycle runs a minute or two**: careful build, a hive found, the Ratel's choice of when, a loud fast raid, a calm
feast, and a richer build after it.

### What stays

- One thumb and portrait. The raid is a drag over the same board, canvas and cells.
- Very Low: the camera stays parked. The hive is one spatial query when the raid starts and one shatter per cell.
- The tabletop: the Dynamo, the 39 cards, the ports and the AP rules.
- The Ratel AI plays the raid as a greedy longest-chain search over 37 cells.

## 4. The seats feed each other

| From | To | What |
|---|---|---|
| Grouper | Wrasse | the plane it flies chooses the shoals |
| Wrasse | Grouper and squad | boosts, walls and blasts landed where aimed, cleaning |
| Honeyguide | Ratel | the hives it finds and holds |
| Ratel | Honeyguide | charges, trigger upgrades, and the hive cleared |

In the best loops each action leads into the next. Here each seat's release also starts the other seat's next beat.

## 5. How to find out, cheapest first

1. **Ratel breath** in the Honeyguide Lab (`Docs/Studios/HoneyguideLab.html`), round 7: `apBank` 9 and the pick of
   three. Measure patterns a minute, AP wasted and the **rest share** (seconds holding AP with no move made) with the
   AI, then play it on a phone. Then the asset (`author_honeyguide_assets.py` keeps lab and asset equal).
2. **Wrasse haul, sling and dock** in the Wrasse Lasso Lab (`Docs/Studios/WrasseLassoLab.html`), round 2. It models the
   board alone, which is enough for the haul, the sling, the shoals' movement and the dock. Add to its scorecard:
   thumb mm a minute (expected to fall), share of time docked, haul size at the throw, throws landed near their target
   and value a minute, against the shipped Sling.
3. **The raid** in the Honeyguide Lab, round 7b: hives as random comb layouts, the chain game, stings, the feast and
   the AI raid. Holding the hive belongs to the pilot, in Unity only.
4. **In Unity only**, once 1 to 3 are picked: shoals seeded by the world, cleaning silence, cleaning allies, and the
   hive as a real prismscape. Then `/artifact-to-unity` for each piece picked.

A lab cannot measure fun, but it can measure the shape. Plot the seat's intensity over a match (actions a second,
thumb speed). Today it is a flat line; the proposal should show peaks and valleys. A phone in Garrett's hand decides.

## 6. Decisions for Garrett

1. **Wrasse:** do loops cinch into a haul that fires when thrown (this proposal), or keep firing on close and add the
   haul only as a bonus?
2. **Wrasse cleaning:** is clearing silence (the Grouper's, and allies') the role you want? It shifts the balance in
   `SILENCE.md`: a squad with a Wrasse recovers faster. Cleaning allies, and Time thrown to allies, need the ally's
   owner to apply it (an RPC); the first cut could be the Grouper only.
3. **Ratel:** `apBank` 3 → 9 (or another number), and a pick of three instead of the random draw?
4. **Ratel raid:** is a hive a hostile surface or volume, as proposed, or something else (a rival's ark, flora)? Flora
   would be very on theme (honey), but breaking it kills lifeforms, which is ecology's to decide
   (`Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`). This proposal keeps the hive to hostile mass.
5. **Which goes to a lab first.**
