---
name: fleet-codex
description: The Fleet Codex - the ONE claude.ai artifact (https://claude.ai/artifact/JDvgFqu1bCfgcHVfCdXaA8) that shows every Cosmic Shore vessel's four abilities, controls, what each element scales (with the authored numbers), its level-5 upgrades and whether each is really wired, flight numbers, its game for each element (Charge game, Mass game, Space game, Time game) and the arena cards that seat it, its docs and its recent changes, in the game's colour-set themes (Nocturne, Photic, Dart Frog) - all read from the shipped assets by Tools/Build/fleet_codex.py, never typed by hand. Use on "/fleet-codex", "open / show the fleet codex", "refresh / update / republish the codex", "the codex is out of date", "tell me everything about <vessel>", "which vessels have open L5s", "what does <element> do on <vessel>", "what is <vessel>'s <element> game", the codex's themes, look or animated controls, after any change to a vessel's abilities, ElementalAbilityMap, prefab flight numbers, class asset or game cards, and from the daily scheduled refresh. Never publishes a second codex.
---

# Fleet Codex

**The artifact:** https://claude.ai/artifact/JDvgFqu1bCfgcHVfCdXaA8 (private to its owner until shared from its Share
menu). It is the ONLY codex: always republish to that URL, never create another.

| Piece | Path |
|---|---|
| Generator (a READER: writes only its `--out`, touches no asset) | `Tools/Build/fleet_codex.py` |
| The page: design C of the board's **Rev 68**, drawn whole as **Rev 69** (picked by Shombith 2026-11-08, "Go ahead build it"; sections 143-144 of `Docs/MENU_REDESIGN_B.md`). One vessel at a time, every detail, calm: a top bar with a vessel **switcher** (previous, the vessel opening the whole fleet as a menu, next), search, the theme and the domain; the **flight-deck window** (the hull flying, the pilot briefing with the story and numbers, its element games with the one Lime call to action, the picked ability's **move** - a scene of it happening by element - with the controller button, key and phone touch pressing on its clock, the ability row); then hairline sections: **Abilities** (one row each: how it fires, what it does, what more of its element does with the authored numbers, its level 5; a crew's rows under seat headers), **Missions** (the games, filtered by element, Arcade, Arena), **Flight and controls** (the numbers and every action on every device), **Recent updates and docs**. `#fleet` is the whole fleet as one table, `#<VesselKey>` one vessel. Type: Saira Condensed and Saira (the board's pick, a departure from the style guide's Aldrich / Chakra Petch). Responsive: below 1100 px the window stacks | `Docs/FleetCodex/index.html` |
| Themes | **three, the game's colour sets: Nocturne, Photic, Dart Frog** (Shombith, 2026-11-08; the film-named Silver Screen / Tesseract / Endurance / Gargantua / HyperSea moved to these, as the style guide's Themes board says). The generator writes `themes.css` and `theme.js` into `--out` with the analytics console's own builder (`make_themes.build(sets=...)`, from `ColorSetCatalog.asset` and the Deep Sea lightness steps), so the codex and the console are one look; the page maps its own names (`--bg`, `--ink`, `--accent`...) onto the `--fs-*` tokens. Picked from the top-right menu, remembered per viewer (`localStorage` `fleetCodexColourSet`), the game's live set by default. Never type a theme colour into the page: a new theme is a colour set in the game's catalog and a row in `THEME_SETS` |
| Games by element | each card's element is the game's own answer, `ModeGenre.TryElementsFor` (the mode's row, else its scoring metric), read by `Tools/Analytics/server/mode_elements.py` `build()`. The page shows the vessel's element game per element ("Squirrel, Charge game: Joust; Mass game: Scurry; Time game: Skim Race"), an arcade card (`ArcadeGames` roster) as the element game, arena cards beneath with their element, a mode with no element under "also". An element with no arcade card says "None yet". A wrong element is fixed in `ModeGenre.cs`, never in the page |
| The move and its gestures | the move's scene is drawn by the ability's ELEMENT (Charge pops a crystal, Mass throws a ring of blocks, Space flips prisms to your domain, Time fills the boost bar, the omni crystal opens into a ring), so every vessel has one; the devices press with the ability's own gesture (tap, hold, hold then release, tap on / tap off) read from its `abilities.json` `trigger` words (`gesture()` in the page), on one 3.2 s clock with the scene; a passive ability shows the sticks and thumbs just flying. The style guide allows this motion ("the hint that teaches the next control"); only transform and opacity move, on one eased curve; reduced motion holds the pressed pose still. A bespoke scene per ability (the board's Squirrel scenes are the element scenes) would be a new field, read from a table, never typed into the page |
| The two hand tables (the only typed facts; each entry cites the doc or code it was read from) | `Docs/FleetCodex/abilities.json` (what each ability DOES, how it fires, what more of its element does, and what an omni crystal does: the maps record only what an element scales) and `Docs/FleetCodex/seats.json` (which seat of a crew vessel owns which ability) |
| Generated data, git-ignored | `Docs/FleetCodex/build/` (`fleet.json`, `img/<Key>.webp`) |
| What it reads and why | `Docs/FleetCodex/README.md` |

Where each fact comes from (so a wrong fact is fixed at its source, never in the page):

| On the page | Source |
|---|---|
| The Flight Certificate (solo vessels: five checks, 20 % each, shown as the percentage only - no stage names, Shombith 2026-11-10; Rev 84, section 155) | `fleet_codex.py` `certificate()`: the map (every ability), the L5 status (5 % per WIRED upgrade), the vessel's arcade cards by element (Time; Mass or Space; Charge). Never typed |
| Each element's tier (S, A, B, C) | `Docs/FleetCodex/tiers.json`, the third hand table: a person's rating (Shombith's, gathered with Garrett on the Fleet Tier Sheet https://claude.ai/artifact/76HqmyoHRaWSM14zTcnPVt); `?` until rated, a dash where the vessel has no game of that element |
| What an ability does, its trigger, the plain 'more Mass ...' line, what the omni crystal does | `Docs/FleetCodex/abilities.json`; an entry whose `ability` no longer equals the map's `AbilityLabel` is DROPPED with a stderr warning (re-read its `source` and fix the entry) |
| Crew seats (a card per seat with its abilities inside) | `Docs/FleetCodex/seats.json`; a seat not in `CrewNames` or an element the map lacks STOPS the build |
| An ability card's level-5 face (the L5 button flips it) | the upgrade's label, description, gate and status from the map. Its art is what the HUD draws at level 5: the icon kit's `CARDS` state named `<Element> 5` on that element's own card (not an options `group`), rendered through `states.compose` with domain parts in silver and shielded parts in the HUD's shielded signal (14 kit cards + 1 HUD sprite on 2026-11-02: the Tiger Moth's gilded keys, the Sparrow's shielded and piercing rounds, the Serpent's sealed holes...); else a Sprite the vessel's HUD view serializes under a `(<ELEMENT> 5)` header, read from the vessel prefab and drawn lit (the Serpent's `pelletSlotsFive`, "Fifth pellet (TIME 5)"); else a file in the hull's icon folder named after the upgrade; else the ability icon, because on every other hull the game's L5 is the lockup bloom plus the plate lift on the SAME icon (`AbilityLockupView.SetUpgraded`; no `upgradedSprite` is assigned anywhere) |
| The domain picker (Jade / Ruby / Gold, beside the stamp) and every colour it applies | `palette()` reads the live palette (the `SO_ColorSet` `ThemeManagerDataContainer.ColorSet` names, by guid; `NocturneColorSetSO.asset` since 2026-11-03) with `SO_ColorSet`'s own maths: signal = `GetDomainSignalColor` (TrailHighlightColor, brightest channel to 1), shielded = `GetShieldedSignalColor` (ShieldedOutsideBlockColor.gamma); omni = `ElementalBarsConfig.asset` `limeColor`. Never a hand-picked colour |
| Icon colours (what the HUD paints each part) | **never a guess by pixel**. A HUD card: `hud_colours()` reads the HUD view script's `<image>.color = ...` writes outside hit/flash methods (reading `ColorTrace.Retrace(role, c)` as `c`, the authored colour the worn set repaints; following a field the script fills elsewhere or a `Color X => ...` property, a null-checked ternary's true branch, a `tint.a > 0` ternary's tint, any other ternary's else), maps each field to its prefab Image, and falls back to the Image's authored `m_Color` (the icon's own Image ignores an alpha of 0: the lockup card draws its sprite). The omni card takes `SetOmniAbilityTint` from `<Key>HUDController.cs` (the Squirrel's shielded), else white. A kit card: the spec's resting state (`LOCKUP`, else the card's `lockup_state`), its named colours given the game's values. Values: domain and shielded from `palette()`; danger = `GetDangerSignalColor` (EnvironmentColors.Danger to full brightness); blue, lime, fire, grey, petal white = `ElementalBarsConfig`; white = `Color.white`. Domain and shielded parts are masks the page fills with the picked domain; the rest are baked. An icon whose colour only runtime code computes is recorded in `abilities.json` `tint` with its source (the Dolphin's seed: omni lime, the domain at Claimed Seed) |
| Vessel images (switcher, window, fleet table) | the hangar preview rendered in the picked domain's livery by `vessel_model/render_cards.py` (`colors_for(scheme, domain)`, its camera and look) for the model the class asset's `PreviewImage` came from; cached in `build/img/dom/<model>_<Domain>_<hash>.webp` (hash = FBX + scheme + renderer + palette). `--render-domains` renders missing ones (needs `bpy`: `python3 -m venv --system-site-packages $SCRATCH/bpyenv && $SCRATCH/bpyenv/bin/pip install bpy`, then run the generator with that python; ~7 min for 57). Without it a vessel keeps its shipped preview (class livery) for every domain. Themes never tint a vessel |
| Ability, input (pad / keyboard), prose, L5 label + description | `Assets/Resources/ElementalAbilityMaps/<Key>.asset`, via `element_ability_table.py --json --verbose` |
| L5 status (wired / open / switched off / named-not-gated / gated-not-named) and its gate site | the `IsUpgradeActive` call sites the prefab reaches (`element_ability_table.py`) |
| "What X scales" (field, ×min → ×max, asset; struck through when switched off) | the `ElementalFloat`s the prefab reaches (`element_ability_table.py`) |
| Name, crew, description, hangar image | `Assets/_SO_Assets/Classes/SO_Class_*.asset` (`PreviewImage`) |
| Crew vessels (both names, seats) | the class asset's `CrewNames`; seats from "N-seat" in the `VesselClassType` comment, else the names' count (the Orca's 4 seats come from its enum comment: its class asset names no crew) |
| Each ability's icon | 1. the icon kit's resting card (`Tools/Build/icon_kit/specs/<hull>.py` `LOCKUP`, rendered in silver through `states.compose`); 2. the vessel's HUD (`abilityIcons` -> the Image's sprite, through the vessel prefab's overrides and the variant chain), accepted only when the HUD is the hull's own or the art sits in `AbilityIcons/<Hull>/` (the Wrasse borrows the Sparrow's HUD); 3. the HUD card's visible child parts composited, alternate states dropped (a part whose name extends another's); 4. the hull's own icon named after the ability (`Manta_Kabloom.png`). None of these: the page shows the element's Codex shape |
| The omni crystal ability | the map's `omniAbilityLabel`, else the vessel container's `vesselCrystalEffects` named (the shared haptics / morph / absorb effects skipped); its icon is the kit's `LOCKUP["Omni"]` or the HUD's `omniAbilitySprite`, else the omni crystal |
| Element marks, controller glyphs, phone art | the element's FLOWER, drawn as the HUD draws it (`KitElementFlower` / `ElementalBarsView.ConfigurePetal`): its petal sprite `Assets/Resources/ElementPetals/<element>_petal.png` (copied UNCROPPED: the petal sits in the sprite's upper half) five times, each turned 72 degrees clockwise, tinted with `ElementalBarsConfig`'s grey and white ticks (`palette.petal`). Shombith 2026-11-08 asked for the upgrade flowers animated on L5, then "weird orientation" on the `ElementIcons/icon_<el>Upgrade_*` PNGs (an older drawing the game does not use for this), so the page builds the flower like the game; a row's Play L5 lights its petals one at a time, petal 0 first. Ability icons carry no tile behind them; the Xbox face buttons and `Trigger_LT` / `Trigger_RT` (`PAD_ART` in the generator; not `ControlGlyphSet`, which draws a PlayStation L1 for LT); `TouchIcon_*` / `TouchGlyph_*` in `HUD UI/Touch` |
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
| Recent updates | NEVER a commit message (Shombith 2026-11-08: "dont show commit messages", then "show recent changes by when somebody updates a game or a vessel"). `changes()` reads git's FILE lists for the vessel's map ("abilities and level-5 upgrades"), prefab ("the vessel"), action assets, class asset, ability icons, model and scripts named after it, and for each of its game cards and the card's scene ("Game updated: Skim Race"), and writes one entry per day: `{date, parts, games}`. A shallow clone's edge commits (listed in `.git/shallow`) look like they add every file and are skipped, so a shallow checkout shows only the days it holds: `git fetch --deepen=200` first for a longer list |

Everything is keyed by the `VesselClassType` member, so a class asset's display name ("Brittle Star") and its map's
name ("BrittleStar") never split one vessel into two rows.

## 1. Refresh (regenerate and republish)

Do this after any vessel change lands, when asked, in the daily scheduled run, and at the end of every ship that
touched a vessel (`/ship` §5.6, all modes).

1. **Build on bleeding-edge, never on a feature branch alone** (Shombith, 2026-11-03: "codex should also read from
   bleeding edge" - the live page had been published from `claude/optimistic-allen-jxrbrw`). `fleet_codex.py` now
   REFUSES a tree that does not contain `origin/bleeding-edge` (exit 1, naming the cause; `--allow-off-bleeding-edge`
   is for a local look and is never published):
   ```bash
   git fetch origin bleeding-edge
   git worktree add --detach "$SCRATCH/be" origin/bleeding-edge
   ```
   **Then fold in the PENDING branches**: shipped branches whose facts bleeding-edge does not carry yet. The list is the
   published `fleet.json`'s `"pending"` (read it in step 3's read) plus, from `/ship` §5.6, the branch being shipped.
   Drop each that has landed (`git merge-base --is-ancestor origin/<it> origin/bleeding-edge`) or no longer exists;
   merge the rest into the worktree (`git -C "$SCRATCH/be" merge --no-ff --no-edit origin/<it>`, local only, never
   pushed). One that conflicts is aborted, left out and named in the report. Pass the survivors to the generator so
   the stamp carries them and the next run keeps them until they merge:
   `--branch-label "bleeding-edge + <pending>" --pending <it> ...` (with no pending branch: `--branch-label bleeding-edge`). `$SCRATCH` is your scratchpad
   directory (Artifact publishes only accept files under the working directory or the scratchpad).
2. **Generate** (about 2.5 min, ~9 with `--render-domains`, which a refresh should pass whenever a model, the colour scheme or the palette changed; rendering the icon kit's cards makes most of it, and it needs numpy and Pillow: without numpy the kit cards fall back to the HUD sprites and the run says so on stderr):
   ```bash
   python3 "$SCRATCH/be/Tools/Build/fleet_codex.py" --out "$SCRATCH/codex" --files-json \
     --branch-label "bleeding-edge[ + <pending>]" [--pending <branch> ...]
   cp "$SCRATCH/be/Docs/FleetCodex/index.html" "$SCRATCH/codex-index.html"
   ```
   **Seed the domain renders first**: they are cached only in `--out`, so a fresh folder renders none and every vessel
   falls back to its plain preview in every domain (a silent regression of the live page). Before generating, read the
   published `img/dom/*` into `$SCRATCH/codex` (Artifact `read` with `paths`, `out_dir` the codex folder); their names
   carry a hash of the model, so an unchanged hull reuses its file and a changed one is dropped (then pass
   `--render-domains`).
   **Publish in batches of at most 255 files, `fleet.json` in the LAST batch**, each batch with the page as `file_path`
   (2026-11-03: 387 files). Later batches add to what is there, and the data goes last so no published version points
   at art that has not arrived yet.
   It prints a `contentHash` (it ignores the timestamp and commit; it covers the themes' hash, so a colour-set change
   republishes) and a `FILES [...]` line, which includes `themes.css` and `theme.js`.
   Read its stderr: an `abilities.json:` warning means an ability was renamed or replaced; read the entry's `source`, rewrite the entry, and commit it with the refresh.
3. **Skip when nothing changed.** Read the published data (`Artifact` `action: "read"`, `url` the codex,
   `path: "fleet.json"`) and compare its `contentHash`. Equal and the page file unchanged: stop, and report "no
   change". **Reading `fleet.json` does NOT count as reading the page** (2026-11-08: the publish was refused twice):
   when `index.html` changed, also `Artifact` `action: "read"` the codex with no `path`, Read the saved source to its
   LAST line, and diff it against the committed `Docs/FleetCodex/index.html` you built on (`git show <base>:...`). The
   only difference should be the publish wrapper (a `<!doctype html>...<body>` first line and a closing
   `</body></html>`); anything else is someone's edit to merge. When a refusal hands you the live source and yours
   already includes it, it says to resend unchanged; do.
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
- **Text from an asset:** Unity writes a double-quoted YAML scalar with escapes (`You’re`); `yaml_scalar` decodes
  them (`_yaml_unescape`). A literal `\uXXXX` on the page means a reader bypassed it.
- **A new field:** add it in `fleet_codex.py` (read it from an asset or code, never type it), draw it in
  `index.html`, run the generator, look at one vessel and the matrix once, republish (§1), commit both files.
- **A new class name in `index.html` collides with the page's own** (2026-11-10, Rev 84): `.stage` is the move scene's
  150 px box and `.row` the four-column ability row, so a certificate label named `stage` drew a black box and a filter
  row named `row` took the ability grid. `grep -n '^\.<name>' Docs/FleetCodex/index.html` before naming a class, and
  look at the page in Chromium (vessel page and `#fleet`) before publishing.
- **A new vessel** needs nothing here: its enum member, class asset and map put it on the page at the next refresh.
- The page follows the artifact page contract and the UI style guide: colours only from the colour-set tokens
  (`themes.css`), no colour per element (elements have no colour, `Docs/Icons/ABILITY_ICON_SKILL_PLAN.md`), the corner
  sliver and nothing rounded, ASCII-only source (entities for symbols), phone width.

## 4. The daily refresh

A scheduled task (created 2026-11-02 by Shombith's request, "Fleet Codex refresh") starts a fresh session every day
that runs §1 (`trig_01AewfLUV66vYQC6b84FRFm5`, 05:41 UTC). Manage it with the scheduled-task tools (`list_triggers`); never create a second one.

## Rules

- One codex artifact. Never publish a second one, and never pass `force`.
- Built on bleeding-edge (plus the pending shipped branches), never on a feature branch alone; the generator refuses
  anything else.
- Never hand-edit `fleet.json` or commit `Docs/FleetCodex/build/`. The only typed facts are the two tables (`abilities.json`, `seats.json`), and each entry names its source; a doc that disagrees with the code loses (`/vessel` §2).
- The generator is a reader: it must never write an asset, and it must never guess (a vessel with no map shows "no
  map", a hull with no prefab numbers shows none).
- Owners: the vessels belong to their owners (`who_works_where.py`); the codex only reads them. The skill and
  generator are tooling (Shombith).
