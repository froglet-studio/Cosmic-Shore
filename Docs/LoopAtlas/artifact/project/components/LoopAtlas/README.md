# LoopAtlas

The whole product as one zoomable map of loops, on three layers. Use it to place a problem before discussing it: find the loop, read its tier, and ask that tier's question.

- **Layer** (Now, Planned, Proposed): what the game has, what the team decided, and the plan with every hole filled by an invention.
- **Map**: a ring is a loop. Inside it are the faster loops it is made of. Click a ring to step into it; the up arrow or Backspace steps out. Diamonds on a ring are its steps; a red diamond with a number counts its holes. Dashed arrows are crossings between places, dotted arrows are what feeds what.
- **Vessel**: every Round holds the picked vessel's role and kit. Change the vessel in the bar, or open *The fleet* > *The fleet's kits* and pick one there.
- **Timescales**: every loop on one clock, with how many passes it takes before the loop above takes its next step.
- **Drivers**: which driver works at which tier, and every hand-off from one loop to another.
- **Holes**: what is missing on this layer. On Proposed, each hole has a fill and the twelve proposals are listed.

The consumer provides nothing: `LoopAtlas.mount(element, {layer, view, vessel, select})`. The model is `LoopAtlas.model`. Keys: arrows move between sibling loops, Enter steps in, Backspace steps out, + and - zoom.
