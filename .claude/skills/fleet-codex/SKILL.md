---
name: fleet-codex
description: The Fleet Codex - the ONE claude.ai artifact (https://claude.ai/artifact/JDvgFqu1bCfgcHVfCdXaA8) that shows every Cosmic Shore vessel's four abilities, controls, what each element scales (with the authored numbers), its level-5 upgrades and whether each is really wired, flight numbers, the game cards that seat it, its docs and its recent changes - all read from the shipped assets by Tools/Build/fleet_codex.py, never typed by hand. Use on "/fleet-codex", "open / show the fleet codex", "refresh / update / republish the codex", "the codex is out of date", "tell me everything about <vessel>", "which vessels have open L5s", "what does <element> do on <vessel>", after any change to a vessel's abilities, ElementalAbilityMap, prefab flight numbers, class asset or game cards, and from the daily scheduled refresh. Never publishes a second codex.
---

# Fleet Codex

**The artifact:** https://claude.ai/artifact/JDvgFqu1bCfgcHVfCdXaA8 (private to its owner until shared from its Share
menu). It is the ONLY codex: always republish to that URL, never create another.

| Piece | Path |
|---|---|
| Generator (a READER: writes only its `--out`, touches no asset) | `Tools/Build/fleet_codex.py` |
| The page: design B of the board's **Rev 48** (picked 2026-11-02; section 115 of `Docs/MENU_REDESIGN_B.md`), in the website's Silver Screen look: a top bar, a filmstrip of the fleet (crew vessels as wider posters with their seats), and the vessel as a bento: the hull, its five abilities (four elements and the omni crystal), ONE control map drawing controller, keyboard and phone with the picked ability lit, its games, the picked ability's full text, docs and changes. `#fleet` is the whole fleet as one table, `#<VesselKey>` one vessel | `Docs/FleetCodex/index.html` |
| Themes | five, picked from the top-right menu and remembered per viewer (`localStorage` `fleetCodexTheme`): Silver Screen (the default, follows light/dark), Tesseract, Endurance, Gargantua, HyperSea. A theme is only tokens on `html[data-skin]` (`--still`, `--display`, `--dscale`...); a new one is one token block plus one row in `THEMES` |
| The two hand tables (the only typed facts; each entry cites the doc or code it was read from) | `Docs/FleetCodex/abilities.json` (what each ability DOES, how it fires, what more of its element does, and what an omni crystal does: the maps record only what an element scales) and `Docs/FleetCodex/seats.json` (which seat of a crew vessel owns which ability) |
| Generated data, git-ignored | `Docs/FleetCodex/build/` (`fleet.json`, `img/<Key>.webp`) |
| What it reads and why | `Docs/FleetCodex/README.md` |

Where each fact comes from (so a wrong fact is fixed at its source, never in the page):

| On the page | Source |
|---|---|
| What an ability does, its trigger, the plain 'more Mass ...' line, what the omni crystal does | `Docs/FleetCodex/abilities.json`; an entry whose `ability` no longer equals the map's `AbilityLabel` is DROPPED with a stderr warning (re-read its `source` and fix the entry) |
| Crew seats (a card per seat with its abilities inside) | `Docs/FleetCodex/seats.json`; a seat not in `CrewNames` or an element the map lacks STOPS the build |
| An ability card's level-5 face (the L5 button flips it) | the upgrade's label, description, gate and status from the map. Its art is what the HUD draws at level 5: the icon kit's `CARDS` state named `<Element> 5` on that element's own card (not an options `group`), rendered through `states.compose` with domain parts in silver and shielded parts in the HUD's shielded signal (14 kit cards + 1 HUD sprite on 2026-11-02: the Tiger Moth's gilded keys, the Sparrow's shielded and piercing rounds, the Serpent's sealed holes...); else a Sprite the vessel's HUD view serializes under a `(<ELEMENT> 5)` header, read from the vessel prefab and drawn lit (the Serpent's `pelletSlotsFive`, "Fifth pellet (TIME 5)"); else a file in the hull's icon folder named after the upgrade; else the ability icon, because on every other hull the game's L5 is the lockup bloom plus the plate lift on the SAME icon (`AbilityLockupView.SetUpgraded`; no `upgradedSprite` is assigned anywhere) |
| The domain picker (Jade / Ruby / Gold, beside the stamp) and every colour it applies | `palette()` reads `OriginalColorSetSO.asset` with `SO_ColorSet`'s own maths: signal = `GetDomainSignalColor` (TrailHighlightColor, brightest channel to 1), shielded = `GetShieldedSignalColor` (ShieldedOutsideBlockColor.gamma); omni = `ElementalBarsConfig.asset` `limeColor`. Never a hand-picked colour |
| Icon colours (what the HUD paints each part) | **never a guess by pixel**. A HUD card: `hud_colours()` reads the HUD view script's `<image>.color = ...` writes outside hit/flash methods (following a field the script fills elsewhere, a null-checked ternary's true branch, a `tint.a > 0` ternary's tint, any other ternary's else), maps each field to its prefab Image, and falls back to the Image's authored `m_Color` (the icon's own Image ignores an alpha of 0: the lockup card draws its sprite). The omni card takes `SetOmniAbilityTint` from `<Key>HUDController.cs` (the Squirrel's shielded), else white. A kit card: the spec's resting state (`LOCKUP`, else the card's `lockup_state`), its named colours given the game's values. Values: domain and shielded from `palette()`; danger = `GetDangerSignalColor` (EnvironmentColors.Danger to full brightness); blue, lime, fire, grey, petal white = `ElementalBarsConfig`; white = `Color.white`. Domain and shielded parts are masks the page fills with the picked domain; the rest are baked. An icon whose colour only runtime code computes is recorded in `abilities.json` `tint` with its source (the Dolphin's seed: omni lime, the domain at Claimed Seed) |
| Vessel images (filmstrip, hero, fleet table) | the hangar preview rendered in the picked domain's livery by `vessel_model/render_cards.py` (`colors_for(scheme, domain)`, its camera and look) for the model the class asset's `PreviewImage` came from; cached in `build/img/dom/<model>_<Domain>_<hash>.webp` (hash = FBX + scheme + renderer + palette). `--render-domains` renders missing ones (needs `bpy`: `python3 -m venv --system-site-packages $SCRATCH/bpyenv && $SCRATCH/bpyenv/bin/pip install bpy`, then run the generator with that python; ~7 min for 57). Without it a vessel keeps its shipped preview (class livery) for every domain. Themes never tint a vessel |
| Ability, input (pad / keyboard), prose, L5 label + description | `Assets/Resources/ElementalAbilityMaps/<Key>.asset`, via `element_ability_table.py --json --verbose` |
| L5 status (wired / open / switched off / named-not-gated / gated-not-named) and its gate site | the `IsUpgradeActive` call sites the prefab reaches (`element_ability_table.py`) |
| "What X scales" (field, ×min → ×max, asset; struck through when switched off) | the `ElementalFloat`s the prefab reaches (`element_ability_table.py`) |
| Name, crew, description, hangar image | `Assets/_SO_Assets/Classes/SO_Class_*.asset` (`PreviewImage`) |
| Crew vessels (both names, seats) | the class asset's `CrewNames`; seats from "N-seat" in the `VesselClassType` comment, else the names' count (the Orca's 4 seats come from its enum comment: its class asset names no crew) |
| Each ability's icon | 1. the icon kit's resting card (`Tools/Build/icon_kit/specs/<hull>.py` `LOCKUP`, rendered in silver through `states.compose`); 2. the vessel's HUD (`abilityIcons` -> the Image's sprite, through the vessel prefab's overrides and the variant chain), accepted only when the HUD is the hull's own or the art sits in `AbilityIcons/<Hull>/` (the Wrasse borrows the Sparrow's HUD); 3. the HUD card's visible child parts composited, alternate states dropped (a part whose name extends another's); 4. the hull's own icon named after the ability (`Manta_Kabloom.png`). None of these: the page shows the element's Codex shape |
| The omni crystal ability | the map's `omniAbilityLabel`, else the vessel container's `vesselCrystalEffects` named (the shared haptics / morph / absorb effects skipped); its icon is the kit's `LOCKUP["Omni"]` or the HUD's `omniAbilitySprite`, else the omni crystal |
| Element marks, controller glyphs, phone art | `Assets/_Graphics/ElementShapes/*_label.png` (the Codex's, never the HUD petal); the Xbox face buttons and `Trigger_LT` / `Trigger_RT` (`PAD_ART` in the generator; not `ControlGlyphSet`, which draws a PlayStation L1 for LT); `TouchIcon_*` / `TouchGlyph_*` in `HUD UI/Touch` |
| Game card art | each card's `CardBackground`, shown in black and white |
| Id and one-line note | `VesselClassType.cs` (value and line comment) |
| In the Vessel Changer | `ToyVesselRoster.Default` |
| Flight numbers | the prefab (`regatta_balance.read_vessel`: `Default*` throttle/minimum, turn scalers, boost) |
| Controls: the Steer row | the prefab's transformer class: one stick when it descends from `SingleStickVesselTransformer` |
| Controls: each ability's Controller and PC button | the ability map's `Input` (`element_ability_table.INPUT_EVENTS`: pad and keyboard labels) |
| Controls: other bound actions (Fire, Ring, Bomb...) | the prefab's `_inputEventShipActions` (and `_touchActionOverrides`, listed as a note) |
| Controls: the Mobile column and notes | `Tools/Build/author_touch_profiles.py` `PROFILES` (thumb lifts and glyphs, cards, extra buttons, slider, tilt); a hull with no row is the default thumb-lift profile |
| Games | `Assets/_SO_Assets/Games/ArcadeGame*.asset` `Vessels` lists + the Arcade/Arena roster lists (`vessel_mode_chart.cards`) |
| Docs | `.md` files named after the vessel (vessel docs, models, studios, its modes) + paths in its enum comment |
| Recent changes | `git log` on the vessel's prefab, map, action assets, scripts named after it and its docs |

Everything is keyed by the `VesselClassType` member, so a class asset's display name ("Brittle Star") and its map's
name ("BrittleStar") never split one vessel into two rows.

## 1. Refresh (regenerate and republish)

Do this after any vessel change lands, when asked, and in the daily scheduled run.

1. **Read from bleeding-edge**, not from your feature branch, so the page states what the game ships:
   ```bash
   git fetch origin bleeding-edge
   git worktree add --detach "$SCRATCH/be" origin/bleeding-edge
   ```
   If `Tools/Build/fleet_codex.py` is not on bleeding-edge yet, run the copy in your own checkout against it
   instead and say so; the page's stamp names the branch and commit it read either way. `$SCRATCH` is your scratchpad
   directory (Artifact publishes only accept files under the working directory or the scratchpad).
2. **Generate** (about 2.5 min, ~9 with `--render-domains`, which a refresh should pass whenever a model, the colour scheme or the palette changed; rendering the icon kit's cards makes most of it, and it needs numpy and Pillow: without numpy the kit cards fall back to the HUD sprites and the run says so on stderr):
   ```bash
   python3 "$SCRATCH/be/Tools/Build/fleet_codex.py" --out "$SCRATCH/codex" --files-json
   cp "$SCRATCH/be/Docs/FleetCodex/index.html" "$SCRATCH/codex-index.html"
   ```
   `img/dom/` holds the domain renders, more than the 255 files one publish takes: publish `img/dom/*` in its own call first, then the page with the rest.
   It prints a `contentHash` (it ignores the timestamp and commit) and a `FILES [...]` line.
   Read its stderr: an `abilities.json:` warning means an ability was renamed or replaced; read the entry's `source`, rewrite the entry, and commit it with the refresh.
3. **Skip when nothing changed.** Read the published data (`Artifact` `action: "read"`, `url` the codex,
   `path: "fleet.json"`) and compare its `contentHash`. Equal and the page file unchanged: stop, and report "no
   change". (The read also satisfies the rule that a session reads an artifact before publishing to it.)
4. **Publish** to the SAME url: `Artifact` with `url: https://claude.ai/artifact/JDvgFqu1bCfgcHVfCdXaA8`,
   `file_path: $SCRATCH/codex-index.html`, `root: $SCRATCH/codex`, `files:` the printed `FILES` list. No `icon`
   (it keeps "ship"). Report the content hash, the commit read and what changed (vessels added, L5 wired counts).
5. `git worktree remove "$SCRATCH/be"`.

## 2. Answer a question from it

For "tell me about <vessel>" or "which vessels have open L5s", generate (or read the published `fleet.json`) and
answer from the data, then link the vessel's view: `<codex url>#<VesselKey>` (e.g. `#BrittleStar`). For a single
element question `python3 Tools/Build/element_ability_table.py <Vessel>` is faster (1-6 s) and is the same source.

## 3. Change what the codex shows

- **A wrong or missing fact:** fix its SOURCE (the table above), never the page or `fleet.json`. A doc that disagrees
  with the codex is the doc's bug (`/vessel` §2: assets and code are the record).
- **A new field:** add it in `fleet_codex.py` (read it from an asset or code, never type it), draw it in
  `index.html`, run the generator, look at one vessel and the matrix once, republish (§1), commit both files.
- **A new vessel** needs nothing here: its enum member, class asset and map put it on the page at the next refresh.
- The page follows the artifact page contract: tokens for both themes, no colour per element (elements have no
  colour, `Docs/Icons/ABILITY_ICON_SKILL_PLAN.md`), ASCII-only source (entities for symbols), phone width.

## 4. The daily refresh

A scheduled task (created 2026-11-02 by Shombith's request, "Fleet Codex refresh") starts a fresh session every day
that runs §1 (`trig_01AewfLUV66vYQC6b84FRFm5`, 05:41 UTC). Manage it with the scheduled-task tools (`list_triggers`); never create a second one.

## Rules

- One codex artifact. Never publish a second one, and never pass `force`.
- Never hand-edit `fleet.json` or commit `Docs/FleetCodex/build/`. The only typed facts are the two tables (`abilities.json`, `seats.json`), and each entry names its source; a doc that disagrees with the code loses (`/vessel` §2).
- The generator is a reader: it must never write an asset, and it must never guess (a vessel with no map shows "no
  map", a hull with no prefab numbers shows none).
- Owners: the vessels belong to their owners (`who_works_where.py`); the codex only reads them. The skill and
  generator are tooling (Shombith).
