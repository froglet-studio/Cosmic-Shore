"""Shrapnel: toucan and newt, an activist. After a night of holding a street behind a barricade of fruit crates,
breakfast is the barricade: a glass-skinned fruit flipped up off one thumb, about to be caught in the bill.

Read blind on 2026-11-12, the avatar gave a stranger the toucan and "lizard (spiny, orange-and-black spotted, e.g.
gila monster or newt-like)", with "reptile spines or a dragon-like crest"; in the whole scene the "spiky crest over
the head looks like a hood or hat worn on top", the braid "hangs from under the crest, no clear origin" and the tail
"curls up behind with unclear attachment". The patch below designs Shrapnel again as one creature (Garrett,
2026-11-11: "we can do a lot better job of expressing the animals together"), by what a newt has and a lizard has not:
smooth moist skin, a crest that is one thin soft sheet the low sun shines through and that has flopped over with the
catch, round soft fingertips, and a flat swimming tail with a wavy border of thin skin, seen from the poncho's hem to
its tip. The braid gets a root. Not painted yet: this is one of the eight weakest reads, and painting them waits for
Garrett's go-ahead."""
SCENE = dict(
    treatment="Frozen at 1/4000 s like a sports frame, a fruit in the air",
    shot=("A photograph frozen at one four-thousandth of a second, like a sports frame: an 85mm lens at chest "
          "height, low morning sun, every drop and crumb stopped in the air. Bright clean colour. The figure is "
          "seen from the thighs up, sitting on top of a barricade of crates."),
    side="right",
    source="profession: an activist holds a street overnight, and breakfast is the barricade",
    seen=("the bill is huge and orange with a black tip, the throat white, and the eye is ringed in blue",
          "a tall ragged newt crest stands along the midline from the brow down the nape, dark olive spotted "
          "with black, and the neck below the white is fire orange blotched with black",
          "the silver-white braid is long and falls over one shoulder",
          "the poncho is striped and zigzagged in pink, blue, yellow and orange"),
    doing=("Shrapnel sits on top of the barricade, legs dangling, and has just flipped "
           "a fruit up off one thumb to catch in the bill. The fruit hangs in the air a hand above the open bill, "
           "its skin as clear as glass with the seeds showing through, drops flying off it. The head is tipped "
           "back a little, the eyes on the fruit and creased at the corners, the huge bill wide open: delighted, "
           "on the edge of laughing. The other small hand holds a second fruit, half peeled. The face is turned "
           "three-quarters to the camera, both eyes open. The newt tail is curled over one shoulder, and the "
           "braid has swung out with the toss."),
    place=("A narrow street of tall shuttered houses, held overnight: across it stands a barricade of fruit "
           "crates, two barrows and the poles of a market awning. The crates are full of fruit with skin like "
           "clear glass, orange, green and violet, and a few have burst on the cobbles. Along the top lie "
           "blankets, a kettle on a brazier gone to ash and the megaphone standing on its bell; plain cloth "
           "banners, yellow and blue with nothing on them, hang from the poles. The street is empty of other "
           "people."),
    light=("Low morning sun straight down the street: warm and hard, lighting the orange bill from the side until "
           "it glows, shining through the fruit in the air and through every flying drop, with long shadows "
           "across the cobbles."),
    wear=("The striped poncho is damp with dew and stained with juice down the front; the megaphone strap has "
          "twisted; the trousers are grey with brazier ash at the knees; both odd laces of the old boots are "
          "undone; there is a smear of soot on the white throat and a seed stuck to the bill."),
    close=(740, 0, 1180, 440),
    patch=dict(
        what=("the skin behind the face, the row of dark points over the head, the two hands, the tail, and where "
              "the braid begins"),
        changes=("the hands",),
        ask=("THE SKIN. Behind the face {name} is a newt. Everything there that is beaded, hard or pointed now is "
             "smooth, moist, bare skin with a fine grain: near black with an olive sheen and a dusting of tiny "
             "white dots, and on the throat, the underside of the neck and the insides of the fingers a fire "
             "orange with large black blotches. The black feathers at the edge of the face thin out toward the "
             "back of the head, and the skin begins among them with no line."
             "\n\n"
             "THE CREST. Where the row of hard dark points stands over the head now there is a newt's crest: one "
             "thin soft sheet of that same skin, standing on its edge along the midline of the head. It begins "
             "just behind the eyes, is two fingers tall over the crown and a hand tall at the nape, and its upper "
             "edge is wavy and uneven. It is as thin as a leaf: the low sun behind it shines through it brown and "
             "amber, darker at its foot. With the head thrown back for the catch it has flopped over to one side, "
             "and bright drops fly from its edge."
             "\n\n"
             "THE HANDS. Both hands are a newt's in a person's shape: small, damp and soft, four fingers and a "
             "thumb with round soft fingertips, near black on the backs and orange blotched with black inside. "
             "They do what they do now: one is up from the toss, and the other holds the second fruit."
             "\n\n"
             "THE TAIL. Where the big curled tail stands behind the shoulder now there is the street: shuttered "
             "houses in the morning light. The tail is a newt's swimming tail. It grows from the base of the spine "
             "and comes out under the back hem of the poncho, lies along the blanket on top of the crates beside "
             "the hip, and lifts its last third beside the shoulder, flat side to the camera. It is tall and flat "
             "from side to side, smooth and wet, near black with a pearl-white stripe along its middle, and along "
             "its upper and lower edges runs a soft wavy border of thin skin that the low sun shines through, "
             "amber."
             "\n\n"
             "THE BRAID. The silver-white braid begins in a narrow strip of silver-white hair low on the nape, "
             "beside the foot of the crest, and hangs over the shoulder as it does now."),
        edit=(   # the whole frame: the head itself changes, so the figure is the painter's (refs.WHOLE)
            ((0, 0), (1536, 0), (1536, 864), (0, 864)),
        ),
    ),
)
