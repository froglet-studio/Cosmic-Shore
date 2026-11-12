"""Tender: parrot and narwhal, a preservationist. Replanting a bleached reef one seed at a time, and opening each
stone-hard seed case in the bill: asked to hold still for a feature photograph, and not pleased about it.

Read blind on 2026-11-12, the avatar gave a stranger the parrot and "seal (harbor or leopard seal)": "a seal's smooth
spotted head, like a hood over a parrot face", "Skin looks rubbery like a mask", "the tail fluke at right floats
unattached to the body". The whole scene was named a narwhal, and of the tusk its reader said "no clear root; it looks
stuck onto the beak". The bust had been named a narwhal at full size: its tusk leaves the face at the near corner of
the mouth. The patch below designs Tender again as one creature (Garrett, 2026-11-11: "we can do a lot better job of
expressing the animals together"): the macaw's scarlet runs over the whole crown and thins into a narwhal's skin down
the back of the head, with a blowhole and a breath where the feathers end; the tusk is a tooth with a root that shows,
and it is what Tender plants with, so its tip is caked with the flat's white sand and the hand that held a dibber
holds the next shoots; the fluke, which the frame cannot join to a body seen from the waist up, is below the frame.
Not painted yet: this is one of the eight weakest reads, and painting them waits for Garrett's go-ahead."""
SCENE = dict(
    treatment="Editorial portrait for a feature, held mid-task under flat bright overcast",
    shot=("An editorial portrait made for a feature about a replanting, the subject asked to hold still in the "
          "middle of the job and not pleased about it: an 85mm lens, flat bright overcast, the pale ground "
          "filling every shadow, a clean composed frame. Natural colour. The figure is seen from the waist up, "
          "standing among the planting."),
    side="right",
    source="profession: a preservationist replants a bleached reef one seed at a time, and opens each seed case "
           "in the bill",
    seen=("the face is scarlet with bare white cheek skin and a big curved bill, pale above and dark below",
          "the top of the head is a narwhal's smooth domed brow of mottled grey-and-white skin, and the neck is "
          "the same, speckled dark",
          "one long straight spiral-grooved ivory tusk juts forward and down from the left side of the upper jaw",
          "the jacket is faded green with a strap of seed pouches across the chest"),
    doing=("Tender has been asked to hold still in the middle of the job, and has, more or less. A seed case as "
           "hard as stone and the size of an apple is clamped in the big curved bill, half opened, its two halves "
           "parting along the seam and the pale seed showing. One green-gloved hand is cupped under it to catch "
           "the seed; the other holds a planting dibber. The head is cocked, and one steel-grey eye is turned on "
           "the photographer with the lid half down: wry and unimpressed. The face is turned three-quarters to "
           "the camera, both eyes open. The long ivory tusk points past the camera, and the grey fluke rises "
           "behind one shoulder."),
    place=("A reef that has bleached and is being planted again, at low water: a wide flat of bone-white lattice "
           "growths, dead and brittle, standing in ankle-deep clear water over pale sand, and between them row on "
           "row of new plantings, each a green shoot no taller than a hand, tied to a thin cane. Beside Tender "
           "floats a wooden tray of seed cases with a sack and a bundle of canes on it. Far off, the living edge "
           "of the old reef rises like a blue-green cliff of lattice, with mist on it. The flat is empty of other "
           "people."),
    light=("Flat bright overcast: soft, even and shadowless, the scarlet feathers and the bare white cheek both "
           "holding detail, the pale sand and the white lattice filling every shadow from below. A dull silver "
           "glare lies on the water."),
    wear=("The green jacket is faded almost grey on the shoulders and wet to the elbows; the seed-pouch strap is "
          "frayed, and one pouch hangs open and empty; the gardening gloves are black with mud in the seams; the "
          "canvas trousers are soaked to the knee; fine white grit sticks to the bill and to the first hand's "
          "length of the tusk."),
    close=(750, 10, 1350, 610),
    patch=dict(
        what=("the skin over the top and back of the head and the neck, the tusk and where it leaves the face, "
              "what the lower hand holds, and the fluke behind the shoulder"),
        bust="the tusk where it leaves the face",
        ask=("THE HEAD. Where smooth grey skin covers the top of the head and frames the face now, the head wears "
             "a scarlet macaw's feathers, the same scarlet as the face, over the whole crown. Down the back of the "
             "head the feathers thin, until single scarlet feathers lie on bare skin, and from there the back of "
             "the head and the whole neck are a narwhal's: smooth wet skin, pale grey, with soft dark blotches "
             "that run into one another along the back of the neck and fade to plain cream at the throat. The bare "
             "white skin of the macaw's cheek runs into that grey skin with no edge between them. Just behind the "
             "crown, where the feathers end, the skin has a small crescent blowhole, and a low soft puff of breath "
             "hangs over it."
             "\n\n"
             "THE TUSK. The tusk no longer comes from behind the bill. It is a tooth of the upper jaw, and it "
             "leaves the face where it does in picture 2: at the near corner of the mouth, just under the hinge of "
             "the bill, through a swollen ring of pink-grey gum. From its root it runs straight, forward and down, "
             "under the bill and the seed case the bill holds and in front of the collar of the green jacket, to a "
             "tip that is in the picture at the lower left: as long as the tusk is now, ivory, with one spiral "
             "groove, wet, and caked for its last hand's length with wet white sand that drips. Neither hand "
             "touches it. The bill and the seed case stay whole and in full view."
             "\n\n"
             "THE LOWER HAND. Where the lower hand holds a wooden tool now, it holds three green shoots by their "
             "stems, their pale roots hanging wet. The glove stays as it is."
             "\n\n"
             "THE TAIL. Where the grey fluke stands behind the shoulder now there is the flat, as it is beside it: "
             "still water, white growths, rows of shoots and the far blue-green edge. The tail hangs below the "
             "frame, and none of it is in this picture."),
        edit=(   # the whole frame: the head itself changes, so the figure is the painter's (refs.WHOLE)
            ((0, 0), (1536, 0), (1536, 864), (0, 864)),
        ),
    ),
)
