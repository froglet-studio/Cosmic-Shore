"""Tamako Deephook, MANGER: ibis and platypus, a hunter. Sample 1 of the four Garrett approved on 2026-11-11.
The kept picture is the 3:2 painting (Docs/Pilots/art/scenes/samples/MANGER-field.jpg) with 40 rows cut from its top
and 120 from its bottom, so the shot below frames itself and takes no FRAMING line.

Read blind on 2026-11-12, the avatar gave a stranger the ibis and "beaver (or otter)": the plate on the forehead read
as "a leather cap or helmet, or a casque/shield-like plate", and in the whole scene as "a flat cap or a mask piece
rather than part of the head". The bust had been named a platypus, and read as an ibis that wears a second bill. The
patch below designs Tamako again as one creature (Garrett, 2026-11-11: "we can do a lot better job of expressing the
animals together"): she has one bill, a platypus's broad flat bill of blue-black skin where it leaves the face,
running on from the middle of its round front edge as the ibis's long scarlet curve, with water on it and breath over
its nostrils; the head turns a little so that its flat top is seen. Three critics then read the first draft of these
words beside the picture (an art director's eye, a painter's, a stranger's), and this is the second draft: the bill
begins in front of the eye and below it (from the forehead it was the cap again, with the eye in its shadow); the
broad part keeps its round front edge and both corners, the outline a platypus is named by, where the first draft drew
it in to a wedge; the head turns only a little, so the long curve the ibis is named by stays in side view and its tip
stays where it is; slate, marble and the string of drops are out of the words; and the hands are left as they are,
since they are small, already dark and wet, and hold the line. The same three then read the second draft against their
first notes, and this is the third: a three-quarter view is out of the words (it is a noun a painter can paint, and it
would shorten the long curve); the turn of the head is given by what it shows. Only the name counts for a platypus:
about one in three (the critics' estimate: nothing is painted or tested). This scene is one of the four Garrett
approved on 2026-11-11, so the run is his to allow by name, and nothing but the head is asked. Not painted yet: this
is one of the eight weakest reads, and painting them waits for Garrett's go-ahead."""
SCENE = dict(
    treatment="Field documentary, a long lens from far off",
    shot=("A documentary photograph taken in the field by a photojournalist who waited hours for it, in the manner "
          "of the best wildlife and expedition photography. A 400mm lens at f/4 from a long way off, the camera low, "
          "at the subject's own height; one blurred stem leaning through the near foreground, the subject sharp, "
          "the distance soft. Natural light only. The subject is at work and has not noticed the camera. The whole "
          "figure is in the frame, head to foot, a little over half the frame's height and off-centre, with the "
          "place filling the rest. True-to-life colour, a little grain, nothing retouched."),
    frame="",
    source="profession: a hunter who wades the feeding flats sets a lure line",
    seen=("the whole face is scarlet and the long bill rose-red, greyer toward its tip",
          "the cape has a broad turned-down collar with magenta piping on every edge",
          "the jerkin is dark brown, the line is a coil of pale hemp rope, and the strap's buckle is square brass"),
    carry="seen whole, the paddle tail is as long as a forearm and a hand, grows from the base of the spine and "
          "comes out under the back of the cape",
    doing=("Tamako kneels on one knee at the edge of a shallow channel on a feeding flat and sets a lure. One "
           "webbed hand pays out wet line from the coil worn at the chest; the other lowers a small hand-carved "
           "wooden float with one glinting crystal bead into the water. The long bill is tipped down over the "
           "work and the eyes are fixed on the line. The paddle tail lies flat on the mud behind, as a brace."),
    place=("The feeding flat lies inside a living cell the size of a valley: grey silt and mirror-still water a "
           "hand deep, stands of pale glassy reeds whose seed heads are clusters of small clear crystals, and far "
           "behind, through mist, the wall of the cell, a translucent sea-green membrane veined like a leaf and "
           "curving up out of sight."),
    light=("Soft, shadowless green-white daylight filtering down through the membrane, the mist brighter behind "
           "the figure, the wet mud shining."),
    wear=("Both knees and shins are plastered with wet grey mud; the hem of the cape is soaked dark and spattered; "
          "a three-cornered tear near the hem has been mended with coarse pale stitches; the leather is cracked "
          "at its creases; the fur of the neck is damp and spiked; there is silt under the claws, and water "
          "stands in beads on the oilskin. Above the mud line the magenta piping still shows bright along the "
          "collar and down the front edges of the cape."),
    close=(490, 155, 860, 525),
    patch=dict(
        what=("the bill where it leaves the face, the dark shape on the forehead, the turn of the head, and the "
              "water on the bill"),
        changes=("the face",),
        ask=("THE BILL. {name} has one bill. Where the small dark shape sits on the forehead now there is the "
             "brown fur of the head, running down to the bill. The bill begins where it begins now, in front of "
             "the eye and below it, and it begins as a platypus's: for its first third it is broad and flat, "
             "two-thirds as wide as the head, with a soft rounded edge, of soft bare skin, blue-black and matte, "
             "moist and finely pored, with two small nostrils on its upper side. The eye sits behind its rear "
             "corner, in the bare red skin, as it does now. The fur of the forehead runs down onto its upper side "
             "in one slope and thins there to single hairs. It keeps its width to a broad round front edge with a "
             "soft round corner at each side: the front edge of a platypus's bill. From the middle of that edge "
             "the bill runs on, one piece with it, as the ibis's long, slim, down-curved bill, as curved as it is "
             "now, and the tip of the bill is exactly where it is now, so a round corner of dark skin stands out "
             "at each side of the red bill's root. The blue-black skin runs on along the top of the red bill as a "
             "narrowing streak and breaks into soft dark blotches in the scarlet. One mouth line runs under both "
             "parts, from the corner of the mouth to the tip, and the bare red skin round the eye runs in soft "
             "blotches into the blue-black skin at the side of the bill."
             "\n\n"
             "THE HEAD. The head is turned a little toward the camera, just enough that the flat upper side of the "
             "bill's broad part, both nostrils and both round corners are seen. The slim red part of the bill is "
             "still seen from the side, with its whole curve, tipped down over the float as it is now, and the eye "
             "is where it is now."
             "\n\n"
             "THE WATER. Beads of water stand on the blue-black skin. One bright drop hangs from the tip of the "
             "bill, and one small soft puff of breath hangs over the nostrils in the cold air."),
        edit=(   # the whole frame: the head itself changes, so the figure is the painter's (refs.WHOLE)
            ((0, 0), (1536, 0), (1536, 864), (0, 864)),
        ),
    ),
)
