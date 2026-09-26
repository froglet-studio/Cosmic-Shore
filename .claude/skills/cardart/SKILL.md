---
name: cardart
description: Use to make or refresh an arcade/arena card's BACKGROUND image - the picture behind a mode's card in the Arcade and Arena grids - by rendering that mode's own intensity-2 arena OFFLINE (no editor) from the shipped generators, courses and spawn profiles. Run it as the card-art step of every /arcadegame or /arenagame mode, and whenever an arena generator, a course generator, a cell config's EnvironmentPrefab or spawn profile, or the domain palette changes. Trigger on SO_ArcadeGame.CardBackground, Assets/_Graphics/ARCADE/CardBackgrounds/**, Tools/Build/render_card_backgrounds.py, Tools/Build/card_art_harness/**, or "card image / card background / card art / thumbnail for a mode".
---

# Card Art — render a mode's card background from its own arena

A card's backdrop is `SO_ArcadeGame.CardBackground` (`GameCard.UpdateCardView`). The obvious way
to make one - fly the mode at intensity 2 and press **0** - needs the running editor, per mode,
forever. It is unnecessary: **an arena is data.** The environment generators emit prism poses,
the course generators emit gates, the spawn profile says what grows where. So the card is made
the way the mode preview's scale model is made (`CellMiniatureBuilder`,
`ModePreviewPlantingModel`), offline, by RUNNING that data - and it is re-made, byte-identically,
whenever the data moves. This is the `/asset-surgery` doctrine applied to pictures: the harness
guarantees STRUCTURE (the geometry is the shipped geometry), the human judges MEANING (does it
read as the mode?).

## 0. The whole loop

```
python3 Tools/Build/render_card_backgrounds.py --report          # what each card is built from
CARD_SHEET=<scratch>/sheet.png \
python3 Tools/Build/render_card_backgrounds.py --check --sheet   # render to temp + contact sheet
#   ... Read the sheet (judge at CARD size), tune the recipe, repeat ...
python3 Tools/Build/render_card_backgrounds.py                    # write PNGs, import, rewire
python3 Tools/Build/render_card_backgrounds.py --check            # must pass (byte-identical)
python3 Tools/Build/author_card_backgrounds.py --strict           # every live card wired
```

`--check --sheet` is the iteration loop: it renders everything into a temp dir, writes a contact
sheet in roster order at half size, and touches nothing in `Assets/`. Commit the PNGs, their
`.meta`, the rewired `ArcadeGame<Mode>.asset` and `Tools/Build/card_backgrounds_manifest.json`.
Needs a dotnet 8 SDK (`~/.dotnet`; a per-user install takes ~40 s, see `/asset-surgery` §4). The
whole roster renders in ~35 s.

## 1. The four tiers — every card states which one it is

| tier | source | a new mode gets it when |
|---|---|---|
| **RUN** | the shipped generator compiled + run in `Tools/Build/card_art_harness/` with every field read off the prefab the intensity-2 cell config's `EnvironmentPrefab` (or the preview's `TrackSpawnablesByIntensity[1]`) names | automatically, if its generator is in `SOURCES` |
| **COURSE** | a shipped pure course generator (`SwitchbackCourse`, `HeadlongCircuit`, `RedlineCourse`, `RegattaCourse`, `BreakwaterCourse` + `BreakwaterStationBuilder`) at intensity 2, on the card's fixed seed, with the shell/gate count mirrored from `GateRaceController.BuildCourse` | a branch in `recipe()` |
| **MODEL** | what only exists at runtime (a grown forest, a controller-built court): the planting measured as `ModePreviewPlantingModel` measures it, each plant a species GLYPH; a court read off the controller's own settings asset | a branch in `recipe()` |
| **MONTAGE** | a meta-mode with no arena (Maelstrom): slanted strips of the cards it can draw | — |

**Never hand-paint a card.** `--check` re-renders and byte-compares, so a hand edit reads as a
stale card. The one sanctioned opt-out for a real screenshot is `HAND_CAPTURED` in the renderer.

## 2. Adding a new mode's card (the /arcadegame and /arenagame step)

1. **`--report`.** The roster is READ off `ArcadeGames.asset` + `ArenaGames.asset`, the arena off
   the mode's own scene (`Cell.CellConfigs`, IntensityWise → index 1) with the preview definition
   as fallback. A mode whose intensity-2 config names an `EnvironmentPrefab` is RUN-tier with no
   code at all — if its generator is compiled into the harness.
2. **Is the generator compiled?** Add its `.cs` (and any pure course file it calls) to `SOURCES`
   and run. Compile errors are shims to add in `UnityShim.cs` / `PlatformShim.cs`. Two rules:
   **geometry shims are FAITHFUL** (a rotation that is merely non-throwing photographs a world of
   axis-aligned prisms), and **every lay-path stand-in is LOUD** (`Debug.LogError` → the run
   refuses to photograph). A generator that lays in `Spawn()` rather than `GenerateTrailData`
   must be read through a public PREVIEW API (`SpawnableWaypointTrack.GetPreviewBlocks` is the
   worked example) — never by letting the harness reach a lay.
3. **Prove fidelity by COUNT.** Compare the harness's prism count against the number the mode's
   own doc or budget tool states. Shipped: Cleave's Swell **14,277**, Hijack's Switchyard **3,978**,
   Scurry's shells **24,966** all reproduce to the prism. A count that disagrees is a shim bug,
   not a tuning question.
4. **No generator?** Add a `recipe()` branch — a COURSE mirror, or a MODEL read off the
   controller's settings asset. Unknown stems RAISE, so a new card cannot silently ship blank.
5. **Frame + stage.** A row in `CAMERAS` (azimuth, elevation, distance × framed radius, fov), and
   in `accents()` the mode's **signature act** — something the mode actually SCORES on (a Dolphin
   cone into a forest, a bloom, a tracer, a creature in a cage, a crystal for a crystal race).
   Cards that share an arena (the cactus forest ×3, the Boneyard ×3, the cages ×2) are told apart
   by camera and act, never by forking the arena.
6. **Judge the sheet at card size** (`Read` it). The plate is 313×208 behind a title, an avatar
   row, a star and a genre petal; a card reads first as a colour and a silhouette. Then write,
   `--check`, `--strict`, commit.

## 3. Traps already paid for (each one shipped a wrong picture first)

- **A `CapsuleMembrane`'s radius is a FIELD (`radius: 1200`), its transform scale is 1** — reading
  `Cell.MembraneRadius` as the root scale modelled every forest inside a one-unit cell.
- **Unity writes a primitive ARRAY as one hex blob** (`bandDomains: 03000000...`). `int()` drops the
  leading zero and mangles it; the driver keeps raw tokens and the harness decodes by the C#
  field's own type.
- **The nucleus is GLASS**, not a ball: drawn opaque it swallowed Joust's whole arena. It (and the
  membrane) are analytic fresnel shells, depth-aware, and near-invisible from inside.
- **Frame the MASS, not the bounds.** One outlier or one long rail makes the bounds diagonal the
  whole cell; the camera frames the samples' centroid at a percentile radius, and a sparse arena
  (Hijack's rails, a gate race) is shot close where the mass is.
- **A controller mutates the arena at runtime** (Wrecking Ball and Scramble resize the nucleus into
  the court): read the controller's settings asset, not the cell config's nucleus prefab.
- **A course generator needs the controller's inputs** (`InnerRadius`/`OuterRadius` from
  `ResolveShell`, gate count from `EndConditionOverrides`, per-lap split): `Generate` with bare
  `ForIntensity` settings returns null.
- **Seeds are int32.** Mask derived seeds to non-negative 31 bits on both sides of the boundary.

## 4. What this is NOT

It is not the game's renderer and does not claim to be: it draws the SAME geometry in the SAME
palette (`OriginalColorSetSO` — base face lerped to rim by fresnel, the prism shader's defining
read, `Docs/PALETTE.md` §2) with ACES and a bloom. A MODEL-tier plant is a glyph and a staged
pilot is staging — say so when showing a card. What stays editor-only is whether it LOOKS right
in the grid beside the others; that is the one playtest this needs.
