# Lessons, dated and measured

Append one entry per lesson, newest at the bottom. State what was measured, not what was felt. Seeded 2026-11-07
from the record (`Docs/SKILL_TRAINING_PLAN.md` §3.2: "`WORMHOLE_SHADERS.md` §1-16 and `PRISM_ANIMATION.md` seed the
LESSONS file"), in the owner's words first. Nothing here is a confirmation: an entry says what was built, what the
owner said, what the record says replaced it, and what that taught. Numbered like
`.claude/skills/vessel-model/references/LESSONS.md`.

## Aug 2026 - the crystal capture (Garrett: "when elemental crystals are captured it looks terrible, and takes far too long. we need a more satisfying capture effect.")

1. **A flourish must never outlast its own payoff.** The crystal dragged to the vessel over 3 s, from a frozen start
   toward a moving hull, at full scale, unlit, still tumbling, then a bare `Destroy`; the Space crystal parked a
   full-size crystal on the hull for over half a second more (`Docs/ECOSYSTEM.md:4485-4502`). The element level lands
   at contact, so three seconds over an instant reward "does not read as a reward; it reads as lag" (:4522-4526).
   Replaced by a beat of 0.44 s (snatch 0.08, suction 0.26, absorb 0.10) against 3.0-3.6 s (:4504-4517).

2. **One look lives in one config, not per prefab.** The duration was authored per prefab, 1 s on two fauna and 3 s on
   eleven flora, "so the same pickup had two speeds" (`ECOSYSTEM.md:4491-4492`); everything now reads
   `Resources/CrystalCaptureConfig`, sized in crystal radii so a grown flora heart and a tiny fauna drop capture
   identically (:4517-4518).

3. **Homing on a moving target is a function of duration.** The old lerp read the hull's live position, which is
   correct, "over 3 seconds a vessel travels far enough that 'correct' still looks like chasing. Shortening the flight
   fixed more of the look than any easing change could" (`ECOSYSTEM.md:4528-4531`).

## 2026-09-16 to 09-22 - the Urchin's cradle (Garrett: "this looks terrible", then after a 10x tone-down "this effect looks really bad to the point i put this down and let this branch go stale")

4. **A harness is not a sheet.** Two rounds, whole prism faces and then wedges, were "faithful to the request as
   written, both were proven correct by the harness, and both read on screen as facets hinging"
   (`Docs/PRISM_ANIMATION.md:1930-1936`). "The cradle passed every measurement it had, twice, while reading as a
   glitch on screen - the measurements were correct and the design was wrong" (`.claude/skills/prism-morph/SKILL.md:424-426`).

5. **When told "looks terrible", do not tune the dials first.** "A deformation is only as smooth as the surface it
   moves, and 24 triangles is not a surface. No rigid per-triangle motion and no amount of tuning could have fixed it"
   (`PRISM_ANIMATION.md:1937-1939`). The rule as the skill keeps it: "If the report comes back 'this looks terrible',
   do not reach for the tuning dials first. Ask whether the motion is smooth enough to be a surface at all. A 10x
   tone-down of a hinging facet is a smaller hinging facet." (`prism-morph/SKILL.md:431-433`). The re-cut, more surface
   (3,072 triangles) and a one-line radial map, playtested as "easily one of the greatest things we have ever made";
   "The difference was not tuning" (`prism-morph/SKILL.md:24-30`).

6. **"The LOOK is a playtest away" is how every effect of this period ended.** "nothing here has been run in the
   editor - the wiring is machine-validated, the HLSL is executed by the harness, and the C# is Roslyn-checked"
   (`PRISM_ANIMATION.md:2028-2030`). The sheet is what was missing, not another proof.

## 2026-10-18 - the ring explosion, and the rule (Garrett: "good start but nothing in the environment should ever look flat like this, that is reserved for ui")

7. **A studio that draws a world effect as an additive sheet is deciding a UI look.** The Brittle Star's five-fold ring
   explosion was flat ribbons (an annulus, a pentagon, five arms), shown in a 2D top render and as an additive
   double-sided sheet; "The second cut, with beams a few units thick, still read as wire across a 780 u disk"
   (`.claude/skills/labmaker/LEARNINGS.md:1600-1605`). What replaced it: draw it lit and flat-shaded in the studio, size
   every thickness as a share of the effect's own size, judge from an oblique lit perspective render, prove it with
   `Blast_IsAVolume_NotAFlatSheet` and a flatten control (:1607-1612; `Assets/_Scripts/Tests/Editor/BrittleStarTests.cs:689`).
   The rule was LOCKED the same day (`Docs/claude/SHADERS_AND_PERFORMANCE.md:7-27`).

8. **A hull built from primitives reads as a placeholder, and no offline render said so.** The same day: "the sparrow,
   dolphin and squirrel all appeared to be cheap remakes that were very far from being ready to replace the original"
   (Garrett, `.claude/skills/vessel-model/SKILL.md:22-25`); "a hull redrawn from lathes, plates and lofts reads as a
   toy next to the artist's mesh (LESSONS 38), and no offline render told us so; the owner's Editor did" (:28-29).

## The switch ring's facets (Garrett, after the first look in Unity; undated in `Docs/SPINDLE_LOD.md`)

9. **Brightness flattens; contrast between facets is what reads.** "they were all very flat at all distances because
   they were so bright that they flattened as their features were all washed out" (`SPINDLE_LOD.md:505-506`). The first
   cut painted the ring between its signal colour and that colour 37 % toward white, "both ends bright, on an unlit
   shader under bloom - so every facet landed in one bright band and the twist, the strands and the facets vanished"
   (:507-509). Now a dark body at 0.18 of the signal and the bright signal, facet shading across the whole range: an
   edge-on facet is the dark body, a square-on facet the signal, about 5.5x brighter (:510-512), held by the harness (W5).

## 2026-10-21 - the Orca's missiles and bomb (Garrett: "increase the fidelity on the fly by wire missiles. they feel cheap. the bomb should not have a ring that says burst. it should use a lit state to show what it will destroy. always go for the 3d in world solution over the 2d flat text solution.")

10. **What "cheap" meant, as far as the record says.** A six-sided cone for the missile, an additive ribbon and a
    `TrailRenderer` for its trail (`Docs/Studios/CREW_OF_FOUR_LAB.md:167-175`). The fix on the record: a rocket of parts
    (body, ogive nose, a band in the crew's colour, four fins, a nozzle), a lit faceted motor flame with a white core
    that ignites over 0.15 s then flickers, a point light on the mass it passes, a body that banks 45 deg into the turn,
    and a plume "not a ribbon": a lit, faceted six-sided tube, 1.4 u at half of a 3 s life, tapering to nothing
    (`Assets/_Scripts/Controller/Vessel/Crew/ORCA_CREW.md:149-159`). NOT EDITOR-VERIFIED
    (`Docs/UNITY_VERIFICATION_CHECKLIST.md:3768-3790`): the owner has not yet said whether this is the opposite of cheap.

11. **The world's own channel existed before the ring was drawn.** The bomb's crosshair "drew a ring the size of the
    blast and 'BURST' over the live bomb"; now PrismLit's bank lights a sphere of the blast's radius in the gunner's
    colour, gated to each domain the blast breaks (`SHADERS_AND_PERFORMANCE.md:36-38`; `ORCA_CREW.md:161-165`). The
    second LOCKED rule came from it (:29-44). Look for the channel (LIT, the morphs, the petals) before drawing a mark.

12. **A screen-edge effect can still be a volume, and the owner's second note moved the READ, not a number.** The
    silence vignette's first cut was 2D; Garrett asked for "a client side perimeter of 3d craziness on the perimeter
    where vignettes typically use 2d effects", then "more overlap, show only half the faceted object that is poking into
    the screen ... it doesn't read as crystals coming in from the edge it read reality trying to pull you somewhere you
    don't want to be" (`Docs/ElementalAbilitySystem/SILENCE.md:143-147`). Shipped: 110 faceted shards, each "a closed
    outward-wound volume and not a flat sheet" (:148-156, :298).

13. **A motion is judged at the camera's distance, not in units.** The silence onset shake at 0.18 "felt nothing from
    the enemy Sparrow hits" on Garrett's second Dog Fight run: "0.18 world units at chase distance is under a degree"
    (`SILENCE.md:137-142`). Now 0.45 for 0.35 s at onset and 0.2 per hit.

## 2026-10-23 to 10-25 - the prism's death, steal and suction (Garrett's rounds 12-15, `Docs/PRISM_ANIMATION.md` §4.12)

14. **The order of events is a look; what shows first is the fact.** Round 14: "the stealing effect is supposed to curl
    the original domain and reveal the stolen domain underneath, but i am seeing it immediately change domain color and
    the peel the changed color to have the changed color also underneath ... the abrupt change on the outside is wrong"
    (`PRISM_ANIMATION.md:2756-2763`). The curl's underside had been stamped with the NEW colour; now the underside is
    the old colour and the only new colour on screen is the prism the roll uncovers.

15. **One vocabulary across interactions, and motion as pieces along a path.** Round 15: "lets use the silver gnomen
    triangles that are used on the explosion shader on the suction shader. Use the same LOD system as well."; round 15b:
    "it would detatch one triangle at a time along the current faces. so we would see the triangle spaced out along
    their path. so instead of one whole face that face simply separates its triangles as it is pulled in"
    (`PRISM_ANIMATION.md:2714-2733`). The first cut of round 15 moved the face toward the eater as one block (:2732-2733).

16. **Contrast comes from the object's own two tones, not an added highlight.** "No white accents. The crack flash, the
    ember front and the glints are gone (`_HotColor`, `_EmberGlow`, `_EmberWhite`, `_BurstFade` retired). The contrast
    is the brighter INSIDE colour on every kind's back faces as the surface opens." (`PRISM_ANIMATION.md:2906-2908`).

## 2026-10-24 - the vortex ring (Garrett: "make the vortex ring loose the flat effect and replace it with a cool warping effect like the black holes use but convey a more subtle toroid instead of a spherical singularity")

17. **A LOCKED rule in a topic file did not stop the next flat effect.** The Pistol's ring drew "the flat effect" six days
    after the rule was locked; it became a lens round its core circle, "a subtle doughnut of bent light with a faint rim
    on the tube's edge, no shadow and no core" (`Docs/BLACK_HOLE.md:475-483`). The skill and its gate have to carry the
    rule; a doc nobody reads first does not. (Here the ring IS the object: `LOOK_LANGUAGE.md` rule 5's exception.)

## 2026-10-27 to 11-01 - the wormhole's 26 rounds (Yash and Garrett; `Docs/Shaders/WORMHOLE_SHADERS.md`, 984 lines, eight looks removed or replaced, one PR reverted)

18. **The lens with nothing added is the part every round kept.** Round 11, the clear water pipe: Yash, "a pleasant
    water ride experience through a water pipe. Transparent pipe ... I should not feel too much of warping ...
    performant"; Garrett, from the Tube Lens Lab, "lets the lensing alone sell the effect." (`WORMHOLE_SHADERS.md:84-85`).
    "Nothing is tinted and nothing is added. Every pixel shows a pixel of the real scene, taken from another place."
    (:82). Garrett's lab (d2380c883, :90) is the source the shader ports.

19. **An additive wireframe survived only as the aim mark.** YASH'S LOOK, the spindle bridge (§2): a lattice of glowing
    six-sided spindles over a formed tunnel in the domain colour; removed 2026-10-29, the reticle (your aiming
    wireframe, the same spindles) kept (:233-287; `Since round 9` :245). The one thing with no world object to sit on is
    the one that stayed additive (`SHADERS_AND_PERFORMANCE.md:41-42`).

20. **A term that ships at zero is a look nobody picked.** The warp tube ("round 4 set it to 0", :293), the mouth dents
    and the collapse ripple ("only touches the screen's edges (Yash: no distortion in the player's eyesight)", :312) all
    shipped at 0 and were deleted on 2026-10-29, with §12.5 saying how to bring each back.

21. **The domain is one accent, never a wash.** The calm inner wall, the "blue tube" (§4): an additive sheen from the
    maker's domain colour to white over every wall "is what made tunnels read blue (Yash, 2026-10-28). He switched it
    off" (:318-324).

22. **An illusion that cannot do the one thing the object IS is a different object, not a cheaper tier of it.** The
    cheap mobile water pipe (§5a, Yash 2026-10-28: "a transparent watery tube, mobile friendly, an illusion of a wormhole
    rather than real lensing", :345) and the aquarium tube (§5b, PR #1620) were reverted: Yash, "this doesn't have an
    outer layer, no light bending / warping mimic, doesn't feel like a wormhole at all". "A mesh skin that samples
    `_CameraOpaqueTexture` can only push the picture sideways at the tube's own pixels. It cannot bend rays round the
    tube, and it has no shell to read as a layer." (:386-388). A phone tier drops cost, not the fact.

23. **A metaphor laid over the object replaced the object; the ask was the object.** The tree (§6, rounds 13-15: a dead
    tree's trunk in watery glass wrapped round every tunnel, off since round 23) and the roots from inside (§7) were
    deleted 2026-10-29: Yash, "I dont want any water shader, or underwater look, I want to feel Im inside the wormhole"
    (:764-765).

24. **Two owners' presets in one shader are two looks.** The PC look (§11, Yash 2026-10-29: "Make the shader good for a
    good quality PC. Keep the final one shader", :537) carried YASH'S and GARRETT'S presets, a tint, absorption,
    dispersion (3 scene reads) and 16 field knobs; the calm-jelly round removed them ("remove Yash's look, Garret's look
    and other etc parameters that are not necessary", :766; §13). The studio mapping fell from 108 to 72 rows (:779).

25. **Comfort is fields on the config with caps in C#; jelly against water was one sine against a train.** The calm
    jelly (§14, 2026-10-29): "pleasant calm distortion, little jelly like clear liquidy hollow from inside tree trunk,
    same will be for the root" (:792-794); the breath is "one smooth sine with a slight twist round the tube, never a
    ripple train, which is what makes it read as jelly rather than water" (:806-808); the first look with a Comfort
    struct and rules of thumb (:872-881), "Speeds are capped in C#, not the shader" (:880).

26. **Twenty-six rounds converged on the physical object, which the first lab already was.** The plain wormhole
    (§16, 2026-11-01): "I want the wormhole effect back, that looks realistic, remove trunk and bark, root related
    shader parts, its gonna be plain worm hole with wider mouth looking cool from outside and inside, bending and
    warping of space time." (:959-960). The network shimmer and pulses and the bridge over formed tubes went; a mouth
    lens, an Einstein ring and a throat shadow came; the mouth widened 35.6 to 44 (:962-978). A look is ONE thing
    (`LOOK_LANGUAGE.md` rule 11); the alternatives live in the lab page and three named restore commits (435ce058c,
    0c8c5218c, 2d1e8726c: §12.1, §13, §15).

27. **When the switches go, the lab that compared them goes too.** The Lavalamp lab and its look buttons were removed
    with the Soliton Tunnel rename (§15, :924-955: "an environmental fundamental any vessel or scene can use", :926).
    The kit's sheet has to be where alternatives are compared, or they will be compared in the shader again.

## 2026-11-03 - the Brittle Star ring's motion smear (Garrett; `Docs/UNITY_VERIFICATION_CHECKLIST.md:21529`)

28. **A smear that brightens is a streak.** The spinning ring "is drawn smeared and no brighter than a still ring on the
    same background; the moment the stick stops, the plain ring is back with no pop" (:21540-21541). NOT RUN IN EDITOR.

## 2026-11-07 - the Manta's trail (`R_VesselActions/MANTA_STING_KABLOOM.md:45-48`)

29. **The fleet's trail is a chain of the one prism, and a vessel's trail is not where its elements show.** "The trail
    is toned down to a Squirrel-style trail (2026-11-07 follow-up): the Squirrel's prism size (20 x 0.75 x 5.5), no
    element growing its volume, no turn flare." The plan lists it among the accepted yardsticks
    (`Docs/SKILL_TRAINING_PLAN.md:243-244`); the doc calls it a follow-up, and the record holds no "yes".
