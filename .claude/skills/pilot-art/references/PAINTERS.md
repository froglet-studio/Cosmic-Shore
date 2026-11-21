# Painters: where the pixels come from, what each costs, who approves

The kit never paints. It briefs, captures, mattes, frames, gates and wires. A painting comes from one of these,
always from `brief.py`'s prompt verbatim, asked for at 1024 x 1280 (portrait 4:5; the Figma tool treats the size as an
aspect hint, so `author.py` reads whatever size arrives) over the key colour the brief named.

## 1. The Figma connector's image models (what round 1 used)

**Update 2026-11-10:** `www.figma.com` is now on the Default environment's allowed hosts (Garrett approved),
so a cloud session downloads `image.url` with curl (`creator.py capture`) and no longer needs `get_screenshot`,
whose Collab-seat read limit runs out after about 30 calls a day. The signature busts use `gpt-image-2.5-sunburst`
at 1024 x 1024, painted with their own background (no key colour, no matte): `/pilot-creator`. Round 2 of the busts
(2026-11-11) was 70 paintings for 50 pilots and round 3 was 8, one for each bust a rule touched. The four realistic
scene samples (`scene.py`, the style not locked) took 19 on the same model: it returns 1536 x 1024 for 3:2,
1536 x 864 for 16:9 (asked as 2048 x 1152) and 1024 x 1280 for 4:5, from a brief of up to about 1,000 words;
`gemini-3.1-flash-image`, tried once, returned 1264 x 848 and lost the second animal (LESSONS 70).
A bust brief never names the game, the studio, the world or a place,
a crew or a faction in it, and never asks for an emblem by name (LESSONS 44, 45); the full-body brief (`brief.py`)
names the pilot's callsign and founding faction.

The claude.ai Figma connector exposes `generate_image` (a text prompt into a Design file; models
`gpt-image-2.5-flare` (the default; round 1), `gpt-image-2.5-sunburst`, `gemini-3.1-flash-image`,
`gemini-3.1-flash-lite-image`). **It spends Figma AI credits on the destination file's plan**: say how many
paintings a round will make before making them, and name the file.

- **The file**: "Cosmic Shore Pilot Art" (key `BPjqBNIQgtY3L6RvghdWcg`, https://www.figma.com/design/BPjqBNIQgtY3L6RvghdWcg)
  in the **Froglet Studio** team (Garrett is admin on it and on his own team; the studio's plan was chosen on
  2026-11-07 because the game's art belongs with the studio). `whoami` first; `create_session` on the file;
  `generate_image(prompt, fileKey, width 1024, height 1280, model)`; every painting lands as a frame in the file,
  so the file is also the round's gallery.
- **Capturing the painting**: `curl -f -L -o Docs/Pilots/art/sources/<name>.png <image.url>` (a bust:
  `creator.py capture`). The thumbnail the generate call itself returns is a 205 x 256 JPEG: never use it. Until
  2026-11-10 the cloud environment's network policy refused `www.figma.com` (`curl` got 403 from the proxy;
  LESSONS 4), and round 1 captured with `get_screenshot` on the returned `nodeId` (`enableBase64Response: true`,
  `maxDimension: 1280`, `contentsOnly: true`; the harness saved the inline PNG under the session's
  `tool-results/` folder at the full 1024 x 1280). That route still works where the host is not allowed, within
  the Collab seat's limit of about 30 reads a day.
- **What the models get right from the brief** (round 1, 6 paintings): the species, the lent feature, the faction
  gear, the exact accent hex as trim and seam lights, the callsign mark, a flat key with a 0.005 OKLab spread, the
  framing. **What they get wrong**: a dark animal in dark gear sinks (LESSONS 2); a prop in the hand sometimes
  replaces the hand on the belt; "no floor" holds, "no shadow" mostly holds.
- A second candidate for the same pilot is a second call with the same prompt; do not vary the prompt by hand -
  vary the spec's `pose` or `extra`, so the variation is recorded.

## 2. Weave models that take REFERENCE pictures (the game's own frames, a pilot's likeness)

Garrett, 2026-11-11: "is there a way to upload screenshots or other content so we can keep consistency between the
game and the art we are generating? ... we will need something like this if we want the same character in more than
one piece of art as well", then "i connected weave". The kit's side is `Tools/Build/pilot_art/refs.py` (the library
of real frames, a pilot's likeness, a record's `refs=` and `patch=`, the links, the run's input, `apply`).

- **The tools**: `weave_find_model` (a model's id and contract), `weave_run_model {id, input, acknowledgedCost?}`,
  `weave_get_model_run_output {predictionIds}`. A run takes about three minutes: poll, never sleep.
- **The painter**: GPT Image 2.6, id `96f53e1c-465d-44d6-90fd-38bcb9fada66`. Inputs `prompt` and `images` (up to
  sixteen https links); params `size {width, height}`, `model: "Sunburst"`, `quality: "high"`,
  `output_format: "png"`. It takes no mask. GPT Image 2.5 Sunburst, the painter of section 1, takes one on Weave
  and "is not available on the Free plan".
- **Two pools of credits.** Section 1's painter spends Figma AI credits on the file's plan; they ran out on
  2026-11-12 after twelve repaints ("Insufficient Figma AI credits ... resets at 2026-11-14T01:41:00Z"). Weave
  spends the workspace's own ("These are your workspace's Weave credits, not Figma AI credits"): 150 a month on the
  Free plan, and this painter is quoted at 11 a run at quality high whatever the size and however many things the
  ask names (3 at medium).
- **Every run is quoted, and the person approves the cost on a card.** A call without `acknowledgedCost` spends
  nothing and returns the quote. Put the cost on a decision card (never free text) and run only what the card
  approved, at the cost it named: a card may approve a named set of runs at so much each and so much in all, each
  still quoted first, and a quote that differs goes back to the person. An interactive route, never an
  autonomous one.
- **The plan's wall.** The Free plan gives an agent two model runs, whatever credits remain. The third came back
  "You've used your 2 free Weave model runs. Upgrade to a paid Weave plan" (2026-11-12, after a quote of 11 and
  with 128 credits left; nothing was spent). The pricing page that day (https://weave.figma.com/pricing) listed
  Starter at 1,500 credits a month and Professional at 4,000, and did not mention the limit. The plan is the
  person's to change: never route round it (another tool, another account, a published workflow). Garrett moved
  the workspace to a paid plan that day ("i've upgraded weave", 04:09 UTC) and the next run went through.
- **Links, not uploads.** The painter fetches each picture by its link. The repository is public, so a picture
  that is committed and pushed has one (`refs.py input` builds them at the pushed HEAD and refuses a picture that
  is not pushed). Figma's upload host is not reachable from a Default-environment session. A link names the
  repository, which the words never do; the game's own frames were asked for.
- **The answer** is served from `s3-alpha-sig.figma.com`, which must be on the environment's allowed hosts (added
  to Default on 2026-11-12, Garrett approving the prompt): `curl -sS -f -L -o <a new scratch folder>/a.png
  '<url>'`, one folder a download, read with `python3 -I`.
- **What it does** (LESSONS 83, 84, 87 to 90): from the scene, four frames and "Edit this photograph, picture 1.
  Change only ..." it painted the game's own bricks, needles, net, crystals, shards and trails, and the ship from
  its four views, on the first run each (Jugger's windows and dome, Scales's dome). It repaints the whole picture
  a little while it works (6.09 and 14.36 of 255 outside the region), so `refs.py apply` takes only the record's
  region and the pilot stays the pilot of the first painting. What an ask names is painted: "faint reflections of
  the bridge" hung a second head in a window, and a "plate" on a microscope came back as a dish.
- **A head** (87, 89): shown the pilot's likeness as picture 2 and given the whole frame as the region, it changed
  Flux's head (an orca's fin, skin and eye patch) and kept his face, horns and hands in place, on the first run.
  A head changed this way is read blind before it is shown: Trace's, drawn in to her first animal's outline, came
  back as asked and lost her second animal to a stranger, and was not taken.
- **Several things in one ask** (88): a run costs the same whatever it asks, so an ask may name several parts. Each
  is judged alone when the answer is in; the record's `edit` holds only the regions taken and `took=` says so.
- **Colour comes from the pictures before the words** (90): Trace's cylinder came back orange beside an orange
  frame, over "jade teal ... indigo and violet". Asked again with the scene alone (`refs=()`) and the words
  pointing at "the round port of this same photograph", it came back in the port's colours. Send only frames in
  the colours wanted, and let a scene that holds the thing already be its own reference.
- **Run the first of a set alone** and look at its answer whole and at full size before the next is quoted.
- **Where it stands** (2026-11-12, 05:00 UTC): five runs made of the six Garrett approved on a card, 55 of the 66
  credits, 1,467 left in the workspace: Jugger, Scales, Flux, Trace, and Trace's cylinder again. One run is
  unused, and the card named it a rerun of those scenes: a new target (ZODIAC's heron brought up to the head, a
  frill of Trace's put to use) is a new card. A run finished in one to three minutes. A quote never shows the
  plan: only a run meets the wall.

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

**The busts' blind read (2026-11-11) is the same test with the grader as a script**, so two rounds can be
compared: `Tools/Build/pilot_art/blind.py prep` shuffles the busts into a scratch folder (the key stays outside
it) and cuts the 64 px icon sheets, `.claude/skills/pilot-creator/references/blind_read.js` is the readers
(one for every ten portraits, two on the icons, told nothing), and `blind.py grade` scores what they named at
two levels: the species, and its kind by the everyday-word list above. `/pilot-creator` section 4.

## 5. What a painting without the kit gets wrong (why the earlier attempt read as terrible)

A prompt that says "anthropomorphic animal pilot, concept art" gets a generic animal in a generic jacket with a
baked backdrop, cropped feet, a floor shadow, a palette that ignores the game's, no heritage, no faction, no
callsign, at whatever framing the model chose - and then it is scaled by hand next to paintings framed to a
different stage. The kit removes each of those: the brief from the record, the key colour and the matte, the
measured framing, the value gate, the sheet beside the references, and the lessons that move into the tables.
