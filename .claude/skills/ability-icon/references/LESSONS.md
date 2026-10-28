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
    `elementColour: 0`, `check_element_colour.py` held it until the 2026-10-26 merge, when it turned out `check_ui_colors.py` (Docs/UI_COLOR_RULES.md, the same rule relayed by Shombith the same day) already did: the icon gate was retired, theirs holds it (and lists the element->colour maps still in code with
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
    `ElementalBarsConfigSO.LimeColor` on a joust. Neither tint is an element colour: a team claim and the fleet's
    warning hue. `rebind.py` `pair=` builds the children in the prefab YAML with deterministic fileIDs and wires
    the view's fields; the kit writes three sprites (the pair in Pair/, each exempt from the whole-icon gates,
    and the white composite the Jousts objective box keeps). C# touched: SquirrelVesselHUDView - no Unity CLI in
    this session, so the editor check is on the board.
28. **Skimming is the second tinted pair, and the split is WHAT IS YOURS.** Garrett: "make the vessel and the
    prisms touching and behind the skimmer the player's domain color. make the prisms in front of the skimmer the
    blue" and "i like b2 the best". The Time card's icon became a container over SkimSelf (hull, skimmer sphere,
    the prisms under it - domain colour, from the same push as the boost fill) and SkimAhead (the prisms ahead -
    `ElementalBarsConfigSO.BlueColor`, the HUD's one blue). Prisms are `motifs.block()` - a box from front-above
    with its seams cut - after a notched band read as "the profile of a chocolate bar". The pair's `pair=` step
    inserts the view's fields after a named anchor (`after=`). Seam slivers trip the specks gate: exempt by name.
29. **The ring stays white, and thinner.** A part the view never tints is still its own child (field None in the
    `pair=` step) so it is not painted with the hull. 5 px. Garrett on the two pairs: "I really like where we
    landed here on both because the icons are teaching the ability" - now the bar at the top of SKILL.md.
30. **A card can be a LIVE READOUT built from parts.** Garrett: "use blocks and straight as the default state. when
    drifting change them to octahedrons. have the angle between vessel and the trail match the player's
    difference between vessel forward and course velocity." The drift card is a stretched TRAIL part (blocks at
    rest, octahedrons while drifting - a drift lays shielded prisms, and shielded reads as a diamond) and a HULL
    part in its own point-anchored child rect on the course line (`pair=` child with a rect), turned every frame
    by the signed forward-vs-course angle the controller now pushes (`SetDriftAngle`; the old binary side lean
    is the fallback for a HUD without the parts). The hull sprite is the mesh centred in its own canvas so the
    child's rotation is about the hull. Unity wraps long flow values onto a second line in prefab YAML - a field
    rewrite must match `{...}` across lines or it leaves a dangling `type: 3}` (rebind `_set_field`).
31. **A part that turns must pivot on the hull's AXIS, not its bounding box.** Garrett: "the trail should be
    directly behind the squirrel so the pointing down vs up has an anti symmetry to it." In view D the nose-tail
    line sits 16 px above the sprite's bbox centre (the wings and pods hang below it), so a hull centred by its
    bbox had the trail running under it and turned about the wrong point. Measure the axis with
    `project_point()` on the nose and tail vertices, draw the hull that much low in its canvas so the axis IS
    the canvas centre (the child rect's pivot), and put the trail on that line.
32. **No state tints on the parts: the angle and the trail change are the signal.** Garrett: "lets have it and
    the trail stay and remain the domain color (but the octahedrons can change to the shielded domain color),
    the dynamic angle and trail change should be sufficient." The drift hull and the resting trail wear the
    domain colour; the drifting trail's octahedrons wear the domain's SHIELDED colour (`SetShieldedTint`, the
    same resolve the omni card gets). driftColor / doubleDriftColor survive only on the single-icon fallback.
    General rule: once a card's parts carry team colour, a state is told by SHAPE (and a team-derived colour),
    not by a second palette.
33. **A ring around a hull reads as a SKIMMER; a quantity is the plate gauge.** The Dolphin's Time card (the boost
    bank fills while it drifts) was offered as the top-down Dolphin inside a boost ring. Garrett: "i like the top
    down view, but the ring now reads as a skimmer. lets just have the whole lower totem fill with the consistency
    of other resources." So the bank is the card's PLATE GAUGE - `AbilityIconBinding.gauge`, the fleet's one
    linear fill the lockup adopts onto the ability plate (Docs/ABILITY_LOCKUP.md "The gauge") - and the card's
    icon is the hull alone (`Dolphin_ChargeFill`, the live mesh from above, domain colour, turned by the live
    drift angle). The authored eleven-step ring retired: `chargeSteps` emptied, the step methods gone, the PNGs
    left where the artist put them. The kit learned three things: a `pair=` child with icon None is a bare gauge
    Image, `binding=` names it on the view's abilityIcons entry, `fields={"x": "[]"}` empties a serialized list;
    and `card.py` draws the gauge (`gauge=0..1`) so a sheet shows a metered card as the HUD does. General rule: a
    HOW-MUCH on a card is never a shape of its own - not a ring, not a ladder - it is the plate filling, the same
    on every card that has one; the icon spends its shapes on WHO and WHAT.

## 2026-10-26 - the Sparrow's Time card: the hull in its own TAKE

Garrett: "time first, build the folded pose." "what do you mean roll pip? ... oh i know. it is letting you know
that the roll is available." "lets not show that that is armed because its cooldown is so low." "make the vessel
pointing to the right. (it is currently pointing left, which may be due to a model error)" "A is the best." "lets
show some jets coming out of the sparrow so the default state communicates motion and doesn't just look like the
vessel silhouette."

34. **A hull's authored TAKE is a card state, and the kit can skin it without Blender.** The Sparrow rests with
    both fans spread (a starburst at 60 px); its SpeedUp take folds them into swept wings, and that fold IS the
    afterburner. `mesh_pose.py` reads the FBX skin (clusters: indexes, weights, bind matrices), the bone chain and
    the take's keys, and linear-blend-skins the mesh with the pose as a delta on the bind pose;
    `mesh_silhouette.silhouette(..., take=)` projects it. Two proofs on the file (`--check`): the rest skin
    reproduces the raw mesh (0.0000) and the node chain lands on every TransformLink (the XYZ Euler convention
    holds). Blender's exporter writes a cluster's `Transform` as the mesh in BONE space (inv(link) * mesh global),
    not the mesh global - 65 distinct matrices on the Sparrow; read it as such or the rest skin is 47 units off.
35. **Nose right is the owner's read, not the file's.** The Sparrow's dossier records the hull flies blade-end
    first (a model error the modeller will fix); the card shows the pointed fuselage to the right because that is
    what reads as a ship ("make the vessel pointing to the right"). `mirror=` is a per-hull call made by looking.
36. **A readout that re-arms faster than a glance is not worth a plate.** The Time plate filled light blue while
    the strafing roll was armed; Garrett: "lets not show that that is armed because its cooldown is so low." The
    pip is unbound (`binding=dict(element=4, gauge=None)`, `deactivate=` on its Image, the view's pip fields
    deleted with `fields={...: None}`) and the plate is bare. Ask of every live signal: does the pilot ever act
    on it?
37. **The default state must still MOVE.** The hull alone, spread or folded, is a silhouette; two white exhaust
    plumes behind it ("jets coming out of the sparrow") make the rest state a ship under way and the boost state
    a ship at full burn - the plumes lengthen with the fold. The jets are their own untinted part (`AfterburnerJets`,
    rest/boost sprites) so the domain colour stays on the hull; the hull sits right of centre to leave them room
    (box 94 at cx 74). `plume()` in the spec: a tapered bar, chamfered at the engine, square at the tip.
38. **A ghost is the ability's PROMISE; the state turns it real.** The Sparrow's Mass card (Turret Stance) fires
    swelling rounds from both pods: while flying the rounds are a ghost (alpha 0.6) of what the stance will fire,
    taking the stance turns them solid with a punch, and Mass 5 swaps them to octahedrons in the domain's shielded
    colour - the same shape rule as the Squirrel's shielded drift trail, so "shielded" is one shape fleet-wide.
    Garrett: "b is good" (both pods at their real spacing over one centreline stream). The upgrade hook is
    `SetAbilityUpgraded(element, upgraded)` on the view, so an L5 that changes WHAT the act makes changes the
    card's part, not only its mark. A rebind `old=` step is retired once its icon's Image becomes a container
    (the sprite is cleared, so no reference remains to keep).
39. **The judging sheet is declared in the spec, not scripted per round.** Every options and states sheet of
    rounds 3-4 was a scratch script; a session without them would have rebuilt the sheet machinery before drawing
    a line. `CARDS` in the spec names a card's parts, its states (part, colour key, alpha), its plate gauge per
    state and the sprite it replaced (with the commit to read it from), and `author.py` draws
    `Docs/Icons/sheets/<hull>_<card>.png` on every run (`states.py`). Colour keys are the HUD's own: domain,
    shielded, blue, white, lime, danger - sampled from the palette and the bars config, never typed in. An options
    round is the same sheet with one entry per candidate. SKILL.md §4.5 is the eight-step wiring recipe a session
    follows for a live card.
40. **The kit draws more than ability rows: the Toy Box portraits (2026-10-26).** Garrett: "update the images on the
    toybox so they are production ready. they should communicate their utility and adhere to the larger design
    principles." The seven toy cards showed the codex baker's `ToyEmblem` disc (a ring round spheres in the toy's
    accent), seven near-identical rings that named nothing. `Tools/Build/icon_kit/toy_portraits.py` now draws one
    glyph per toy in this language and writes it into the file the codex page points at (guid kept; a page with no
    image gets one through the "filled only when empty" tier), with its own card-mock sheet
    (`Docs/Icons/sheets/toybox.png`) and `--check`. Three things it taught: (a) **the same grammar for the same act**
    - the three "pick one" toys (vessel, domain, world) all put the thing you get SOLID and the alternatives as
    ghosts, so the row reads as one family; (b) **two hulls side by side must share a view** (both plan) or they
    read as two kinds of thing, and a hull's widest span (the Dolphin's wings) decides where an arrow can go - it
    went between the tails, not across the middle; (c) **the 60 px gates are not a portrait's gates**: a 128 px card
    portrait is exempted from `components`/`gap` BY NAME with the reason, never by loosening `verify.py`. The
    generator that made the old art (`ToolPortraitBuilder`) was retired and the baker stands down on Tool pages, so
    a re-bake cannot put the spheres back.

41. **When the act is hard to SEE on the mesh, change the pose in the model, not the drawing (2026-10-27, the
    Grizzly).** Dig In wanted the anchor sunk into the ground, but in the model's first `dig` take the anchor only
    raked, and its tines reached 3-6 px below the engine pods in the side view. A ground plate low enough to catch
    them also caught the pods, and one high enough to clear the pods caught nothing. Extending the tines in the icon
    would have been a drawing of the hull. The fix went in the model's spec (`vessel_model/specs/grizzly.py`): the
    `dig` take now PLUNGES the anchor 1.3 units as it rakes, and the tines reach 20 px below the pods. The icon renders
    that take (`mesh_silhouette(..., take="rig|dig")`), the plate sits 7 px under the pods, and the plate is cut 6 px
    round the tines so they read as sunk into it. Two more:
    - **A rolled hull's rear edge is not its engines' rows.** Rolled nose-down, the side view's leftmost column caught
      only the upper engine row. Measure the rows on the UNROLLED silhouette, then turn the bars with the hull about
      the same centre: `canvas.rotate` and the silhouette's `roll` share one convention.
    - **A hull with an embedded HUD rebinds in the vessel prefab.** The Grizzly has no `<Hull>HUDVariant`: its
      GrizzlyHUD lives in `Grizzly.prefab`, so `REBIND` names that file. A placeholder another screen also shows
      (`CannonIcon-PLACEHOLDER.png` in `ModeControlsLibrary.asset`) is left to that screen's owner.

## 2026-10-27 - the Sparrow's Space card: options are judged in the LOCKUP

Garrett: "going forward when showing vessel icons lets show the before and the other icons of the vessel so we can
consider how they distinguish themselves too."

42. **A candidate is judged beside its siblings, never alone.** The first Space options sheet showed each candidate on
    its own card; one of them (the Sparrow firing from both pods) only looks wrong next to the Mass and Time cards,
    where it is the third spiky hull in a row of four. The options sheet is now the hull's whole lockup per
    candidate, with the BEFORE row first: `CARDS` entries share a `group=`, the spec declares `LOCKUP` (its other
    cards as the HUD draws them today, parts and colours), and `author.py` writes `<hull>_<group>_options.png`
    (`states.options_sheet`). The family gate (IoU at 24 px) measures the same thing; the sheet lets the owner see it.

43. **A live QUALITY is drawn as the thing doing it; its LEVELS are segments in the plate.** The Sparrow's Space card
    (2026-10-27). Garrett: "the icon we had before the current had a way to show that there were different heat
    buckets that had different spread. if you could convey the realtime spread value and which level they are on
    that would be best." Then: "i like what option E is trying to do, but the clarity of F is good. lets try to get E
    using more shots and the whole space to define the angle"; "i like the ease of reading the lines as the
    boundaries between levels but i don't like how it visually dominates the whole space"; "option E2 looks great".
    What landed: the icon is ten rounds on their rays inside the gun's live cone (one stream cold, the card's full
    height in the collapse; every rank puts one round on a cone EDGE so the angle is drawn, not guessed); the heat
    gauge's phase joins are a GAP through the fill once passed and a short dim edge tick while ahead
    (`card.py gauge_marks_style="segments"`, `SparrowHUDView.PaintGunHeatMarks`). Three things it taught:
    (a) **measure the mechanic before drawing the ability text** - the description said Space extends the guns'
    range, but Round 7 of SPARROW_SPRAY_ACCURACY.md fixed the range; the first options round drew "reach" and was
    wrong for that reason. (b) **A real value too small to see gets a readable value per level**, interpolated
    between knots (`SprayCardAngle`), never a fake constant: the card still moves every frame. (c) **When a part
    repeats N times along a live layout, generate the N Images in the view from ONE part sprite** and serialize the
    layout (the kit's SPRAY_RANKS = the view's `gunSprayRounds`); the prefab gets only the sprites and the
    container step - ten prefab children would be ten things to keep in step.

## 2026-10-28 - the Rhino's Charge card: a part that FOLLOWS the real thing

44. **When the card follows a live object, follow its SHAPE and its FACING, not only its angle.** Three rounds: a
    domain-coloured lozenge ("make it look more sword like and not team colored... the idea is to follow the actual
    sword"), a straight sword ("more like a katana."), then the katana turned with the swipe - and Garrett's two notes
    on it were both about facing: "flip the sword as well as rotate it so it is swiping with the sharp part" and "the
    both triggers chop should remove the curve so it looks like it is from behind". An asymmetric part (one edge, a
    curve) that only ROTATES shows its spine leading half the time. Draw it once with the edge on a known side, let the
    view MIRROR it about the pivot so the edge leads the motion (and keep the last side at rest, so letting go does not
    flip it), and give each view the object really has (from the side, from behind) its own sprite, switched at a
    threshold the game already uses (here the energize stance's summed pull) rather than cross-faded: a cross-fade of
    two silhouettes reads as a double image. Pin the facing in a pure function with a test, since a sign slip draws a
    plausible wrong card. A tool that is the Vessel's own (a sword, a weapon) wears white / danger, not the domain.

## 2026-10-28 - the Rhino's Mass, Space and Time cards: the mistakes that cost a round each

Garrett, after the Rhino's third card: "look over the iterative mistakes made and be sure to update the icon creation
skills with your learning so you get progressively better at developing icons for vessels." Every lesson below cost at
least one options round; together they are the pre-send review in SKILL.md §3 step 2.5.

45. **A copy of a moving part is mirrored WITH it.** The Mass card (Afterimage) drew the sword's prism copies at the
    right angles, but "the after image is flipped the wrong way": the copies are the sword where it WAS in the same
    swing, so they take the sword's facing as well as its angle. Lesson 44 applied to every ghost, trail and echo of a
    part: whatever transform the live part gets (mirror, foreshortening, the curve's side), its copies get too. Check
    a ghost against the part it echoes before sending, not against the brief.

46. **Every mark on the card must answer "what is this showing?" with something nothing else on the card shows.** The
    Space card's first round put a plate fill under a blade whose LENGTH already was the Space level. Garrett: "what is
    the fill showing?" - and on the Space 5 tick: "yeah everything has an L5 upgrade so that is inconsistent". Two
    rules: (a) one quantity, one place - if the icon draws a how-much (a length, a count), the plate does not repeat
    it, and vice versa; (b) the level-5 upgrade is never marked as a level (every ability has one), it is drawn as the
    ACT it unlocks. Before sending, name in one line what each part and the plate tell; a part with no line goes.

47. **Draw the upgrade as the act on its real target.** "show it hitting a supershielded prism and it breaking above
    5": Shield Breaker became the blade's tip in a super-shielded prism, whole below Space 5 and shattered from it. The
    level-5 state is a STATE of the card (a swap the HUD makes off the real upgrade flag), not a badge.

48. **A game object is drawn from its real geometry, not from memory.** "that does not look like a stelated
    octahedron, you need a better supershielded prisms icon": the hand-drawn star was a guess. The fix read the game's
    generator (`StellatedOctahedronMeshGenerator`), built the solid (the two tetrahedra, every face), and projected
    it. Lesson 24 ("fidelity means the hull's real geometry") holds for every object on the card - prisms, crystals,
    projectiles - and the source is the mesh or the generator, found before the first sketch.

49. **Flat, one colour, in its most iconic projection.** The first true stella octangula was a shaded three-quarter
    view; Garrett: "we need a flatter design in order to play well with the others". The row is flat: one colour per
    part, facets told by SEAMS (a few px of knock-out), never by shade. Choose the projection where the solid reads as
    its own glyph (a stella octangula down a body diagonal is the six-point star) - pick it by measuring several
    views, not by default.

50. **A secondary object knocks out around the primary; it never covers it.** "it should not occlude the sword or the
    max length ticks". Cut every secondary part clear of the parts above it in meaning with a grown mask (`toy_portraits
    .grown(mask, px)`, ~2.5 px) - the same knock-out the seams use - so the focal object and every reference mark stay
    whole at 40 px. Check overlaps part by part, at 40 px, before sending.

51. **A destroyed state is the object's real pieces separated along its real seams.** "it should have a better flat
    destroyed state than the flat state with some erased. it should remake with the correct separation of faces." A
    broken thing is not the whole thing with holes: split it into the pieces the game breaks it into (here each spike's
    facets), push each out along its own direction (a fraction of its distance from the centre), keep each piece whole.

52. **Draw the mechanic, AND what it means for the vessel.** The Time card (Ramp Spool) went out as three ways to draw
    "speed": streaks, chevrons, a slope. Garrett: "i think this ability is about the rhino and its invulnerability to
    danger prisms so it should have a rhino silhouette and a tick on the meter ... that shows the barrier come up to
    protect against the danger prism." Then: "also using the mechanic to produce icons still seems very sensible to
    not discard." So the mechanic stays the source (the speed is the plate, its 70% threshold the tick, the ward and the
    danger prism the real objects the code names) - what the first round missed is the PAYOFF the pilot plays for (the
    level-5 ward, untouchable above 70%) and WHO gains it (the hull). Brief every card from the mechanic first, then ask
    "who gains what, against what"; when the act changes the vessel itself (protected, faster, heavier), the vessel is
    on the card.

53. **A threshold on the plate is the Sparrow's segment mark, never a line across.** The 70% Momentum mark went out as a
    full-width line; "not the bar across. updated sparrow has a better solution": `gauge_marks_style="segments"` - a
    short dim tick at each slanted edge while it is ahead, a gap through the fill once passed
    (`SparrowHUDView.PaintGunHeatMarks`). Lesson 43 already said it for levels; it holds for a single threshold too.
    Default every plate mark to "segments"; "lines" is the rejected style.

54. **Reuse the game's own 2D art before drawing a hull.** The Rhino's FBX cannot be posed without Blender here, but
    the game ships a top-view HUD silhouette in parts (`Assets/_Graphics/Silhouettes/Rhino/RhinoBody.png` + two
    `RhinoWing.png`), laid out at the dossier's measured pod offsets. Search `Assets/_Graphics/Silhouettes/` (and the
    sparse checkout's excluded paths: `git ls-files | grep -i <hull>`) before concluding a hull cannot be drawn.

55. **Keep the act's motion away from the plate's marks.** The Time card's round 3 flew the Rhino nose UP, so the ward
    and the danger prism sat at the top of the card, right under the fill line and the 70% ticks. Garrett: "lets have
    the rhino travel left to right so it doesn't cause visual confusion between the impact event and the threshold".
    The plate fills bottom to top, so anything the icon moves or strikes reads as part of the meter when it runs
    vertically; run it ACROSS (a hull flies left to right, as the Sparrow's does) and send anything it throws off
    sideways or down, clear of the marks. Check where every plate mark lands on the card before placing the act.
