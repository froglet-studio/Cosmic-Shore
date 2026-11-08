# Fleet Codex

The artifact: https://claude.ai/artifact/JDvgFqu1bCfgcHVfCdXaA8. One page for every vessel: its four abilities and
their buttons, what each element scales (with the authored numbers), the level-5 upgrades and whether each is really
wired, flight numbers, the game cards that seat it, its docs and its recent changes. Asked for by Shombith,
2026-11-02: "an artifact which shows all these details for all vessels and is updated when new updates are added or
abilities are changed".

- `index.html`: the page, design B of the design board's Rev 48 (section 115 of `Docs/MENU_REDESIGN_B.md`), in the Froglet UI style guide's look (`Docs/UI_STYLE_GUIDE.md`, Frogma's Style Guide tab: Aldrich and Chakra Petch, the corner sliver, prism panels, the sea lines) as the analytics console draws it. It fetches `fleet.json`, `themes.css`, `theme.js` and `img/` from beside it: a filmstrip of the fleet (crews as crews), the vessel as a bento of tiles, its five abilities with the game's own icons, its games one element at a time, and one control map drawing controller, keyboard and phone with the picked ability's control playing its gesture.
- `abilities.json`: what each ability does, how it fires and what more of its element does, plus each hull's omni crystal (written 2026-11-02 from each vessel's mechanics doc or code, cited per entry). The maps record only what an element scales, so this is a hand table: the generator drops an entry whose ability was renamed and warns.
- `seats.json`: which seat of each crew vessel owns which ability, cited per crew doc; the build fails when it stops matching the class asset or map.
- Themes: the game's three colour sets, **Nocturne, Photic and Dart Frog** (Shombith, 2026-11-08; the film-named themes moved to these), from the top-right menu, remembered per viewer (`localStorage` `fleetCodexColourSet`), the game's live set by default. `fleet_codex.py` writes them as `themes.css` / `theme.js` with the analytics console's own builder (`Tools/Analytics/console/make_themes.py`, `build(sets=...)`) from `ColorSetCatalog.asset`, so the codex and the console are always one look.
- Games: one per element (Squirrel: Charge game Joust, Mass game Scurry, Time game Skim Race). A card's element is the game's own answer, `ModeGenre.TryElementsFor` (read by `Tools/Analytics/server/mode_elements.py`); an arcade card is the vessel's element game, an arena card is listed below with its element.
- Controls: the picked ability's button, key and phone touch play its gesture (tap, hold, hold then release, tap on / tap off), read from its `abilities.json` trigger words; the style guide's one allowed motion here, "the hint that teaches the next control". Each ability card flips on its L5 button to the level-5 face, drawn with the game's own level-5 art (the icon kit's `<Element> 5` card state, or the HUD's own level-5 sprite) where the game has one.
- Domains: a Jade / Ruby / Gold switch in the top bar tints every icon and shows every vessel in that domain's livery, with colours read from the live palette (whichever `SO_ColorSet` `ThemeManagerDataContainer` names: `NocturneColorSetSO` since 2026-11-03) the way `SO_ColorSet` computes them; the vessel images are rendered by `Tools/Build/vessel_model/render_cards.py` (`fleet_codex.py --render-domains`, needs `bpy`).
- `Tools/Build/fleet_codex.py`: writes `build/fleet.json` and `build/img/<Key>.webp` from the shipped assets and git.
  A reader; `build/` is git-ignored.
- `/fleet-codex` (`.claude/skills/fleet-codex/SKILL.md`): how to refresh and republish, where every fact comes from,
  and how to add a field.
- A daily scheduled task republishes it from bleeding-edge when the content hash changed.

Apart from the two tables above, nothing on the page is typed by hand, so it cannot drift from the game. When it is wrong, the asset it read is wrong
(or the reader is), and that is where the fix goes.
