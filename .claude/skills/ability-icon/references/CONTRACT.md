# What the game binds (the contract an icon lands in with zero edits)

| Fact | Value | Where |
|---|---|---|
| File | `Assets/_Graphics/Icons/AbilityIcons/<Hull>/<Hull>_<Act>.png`, 128 x 128 RGBA, RGB pure white, shape in alpha | every `author_*_icons.py`; `verify.py` gate `white128` |
| `.meta` | the house 128 px sprite meta (`Icon_Generate.png.meta` lineage: sprite, maxTextureSize 128, no mipmaps) with the icon's guid | `icon_kit/author.py` copies `Manta_Kabloom.png.meta`'s shape for a NEW icon |
| The guid never moves | the HUD binds the sprite by guid; a redraw writes the PNG and leaves the `.meta` | `AbilityIconBinding.sprite` on `<Hull>HUDVariant.prefab` (or `Manta.prefab`, `Jacks.prefab`); `--check` on the old generator asserts the metas |
| New icon guid | `md5("CosmicShore/IconKit/<Hull>/<name>")` | `author.py guid_for` |
| Folder metas | `AbilityIcons.meta` and `<Hull>.meta` exist and are committed (a folder without one is re-minted on every machine) | `author_manta_icon_placeholders.py` owns `AbilityIcons.meta`; a hull's generator owns its folder's |
| Drawn at | 60 px (`AbilityLockupStyle.asset` `iconBoxSize`); scale = 60 / authored size, so every 128 px icon lands at x0.47 | `Docs/ABILITY_LOCKUP.md` |
| Order | Charge, Mass, Space, Time, left to right, under the flower that upgrades it | `VesselHUDView.AbilityDisplayOrder`; `Docs/claude/VESSEL_HUD_AND_ELEMENTS.md` |
| Tint | a VIEW tints an icon by state (a flash, a gauge) or by team (a domain colour); never by element - the rev 3 element colouring is OFF (`elementColour: 0`, `check_element_colour.py`); the petals are never tinted | `Docs/claude/VESSEL_HUD_AND_ELEMENTS.md` "Elements have no colour" |
| Over the icon | the cooldown veil (clockwise radial, `cooldownVeilColor` a 0.72) sweeps OVER the icon; the gauge fill sits behind it on the plate | `Docs/ABILITY_LOCKUP.md` |
| Upgrade sprite | `AbilityIconBinding.upgradedSprite` (optional, authored art, swapped at level 5) | same contract, name it `<Hull>_<Act>_L5.png` |
| Locked card | a slot with no icon draws LOCKED (hairline mark), never a padlock | the lockup |
| A card as a tinted PAIR | the icon Image is a transparent container (no sprite, alpha 0; the lockup kerns and punches it) over stretched child Images the VIEW tints by state and team (the Dolphin's jaws; the Squirrel's two joust hulls) | `rebind.py pair=`; `SquirrelVesselHUDView.PaintJoustPair` |
| Gauges are not icons | the Urchin's `Urchin_ChargeRing.png` is the hold-charge ring the HUD draws round the Charge card; the Sparrow's missile gauges likewise | `author_urchin_hud.py` |
| Placeholders | `*-PLACEHOLDER.png` is the name of art owed (Rhino 4, Scarab 4, Serpent 3 on 2026-10-24); the art pass overwrites the PNG and keeps the name's guid, renaming the file AND updating the binding only in the same change | `author_hull_icon_placeholders.py` |
| Card art | the class card (`SO_Class_<Hull>.IconActive`) and the arcade card background are NOT this contract: `vessel_model/render_cards.py` and `/cardart` | `check_vessel_class_icons.py` |
