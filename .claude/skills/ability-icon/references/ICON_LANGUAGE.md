# The ability-row icon language (the intention)

Confirmed by Garrett 2026-10-24 (angular; the flower owns the element; a hull's own silhouette is fair
game where appropriate). Style owner for the HUD it sits on: Shombith (`Docs/STYLE_FOUNDATION.md`).
Every number is in icon px on the 128 px sprite; the lockup draws it at 60 px (x0.47) and a phone
can show it near 40.

| Rule | Value | Why |
|---|---|---|
| The icon TEACHES the ability | who acts (the hull's real mesh), on what, and what changes - told by TINT on separately-tinted parts (yours = domain colour, theirs = danger, untouched = the HUD blue, landed = lime), never by element | Garrett 2026-10-25: "the icons are teaching the ability" |
| HOW MUCH is the plate, not a shape | a bank, a charge, a readiness is the card's plate gauge (`AbilityIconBinding.gauge`, the lockup's one linear fill of the lower totem); the icon never draws a meter | Garrett 2026-10-25 on a boost ring: "the ring now reads as a skimmer. lets just have the whole lower totem fill with the consistency of other resources" |
| LIVE where the act is live | a part the HUD turns by the REAL number (the hull by its forward-vs-course angle), swaps by the real state (blocks → octahedrons when the drift shields), tints by the real team; a state is told by shape and team colour, never a second palette | "as long as the dolphin icon's angle matches the drift angle"; "the dynamic angle and trail change should be sufficient" |
| LEVELS are segments in the plate | a how-much with levels (heat phases) cuts its fill into one bar per level reached and marks the levels ahead as short dim edge ticks - never lines across the whole card | Garrett 2026-10-27: "i like the ease of reading the lines as the boundaries between levels but i don't like how it visually dominates the whole space" |
| One object names the act | ≤ 3 shapes, 1 focal mass, ≤ 4 connected components | a scene is a diagram at 128 and noise at 40 |
| Optical box | 116 px (margin 6), long axis fills ≥ 80 % of it | the lockup scales by authored size, so a timid icon draws small on the HUD |
| Primary stroke | 11 px (5.2 at 60) | reads under the gauge fill and the cooldown veil |
| Secondary stroke | 7 px (3.3 at 60) | fuses, tethers, grooves |
| Never under | 6 px, strokes AND gaps | 2.8 px at 60, still ≥ 2 px at 40 |
| Caps and joins | butt caps, mitred joins, zero corner radius | STYLE_FOUNDATION §5; round is the retired language |
| Round things | allowed (discs, rings as 96-gons) - round CORNERS are not | a bomb is round; its neck is square |
| Fill | filled silhouette first; line-weight only for the secondary element | a hairline icon disappears under the veil |
| Colour | pure white, shape in alpha; 50 % alpha only for a GHOST (previous position, second mode) | the HUD tints by STATE (armed, spent, danger) and by TEAM (a stolen prism, a locked crystal); never by element, never baked |
| Element | never drawn on the icon, and never a colour | the flower above owns it; "jade is not time, gold is not charge" (Garrett 2026-10-24, LOCKED - `Docs/UI_COLOR_RULES.md`, `check_ui_colors.py`) |
| Motion | a shape: a lean, a swept band, one chamfered bar, a displaced ghost | three speed lines are every boost in the fleet |
| Negative space | prefer a cut to a stroke: a crescent makes a sphere, a hole makes a blast's core, a notch makes a reticle | the Starlings' Opposing Mass is the pattern |
| Family | a hull's four share weight, box and vocabulary and differ in silhouette (IoU ≤ 0.75 at 24 px) | four fonts on one HUD read as four games |
| Fleet | the same thing is the same pictogram everywhere (VOCABULARY.md) | a prism is a prism on every hull |
| Hull silhouette | the hull's REAL mesh (`mesh_silhouette.py`), in the view the owner picked from the grid, where the act IS the hull doing something (the joust, the skim, the drift, Soar, Trail Rider); never a generic chevron, never a hand-simplified drawing | Garrett: the Dolphin's jaw on its Space icon; "it should read as the squirrel silhouette vs a generic one"; "the fidelity is too low. this reads too abstractly" |

The yardstick set (icons that read, keep them in sight on every sheet): Thresher Wrecking Ball /
Winch / Plant, Shrike Void Lance / Nest / Targeting Array, the Starlings' cards, Stoat Ripple, Scarab
Throttle - and, after round 1, Manta Kabloom / Sting, Urchin Track Projector / Trail Rider.
