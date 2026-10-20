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
