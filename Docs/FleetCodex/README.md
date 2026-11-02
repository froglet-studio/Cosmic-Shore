# Fleet Codex

The artifact: https://claude.ai/artifact/JDvgFqu1bCfgcHVfCdXaA8. One page for every vessel: its four abilities and
their buttons, what each element scales (with the authored numbers), the level-5 upgrades and whether each is really
wired, flight numbers, the game cards that seat it, its docs and its recent changes. Asked for by Shombith,
2026-11-02: "an artifact which shows all these details for all vessels and is updated when new updates are added or
abilities are changed".

- `index.html`: the page, design B of the design board's Rev 48 (section 115 of `Docs/MENU_REDESIGN_B.md`), in the website's Silver Screen look. It fetches `fleet.json` and `img/` from beside it: a filmstrip of the fleet (crews as crews), the vessel as a bento of tiles, its five abilities with the game's own icons, one control map for controller, keyboard and phone.
- `abilities.json`: what each ability does, how it fires and what more of its element does, plus each hull's omni crystal (written 2026-11-02 from each vessel's mechanics doc or code, cited per entry). The maps record only what an element scales, so this is a hand table: the generator drops an entry whose ability was renamed and warns.
- `seats.json`: which seat of each crew vessel owns which ability, cited per crew doc; the build fails when it stops matching the class asset or map.
- Themes: Silver Screen, Tesseract, Endurance, Gargantua and HyperSea, from the top-right menu. Each ability card flips on its L5 button to the level-5 face.
- `Tools/Build/fleet_codex.py`: writes `build/fleet.json` and `build/img/<Key>.webp` from the shipped assets and git.
  A reader; `build/` is git-ignored.
- `/fleet-codex` (`.claude/skills/fleet-codex/SKILL.md`): how to refresh and republish, where every fact comes from,
  and how to add a field.
- A daily scheduled task republishes it from bleeding-edge when the content hash changed.

Apart from the two tables above, nothing on the page is typed by hand, so it cannot drift from the game. When it is wrong, the asset it read is wrong
(or the reader is), and that is where the fix goes.
