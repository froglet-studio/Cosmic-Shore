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

## 2026-10-25 - one icon: the Squirrel's Crystal Joust

Garrett: "your before pictures are not accurate or exhaustive." "lets focus on one icon for one vessel." "lets get
one thing right. then move on."

19. **A BEFORE is the card as the HUD draws it, in every state, in every place the sprite shows** - not the
    sprite on a square. `card.py` draws the lockup card from `AbilityLockupStyle.asset`'s own numbers (two
    trapezoids, the slant edge, the flower cell, the 60 px icon box) with the state tint (the joust flash) and
    the sheet now uses it; the one-icon sheet also shows the other surface the sprite was on (the Jousts
    metric icon in the arcade launch panel) and the fleet glyph the new icon is built on. It still says
    "drawn offline" on it: the Editor is the truth, and the flower is an approximation.
20. **One icon per round when the owner says so.** Three candidates on the card (the fleet's joust glyph with
    the crystal at the impact; the cleaved gem; the hull through the gem) beat eight redrawn icons nobody
    asked for. The joust glyph won because the Jousts OBJECTIVE and the Crystal Joust ABILITY are one act,
    and one act wears one pictogram (`objective_jousts.png`, the objective set's `jousts()`).
21. **Measure a failing gate before guessing.** The component gate wanted the spark to be one mass with the
    gem (start the marks inside it); the gap gate then named four narrow wedges between each spark and the
    lance arm beside it - found by dumping the tight-gap mask's quadrants, fixed by steepening the sparks.
    Two blind tweaks had already missed.
22. **"The Squirrel" means the Squirrel's silhouette, not a chevron.** Garrett on the first "through" option: "it
    should read as the squirrel silhouette vs a generic one." The silhouette comes from the hull's OWN mesh:
    `hull_view.py` projects the FBX (bind pose, file frame, nose +Y) to top / side / front masks with no Blender,
    and the spec's `squirrel_top()` is drawn to those proportions (slim wedge body, forward-swept wings with pod
    tips, a ribbed tail). Two ribs, not three - three fuse into a fishbone at 40 px.
23. **Keep the creature off the wings.** With the body overlapping the hull's widest part the composite was a
    blob at 40 px in every placement; with the creature at the NOSE (the hull punching through it, wings clear
    of the halves) it reads at 40. Place the second object where the silhouette is narrowest.
24. **"Fidelity" means the hull's real geometry.** Garrett on the drawn silhouette: "the fidelity is too low. this
    reads too abstractly." `mesh_silhouette.silhouette()` projects the FBX mesh itself (parallel projection,
    pitch / yaw, a 1.2 px thickening so the ribs survive 60 px); twelve views went on one sheet and Garrett chose
    D (tipped 45, turned 15). The hand-drawn `squirrel_top()` is retired. A real silhouette fails the `gap` gate
    on its ribs by design: the icon carries `exempt={"gap": "<who, why>"}`, printed on every run, never silent.
25. **Measure the projection before composing on it.** Three rounds put the creature up-right of the nose because
    I assumed yaw 15 tilted the hull's axis; `project_point()` on the nose and tail vertices showed the axis is
    HORIZONTAL in view D and the fitted box IS the hull's length. Compose from projected points, not from the
    numbers that produced them. And the creature at the nose needs a narrow crack (12 px) - a wide channel turns
    a body into two brackets.
26. **The opponent is the same hull, smaller.** Garrett: "perhaps use a second smaller squirrel." A hexagon at
    the nose read as brackets; a second Squirrel (the same mesh at 40 px, rolled 35 degrees nose-up: knocked)
    ahead of the big one reads as the overtake at every size, and needs no new pictogram. `silhouette(roll=)`
    turns a hull in the plane exactly (on the projected points, not a raster rotate). Two thin hulls sit at the
    coverage floor: thicken them (1.5 / 1.6 px) rather than shrink the gap between them.
27. **A card can be a PAIR the HUD tints separately - that is a view change, not a sprite.** Garrett: "the two
    squirrels should be facing each other and the same size. the right facing squirrel should be the players
    domain color. the other squirrel can be the danger color (fire). when the player jousts then both can turn
    lime." The Dolphin's jaw pair was the pattern: the card's icon Image becomes a transparent container (no
    sprite, alpha 0 - the lockup still kerns and punches it) over two stretched child Images; the view paints
    them from pushes it already receives (SetPlayerDomainColor, SetDangerTint) and snaps both to the shared
    `ElementalBarsConfigSO.limeColor` on a joust. Neither tint is an element colour: a team claim and the fleet's
    warning hue. `rebind.py` `pair=` builds the children in the prefab YAML with deterministic fileIDs and wires
    the view's fields; the kit writes three sprites (the pair in Pair/, each exempt from the whole-icon gates,
    and the white composite the Jousts objective box keeps). C# touched: SquirrelVesselHUDView - no Unity CLI in
    this session, so the editor check is on the board.
28. **Skimming is the second tinted pair, and the split is WHAT IS YOURS.** Garrett: "make the vessel and the
    prisms touching and behind the skimmer the player's domain color. make the prisms in front of the skimmer the
    blue" and "i like b2 the best". The Time card's icon became a container over SkimSelf (hull, skimmer sphere,
    the prisms under it - domain colour, from the same push as the boost fill) and SkimAhead (the prisms ahead -
    `ElementalBarsConfigSO.blueColor`, the HUD's one blue). Prisms are `motifs.block()` - a box from front-above
    with its seams cut - after a notched band read as "the profile of a chocolate bar". The pair's `pair=` step
    inserts the view's fields after a named anchor (`after=`). Seam slivers trip the specks gate: exempt by name.
