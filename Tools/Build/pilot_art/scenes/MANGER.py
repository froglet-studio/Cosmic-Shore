"""Tamako Deephook, MANGER: ibis and platypus, a hunter. Sample 1 of the four Garrett approved on 2026-11-11.
The kept picture is the 3:2 painting (Docs/Pilots/art/scenes/samples/MANGER-field.jpg) with 40 rows cut from its top
and 120 from its bottom, so the shot below frames itself and takes no FRAMING line.

Read blind on 2026-11-12, the avatar gave a stranger the ibis and "beaver (or otter)": the plate on the forehead read
as "a leather cap or helmet, or a casque/shield-like plate", and in the whole scene as "a flat cap or a mask piece
rather than part of the head". The bust had been named a platypus, and read as an ibis that wears a second bill. The
patch below designs Tamako again as one creature (Garrett, 2026-11-11: "we can do a lot better job of expressing the
animals together"): she has one bill, and it is both animals', a platypus's wide flat bill of slate skin where it
leaves the forehead, drawing in to the ibis's long scarlet curve, with water on it and breath from its nostrils; the
head turns a quarter so that its width is seen; the hands are bare and webbed. Only the name counts for a platypus, so
this is the pilot least sure to be named. This scene is one of the four Garrett approved on 2026-11-11, so the run is
his to allow by name; nothing but the head and the hands is asked. Not painted yet: this is one of the eight weakest
reads, and painting them waits for Garrett's go-ahead."""
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
        what=("the bill where it leaves the forehead, the turn of the head, the water on the bill, and the two "
              "hands"),
        changes=("the face", "the hands"),
        ask=("THE BILL. {name} has one bill, and it is both animals'. Where the small dark shape sits on the "
             "forehead now, the bill itself begins, and it begins as a platypus's: for its first third it is as "
             "wide as the head and flat, a finger thick, of soft bare skin the colour of wet slate, blue-black and "
             "finely pored, with two small nostrils on its upper side, its edge soft and rounded. It runs straight "
             "out of the forehead in one slope, and the brown fur of the head thins to single hairs out onto it. "
             "From the middle of its front it draws in and runs on as the ibis's long, slim, down-curved bill, as "
             "long and as curved as it is now. Over a thumb's length the slate skin turns dusky plum and then the "
             "scarlet of the rest. One mouth line runs under both parts, from the corner of the mouth to the tip, "
             "and the bare red skin round the eye marbles into the slate at the side of the bill."
             "\n\n"
             "THE HEAD. The head is turned a quarter toward the camera, so the flat upper side of the bill's wide "
             "part is seen, and it is still tipped down over the work."
             "\n\n"
             "THE WATER. Beads of water stand on the slate skin. A string of drops falls from the tip of the bill "
             "into the channel and rings it, and two thin threads of breath rise from the nostrils into the cold "
             "mist."
             "\n\n"
             "THE HANDS. Both hands are bare, and they are a platypus's in a person's shape: four fingers and a "
             "thumb in dark wet skin, with a thin web of skin between the fingers. They hold the line and the "
             "float as they do now."),
        edit=(   # the whole frame: the head itself changes, so the figure is the painter's (refs.WHOLE)
            ((0, 0), (1536, 0), (1536, 864), (0, 864)),
        ),
    ),
)
