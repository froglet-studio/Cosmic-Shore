"""Wyvendo Waveline, TALON: lynx and jackal, a hunter. A tracker in a brickyard at sunrise, holding up the brick
that proves something enormous strolled through town in the night, and delighted about it.

Read blind on 2026-11-12, the avatar gave a stranger the lynx and "squirrel": "tail is studded with odd burr-like
specks and is not clearly attached", and in the whole scene "it looks like it stands behind the shoulder rather than
grows from the body". No black-and-silver saddle shows anywhere in the picture, so nothing of the jackal is in it but
a tail. The patch below designs Wyvendo again as one creature (Garrett, 2026-11-11: "we can do a lot better job of
expressing the animals together"): the black-backed jackal's saddle lies over the top of the head, the neck and the
shoulders, sharp against rust red, and stands a little with his delight; the tail comes round the hip and up to the
brick, where its black end has just swept the grit out of the print. Two furred animals merge and a jackal is a fox to
nearly everyone, so the honest gain here is a fox in place of a squirrel. Not painted yet: this is one of the eight
weakest reads, and painting them waits for Garrett's go-ahead."""
SCENE = dict(
    treatment="Raking-light documentary at sunrise, eye to eye",
    shot=("A documentary photograph in raking sunrise light: a 35mm lens at the subject's eye level, low sun "
          "skimming the ground so every mark stands up in relief, long shadows, clear warm colour. The subject "
          "has found something and is showing it to the camera. The whole crouching figure is in the frame and "
          "fills most of its height."),
    side="left",
    source="profession: a tracker finds, in a brickyard at sunrise, the print of what walked through town in the "
           "night",
    seen=("the cheek ruffs are pale and wide, and the black ear tufts are as long as a finger",
          "the saddle is black grizzled with silver from the crown down the back of the neck, and the fur at the "
          "sides of the neck is rusty orange",
          "the fang at the throat is as long as a thumb, on a leather cord"),
    doing=("Wyvendo crouches between two rows of drying bricks and holds one up to the camera in both gloved "
           "hands, tilted to catch the low sun: pressed deep into the clay is a single paw print as wide as a "
           "dinner plate, four toes and a pad, the claw marks as long as fingers. Wyvendo looks straight into the "
           "lens over the top of the brick, eyes wide, mouth open on the edge of laughing, delighted. The bushy "
           "tail stands straight up behind the shoulder, bristling."),
    place=("A brickyard at the edge of a town at sunrise: hundreds of pale wet bricks laid out to dry in long "
           "straight rows on raked sand, running away to a kiln with a cold chimney and a stack of fired red "
           "brick. A line of the same great prints runs across the rows, one brick stamped in every row, and away "
           "under the fence. Beyond the fence, roofs, and above the roofs a pale green sky that curves like the "
           "inside of a bowl."),
    light=("The sun is a hand above the roofs and comes flat along the ground from one side: each row of bricks "
           "throws a long shadow, the print is carved out in light and shade, and the face is lit warm from the "
           "side, with a rim of gold on the ear tufts and the ruff."),
    wear=("Wet clay is smeared to the elbows of the cream shirt and dries pale on the knees of the breeches; the "
          "vest is buttoned one button out; there is a thumb mark of clay on the muzzle; burrs are caught in the "
          "tail."),
    close=(245, 0, 735, 490),
    patch=dict(
        what=("the fur over the top and back of the head, the neck and the shoulders, and the tail, which leaves "
              "its place behind the shoulder and comes round the body to the brick"),
        ask=("THE COAT BEHIND THE FACE. Behind the face {name} is a black-backed jackal. From between the ears a "
             "broad band of long coarse hair, black thickly mixed with silver-white, covers the top of the head "
             "behind the brow, the back of the head, the back of the neck and the tops of both shoulders, where it "
             "lies over the collar of the shirt. It stands up a little with his delight, and the low sun rims it "
             "with gold. The black backs of the ears run into it. Along each side of the neck it ends in one sharp "
             "line, and below that line the fur is plain rust red."
             "\n\n"
             "THE TAIL. Where the tail stands behind the shoulder now there is the sky and the far stacks of "
             "brick. The tail grows from the base of the spine and comes forward round the hip, along the thigh "
             "and up over the forearm: as long as his leg, full but straight, rust red, with the black-and-silver "
             "hair along its upper side and a black end as long as a hand. That black end lies against the top "
             "corner of the brick: he has just swept the grit out of the print with it. Dust still hangs round it "
             "in the low light, and the burrs are still caught in it. Both gloved hands stay on the brick as they "
             "are."),
        edit=(   # the whole frame: the head itself changes, so the figure is the painter's (refs.WHOLE)
            ((0, 0), (1536, 0), (1536, 864), (0, 864)),
        ),
    ),
)
