# LoopAtlas

The whole product as one zoomable map of loops, on three layers. Use it to place a problem before discussing it: find the loop, read its tier, and ask that tier's question.

- **Layer** (Now, Planned, Proposed): what the game has, what the team decided, and the plan with every hole filled by an invention.
- **Map**: a ring is a loop. Inside it are the faster loops it is made of, and one card. Click a ring to step into it; the up arrow or Backspace steps out. Diamonds on a ring are its steps; a red diamond with a number counts its holes; a tag such as x3-6 is how many passes before the loop above takes its next step. Dashed arrows are crossings between places, dotted arrows are what feeds what.
- **The card**: everything about a loop is inside its ring. The card sits at the top of the ring, where the name is; a loop with nothing inside it is all card. Click the card, or press Read, to zoom into it: what the loop is on each layer, its steps, its drivers, where it leads, its holes and its sources. A card with more to say is smaller print, so you zoom further; click a box to bring it to reading size, and drag to read on. Underlined lines go to that loop.
- **Vessel**: every Round holds the picked vessel's role and kit. Change the vessel in the bar, or open *The fleet* > *The fleet's kits* and pick one there.
- **Timescales**, **Drivers**, **Holes**: the same loops as lists. A row opens that loop on the map.

This card cannot go full screen: the frame it runs in does not allow it. The same map as a full page is the Loop Atlas Map artifact (https://claude.ai/artifact/8XkvFKuTFeEpRoLfp8QQpw).

The consumer provides nothing: `LoopAtlas.mount(element, {layer, view, vessel, select, card})`. The model is `LoopAtlas.model`. Keys: arrows move between sibling loops, Enter steps in, Backspace steps out, I or Space reads the card, + and - zoom.
