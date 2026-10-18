# Modelling a vessel that reads

The chase camera sits 17 (Squirrel) to 250 (Serpent) units behind the hull with a 60 degree
vertical FOV: a 5-unit hull at 20 units is ~250 px wide and ~60 px tall on a 1080p screen.
From there the pilot sees the STERN three-quarter: engine mouths, wing silhouette, the tail,
the back of the canopy. Design for that view first; the front is for the codex and the card.

## 1. Read the shipped file before you draw (to know what binds, not what to copy)

```
python3 Tools/Build/vessel_model/part_map.py "<shipped.fbx>"      # frame, parts, bones, bounds
python3 Tools/Build/vessel_model/fbx_probe.py "<shipped.fbx>" --takes
python3 - <<'EOF'
import sys; sys.path.insert(0,'Tools/Build/vessel_model'); import render as R
R.contact_sheet("<shipped.fbx>", "<scratch>/ref_sheets", tag="<vessel>")
EOF
```
Write down: the part list with bounds (these are your targets), the nose end, which bone owns
each part, the slot shares, and what the artist's silhouette is MADE of (the Squirrel: a head
with ears, a feather fan, four pods, nine chevrons; the Dolphin: two jaws, a coil, thin wings,
six bells). Keep that vocabulary - the game's HUD icons, docs and players know it.

## 2. Build order (one object per part, then join)

1. **Body first**: `forms.fuselage(stations=[(t, w, h_top, h_bottom, n_top, n_bottom, dy)])` -
   a superellipse loft; `n` 2 = round, 3-4 = boxy, different top/bottom = flat belly + domed
   back. Six to eight stations is plenty; the shape lives in the numbers, not the count.
2. **Appendages rooted INSIDE the body**: wings (`forms.wing`: airfoil loft, sweep, dihedral,
   twist, taper), fans of `plate(feather(...))`, fins (`plate(blade(...))`), pods
   (`lathe` profiles with a collar step and a sunk mouth), tendrils (`tube_along`). A part's
   root sits 0.1-0.3 units inside its neighbour: nothing floats, ever.
3. **Struts** wherever a pod or fan hangs off the body (`tube_along`, 0.05-0.15 radius).
4. **Mirror** the right side into the left with `forms.mirror_part` (winding repaired) and
   name the twin by the rig's convention (`.L/.R`, `.l/.r`).
5. **Paint** by rule, not by hand: `paint(obj, slot, lambda c, n, i: ...)` on face centre,
   normal and current slot. Glass = top faces of a region, sunk with `inset_faces(..., depth=,
   slot=W)`. Domain = leading edges, top plates, bands around pods, the keel stripe. Engine =
   the inner throat of each mouth (`inward(c, n, origin, axis)` in `specs/dolphin.py`).
6. **Finish** each hard part: `finish(f, obj, bevel_width=0.008-0.012, sharp_deg=38-45)`.
   Bevel before joining (the modifier needs a closed part), sharpen after joining too.
7. **Join** with the vessel's slot names; `set_smooth`; `box_uv`.

Budget: 6-18k triangles. Spend them on what the camera looks at (mouths, canopy frame, wing
leading edges, the tail), not the belly.

## 3. Part vocabulary that has worked

| want | call |
|---|---|
| a dome / helmet / head | `fuselage` with 6 stations, `n` 2.6-3.2, `dy` rising to the crown; visor by `inset_faces` on the front-top |
| an engine pod with a mouth | `lathe([(0,0),(r*.7,.02),(r,.1),(r,.3),(r*1.1,.34),(r*1.1,.42),(r,.46),(r,.62),(r*.85,.8),(0,.82)])` then `inset_faces` on the end cap, depth 0.05, slot Engine |
| an exhaust bell (open) | `lathe` profile out-and-back with `cap=False`, inner wall painted Engine |
| a feather / petal plate | `plate(feather(length, width, tip_round=.7, root_width=w*.45), thickness=.05-.06)` then rotate (yaw back, droop, roll) |
| a chevron / rib tail | `plate(crescent(outer_r, inner_r, span_deg=150, tips_sharp=.35), thickness=.15-.2)`, nine of them on a drooping `tube_along` spine |
| ears / spikes / blades | `plate(blade(length, width, tip=.12-.25))` or `cone` |
| a thin swept wing | `wing(root, span, chord_root, chord_tip, sweep, dihedral_deg, twist_deg, thickness=.09-.16, sections=6-8)` |
| a coil / spring / throat | `lathe` of an alternating-radius profile |
| panel lines | `inset_faces(pred, 0.02, depth=0.012)` on a band of faces; three per flank reads as plating |

## 4. The critique loop (what to look for on the sheet, in order)

1. **Connectivity** - every part grows out of another. Floating pills kill a model before
   anything else is judged (Squirrel v1).
2. **Silhouette from `chase`** - can you name the vessel from the shape alone at that size?
3. **The domain band** - one strong band of team colour the eye finds in a tenth of a second
   (26% of the Squirrel's faces; 20% of the Dolphin's). Not confetti, not a blob.
4. **Mouths and glass** - engine mouths read as separate lit holes; the canopy as sunk glass.
5. **Detail** - bevel highlights, panel lines, collars. Last, and only where it is seen.
6. **Morph row** - name each extreme from the chase tile. If "mass" reads as "space", fix the
   masks (the shipped Dolphin's `mass` stretches the wingspan 43% and reads as Space).
7. **Pose row** - each pose moves the whole hull. The blend tree halves them; author big
   (tail tip 1-1.5 units for pitch on a 6-unit hull, 2 units sideways for yaw, 30-40 degree
   wing banks for roll).

Change three things per iteration, re-render, re-judge. Two to five rounds is normal.

A predicate's threshold must sit BETWEEN face centres, never on one: `paint` and `inset_faces`
refuse a selection that flips under a 1e-5 nudge (`forms.UnstableSelection`), because bmesh
ops leave ~1e-7 of run-to-run noise in new vertices and a face on the line makes the build
nondeterministic, which makes `--check` lie (LESSONS 26). When it fires, move the number.

## 5. Element morphs that a pilot can name

| element | means | do | avoid |
|---|---|---|---|
| Space | reach | extend wings/fins/tail/jaws outward along their own axis (`stretch_along(ramp=)` from the root) | scaling the whole hull |
| Time | speed | sweep wings back (`sweep`), lengthen bells aft, taper and stretch the nose, thin the wings (`scale_about(axes=(0,0,1))`) | anything that reads as "bigger" |
| Charge | weapon | thicken guns/jaws/spikes, flare mouths, raise ears/spines | moving the body |
| Mass | volume | `scale_about(1.2-1.3, axes=(1,0,1))` on the body + `inflate(0.05-0.1)` | inflating wings (reads as Space) |

Target 4-12% of the diagonal per shape; the verify gate refuses under the spec's `min_travel`.

## 6. Poses (world axes, Blender frame: X pitch, Y roll, Z yaw; nose on -Y)

- `pitchup`: tail segments curl DOWN (-X, growing along the tail), fans/wings nose up, head lifts.
- `yawright`: the tail swings to the OUTSIDE of the turn (+Z on the tail), fans split, head counters.
- `rollright`: wings/fans counter-rotate about Y (left up, right down), tail twists along Y.
- `speedup`: everything streamlines - wings/fans fold back (yaw toward +Y), pods pull forward,
  tail straightens; `speeddown`: the fan spreads as an airbrake, pods pull in, tail flares.
- Jaws, bays and spines belong to the ability takes (`jawcharge`, `Missile Launch`), not the stick.
