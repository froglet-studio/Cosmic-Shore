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
  | Opposing vessels | Debuff a vessel (silence, slow, petal, stripped crystal, hijack) | 25 s |
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
