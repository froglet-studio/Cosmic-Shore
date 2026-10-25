# The ability-row icon language (the intention)

Confirmed by Garrett 2026-10-24 (angular; the flower owns the element; a hull's own silhouette is fair
game where appropriate). Style owner for the HUD it sits on: Shombith (`Docs/STYLE_FOUNDATION.md`).
Every number is in icon px on the 128 px sprite; the lockup draws it at 60 px (x0.47) and a phone
can show it near 40.

| Rule | Value | Why |
|---|---|---|
| One object names the act | ≤ 3 shapes, 1 focal mass, ≤ 4 connected components | a scene is a diagram at 128 and noise at 40 |
| Optical box | 116 px (margin 6), long axis fills ≥ 80 % of it | the lockup scales by authored size, so a timid icon draws small on the HUD |
| Primary stroke | 11 px (5.2 at 60) | reads under the gauge fill and the cooldown veil |
| Secondary stroke | 7 px (3.3 at 60) | fuses, tethers, grooves |
| Never under | 6 px, strokes AND gaps | 2.8 px at 60, still ≥ 2 px at 40 |
| Caps and joins | butt caps, mitred joins, zero corner radius | STYLE_FOUNDATION §5; round is the retired language |
| Round things | allowed (discs, rings as 96-gons) - round CORNERS are not | a bomb is round; its neck is square |
| Fill | filled silhouette first; line-weight only for the secondary element | a hairline icon disappears under the veil |
| Colour | pure white, shape in alpha; 50 % alpha only for a GHOST (previous position, second mode) | the HUD tints by STATE (armed, spent, danger) and by TEAM (a stolen prism, a locked crystal); never by element, never baked |
| Element | never drawn on the icon, and never a colour | the flower above owns it; "jade is not time, gold is not charge" (Garrett 2026-10-24, LOCKED - `check_element_colour.py`) |
| Motion | a shape: a lean, a swept band, one chamfered bar, a displaced ghost | three speed lines are every boost in the fleet |
| Negative space | prefer a cut to a stroke: a crescent makes a sphere, a hole makes a blast's core, a notch makes a reticle | the Starlings' Opposing Mass is the pattern |
| Family | a hull's four share weight, box and vocabulary and differ in silhouette (IoU ≤ 0.75 at 24 px) | four fonts on one HUD read as four games |
| Fleet | the same thing is the same pictogram everywhere (VOCABULARY.md) | a prism is a prism on every hull |
| Hull silhouette | fair game where the act IS the hull doing something (Soar, Trail Rider) | Garrett: the Dolphin's jaw on its Space icon |

The yardstick set (icons that read, keep them in sight on every sheet): Thresher Wrecking Ball /
Winch / Plant, Shrike Void Lance / Nest / Targeting Array, the Starlings' cards, Stoat Ripple, Scarab
Throttle - and, after round 1, Manta Kabloom / Sting, Urchin Track Projector / Trail Rider.
