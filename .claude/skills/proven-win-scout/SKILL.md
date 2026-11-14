---
name: proven-win-scout
description: Find candidates for /proven-win - exact, cost-free performance improvements in the Cosmic Shore Unity project - by reading the code (no Unity needed) through fixed lenses, checking each against the C# that writes its values at run time, and writing a ranked queue whose every entry has the fields /proven-win needs. Use on "/proven-win-scout", "find more wins", "what can we prove next", "sweep for exact savings", or when WINS.md's queue is empty.
---

# Proven Win scout

You add entries to the ONE queue, `Docs/ProvenWin/QUEUE.json`, through `Tools/Build/proven_win/queue.py add`; you never
change game files and never measure. Every entry is a prediction for `/proven-win` to measure. Before searching, read:
- `Docs/ProvenWin/LENSES.md`: the lenses below with what each has found and been rejected for, and every lesson
  learned so far (newest last). Apply the lessons: they exist because a candidate failed for that reason.
- `python Tools/Build/proven_win/queue.py list --all`: what is queued, measuring, won, rejected and inconclusive.
  `add` refuses an id that exists and a candidate in the same place with the same title as a rejected one; do not
  try to slip one past it with new words.
Aim to leave at least 5 queued entries; stop at 10.

## 1. Lenses (search in this order; each has found real wins)

1. **Work multiplied by an exact zero.** A term computed and then multiplied by a value that is exactly 0 over a
   large region (a `smoothstep` mask, a saturated weight, an intensity a script sets to 0). W1 (a silenced crawl, 3x),
   W3 (sky dust off the galactic band), W4 (empty star cells).
2. **A feature switched off at run time but still computed.** Grep for `SetFloat(<id>, 0` and property blocks that zero
   a shader's intensity, then read the shader: does it still compute the effect? (W1.)
3. **A search over cells or neighbours that can be narrowed exactly** by a bound on its inputs (W2: the lace angle
   bounds where a Voronoi feature can lie, so 6 cells instead of 9).
4. **Work done for things nobody can see.** `updateWhenOffscreen`, instanced draws with whole-batch bounds, cameras
   rendering under an opaque cover, components animating hidden UI (W5).
5. **Every-frame writes of an unchanged value** that dirty a canvas or a renderer. Careful: several writers mask each
   other (Menu_Main's canvas), so propose them as ONE freeze-all candidate first.

## 2. For each hit, check it is real (all required; drop the hit if one fails)

- **Runtime writers**: grep `Assets/_Scripts` for every property or field the saving depends on. Write down each
  writer with file:line and the value it writes IN THE GAME. The asset's value is not the game's value until you have
  shown nothing overwrites it. (W1 was first measured in the asset's state, which the game never draws.)
- **In use**: the scenes, prefabs and config assets that draw it, by guid, and which platform tiers draw it at full
  price (`PlatformProfile_*`).
- **Exact**: the algebra for why the skipped result is exactly what the original computes (`0 x finite = 0`,
  `smoothstep` saturates to exactly 0). If it is only approximately equal, say "approximate" and rank it low: those
  were rejected (W2's 2x2 version moved pixels 140 levels).
- **Lines moved**: propose the change as an INSERTED early-out with nothing reordered. On Vulkan even pure code motion
  changed pixels.

## 3. The queue entry (one JSON file per candidate, then `queue.py add <file>`)

```json
{
 "id": "short-kebab-id",
 "title": "one line: what the change does",
 "where": ["Assets/<path>:<first>-<last line>"],
 "runtimeWriters": "every writer, file:line, the value IN THE GAME (or: none, grepped <names>)",
 "inUse": "scenes / prefabs / configs by guid; platform tiers at full price",
 "waste": "what is computed and thrown away, how often, and how you estimated it",
 "change": "the inserted lines, nothing moved",
 "exactness": "bit-identical | display-identical | approximate",
 "bench": "shader | scene",
 "probes": "early-outs: what probe_reach and probe_guard return; anything the bench state needs",
 "negativeControl": "the deliberately wrong variant",
 "otherStates": "states the game also draws it in, where the change must not cost",
 "owner": "who_works_where.py owner",
 "lane": "approvals.py lane",
 "gain": "predicted gain, and what is unmeasured",
 "confidence": "high | medium | low",
 "lens": "which LENSES.md lens found it"
}
```
`add` refuses an entry with a field missing, an exactness or bench outside the lists, or a `where` file that does not
exist; it records the commit and each file's hash so `/proven-win` can tell when the code under an entry moved.
A change neither bench can measure (`bench` must be shader or scene) is not a candidate yet: write it into LENSES.md as
a lens note instead.

## 4. Rank, add, commit

`queue.py` ranks queued entries by exactness, then confidence. Add every entry, run
`python Tools/Build/proven_win/queue.py render`, and commit `QUEUE.json` and `QUEUE.md` (and any new lens note in
LENSES.md) together. Say in one line how many you added and which lens found each.
