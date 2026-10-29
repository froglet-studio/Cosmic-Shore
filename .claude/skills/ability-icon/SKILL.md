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

> Garrett, 2026-10-25, on the Crystal Joust and Skimming pairs: "I really like where we landed here on both
> because the icons are teaching the ability." That is the test every icon now passes or fails: a pilot who
> has never used the ability learns from the card WHO acts (the hull, its real mesh), WHAT it acts on (the
> opponent, the prisms) and WHAT CHANGES (yours in the domain colour, theirs in danger, the untouched in blue,
> lime when it lands). A mark that only labels the ability is the floor; a mark that teaches it is the bar.

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
python3 Tools/Build/icon_kit/mesh_silhouette.py "<fbx>" --out <sheet.png> [--mirror] [--take "<stack>"]   # the lettered view grid of a hull
python3 Tools/Build/icon_kit/mesh_pose.py "<fbx>" --takes | --check   # a hull's authored takes; prove the offline skin on the file
python3 Tools/Build/icon_kit/toy_portraits.py [--preview | --check]   # the Toy Box / codex Tool portraits (LESSONS 40)
```

Every run also draws each card's STATES sheet from the spec's `CARDS` (`states.py`: the card before and after, in every
state, per sample domain) to `Docs/Icons/sheets/<hull>_<card>.png` - that is the sheet the owner judges. An OPTIONS
sheet (three candidates, the old first) is the same thing with one `CARDS` entry per candidate; delete the losers once
the owner picks.

A hull is a spec, `Tools/Build/icon_kit/specs/<hull>.py`: `HULL` and `ICONS = [dict(name, element,
act, draw)]`, where `draw()` composes masks from `canvas.py` (the engine) and `motifs.py` (the
vocabulary) in icon px (0..128, y down) and returns a mask or `[(mask, alpha), ...]` (0.5 = a ghost).
The sheet lands in `Docs/Icons/sheets/<hull>.png`: BEFORE (as HEAD committed it, or the sprite a new
icon replaces) and AFTER, each as the CARD the lockup draws (`card.py`, from `AbilityLockupStyle.asset`'s
numbers: the two trapezoids, the flower cell, the 60 px icon box) plus the raw icon at 60 and 40. A one-icon
round adds the states (the joust flash, a spent ring) and every other surface the sprite is on. **Read the sheet** (the Read tool on
the PNG) before writing; the gates are a floor, not a judgement - Urchin Slip's three circles passed
every gate and named nothing.

## 2.5 What a card can be (measured on the HUD first - `card.py`, `rebind.py`, the view)

| Shape | When | Kit | HUD |
|---|---|---|---|
| One white sprite | the act is one object and nothing about it changes | `ICONS` entry | `AbilityIconBinding.icon` by guid |
| A tinted PAIR / TRIO | the act has sides: yours, theirs, the untouched | `rebind.py pair=` children (field None for a part never tinted) | a transparent container; the view paints each child (domain, danger, blue, lime) |
| LIVE PARTS | the act has a live number or state: an angle, a swap | `pair=` child with a rect for a part that turns; two sprites for a swap | the controller pushes the REAL value every frame; the view eases toward it and never caps a plan view |
| The PLATE GAUGE | the act has a how-much | `pair=` child with icon None + `binding=`; `card.py gauge=` on sheets | `AbilityIconBinding.gauge`; the view writes `fillAmount` only |
| A generated readout | the shape IS data (the Dolphin's blast profile) | nothing to draw; measure and leave | `EnsureGeneratedAbilityIcons` |

Worked examples, in order: the Squirrel's Crystal Joust (pair), Skimming (trio), Drift (live parts), the Sparrow's Space card (N generated parts on a live layout over a segmented gauge), the Dolphin's
Time card (one part + the plate gauge). `specs/squirrel.py`, `specs/dolphin.py`.

## 3. The round

0. **One card at a time, with the owner** ("lets focus on one icon for one vessel"; "lets get one thing right. then
   move on"). Measure the HUD first: what the card is today (a sprite, a pair, a readout, a ladder) and what the
   view writes on it per frame. The BEFORE sheet is the card as the HUD draws it, every state, never the raw PNG
   ("your before pictures are not accurate or exhaustive").
1. **Brief, one line per icon**: the MECHANIC first (measured in code: what moves, which number, which threshold -
   the source of every part and every plate mark), then WHO gains WHAT, against WHAT (the payoff the pilot plays for,
   the level-5 state included), and the ONE object that names it. When the act changes the vessel itself
   (protected, faster), the vessel is on the card (LESSONS 52). Find the real geometry of every object on the card
   first - the hull's mesh or the game's own silhouettes (`Assets/_Graphics/Silhouettes/`), a prism's generator
   (LESSONS 48, 54). Write the brief in the spec's docstring, with what the old icon was.
2. **Options with the OLD first, in the hull's whole LOCKUP** ("show the old when comparing"; "going forward when showing
   vessel icons lets show the before and the other icons of the vessel so we can consider how they distinguish
   themselves too", Garrett 2026-10-27): three or four candidates on the real card, each in its states (rest, mid,
   landed) at 60 and 40 and 128, gate verdicts under each, the recommendation named. Give the candidates' `CARDS`
   entries one `group=` and the spec a `LOCKUP` (the hull's other cards as the HUD draws them today): `author.py`
   then writes `<hull>_<group>_options.png` - the BEFORE row and one row per candidate, each the full four-card row
   with the candidate in its slot. That is the sheet the owner gets; a candidate is never shown alone.
   When a hull is in the picture, send the mesh grid first (`mesh_silhouette.py <fbx> --out ...`, `--mirror` when
   the file frame's nose is -Y) and let the owner pick the view. Then draw, preview, look at 40 px first. Say
   what fails the language in words before touching numbers (round 1 of the Manta: "the lobes read as ears - a
   fox, not a ray").
2.5 **Review the sheet against the round-costing mistakes before sending** (LESSONS 44-59; each one cost Garrett a
   round): (a) every part and the plate answer "what is this showing?" with something nothing else on the card
   shows, and no level-5 mark - the upgrade is a state; (b) a moving part and every copy of it face the way they move
   (mirror, curve); (c) flat: one colour per part, seams not shading; (d) no part covers the focal object or a
   reference mark at 40 px; (e) a broken state is real pieces apart; (f) plate marks are "segments", never lines
   across; (g) objects are drawn from their real mesh or generator; (h) the act runs ACROSS the card, clear of the
   plate's fill line and marks (LESSONS 55); (i) anything with a size is to scale, measured from the prefabs
   (LESSONS 56); (j) what the ability destroys is negative space, what it spares is whole outside (57); (k) a region
   is a filled white shape, not an outline or a grey fill, and the card sits beside `sparrow.png` / `rhino.png` at the
   same weight (58); (l) a level that changes a size keeps the main shape bold and scales what it is measured against
   (59). Fix a failure before the owner sees it.
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
  edge, an icon or a sheet; the four domain colours are team identity only. `Docs/UI_COLOR_RULES.md`, gated by `check_ui_colors.py`.
- **Measure the HUD first.** On the older hulls a card is often a live readout (a generated graphic, a jaw pair,
  a step ring, a sprite ladder); a sprite swap polishes none of that. A ladder or a step ring is a HOW-MUCH
  drawn as shapes: move it to the plate gauge (the Dolphin's Time card) rather than redrawing its rungs; the
  Sparrow's rocket ladder is the one that still stands as rungs.
- **A quantity is the plate gauge, never a ring.** A ring round a hull reads as a skimmer (Garrett, the Dolphin's
  Time card). How-much goes to `AbilityIconBinding.gauge` - the lockup's one linear fill of the lower totem - and
  the icon spends its shapes on who and what. A sprite ladder is the same mistake in steps.
- Round caps are the old language. Every new icon is angular; a hull is redrawn whole so its four
  match, never one icon in the new language next to three in the old.
- Two sheets, not one: look at 128 to judge the drawing, at 40 to judge the icon.

## 4.5 Wiring a LIVE card, step by step (the Dolphin's and Sparrow's Time and Mass cards are the worked examples)

1. **Measure the HUD**: the card's icon Image fileID and the view MonoBehaviour's fileID in `<Hull>HUDVariant.prefab`
   (`grep -n "abilityIcons:"`, the `--- !u!114 &<id>` above it), what the view writes on it today, and what the
   controller can read (`IVesselStatus`: `IsDrifting`, `IsBoosting`, `Course`, `Domain`, `IsTranslationRestricted`;
   the SOAP events it already subscribes to).
2. **Spec**: draw each PART as its own icon in `Parts/` (`subdir="Parts", family=False`, exemptions named), the
   composite under the card's shipped name (its guid is kept), a `CARDS` entry with every state, and a `REBIND`
   `pair=` step: `container=` the icon's fileID, `view=` the view's, children in draw order (first draws under),
   `fields=` the sprites the view swaps, `binding=` for a plate gauge (`gauge=None` to unbind one),
   `deactivate=` for a readout nothing binds any more.
3. **View** (`<Hull>HUDView.cs`): `[SerializeField] Image` per part, `Sprite` per swap, a `Color` fallback,
   `SetXColor(...)` (domain / shielded / blue from `ElementalBarsConfigSO`), `SetX(bool or float)` for the state
   (swap, alpha, rotation, fillAmount; a `DOPunchScale` on the beat), reset in `Initialize()`, tweens killed in
   `OnDestroy()`; `SetAbilityUpgraded` override when the L5 changes WHAT the act makes. Never a colour per element.
4. **Controller** (`<Hull>HUDController.cs`): push the REAL number or flag every frame in `Update()` (the Squirrel's
   `DriftAngleDegrees`, the Dolphin's `IsDrifting`, the Sparrow's `IsBoosting`) or from the event the HUD already
   has (the Sparrow's stationary-mode event); resolve colours once per frame through `[Inject] GameDataSO`
   (`ColorSet.GetDomainSignalColor`, `ThemeManagerData.GetShieldedSignalColor`; alpha 0 until resolved).
5. **Editor wirer**: if `VesselAbilityRowWirer.cs` builds this hull's slots, teach it the parts so a re-run is a no-op.
6. **Gates**: `author.py <hull>` then `--check`, `verify.py --self-test`, `check_console_logging.py`,
   `check_conditional_compilation.py`, `check_ui_colors.py`; read the states sheet.
7. **Record**: `LESSONS.md` (the owner's words first), `Docs/ABILITY_LOCKUP.md`'s gauge table if a gauge moved,
   `Docs/Icons/ABILITY_ICON_SKILL_PLAN.md` round status, a team task for the editor check
   (`team_tasks.py add --area hud --kind test ...`, Yash or the owner) - there is no Unity CLI in a cloud session,
   so every C# change ships editor-unverified and the commit says so.
8. **Commit** the spec, PNGs + metas, prefab, C#, sheets and docs together, conventional message, "Affects: the
   <Hull> HUD"; one card per commit.

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
