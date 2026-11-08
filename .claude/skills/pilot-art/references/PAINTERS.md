# Painters: where the pixels come from, what each costs, who approves

The kit never paints. It briefs, captures, mattes, frames, gates and wires. A painting comes from one of these,
always from `brief.py`'s prompt verbatim, asked for at 1024 x 1280 (portrait 4:5; the Figma tool treats the size as an
aspect hint, so `author.py` reads whatever size arrives) over the key colour the brief named.

## 1. The Figma connector's image models (what round 1 used)

The claude.ai Figma connector exposes `generate_image` (a text prompt into a Design file; models
`gpt-image-2.5-flare` (the default; round 1), `gpt-image-2.5-sunburst`, `gemini-3.1-flash-image`,
`gemini-3.1-flash-lite-image`). **It spends Figma AI credits on the destination file's plan**: say how many
paintings a round will make before making them, and name the file.

- **The file**: "Cosmic Shore Pilot Art" (key `BPjqBNIQgtY3L6RvghdWcg`, https://www.figma.com/design/BPjqBNIQgtY3L6RvghdWcg)
  in the **Froglet Studio** team (Garrett is admin on it and on his own team; the studio's plan was chosen on
  2026-11-07 because the game's art belongs with the studio). `whoami` first; `create_session` on the file;
  `generate_image(prompt, fileKey, width 1024, height 1280, model)`; every painting lands as a frame in the file,
  so the file is also the round's gallery.
- **Capturing the painting in a cloud session**: the result's `image.url` is on `www.figma.com`, which the cloud
  environment's network policy refuses (`curl` gets 403 from the proxy; LESSONS 4). Call `get_screenshot` on the
  returned `nodeId` with `enableBase64Response: true`, `maxDimension: 1280`, `contentsOnly: true`: the harness
  saves the inline PNG under the session's `tool-results/` folder as `mcp-Figma-blob-<...>.png` at the full
  1024 x 1280; copy it to `Docs/Pilots/art/sources/<name>.png`. (The thumbnail the generate call itself returns is
  a 205 x 256 JPEG: never use it.) On a desktop session with the host allowed, `curl -f -L -o` the URL instead.
- **What the models get right from the brief** (round 1, 6 paintings): the species, the lent feature, the faction
  gear, the exact accent hex as trim and seam lights, the callsign mark, a flat key with a 0.005 OKLab spread, the
  framing. **What they get wrong**: a dark animal in dark gear sinks (LESSONS 2); a prop in the hand sometimes
  replaces the hand on the belt; "no floor" holds, "no shadow" mostly holds.
- A second candidate for the same pilot is a second call with the same prompt; do not vary the prompt by hand -
  vary the spec's `pose` or `extra`, so the variation is recorded.

## 2. Weave models with REFERENCE images (for a style-matched repaint)

`weave_find_model` / `weave_run_model` (through the same connector) run image models that take reference images
(a few of the painted set as style anchors plus the brief). Every run needs the person's explicit approval of the
quoted credit cost in the session (the tool refuses without it), so this route is for an interactive session,
never an autonomous one. Use it when a pilot must match a specific painted reference's handling more closely than
the text brief achieves.

## 3. An artist

Hand the artist the brief (`brief.py <spec>`), the sheet of the nearest references, and this delivery spec:
1024 x 1280 or larger, portrait 4:5, the figure complete head to boots on a flat `#00FF00` (or the brief's key),
or as a PNG with its own alpha. Drop it in `Docs/Pilots/art/sources/<name>.png`, set `key="alpha"` for an alpha
delivery, and run `author.py <spec> --preview`: the artist's painting is framed, gated and sheeted exactly like a
model's, so the Hangar column stays one set. The 32 painted references were made this way before the kit existed
and are the envelope it measures against.

## 4. Not yet: a procedural painter

A parts-based procedural painter (SVG or canvas through the pre-installed Chromium, or PIL) would be free and
deterministic and could paint every unique pilot on demand, but nothing procedural reaches the painted set's
rendering. If it is ever built it plugs in as a source like any other (`key="alpha"`), and the gates and the sheet
judge it the same way. The per-person question (unlimited unique pilots, one PNG per person) is
`Docs/Pilots/PORTRAITS.md` section 6.

## 6. The stranger test (the blind reading that measures section 0's bar)

Round 2 (2026-11-07) ran it as a workflow over the fourteen sprites:

```
items: p01..p14 -> {spec, person, callsign, first, second, lend, faction, domain, accent, mark}, written to
       <dir>/items.json; <dir>/pNN_full.png (the sprite on #222645) and <dir>/pNN_thumb.png (44x56 box, 4x nearest)
pipeline(ids,
  id -> parallel(thumb reader (a two-second glance), thumb reader (a long look), full-size reader),
        each: "open ONLY <file>; name every animal most obvious first, the one animal you would call this pilot,
               humanoid or not, the trim and glow colour, the worn items, your confidence",
  (readings, id) -> grader: reads items.json for the truth, scores first_animal_full / first_animal_thumb
        (named, close relative, missed), second_feature, colour, mark, humanoid, verdict (pass, weak, fail),
        and the ONE brief change that would fix a non-pass)
```

Readers never see a file name that could leak the species (hence p01...), never open a second file, and never see
the spec. The grader's "close relative" list keeps everyday words honest (yak: bull or ox; ocelot: leopard or
spotted cat; moose: elk; crane: stork or heron; ibis: flamingo or curlew; mantis: grasshopper; orangutan: ape;
chameleon: lizard; hedgehog: porcupine). Results and what they changed: LESSONS.md, round 2.

## 5. What a painting without the kit gets wrong (why the earlier attempt read as terrible)

A prompt that says "anthropomorphic animal pilot, concept art" gets a generic animal in a generic jacket with a
baked backdrop, cropped feet, a floor shadow, a palette that ignores the game's, no heritage, no faction, no
callsign, at whatever framing the model chose - and then it is scaled by hand next to paintings framed to a
different stage. The kit removes each of those: the brief from the record, the key colour and the matte, the
measured framing, the value gate, the sheet beside the references, and the lessons that move into the tables.
