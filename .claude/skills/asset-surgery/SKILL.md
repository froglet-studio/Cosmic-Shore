---
name: asset-surgery
description: Do "editor-only" Unity work programmatically, not as an in-editor checklist for the human - ShaderGraph node wiring via JSON synthesis, prefab/scene YAML component surgery, SO asset re-authoring, binary FBX read AND write (edit a model in place, keeping its guid and fileID, validated with assimp - see 4.8/4.8c), and REAL compilation of the C#/HLSL you are about to commit (dotnet-sdk/Roslyn + clang are installable here - see 4 and 4.5c; do not settle for inspection). Use whenever the plan drifts toward "I'll prepare instructions and you do it in the editor", whenever a task touches .shadergraph/.prefab/.unity/.asset/.fbx files directly, or when the human says "you can do this" / "write a tool for it". Editor time is for PLAY TESTING and what genuinely needs the running editor - not mechanical asset edits you can machine-validate.
---

# Asset Surgery — do it programmatically, prove it before writing

Origin: the clock-material migration (2026-08). The session initially handed the
human a multi-phase in-editor ShaderGraph wiring checklist, rationalized as
"node splicing is semantic, belongs to eyes-on editing." The human pushed back
twice — "I think you can do these things or write tools to do these things" —
and was right: all four graph phases, the prefab component excisions, and a
1,600-line deletion pass shipped out-of-editor, first-try clean, because every
edit was **machine-validated before writing**. This skill captures that method
so the confidence doesn't have to be re-learned.

## 0. The doctrine

- **Punting is the last resort, not the default.** Before writing any
  "steps for you to do in the editor," ask: what EXACTLY needs the running
  editor? Almost always only: play testing, device profiling, play-mode
  measurement tools, and final import verification. Everything mechanical —
  edits to serialized assets, wiring, deletions, re-authoring numbers — you can
  do, because the formats are text and correctness is checkable by machine.
- **Risk is managed by validation, not by human hands.** A human clicking
  through the Shader Graph UI is not safer than a script that parses every
  block, resolves every reference, asserts every invariant, and only then
  writes. It is slower and less repeatable.
- **The human is the SEMANTIC gate, not the mechanical one.** Structure your
  work so their one playtest validates meaning ("does it look right?") while
  your validation already guaranteed structure ("does it parse, resolve,
  compile?").
- **Incremental trust loop.** On a new technique: do ONE representative
  instance → human playtests → then batch the rest without asking. ("Do it for
  the block grow graph. I will test. Then you will be able to confidently do
  the rest.")

## 1. The safety pattern (applies to every technique below)

1. **Read + parse the WHOLE file first.** Build an object model (every JSON
   block / YAML document), not a regex-only view.
2. **Idempotency guard first.** If the edit already landed, print "already
   done" and exit 0. Scripts get re-run.
3. **Donor-clone for schema exactness.** Never hand-author a serialized object
   from documentation. Clone a same-file donor of the same type and rewrite
   only identity fields (IDs, names, values). The file's own serializer version
   is then correct by construction.
4. **Validate EVERYTHING before writing anything.** Rebuild the output in
   memory; assert: every block/document parses; IDs unique; every node, slot,
   edge, property, child-list reference resolves; the specific edit landed
   (feeder counts, renamed fields, removed IDs truly gone). A failed assert
   costs nothing because the file was never touched.
5. **Write once, verify once more** (re-read and re-run the checks if the edit
   was risky). Git is the rollback — one file per concern, commit early.
6. **Leave a safety net in the repo:** an idempotent in-editor repair tool
   (auto-wire / re-add), a validator menu item that reports ✅/❌ per
   requirement, and fail-loud runtime diagnostics (once-per-offender errors
   naming exactly what's unwired). These catch reverts and future drift — and
   they're what makes strict no-fallback modes safe to ship.
7. **If the tool WRITES assets, its output is the deliverable — wire it to
   ship.** You cannot see that output: the tool runs in the human's editor,
   minutes or days later, and the result lands in THEIR working tree while your
   branch carries only the tool. That is how a migration merges half-landed —
   code that expects a scene nobody pushed, broken everywhere, with nothing in
   the diff to explain it. So: `FrogletToolChangeLedger.Record(ToolName, path)`
   in the same block that writes each asset, and
   `FrogletToolShipPanel.Draw(Ship, this)` at the bottom of `OnGUI` — that gives
   the human **Validate & Push** (stages only this tool's paths) and **Retire
   Tool** (deletes the one-off once its output is safely pushed). Contract and
   rules: `Docs/TOOLING.md` § "Tool output is a deliverable"; the end-of-branch
   gate is `/ship-tools` (and `/ship` §2.5, which no ship mode may skip). A
   READER tool that only logs needs none of it — say so in its doc comment so
   nobody hunts for output that was never meant to exist.

## Where the techniques live (read on demand)

Sections 2–5 live in `references/` next to this file. Section numbers are unchanged, so "`/asset-surgery` §4.8c"
still means the same text: open the file for that section and read only what the task needs. §0, §1 and §6 stay here.

| Section | File | What it covers |
|---|---|---|
| §2 | [`references/SHADERGRAPH.md`](references/SHADERGRAPH.md) | ShaderGraph JSON synthesis (§2, §2a): wiring nodes in a `.shadergraph` without the editor. |
| §3 | [`references/YAML_SURGERY.md`](references/YAML_SURGERY.md) | Prefab/scene YAML surgery (§3): adding, removing and rewiring components in `.prefab` / `.unity` / `.asset` files; fileIDs and guids. |
| §4 | [`references/CSHARP_VERIFICATION.md`](references/CSHARP_VERIFICATION.md) | C# verification (§4): getting a real compiler — `Tools/Build/unity_refcompile`, dotnet-sdk/Roslyn, its false positives. |
| §4.5–4.5e | [`references/OFFLINE_PROOFS.md`](references/OFFLINE_PROOFS.md) | Offline simulation and proofs (§4.5–4.5e): deterministic generators, fragment shaders, generated geometry, compiling and rasterizing HLSL with clang, control loops, photographing a generator. |
| §4.6–4.9c | [`references/ASSET_AUTHORING.md`](references/ASSET_AUTHORING.md) | Assets and models (§4.6–4.9c): hand-authoring asset trios, recovering deleted values, headless FBX interrogation, binary FBX surgery and blend-shape normals, prefab-nesting audits, stripping dead keys, proving scene UI wiring. |
| §5 | [`references/TRAPS.md`](references/TRAPS.md) | Traps learned the hard way (§5): check these BEFORE debugging for an hour. |

Sub-sections by file (grep the heading to jump): `OFFLINE_PROOFS.md`: 4.5, 4.5b, 4.5b-geo, 4.5c, 4.5c-r, 4.5d, 4.5e; `ASSET_AUTHORING.md`: 4.6, 4.7, 4.8, 4.8b, 4.8c, 4.8d, 4.9, 4.9b, 4.9c.

## 6. When the editor genuinely IS required

Device soak tests, profiling, visual judgment, play-mode-state measurements,
and final import verification of hand-authored assets. Even then: build the
measuring tool + validator so the human runs ONE menu item and pastes ONE
output back — then YOU act on the numbers (the PhaseThresholds re-baseline
pattern: they ran the measurer, the session authored the six configs from the
pasted output).

**Narrowed 2026-08:** "play-mode measurement" no longer covers a *deterministic*
generator's baseline — see §4.5, which measures it offline and uses the in-editor
measurer as a CONFIRMATION step rather than the source. Keep asking which half of
a measurement is actually play-mode-dependent; often it is neither.

**Visual judgment is the softest of these — simulate it rather than punting it.**
Once §2a has told you what the shader does with a value, you can reimplement that
one path offline and RENDER the candidates: rasterise/raytrace the actual geometry
(a box is a 10-line slab intersection), apply the traced formula, tonemap (ACES) and
sRGB-encode, and lay the options out as a labelled before/after sheet. `pip install
numpy pillow` if the sandbox lacks them. This turns "which of these four palettes is
best?" from a question you ask the human into one you answer and then have them
confirm — and it catches the option that's numerically perfect and ugly (matching a
cool reference hue's chroma exactly turns a warm hue to khaki; only the render shows
it). Ship the sheet WITH the change so the human's playtest starts from your read,
and still say plainly that the sheet is a simulation, not the engine.
