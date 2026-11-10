# The colour trace: every colour from the worn colour set

Garrett, 2026-11-10: "we want every single color in the game to respect the colorset. no pixel anywhere should ever
recieve a color that can be traced somewhere but a colorset. this definitely includes the UI, but i notice many other
colors that are not respected in the skybox etc. you should be able to extract any baked in colors and then determine
which domain they are in. then extend the color set to include that property ... for the photic set you can map the
baked value into its colorset. for all the others calculate a new appropriate values."

This is how that works, what it covers today, and what is still outside it. The tool is
`Tools/Build/colour_trace.py`; the table it writes is `colour_trace.json` beside this file; the colours live on every
`SO_ColorSet` as `TracedColors`; the runtime is `ColorTrace` (lookups), `UIThemeLive` (the UI) and `WorldColorTrace`
(the world).

## 1. The loop

1. **Scan.** Every colour authored in first-party assets and runtime code: component fields, prefab-instance
   overrides (resolved through variants and nested prefabs), materials (and the materials inside font assets), render
   settings, volume profiles, shader defaults and constants, and C# literals (multi-line constructors, target-typed
   `new(...)`, `Color32`, hex strings). About 38,000 colours.
2. **Status.** Each colour is exactly one of:
   - **inert**: it never reaches a pixel. Every rule is named in the table with its reason (`inert_why`): an asset no
     build can reach, a scene outside the build, a TextMesh Pro field TMP never reads, a particle module that is off, a
     gradient key past `m_NumColorKeys`, a ColorBlock without the ColorTint transition, a material property its shader
     does not declare or declares as a vector, fog that is off, a C# literal the compiler-checked review
     (`colour_trace_review.json`) shows is overwritten, a fallback or development-only, and so on;
   - **token**: a UI colour equal to a UI theme token. The theme the worn set gives (`ColorSetUITheme`) paints it;
   - **fixed**: pure white or pure black: the identity tint over art and the absence of light, the same in every set
     (so white art tints are never dyed);
   - **traced**: a `TracedColor` every set carries, found on screen by its value and repainted from the worn set;
   - **untraced**: it reaches a pixel and nothing paints it from the set yet. The table lists every one; `--check`
     fails on any new one (a ratchet: the list only shrinks).
3. **Ids.** A traced colour's id is its role and its value as authored: `ui:747BAD`, `world:FFB21F`, and
   `world:FF0101x1.50` for HDR (the brightest channel, in hundredths). `ui` is canvas graphics, text, selectables and
   UI materials; `world` is materials, particles, trails and lines, lights, cameras, sprites and the sky.
   `ui-token:<Token>` carries the UI theme tokens a set gives directly (the tooltip and the four element colours).
4. **Values.** Every set holds a value for every id (`SO_ColorSet.TracedColors`):
   - **Photic**, the set the game was authored in, holds the baked value (`colour_trace.py --write-photic` writes its
     block; nothing else in `PhoticColorSetSO` changes);
   - **every other set** computes its own from its pigments (`colour_trace.py repaint`, written by
     `author_colour_set_assets.py`):
     - a grey keeps its value (every grey is Blue's, and a grey has no hue to change);
     - a UI ground (a Deep Sea navy: Blue, chroma 0.10 or less, L 0.70 or less) keeps its lightness and chroma and
       takes the hue of the set's Jade face, the way the UI theme re-hues its panels;
     - any other colour of family F wears the set's F **rim** pigment, keeping its brightness (brightest linear
       channel), its alpha and its whiteness beyond Photic's F rim: how the palette writer paints every rim field.
       Rims always name their family (rule 4); a face may be quiet and of any hue, so faces are never used (the sky
       prototype showed Nocturne's Blue face turning the nebula Jade);
     - a colour in the band between two families is read as the nearest one;
     - a world colour that IS a field of `PhoticColorSetSO`, or a multiple of one (a material baked from the field at a
       brightness: the omni crystal's element shades are `ElementalCrystalColors` x 0.988), takes the set's own field
       at the same scale;
     - a UI token colour takes the set's own token (`ColorSetUITheme.Build`, ported line for line), for Photic too,
       because the UI theme re-hues Photic's tokens;
   - **a set may retune any id**: `colour_sets.json`, `sets.<key>.traced: {"<id>": "#RRGGBB"}`, then the writer. An
     edit in the inspector is drift the writer's `--check` reports.
   - Every set's values are unambiguous: no two ids share a value in a role, and no value lands on Photic's value of
     another id (the writer moves a clash by one 8-bit step), so a colour found on screen always names one id.
5. **Painters** (play mode only; nothing is painted in edit mode):
   - `ThemeManager.WearTrace` wears the set (`ColorTrace.Wear`) at startup and on every Profile preview or apply,
     before the domain material sets are repainted, so the prism entities drawn with a traced material are re-stamped
     with them;
   - **UI** (`UIThemeLive`): a Graphic's colour that is a token takes the live token; any other colour the trace
     knows takes the worn set's value: Graphic colours, TextMesh Pro vertex gradients, ColorTint selectables'
     ColorBlocks, Shadow and Outline effect colours, input-field carets and selections. New UI is swept before it
     draws (root canvases whose hierarchy changed, every frame; the list of root canvases is refreshed once a second).
     The set gives the tooltip and element tokens (`ColorSetUITheme.Build` reads `ui-token:` ids);
   - **world** (`WorldColorTrace`, on every wear and every scene load): every Color property of every loaded
     material (the HyperSea sky included), particle systems (start colour, colour over lifetime and by speed, trails,
     colour custom data, each by its mode), trails and lines, lights, solid-colour camera clears, sprites, world-space
     TextMesh Pro, fog and ambient light, and post-processing colour parameters. Prefab assets and materials are
     painted where they live in memory, so everything spawned later is born in the worn set; nothing is repainted per
     frame or per spawn;
   - **code and configs**: a colour in C# or a config field is read through `ColorTrace.Retrace(role, colour)` where
     it paints (an SO's accessor, a view's paint statement). It allocates nothing;
   - **the editor** (`WorldColorTraceEditor`): every asset the world painter touched is put back when play ends, and
     before the editor saves assets during play, so no `.mat` or prefab in git ever takes a set's colours.
6. **The gate.** `python3 Tools/Build/colour_trace.py --check` fails when the scan and the table disagree (a new colour,
   a colour nothing authors any more, a baked value that moved), on a new untraced colour, when Photic's block is
   stale, when a set asset lacks the table's ids, or when a set's values are ambiguous.
   `author_colour_set_assets.py --check` fails when a set's block drifted.

```
python3 Tools/Build/colour_trace.py --build                # the table, from the scan
python3 Tools/Build/author_colour_set_assets.py            # every set's TracedColors
python3 Tools/Build/colour_trace.py --write-photic         # Photic's block
python3 Tools/Build/colour_trace.py --check                # the gate (and --self-test)
python3 Tools/Build/colour_trace.py --summary              # counts by status, rule, role
python3 Tools/Build/colour_trace.py --report               # what is untraced, by file
```

When a person changes a colour anywhere in the game, the gate fails until `--build` and the writers run: the new value
becomes Photic's, and every other set computes its own.

## 2. What is still outside the trace (measured 2026-11-10)

The ratchet list (`colour_trace.json` `untraced`) names each colour. What remains, by kind:

- **Four occupied swim lanes, waiting on their occupants.** The code edits that route the Voyage map's, the
  Grizzly's, the Thresher's and the Stoat's colours through the trace are written but not applied, because those lanes
  had unmerged work in flight on 2026-11-10 (the Voyages map: Shombith, `claude/epic-fermat-6c7n2l`; Grizzly, Thresher
  and Stoat: Yash, `Ys-bleeding-edge`). Each is a patch in `colour_trace_waiting/` (`git apply
  Docs/VesselModels/colour_trace_waiting/<lane>.patch`, then `colour_trace.py --build` and the writers). Until then their
  68 colours are on the untraced list.

- **Colours baked into shader code.** A colour node or constant counts only when it is wired to the output (a
  ShaderGraph node with an edge path to a Block node) and a drawn material uses the shader. Of 50 ColorNodes, 31 are
  wired to nothing and 18 sit in graphs no reachable material draws; the one left is CageGraph's dark blend constant
  (the cell nucleus). HLSL constants left: the black-hole lens's ring, core, shell and gravity-wave glows
  (`BlackHoleLens.hlsl`) and the Soliton tunnel's glint. The HyperSea sky's star ramp was the largest one and is now
  four material colours (`_StarColorHot/White/Warm/Cool`, saved at today's values). The fix for the rest is the same:
  a Color property the material saves (shader asset surgery), which the world painter then repaints.
- **Textures.** The trace reads colours, not pixels. The texture audit measured every shipped image:
  - masks (white or grey sprites tinted by a Graphic) are already covered through their tint;
  - 151 sprites bake a Deep Sea token flat and 27 another flat colour: whiten each PNG in place (same guid and
    borders) and move the colour into the Graphic's tint, where the UI theme or the trace paints it;
  - art (56 rendered card backgrounds, 95 vessel renders, 107 pilot portraits and avatars, 45 codex illustrations, the
    static sky for low tiers): either re-render per set (`render_card_backgrounds.py`, `bake_static_skybox.py`) or a
    runtime art grade (a shader that maps Photic's anchors to the worn set's). The painted portraits are never
    overwritten (`Docs/Pilots/PORTRAITS.md`), so only a grade can move them;
  - exceptions the rule cannot reach: platform controller glyphs (Xbox and PlayStation colours), the Google "G"
    brand mark, the app icon and splash (drawn before any script runs).
- **Copies made before a wear.** A colour copied out of a material or config into a cache before the set is worn
  keeps the old value until it is copied again: Spindle's leaned and phase variants, SpindleLodSystem's copies,
  CapsuleMembrane's draw colours, CellVisualTint's capture, SnowChanger's clones, PrismSlice's runtime material,
  CrystalAccentTint's arcs, ToyFactory's colour-keyed caches, fauna GPU palettes, vessel tails and jets painted at
  spawn. They are right after a scene load (the world painter runs before Start); after a Profile preview in
  Menu_Main they show the previous set until they repaint. Each owner can subscribe its re-apply to
  `ColorTrace.Changed`.
- **Particles alive at a wear** keep their emitted start colour until they die (a fraction of a second).

## 3. Decisions waiting on people

- **Photic's UI tokens (Garrett and Shombith).** "Photic = baked" holds for every traced colour, but the UI theme
  re-hues Photic's own tokens from its Jade face (Shombith, 2026-11-09), so under Photic the UI is not the authored
  `UITheme.asset`. Token colours outside Graphics (a material, a tint, a code literal) follow the theme, so the UI
  stays one look. One answer is needed: keep the derived Photic theme, or make Photic's tokens the authored ones.
- **The element colours and the tooltip (Shombith).** They used to stay authored in every set; they are now each
  set's own (Photic keeps the authored values, every other set paints them with its family rims).
  `ColorSetUIThemeTests` holds the new rule.
- **Domain colours baked off the domain slot (vessel owners).** A few hulls draw a fixed Jade or Ruby material where
  their domain should show (GreenVesselMaterial on the SkyBurst projectile, RedAccentVesselMaterial on slot 0 of the
  Goby and the Squirrel, the Serpent's blue and lime). The trace makes them the set's Jade or Ruby; binding them to the
  pilot's domain is the owner's fix.
- **Texture lanes and the art grade (Garrett).** Section 2.

## 4. Where it came from

The rules were measured by eleven read-only audits on 2026-11-10 (sky and environment, the UI kit, HUD prefabs, menu
prefabs, scenes, particles and lines, materials and shaders, configs, C# literals with Roslyn, textures, and the
runtime painters). Their findings are the `INERT` rules in `colour_trace.py`, the review in `colour_trace_review.json`
and section 2 above.
