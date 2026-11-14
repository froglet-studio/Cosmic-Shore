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
   `ui-token:<Token>` carries every UI theme token and the sea lines' four colours (`SeaLine`, `SeaMid`, `SeaEnd`,
   `SeaGlow`): the UI theme a set gives is exactly these (`ColorSetUITheme.Build` reads them).
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
     - a UI token colour takes the set's own token. Photic's tokens are the authored theme (`UITheme.asset`, with
       the selection Jade as the style guide decided, D3) and the sea material's own colours, so under Photic the UI is
       exactly as authored; every other set's are what the UI theme derives from its pigments (`token_value`; the
       derivation is `ColorSetUITheme.Build`'s, which a set that carries no tokens still gets);
   - **a set may retune any id**: `colour_sets.json`, `sets.<key>.traced: {"<id>": "#RRGGBB"}`, then the writer. An
     edit in the inspector is drift the writer's `--check` reports.
   - **Every value names one colour, in every set and both roles** (`colour_trace.py unambiguous`). A colour on screen
     is found by its value, and the runtime asks every set worn in the session (a copy can keep a colour from two
     wears ago), so no value of any set may land on another colour's id or on any set's value of another colour. One
     colour drawn in both roles (`ui:H` and `world:H`) is one colour. A UI value that is no theme token also stays out
     of UIThemeLive's tolerance (0.6/255) of every set's live UI theme token, since UIThemeLive asks for a token first.
     A clash moves to the nearest free 8-bit value (Photic first, then the sets in `colour_sets.json` order; Photic's
     baked values are their own ids and never move). Measured 2026-11-10: 939 of the 4,920 ui and world values moved, none by more
     than 2/255 on a channel; before this rule the sets held 329 values the runtime could misread.
5. **Painters** (play mode only; nothing is painted in edit mode, or once play is stopping):
   - **the lookup** (`ColorTrace.TryRetrace`): a colour is named from the ids themselves (each id IS its authored
     colour: `TryColorOf`), then from what the authored set and every set worn since paint. A colour that already is
     the worn set's (to its 8-bit id) comes back exactly as it is, so repaints are idempotent and wearing Photic leaves
     every baked float alone. A theme token's id finds its authored colour too, although Photic's value for it is the
     re-hued theme's;
   - `ThemeManager.WearTrace` wears the set (`ColorTrace.Wear`) at startup and on every Profile preview or apply,
     before the domain material sets are repainted, so the prism entities drawn with a traced material are re-stamped
     with them. The colours ThemeManager paints on its domain material sets are its own (`WorldColorTrace.Own`): the
     trace never reads a domain colour as a traced one;
   - **UI** (`UIThemeLive`): a Graphic's colour that is a token takes the live token; any other colour the trace
     knows takes the worn set's value: Graphic colours, TextMesh Pro vertex gradients, ColorTint selectables'
     ColorBlocks, Shadow and Outline effect colours, input-field carets and selections. The UI prefab assets in memory
     are painted where they live, so rows, cards and windows instantiated from them are born in the theme (a list
     rebuilt with the same number of rows never changes its canvas's count). Anything else new is swept before it
     draws: a root canvas whose hierarchy changed is marked and swept at most every 0.25 s (a change seen while it
     waits is still swept); the list of root canvases is refreshed once a second. The set gives the tooltip and element
     tokens (`ColorSetUITheme.Build` reads `ui-token:` ids);
   - **world** (`WorldColorTrace`, on every wear and every scene load): every Color property of every loaded
     material (the HyperSea sky included), particle systems (start colour, colour over lifetime and by speed, trails,
     colour custom data, each by its mode), trails and lines, lights, solid-colour clears of game cameras, sprites,
     world-space TextMesh Pro, the render settings' fog and ambient colours, and post-processing colour parameters.
     When the sky's material changes, the ambient light and reflections it gives are worked out again
     (`DynamicGI.UpdateEnvironment`). Prefab assets and materials are painted where they live in memory, so everything
     spawned later is born in the worn set; nothing is repainted per frame or per spawn. A material loaded later (a
     `Resources.Load` on first use) is painted by its loader (`WorldColorTrace.Paint(material)`: the Switchback path
     rays, the black-hole glow, the Astro League ball's trail); the gate names any loader of a traced Resources
     material that does not;
   - **code and configs**: a colour in C# or a config field is read through `ColorTrace.Retrace(role, colour)` where
     it paints (an SO's accessor, a view's paint statement). It allocates nothing;
   - **the editor** (`WorldColorTraceEditor`): when play stops, painting ends first (`WorldColorTrace.Stop`: the
     scenes torn down afterwards may wear a set again), then every asset colour a painter gave (the world painter's,
     and UIThemeLive's UI prefab assets) is put back if the asset still holds it; a colour a person changed during play
     is theirs and stays. An asset's dirty flag is cleared only when every recorded colour went back, it was not dirty
     before play painted it and nothing else marked it dirty since, so a person's other unsaved edit keeps its flag.
     The same happens before the editor saves assets during play (then it paints again) and before scripts recompile
     during play, and a second pass runs back in edit mode. Prefab Mode's stage and inspector previews are never
     painted, so no `.mat` or prefab in git ever takes a set's colours.
7. **Textures: the colour grade** (`Tools/Build/colour_grade.py`, `ColorGrade.cs`; Garrett: "if rerendering textures
   per set is the best you can do, but perhaps you can get clever"). A picture is millions of colours, so the trace's
   own rule (step 4) is applied to every colour of an sRGB grid and kept as a small table per set and role: 33^3 for
   the UI and for the world, `Assets/Resources/ColorGrades/<SetAsset>_ui.bytes` and `_world.bytes`, about 108 KB
   each. Inside a family's cell the table IS the trace's rule; where a picture's gradient crosses a family border,
   the grey threshold or the UI ground's edge, the rule is blended so no seam shows (a 0.002 step never moves the
   output more than 0.007 in OKLab; the 33^3 table's interpolation error is under 0.011 for 99 % of colours).
   When a set is worn, `ColorGrade` builds the two tables into Texture3Ds and switches `COSMIC_COLOR_GRADE` on;
   `ColorGrade.cginc` looks every texel up, except greys, which keep their value and skip the lookup. Photic has no
   table: the keyword is off and costs nothing. Nothing is copied per set and no picture file changes, so the
   painted portraits (`Docs/Pilots/PORTRAITS.md`) follow the set too.
   - **What draws through it:** every Graphic that names no material (the default UI material becomes
     `CosmicShore/UI/GradedDefault`, UI/Default with the grade, through `GradedDefaultUI`), so a sprite assigned
     later or a Graphic made in code is graded as well; the repo's UI shaders that sample a picture (CardDrift,
     HullLit, SilhouetteHolo, ElementRim); and the static sky (StaticSkyPanorama, the world table).
   - **What keeps its authored colours:** a Graphic that calls `ColorGrade.KeepAsAuthored` (the sign-in providers'
     marks, in SignInChooser and AccountSettingsView).
   - **The record:** `colour_grade.json` classifies every texture a build can load: 582 coloured canvas pictures,
     the static sky, 8 exempt with their reasons, 468 grey masks that need nothing. A new coloured texture that no
     graded shader draws fails the gate.
8. **The gate.** `python3 Tools/Build/colour_trace.py --check` fails when the scan and the table disagree (a new colour,
   a colour nothing authors any more, a baked value that moved), on a new untraced colour, when Photic's block is
   stale, when a set asset lacks the table's ids, when any value of any set could be read as another colour (or, for
   UI, as a theme token), when an authored UI colour sits inside a set's token tolerance, or when code loads a traced
   Resources material without painting it. `author_colour_set_assets.py --check` fails when a set's block drifted. `colour_grade.py --check` fails when a set's
   tables drifted from its colours (by more than 1/255) or a coloured texture is not graded. All of them run in the
   `/ship` gates (`Tools/Build/ship_gates.py`).
   `ColorTraceTests.EveryShippedSet_FindsWhatEveryOtherSetPainted` wears every shipped set and reads every value of
   every set back (54,120 lookups; run on the real `ColorTrace.cs` under .NET on 2026-11-10: 0 misread, against 79
   with the values the sets held before).

```
python3 Tools/Build/colour_trace.py --build                # the table, from the scan
python3 Tools/Build/author_colour_set_assets.py            # every set's TracedColors
python3 Tools/Build/colour_trace.py --write-photic         # Photic's block
python3 Tools/Build/colour_grade.py                        # every set's texture grade tables and the texture record
python3 Tools/Build/colour_trace.py --check                # the gate (and --self-test)
python3 Tools/Build/colour_trace.py --summary              # counts by status, rule, role
python3 Tools/Build/colour_trace.py --report               # what is untraced, by file
```

When a person changes a colour anywhere in the game, the gate fails until `--build` and the writers run: the new value
becomes Photic's, and every other set computes its own.

## 2. What is still outside the trace (measured 2026-11-10)

The ratchet list (`colour_trace.json` `untraced`) names each colour. What remains, by kind:

- **Colours baked into shader code: none left (2026-11-11).** A colour node or constant counts only when it is wired
  to the output (a ShaderGraph node with an edge path to a Block node) and a drawn material uses the shader. The last
  ones are now material colours saved at the sRGB encodings of the old constants (the shader sees the same numbers
  once Unity reads a Color into linear): the HyperSea sky's star ramp (`_StarColorHot/White/Warm/Cool`), the
  black-hole lens's ring, core, shell and gravity-wave glows (`_BHRingColour`, `_BHCoreColour`, `_BHShellColour`,
  `_BHWaveColour`), the Soliton tunnel's glint (`_MWGlintColour`) and CageGraph's nucleus blend (`_Nucleus_Dark`, a
  property node where the HDR Color node was). `colour_trace.json` lists 0 untraced colours.
- **Textures: graded, not re-rendered (2026-11-11).** See section 1.7. What the grade does not reach, each for a
  reason: the sign-in providers' own marks (Google's, Apple's; their brand rules fix them), the app icon and splash
  (the platform draws them before any script runs), TextMesh Pro's emoji atlas (no text draws one). A picture drawn
  by a custom UI shader that does not include `ColorGrade.cginc` would also escape it; the repo's four that sample a
  picture include it, and `colour_grade.py --check` fails on a coloured world texture nothing grades.
- **Copies made before a wear.** A colour copied out of a material or config into a cache before the set is worn
  keeps the old value until it is copied again: Spindle's leaned and phase variants, SpindleLodSystem's copies,
  CapsuleMembrane's draw colours, CellVisualTint's capture, SnowChanger's clones, PrismSlice's runtime material,
  CrystalAccentTint's arcs, ToyFactory's colour-keyed caches, fauna GPU palettes, vessel tails and jets painted at
  spawn. They are right after a scene load (the world painter runs before Start); after a Profile preview in
  Menu_Main they show the previous set until they repaint. Each owner can subscribe its re-apply to
  `ColorTrace.Changed`. (A copy written back into a material later is still found: the lookup asks every set worn
  in the session.)
- **Particles alive at a wear** keep their emitted start colour until they die (a fraction of a second).
- **UI prefabs loaded on first use** (`Resources.Load` of the quests window, a toast row) are not in memory at the
  repaint: their first instance is swept when it changes its canvas's count, a later one is born themed.
- **Baked lighting.** The four scenes with a lighting data asset keep their baked light probes; the ambient light
  and reflections the sky gives are recomputed on a sky repaint (`DynamicGI.UpdateEnvironment`).
- **Editor objects made with DontSave outside play** (an editor tool's preview objects) are painted during play and
  keep the colours afterwards; they are never saved.

## 3. The rule, and what still breaks it

**LOCKED (Garrett, 2026-11-10): every colour comes from the colour set.** "There is no reason for a vessel owner to be
breaking this rule: all colors come from a color set. If that rule is broken then it is a sign of incompleteness or a
bug, not something to be respected." Nothing here is anyone's decision; each break below is work, fixed where it is
found.

- Photic's UI was the theme's derivation, not the authored theme: fixed 2026-11-11 (section 1.4). The authored
  theme's soft text and danger sit at 3.6:1 and 4.4:1 on a panel, under the style guide's 4.5:1 for text; the
  derived themes lift theirs (`make_themes.fit`), Photic shows them as authored.
- The Voyage map, Grizzly, Thresher and Stoat colours: applied 2026-11-11 (they had been held back while those lanes
  had work in flight).
- The hulls and rounds that drew a fixed Jade or Ruby where their owner's domain should show: fixed 2026-11-11.
  The SkyBurst and Explodable projectiles' particles and the AI Gunner's gun drew `GreenVesselMaterial`, the
  template every domain's ship material is cloned from; they now take their owner's domain ship material
  (`ThemeManagerDataContainerSO.PaintDomainPlaceholders`, from `Projectile.LaunchProjectile` and `Gun`). The Goby's
  and Squirrel's `RedAccentVesselMaterial` is replaced at spawn on the live ship (a skinned mesh's slot 0); the
  display hulls (toybox matrices, replay ghosts) painted slot 1 instead, so they wore the team colour on the body:
  `VesselModelBuilder.ResolveDomainMaterials` now follows the live rule (`DomainMaterialSlotTests`). The Serpent's
  Blue and Lime sit on an inactive model nothing draws.
- The colours in shader code: none left (section 2).
- The textures: graded, every one a build can load (section 1.7), but for the marks whose owners fix them.
- The rest is section 2.

## 4. Where it came from

The rules were measured by eleven read-only audits on 2026-11-10 (sky and environment, the UI kit, HUD prefabs, menu
prefabs, scenes, particles and lines, materials and shaders, configs, C# literals with Roslyn, textures, and the
runtime painters). Their findings are the `INERT` rules in `colour_trace.py`, the review in `colour_trace_review.json`
and section 2 above.
