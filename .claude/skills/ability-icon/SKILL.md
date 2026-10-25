---
name: ability-icon
description: Make, redraw or raise the floor of a Cosmic Shore ABILITY-ROW ICON - the four white glyphs in a vessel's lower-right ability lockup (Assets/_Graphics/Icons/AbilityIcons/<Hull>/) - through the one icon kit (Tools/Build/icon_kit/: angular drawing engine, shared pictograms, legibility gates, before/after sheet, writer that keeps every guid). Use for ANY request to make, replace, improve, "fix the X icon", "the icons look terrible", give a new hull its four icons, retire a *-PLACEHOLDER.png, or touch Tools/Build/author_*_icon*.py; and for other small 2D glyph sets (objective, chrome, touch) when they are redrawn. Loads the icon language (the intention Garrett and Shombith confirmed), the contract the HUD binds, the pictogram vocabulary, the pipeline and the critique loop, and the measured lessons. The 2D twin of /vessel-model.
---

# Ability Icon Protocol

You are about to draw an icon a pilot reads at **60 px** in the heat of a match (the lockup's
`iconBoxSize`; the element flower above it is 44 px). Garrett, 2026-10-24: "the current icons
range from decent to terrible ... we will improve your icon development skill by raising the floors
in our existing icons." The plan and the audit that started this: `Docs/Icons/ABILITY_ICON_SKILL_PLAN.md`
and `Docs/Icons/ability_icons_audit_2026-10-24.png`.

## 0. The bar, in one paragraph

An icon **names the ACT in one object**. Not the hull doing it, the target and an arrow; the one
thing a pilot who has used the ability once would draw: the bomb, the blast, the rail into the
distance, the ball lifting off the ribbon. Angular (zero corner radius, butt caps, mitred joins - the
style foundation's §5, confirmed by Garrett 2026-10-24), **filled mass first** (the gauge fill and
the cooldown veil sweep OVER the icon; a hairline drowns under them), one focal mass, at most three
shapes, one stroke weight, filling the optical box. The element flower above the icon owns the
element: an icon never repeats its element's mark ("I don't see what value the redundancy could
provide"). A hull's own silhouette is fair game where it IS the act (the Dolphin's jaw on its Space
icon; the Manta soaring; the Urchin on its ribbon) - and it is the hull's MESH projected (`mesh_silhouette.py`),
never a drawing of it ("the fidelity is too low. this reads too abstractly"). Test every icon by covering everything but its
largest shape: if the act is gone, it is a scene, not a symbol.

## 1. Read before you touch

- `references/ICON_LANGUAGE.md` - the intention: weights, box, shapes, what motion looks like.
- `references/CONTRACT.md` - what the game binds: size, channels, the guid that must not move, the
  `.meta`, the element order, how the lockup scales and tints, gauges that are not icons.
- `references/VOCABULARY.md` - the shared pictograms (`Tools/Build/icon_kit/motifs.py`); the same
  thing is the same shape on every hull.
- `references/LESSONS.md` - what each round taught. **Append to it every round.**
- The ability itself: `Assets/Resources/ElementalAbilityMaps/<Hull>.asset` (labels, descriptions,
  level-5 upgrades), the class doc under `Assets/_Scripts/Controller/Vessel/R_VesselActions/`, and
  `/element-ability-table` when the mapping is in doubt.
- `git fetch origin && python3 Tools/Build/who_works_where.py query Assets/_Graphics/Icons/AbilityIcons/<Hull>`.

## 2. The pipeline (one command per hull)

```
python3 Tools/Build/icon_kit/author.py <hull> --preview   # render, gate, sheet; writes nothing under Assets/
python3 Tools/Build/icon_kit/author.py <hull>             # ... and write the PNGs (metas and guids kept)
python3 Tools/Build/icon_kit/author.py <hull> --check     # drift gate (run it in /ship)
python3 Tools/Build/icon_kit/verify.py --self-test        # the gates prove they fire
python3 Tools/Build/icon_kit/verify.py <pngs>             # measure ANY icon, hand-drawn included
```

A hull is a spec, `Tools/Build/icon_kit/specs/<hull>.py`: `HULL` and `ICONS = [dict(name, element,
act, draw)]`, where `draw()` composes masks from `canvas.py` (the engine) and `motifs.py` (the
vocabulary) in icon px (0..128, y down) and returns a mask or `[(mask, alpha), ...]` (0.5 = a ghost).
The sheet lands in `Docs/Icons/sheets/<hull>.png`: BEFORE (as HEAD committed it, or the sprite a new
icon replaces) and AFTER, each as the CARD the lockup draws (`card.py`, from `AbilityLockupStyle.asset`'s
numbers: the two trapezoids, the flower cell, the 60 px icon box) plus the raw icon at 60 and 40. A one-icon
round adds the states (the joust flash, a spent ring) and every other surface the sprite is on. **Read the sheet** (the Read tool on
the PNG) before writing; the gates are a floor, not a judgement - Urchin Slip's three circles passed
every gate and named nothing.

## 3. The round

1. **Brief, one line per icon**: the act, and the ONE object that names it. Write it in the spec's
   docstring, with what the old icon was.
2. **Draw, preview, look at 40 px first.** Say what fails the language in words before touching
   numbers (round 1 of the Manta: "the lobes read as ears - a fox, not a ray").
3. **Gates green** (`verify.py`: white 128, coverage 0.10-0.45, long axis ≥ 80 % of the 116 px
   optical box and inside it, thin share ≤ 0.35, tight-gap boundary ≤ 0.25, ≤ 4 components, no
   specks, ≥ 85 % mass at 40 px, the hull's four tell apart at 24 px). A gate that is wrong is
   argued in `verify.py` with a negative control, never skipped for one icon.
4. **Write** and commit the spec, the PNGs, the sheet, and the stand-down in the old generator
   (§5) together. Never hand-edit a PNG or a `.meta`.
5. **The owner looks** (Garrett reviews the sheet - when he asks for ONE icon, show three candidates on the
   card and pick one, nothing else changes; a flight test when the row is on a HUD change).
   Fold what he says into `LESSONS.md` and, if it is a rule, `ICON_LANGUAGE.md` - before the next
   hull. One hull per round (the `/vessel-model` loop).

## 4. Rules that keep being relearned

- Prisms in a row need a gap ≥ 6 px or they fuse into a zigzag at 40 px (Yastri, round 1).
- A silhouette's smallest feature is the first thing a viewer names (lobes → ears). Remove the
  feature; do not shrink it.
- Three speed lines say "boost" on every hull and so say nothing; ONE chamfered bar, or a lean, is
  the fleet's motion mark.
- A gauge sprite (the Urchin's charge ring) is not an ability icon: it is a bare ring on purpose.
  Check `author_<hull>_hud.py` before calling a ring "generic".
- **Elements have no colour** (LOCKED, Garrett 2026-10-24): never a colour per element on a card, a blob, an
  edge, an icon or a sheet; the four domain colours are team identity only. `check_element_colour.py`.
- **Measure the HUD first.** On the older hulls a card is often a live readout (a generated graphic, a jaw pair,
  a step ring, a sprite ladder); a sprite swap polishes none of that, and a ladder is ONE icon in several
  states (`subdir`, `family=False`, the card's sprite is the top rung, `rebind.py list=` rewrites the view's
  list). The Dolphin had nothing to redraw.
- Round caps are the old language. Every new icon is angular; a hull is redrawn whole so its four
  match, never one icon in the new language next to three in the old.
- Two sheets, not one: look at 128 to judge the drawing, at 40 to judge the icon.

## 5. Wiring and hand-off

The prefab bindings (`AbilityIconBinding` on `<Hull>HUDVariant.prefab`, `Manta.prefab`,
`Jacks.prefab`) reference the PNG by **guid**; the kit writes the PNG and leaves the `.meta`, so a
redraw changes no prefab. A NEW hull's four `.meta`s are written by the kit with a deterministic guid
(`guid_for(hull, name)`), and its `author_<hull>_assets.py` binds them. An icon that REPLACES a sprite under
another name (a placeholder, borrowed art) carries `REBIND` steps in its spec - one per referencing file, with
the count of references it expects - and `before=` so the sheet still shows what it replaced; references
outside the HUD (another screen's prefab) are left to their owners. When a hull moves onto the
kit, its old generator **stands down** on the PNGs (keeps the metas and anything else it owns, prints
where the PNGs now come from) - `author_manta_icon_placeholders.py` and `author_urchin_hud.py` are
the pattern; a generator that still re-emits the placeholder would undo the art on its next run
(CLAUDE.md, the spent one-shot rule). Status of each hull: `Docs/Icons/ABILITY_ICON_SKILL_PLAN.md` §8.
