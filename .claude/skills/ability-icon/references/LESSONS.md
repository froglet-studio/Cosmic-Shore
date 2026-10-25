# Lessons (append one block per round; the owner's words first)

## 2026-10-24 - the audit, and round 1 (Manta, Urchin)

Garrett: "the current icons range from decent to terrible." "Angular is probably good." "The flower
probably owns it. I don't see what value the redundancy could provide." "[Hull silhouettes] are fair
game where appropriate. Dolphin uses its jaw silhouette to good effect in its space ability icon."
"We will improve your icon development skill by raising the floors in our existing icons."

1. **Fifty-five icons came from twelve scripts and three drawing engines**, and the ability row
   disagreed with the style foundation on its most basic question (round vs angular). The language
   was never written down; each script cited "the house icon language" as one sentence in the
   previous script's docstring. Writing it down (ICON_LANGUAGE.md) and putting one engine under it
   is most of the fix.
2. **The gates had to be calibrated on the fleet before they meant anything.** First draft (12 px
   margin, coverage ≥ 0.14, 7 px thin threshold) failed 54 of 56 including everything Garrett called
   decent. The fleet draws to a ~4 px margin because the lockup scales by authored size; the Thresher
   set covers 0.12-0.16; round-capped strokes are ~5 px. Calibrated: margin 6, coverage 0.10-0.45,
   thin < 5 px share ≤ 0.35, tight-gap boundary share ≤ 0.25. Now the Thresher fails only on what is
   really wrong with it (chain-link and spike gaps, debris specks), the Shrike on being too thin
   (coverage 0.08-0.10), the Starlings' cards on looking alike at 24 px.
3. **Gates are a floor, not a verdict.** Urchin Slip (three circles) and Track Projector (a ladder)
   passed every gate and named nothing. The sheet at 40 px, read by a person, is the judgement.
4. **`Urchin_ChargeRing.png` is a gauge, not an icon.** The audit called a bare ring "generic"; it is
   the hold-charge ring the HUD draws round the Charge card. Read `author_<hull>_hud.py` before
   judging a sprite in the icon folder.
5. **Prisms in a row fuse into a zigzag at 40 px** unless the gap is ≥ 6 px (Yastri round 1: two
   prisms overlapped by 3 px and read as a chain). Two big prisms beat three small ones.
6. **The smallest feature of a silhouette is what gets named.** The Manta's cephalic lobes, drawn
   tall, made the Soar icon a fox's head at 40 px (ears + snout). Flattened to short lobes on a blunt
   head, it is a ray. Remove or flatten; shrinking keeps the read.
7. **Sleepers across a receding track read as a stacked tree.** A single-track rail is a solid taper
   with a centre groove cut out - the negative-space rule.
8. **The sheet's BEFORE must come from HEAD, not disk**, or the first write erases its own comparison
   (the first `--check` after writing drew new vs new). `author.py committed()` reads `git show`.
9. **A spent generator must stand down on the files the kit now owns and keep the rest**:
   `author_manta_icon_placeholders.py` keeps the metas and folder metas; `author_urchin_hud.py`
   keeps the metas, the HUD variant and the charge-ring gauge. Both `--check` clean, and a re-run
   cannot put the placeholder back.
10. **Soar is the weakest of the eight** (a ray at 60, a delta at 40); it is the one to ask about
    first. Kabloom (blast with a hollow core), Sting (octagon bomb), Track Projector (road to the
    horizon) and Trail Rider (ball on the ribbon) are the ones that moved most.

## 2026-10-24/25 - round 2 (Squirrel, Sparrow; the Dolphin measured and left)

Garrett: "this is slightly better. but not quite there. first we need to make sure we never conflate the domain
colors with elemental identity. jade is not time. gold is not charge. mass is not blue. space is not ruby ... we
need to set a rule that we explicitly do not do this." "lets start with the squirrel, dolphin, and sparrow. their
abilities are more locked in. lets get their icons polished."

11. **Elements have no colour - LOCKED.** The lockup's rev 3 lava colours (Charge gold, Mass blue, Space violet,
    Time teal) were the domain palette read as elements; the sheet of round 1 repeated the mistake by washing
    each plate in "its element's" colour. Both are gone: `AbilityLockupStyle.asset` serialises
    `elementColour: 0`, `check_element_colour.py` holds it (and lists the element->colour maps still in code with
    their owners), the sheet draws white on the plate. Rule text: `Docs/claude/VESSEL_HUD_AND_ELEMENTS.md`
    "Elements have no colour".
12. **Measure the HUD before deciding what an icon IS.** On these three hulls the "icon" is often a live
    readout: the Dolphin's Charge card is a generated blast cross-section, its Space card the two jaw halves
    opening to the next gape, its Time card an 11-step ring; the Squirrel's Space card is a generated reach
    ring with a count; the Sparrow's Charge card is a sprite LADDER. A sprite swap polishes none of that. The
    Dolphin had nothing to redraw and was left alone; the Squirrel got two cards and its two drift sprites;
    the Sparrow got three cards, a five-rung ladder and the weapon-mode pair.
13. **A ladder is one icon in several states, not several icons**: its rungs live in their own folder, are
    `family=False` (they are each other's variants on purpose), the card's own sprite IS the top rung, and the
    view's `missileIcons` list is rewritten to all five (`rebind.py` `list=`). The old three-rung art had a dark
    decagon PLATE baked in and sat on the lockup's plate; the bay holds four rockets and the ladder said two.
14. **A ghost at alpha 0.5 sits on the gates' threshold** and vanishes at 40 px (the empty rack lost its
    ghosts). Ghosts are 0.6.
15. **Borrowed art is a rebind, not a redraw**: the Squirrel's Time card wore the Sparrow's boost icon, and the
    Sparrow's prefab referenced that boost icon TWICE (the Time card and a legacy `BoostIcon` image). A rebind
    step asserts the count it expects (`expect=`), so a second reference is found rather than silently swapped.
16. **The placeholder the skull was is also the Jousts metric icon** (`ModeControlsLibrary.asset`): every
    reference to a sprite being replaced is its own rebind step, and the ones outside the HUD (the drift
    placeholder in `AbilityControlRow.prefab`, the cannon one in `HomeScreen.prefab`) are left for their owners.
17. **The drift's sweep needs to be as heavy as the hull.** Round 1's arc was a hairline behind an arrow and
    read as an arrow; at 12 px the arc under the tail reads as a slide.
18. The weakest of this round: the Sparrow's Turret Stance (a pawn at 40 px) and the Squirrel's Crystal Joust
    (a gem with a cut - the joust itself is not in it). Ask about those first.
