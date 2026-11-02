# Fleet Codex

The artifact: https://claude.ai/artifact/JDvgFqu1bCfgcHVfCdXaA8. One page for every vessel: its four abilities and
their buttons, what each element scales (with the authored numbers), the level-5 upgrades and whether each is really
wired, flight numbers, the game cards that seat it, its docs and its recent changes. Asked for by Shombith,
2026-11-02: "an artifact which shows all these details for all vessels and is updated when new updates are added or
abilities are changed".

- `index.html`: the page, one page in design B of the design board's Rev 45 (section 114 of `Docs/MENU_REDESIGN_B.md`). It fetches `fleet.json` from beside it. The rail lists the fleet overview and every vessel; a vessel has Elements, Game Modes, Controls (Controller / PC / Mobile) and Docs and changes tabs.
- `Tools/Build/fleet_codex.py`: writes `build/fleet.json` and `build/img/<Key>.webp` from the shipped assets and git.
  A reader; `build/` is git-ignored.
- `/fleet-codex` (`.claude/skills/fleet-codex/SKILL.md`): how to refresh and republish, where every fact comes from,
  and how to add a field.
- A daily scheduled task republishes it from bleeding-edge when the content hash changed.

Nothing on the page is typed by hand, so it cannot drift from the game. When it is wrong, the asset it read is wrong
(or the reader is), and that is where the fix goes.
