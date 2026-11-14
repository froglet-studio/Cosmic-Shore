# Fleet Codex

The artifact: https://claude.ai/artifact/JDvgFqu1bCfgcHVfCdXaA8. One page for every vessel: its four abilities and
their buttons, what each element scales (with the authored numbers), the level-5 upgrades and whether each is really
wired, flight numbers, the game cards that seat it, its docs and its recent updates. Asked for by Shombith,
2026-11-02: "an artifact which shows all these details for all vessels and is updated when new updates are added or
abilities are changed".

- `index.html`: the page, design C of the design board's Rev 68 drawn whole as Rev 69 (sections 143-144 of `Docs/MENU_REDESIGN_B.md`, built 2026-11-08): one vessel at a time with a switcher in the top bar, the flight-deck window (the hull flying, the briefing and story, its element games, the picked ability's move playing with its devices, the ability row), then Abilities (each level 5 with a Play L5 button that lights the element's upgrade flower petal by petal), Missions (filtered), Flight and controls and Recent updates (in plain words, never commit messages). `#fleet` is the whole fleet as cards, solo vessels then crews, each with its abilities and its element games as flowers. No docs, no idle motion on the hull (Shombith 2026-11-08). It fetches `fleet.json`, `themes.css`, `theme.js` and `img/` from beside it.
- `abilities.json`: what each ability does, how it fires and what more of its element does, plus each hull's omni crystal (written 2026-11-02 from each vessel's mechanics doc or code, cited per entry). The maps record only what an element scales, so this is a hand table: the generator drops an entry whose ability was renamed and warns.
- `tiers.json`: how good each SOLO vessel is in each element's game, S to C, a person's rating (Rev 84, section 155 of `Docs/MENU_REDESIGN_B.md`); the page shows `?` until a letter is given. The **Flight Certificate** beside it is not typed: `fleet_codex.py` `certificate()` reads it from the map, the level-5 status and the arcade cards.
- `seats.json`: which seat of each crew vessel owns which ability, cited per crew doc; the build fails when it stops matching the class asset or map.
- Themes: the game's three colour sets, **Nocturne, Photic and Dart Frog** (Shombith, 2026-11-08; the film-named themes moved to these), from the top-right menu, remembered per viewer (`localStorage` `fleetCodexColourSet`), the game's live set by default. `fleet_codex.py` writes them as `themes.css` / `theme.js` with the analytics console's own builder (`Tools/Analytics/console/make_themes.py`, `build(sets=...)`) from `ColorSetCatalog.asset`, so the codex and the console are always one look.
- Games: one per element (Squirrel: Charge game Joust, Mass game Scurry, Time game Skim Race). A card's element is the game's own answer, `ModeGenre.TryElementsFor` (read by `Tools/Analytics/server/mode_elements.py`); an arcade card is the vessel's element game, an arena card is listed below with its element.
- The move: the picked ability plays a scene of it happening (by element) beside it, while its button, key and phone touch play its gesture (tap, hold, hold then release, tap on / tap off), read from its `abilities.json` trigger words; the style guide's one allowed motion here, "the hint that teaches the next control". Each ability card flips on its L5 button to the level-5 face, drawn with the game's own level-5 art (the icon kit's `<Element> 5` card state, or the HUD's own level-5 sprite) where the game has one.
- Domains: a Jade / Ruby / Gold switch in the top bar tints every icon and shows every vessel in that domain's livery, with colours read from the live palette (whichever `SO_ColorSet` `ThemeManagerDataContainer` names: `NocturneColorSetSO` since 2026-11-03) the way `SO_ColorSet` computes them; the vessel images are rendered by `Tools/Build/vessel_model/render_cards.py` (`fleet_codex.py --render-domains`, needs `bpy`).
- `Tools/Build/fleet_codex.py`: writes `build/fleet.json` and `build/img/<Key>.webp` from the shipped assets and git.
  A reader; `build/` is git-ignored.
- `/fleet-codex` (`.claude/skills/fleet-codex/SKILL.md`): how to refresh and republish, where every fact comes from,
  and how to add a field.
- A daily scheduled task republishes it from bleeding-edge when the content hash changed.

Apart from the two tables above, nothing on the page is typed by hand, so it cannot drift from the game. When it is wrong, the asset it read is wrong
(or the reader is), and that is where the fix goes.
