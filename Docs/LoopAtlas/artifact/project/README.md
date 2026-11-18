The Loop Atlas is the shared picture of Cosmic Shore as loops inside loops: what a player does in a tenth of a second, in a round, in a sitting, and over months, and what moves them from one to the next. Use it to say where a problem sits before discussing it, and to check that a plan covers every tier.

## Read it

- Open **Components > LoopAtlas**. That card is the atlas. The other cards are its vocabulary.
- Pick a layer. **Now** is the game today. **Planned** is the stabilised version the team has decided on. **Proposed** is the plan with changes and with every hole filled by an invention.
- A ring is a loop. The rings inside it are the faster loops it is made of. Click to step in.
- Pick a vessel. Every Round holds that vessel's role and kit: its four elemental verbs, its omni crystal, its combos and its feel.

## Say it

- Name the tier first: **Pulse** (0.1 s to 3 s), **Verb** (2 s to 20 s), **Combo** (8 s to 60 s), **Role** (20 s to 3 min), **Round** (60 s to 6 min), **Session** (10 min to 45 min), **Arc** (1 d to 4 wk), **Era** (2 wk to 6 mo).
- A loop **steps up** when enough passes are done for the loop above to take its next step, and **steps down** into the loops it is made of. A change between loops at the same tier is a **crossing**.
- A **driver** is why a player stays in a loop. A **hand-off** is what moves them out of it.
- A **hole** is something a layer needs and does not have. On the Proposed layer a hole has a **fill**, which is an invention until someone accepts it.

## Draw it

- Draw a loop as a ring in its tier's token (`tier-0` to `tier-7`), with its steps as diamonds clockwise from the top.
- Use `planned` for what the plan has and the game lacks, `proposed` for what is only proposed, `hole` for a hole (always with a count or the word), and `selection` for the picked loop.
- Set names and buttons in `label` (Chakra Petch 600, buttons in capitals) and sentences in `read` (Aldrich). Text is `text` on `ground`, `ground-deep` or `surface`; `text-soft` only on `ground` and `ground-deep`.
- Cut corners with `sliver-small` and `sliver-large`. Nothing is rounded except a loop.
- Never give an element a colour. Mark an element with its name.

## Use it as data

- Agents read `assets/Model/loop-atlas.json`: tiers, drivers, hand-offs, every loop with its layers, holes and sources, every edge, every vessel's kit, and the proposals.
- Today it holds 38 loops above the kit on Now, 40 on Planned and 43 on Proposed, 24 vessels, 20 holes on Now and 18 in the plan.
- It is built from the repository by `Tools/Build/loop_atlas.py`. Change `Docs/LoopAtlas/loop_model.json`, never the files here.
