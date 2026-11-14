# Proven Win candidates

One folder per candidate: `Docs/ProvenWin/candidates/<id>/`. `run.py` measures it, `verdict.py` judges it,
`signoff.py` builds its page. Worked examples, both re-measured end to end with these tools: `example-w4-sky-stars/`
(shader kind) and `example-w5-space-hearts/` (scene kind). Copy the one of your kind and change the fields; do not
write one from nothing. `auth-cameras-under-cover/` is a real candidate that ended inconclusive: the scene moves on to
Menu_Main by itself, and the bench reports that instead of measuring.

```
<id>/
  candidate.json          what to measure (below)
  variants/A.*            the file as shipped (copy it from origin/bleeding-edge, never from memory)
  variants/B.*            the change
  variants/C.*            the change made deliberately WRONG (a bound too tight, a skip that always fires)
  variants/probe_*.*      early-outs only (below)
  reports/<api>/<role>.json   written by run.py; committed: they are the evidence
  verdict.json, verdict.md    written by verdict.py; committed
  (out/, specs/, logs, signoff/ are scratch and gitignored)
```

## candidate.json, shader kind

```json
{
 "id": "w4-sky-stars",
 "title": "one line: what the change does",
 "owner": "who owns the file (who_works_where.py)",
 "lane": "the swim lane approvals.py names",
 "kind": "shader",
 "earlyOut": false,
 "runtimeWriters": "REQUIRED. Every C# line that writes the values the change depends on, with file:line, and what they write in the game. 'None: the .mat values are the game's' only after you grepped every property name in Assets/_Scripts.",
 "exposure": "REQUIRED. Which scenes/prefabs draw it, on which platform tiers.",
 "spec": { "...": "ShaderABBench spec fields: material, mesh, swapFile, includeChain, floats, colors, globalVectors, prepare, cameraDistance, modelScale, orthographic, driveGlobalTime, clockProperty, ..." },
 "variants": {"A": "variants/A.shader", "B": "variants/B.shader", "C": "variants/C.shader"},
 "states": {"crawlOn": {"floats": []}},
 "signoff": {"heading": "...", "subject": "...", "whatChanged": "...", "changeName": "...", "frameNote": "...", "notMeasured": ["A phone GPU."]}
}
```

- `spec.floats` / `colors` / `globalVectors` / `prepare` are how the bench draws the material **in the game's state**,
  not the asset's. Fill them from `runtimeWriters`. W1 was first measured on the asset's state and that was the wrong
  crystal; the creature-body bloom bench never reached its state at all.
- `states`: every other state the game draws this material in (a feature switched on or off, a far LOD). The change
  must not cost anything there: verdict.py fails it if B is slower in a state (the envelope cull on top of W1 cost
  1.6 % on D3D11 in the silenced state, and was rejected for it).
- `earlyOut: true` (any change that skips work under a condition) REQUIRES two probes, or the verdict is HOLD:
  - `probe_reach`: B returns a constant UNCONDITIONALLY where the skipped code was. It must change the picture: the
    code is reached in the bench.
  - `probe_guard`: B returns that constant ONLY inside the guard. It must change the picture: the guarded state
    actually happens in the bench. If it does not, the bench is wrong, not the change.

## candidate.json, scene kind (a component that should stop running)

```json
{
 "id": "w5-space-hearts", "kind": "scene", "metricCounter": "MeshSkinning.Update",
 "runtimeWriters": "...", "exposure": "...",
 "scene": {
   "common": {"prefab": "Assets/_Prefabs/Environment/CrystalSpace.prefab", "spawnBehind": 200, "spawnInView": 49, "setupMethod": "none"},
   "change": {"offscreenBoundsSize": 0.0254},
   "neg":    {"offscreenBoundsSize": 0.00001}
 }
}
```
Keys under `scene` are `RunInEditor` flags without the dash (`-scene`, `-prefab`, `-ownerType`, `-toggledType`,
`-ownerPathContains`, `-toggleTypes`, `-toggleUnder`, `-keepTypes`, `-spawnBehind`, `-spawnInView`,
`-offscreenBoundsSize`, `-offscreenAOn`, `-diagnoseFrames`, `-setupMethod`). `-setupMethod none` when there is no
setup call; `-ownerType none` when the candidate only toggles a set (`toggleTypes`). Several writers dirtying one thing mask each other: switch
them off TOGETHER (`toggleTypes`) before concluding anything from one.
