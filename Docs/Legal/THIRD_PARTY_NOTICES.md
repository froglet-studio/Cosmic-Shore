# Third-party notices — the attribution register

The canonical list of third-party assets shipped inside the game and the attribution each
one requires. **This is the source for the credits.** There is no credits screen in the
build yet (see "Where this has to surface" below); until there is, this file is where an
entry lands, and the licence text itself ships beside the asset it covers.

> Not legal advice. Same caveat as the rest of `Docs/Legal/` — counsel signs off before ship.

---

## Fonts — SIL Open Font License 1.1

All three are OFL 1.1, which permits commercial use and embedding in a game with **no
royalty and no obligation to open anything else**, on two conditions we meet: the copyright
notice and licence travel with the font, and we never sell the fonts themselves. The full
`OFL.txt` for each family ships in its folder.

| Family | Weights shipped | Copyright | Licence file |
|---|---|---|---|
| **Chakra Petch** | 400, 500, 600, 700 | Copyright 2018 The Chakra Petch Project Authors (<https://github.com/m4rc1e/Chakra-Petch.git>) | `Assets/_Graphics/Fonts/ChakraPetch/OFL.txt` |
| **Space Grotesk** | 300, 400, 500, 600 | Copyright 2020 The Space Grotesk Project Authors (<https://github.com/floriankarsten/space-grotesk>) | `Assets/_Graphics/Fonts/SpaceGrotesk/OFL.txt` |
| **JetBrains Mono** | 400, 500, 700 | Copyright 2020 The JetBrains Mono Project Authors (<https://github.com/JetBrains/JetBrainsMono>) | `Assets/_Graphics/Fonts/JetBrainsMono/OFL.txt` |

**Credits copy — paste this verbatim into the credits screen when it exists:**

> **Typefaces**
> Chakra Petch © 2018 The Chakra Petch Project Authors.
> Space Grotesk © 2020 The Space Grotesk Project Authors.
> JetBrains Mono © 2020 The JetBrains Mono Project Authors.
> All three are licensed under the SIL Open Font License 1.1.

Two OFL clauses worth knowing before anyone touches these files:

- **The Reserved Font Name rule.** None of these three declare a Reserved Font Name, so a
  modified build may keep the original name. If a future family does declare one, a modified
  copy must be renamed.
- **Derivative works stay OFL.** The generated `.asset` files embed rasterised outlines, so
  they are a derivative of the font software and are covered by the same licence — which is
  satisfied by shipping `OFL.txt` alongside them, as we do.

---

## Other third-party assets already in the tree

Registered here so this file is the whole list rather than the newest slice of it. Neither
was added by the font work; both predate it.

| Asset | Location | Notice |
|---|---|---|
| EmojiOne sprite sheet (TextMesh Pro sample content) | `Assets/Unity Assests/TextMesh Pro/Sprites/` | `EmojiOne Attribution.txt` |
| Placeholder vessel models | `Assets/_Models/Vessel Models/Placeholder/` | `CC_Attribution_For_Placeholder_Models.txt` (Creative Commons) |

Packages consumed through the Unity Package Manager (URP, Netcode, UniTask, Reflex, DOTween,
FMOD, NiceVibrations, PlayFab, …) carry their own licences in their package folders and are
not restated here; add a row only when an asset is **vendored into `Assets/`**.

---

## Where this has to surface

The build currently has **no credits screen**, so nothing in the game displays these notices
today. That is a gap, not a decision — OFL 1.1 requires the notice to travel with the
software, and shipping `OFL.txt` inside the game data satisfies it, but a credits screen is
what a player and a store reviewer expect to find.

The cheapest home is the settings modal, which already owns this shape of row:
`GameSettingsPanelController` renders link rows through `Application.OpenURL` (that is how
the privacy-policy row works, `Docs/Legal/README.md` § "Privacy policy → three places"). A
**Credits** row can either open a hosted page or push a scrolling in-game panel; either way
the copy above is what it shows.

Until that exists, adding a font is two steps: ship its `OFL.txt` in the family folder, and
add a row to the table above.
