# What the game binds (the contract a portrait lands in with zero edits)

| Fact | Value | Where |
|---|---|---|
| File | `Assets/_Graphics/Pilots/Portrait_<Name>.png`, 220 x 280 RGBA, the figure on transparency, boots on the bottom edge | the painted set's own size; `finish.py SIZE`; `verify.py` gate `size` |
| `.meta` | the set's importer (`Portrait_Mason_Placeholder.png.meta`, serializedVersion 13: sprite, single, `alphaIsTransparency: 1`, `spritePixelsToUnits: 100`, `maxTextureSize: 2048`, mipmaps off) with the kit's guid | `author.py meta_text`; written once, never rewritten |
| The guid never moves | `md5("CosmicShore/PilotArt/<Name>")`; the pilot asset binds the sprite by guid, so a redraw writes the PNG and leaves the `.meta` | `author.py guid_for`; `--check` asserts it |
| Wiring | `PilotSO.portrait` is `{fileID: 21300000, guid: <png guid>, type: 3}`; written by `author_pilot_assets.py` from `Tools/Build/pilot_art/wired.json` (pilot id -> png, guid, spec); an unwired callsign keeps `{fileID: 0}` | `Assets/_SO_Assets/Pilots/Roster/Pilot_<Callsign>.asset`; `author_pilot_assets.py wired_portraits` |
| Stand-in flag | `PilotSO.portraitIsStandIn` marks a spare standing in (Shombith, 2026-10-26); the kit writes real portraits, flag 0 | `PilotSO.cs` |
| Where it shows | the Hangar's pilot card: `Portrait` Image at (10, 10), 44 x 56, preserveAspect (`author_menu_hangar.py pilots_page`); the Squads page card's `pilotImage` (`author_menu_ark.py`); `VoyageOpenCard` | `HangarPilotsPage.cs`, `SquadsPage.cs` |
| The plate behind it | Deep Sea `surface` #222645 on `ground` #00041F (the sheet draws the same) | `Docs/UI/froglet-tokens.json` |
| The source | `Docs/Pilots/art/sources/<name>.png`: the captured painting, 1024 x 1280 (the model's 4:5), over the key colour the brief named, or with its own alpha (`key="alpha"`) | the spec's `source`; `author.py --check` re-finishes it |
| The sheet | `Docs/Pilots/art/sheets/<name>.png` (one) or `round.png` (several); `references.png` is the audit of the painted set | `sheet.py` |
| The envelope | `Docs/Pilots/art/style_envelope.json`, measured from the painted set; re-measure only when the set changes | `measure.py --refs` |
| Drift gate | `python3 Tools/Build/pilot_art/author.py --check` (every spec), in `ship_gates.py GENERATOR_EXTRA`; plus `author_pilot_assets.py --check` for the wiring | `/ship` |
| The painted set | the 32 `Portrait_<Name>_Placeholder.png` / `PilotPortrait_*` files are the reference set and stay untouched; their guids are still referenced by `SO_Captain_*` (legacy), `Menu_Main.unity`, `DIalogue1.asset`, `DialogueUIPrefab.prefab`, `GameOverPanel.prefab`, `GuideSpeaker_Trace.asset` | `grep -rl <guid> Assets` |
| Not this contract | the hull renders on the class cards (`/vessel-model render_cards.py`), the arcade card backgrounds (`/cardart`), the ability icons (`/ability-icon`), the Toy Box portraits (`icon_kit/toy_portraits.py`) | |
