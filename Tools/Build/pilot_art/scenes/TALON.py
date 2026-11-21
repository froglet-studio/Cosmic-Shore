"""Wyvendo Waveline, TALON: lynx and jackal, a hunter. A tracker in a brickyard at sunrise, holding up the brick
that proves something enormous strolled through town in the night, and delighted about it.

Read blind on 2026-11-12, the avatar gave a stranger the lynx and "squirrel": "tail is studded with odd burr-like
specks and is not clearly attached", and in the whole scene "it looks like it stands behind the shoulder rather than
grows from the body". No black-and-silver saddle shows anywhere in the picture, so nothing of the jackal is in it but
a tail. The patch below designs Wyvendo again as one creature (Garrett, 2026-11-11: "we can do a lot better job of
expressing the animals together"): the tail comes round the hip, along the thigh and up between the arms to the brick,
where its black end has just brushed the grit from the edge, and the dark grizzled fur of a jackal's back runs from
between the ears down into the collar and along the tail. Three critics then read the first draft of these words
beside the picture (an art director's eye, a painter's, a stranger's), and this is the second draft: the tail passes
under the forearm and rises between the arms (over the forearm it ran on over a glove, and a hidden hand comes back as
a third hand); its black end lies at the brick's corner beside the chin, inside the avatar, with the grit it has
pushed up, since a stroke that is over cannot be painted; the dark fur no longer lies over the shoulders (fur over a
collar is a fur collar) and is one grey with no white in it (a white band on a head is a skunk or a badger). The same
three then read the second draft against their first notes, and this is the third: the sky where the tail stands now
is said as it is, pale gold (the second draft called a sunrise evening, and an evening can be painted). Two furred
animals merge and a jackal is a fox to nearly everyone: a fox, a wolf or a dog, which counts as its kind, named from
the avatar is four or five in ten, and a jackal by name one in twenty (the critics' estimate: nothing is painted or
tested). Not painted yet: this is one of the eight weakest reads, and painting them waits for Garrett's go-ahead."""
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
        what=("the fur over the top of the head between the ears and down the back of the neck, and the tail, "
              "which leaves its place behind the shoulder and comes round the body to the brick"),
        ask=("THE FUR BEHIND THE FACE. Behind the face {name} is a black-backed jackal. From between the ears back "
             "over the top of the head and down the back of the neck the fur is dense, short on the head and "
             "longer down the neck: black evenly mixed with fine pale grey hairs, so that all of it is one dark "
             "grizzled grey. It goes down inside the collar of the shirt, which lies over it. Where it meets the "
             "pale cheek ruff at each side, the dark hairs and the pale ones lie mixed together for a short way; "
             "the ruff and the chest stay as they are."
             "\n\n"
             "THE TAIL. Where the tail stands behind the shoulder now there is what lies behind it: the pale gold "
             "sky, the far trees and the stacks of brick. The tail grows from the base of the spine and comes "
             "forward round the hip on the side where it stands now. It lies along the top of the thigh on that "
             "side, passes under the forearm that rests over that thigh and rises in front of the chest between "
             "the two arms, in one long easy bend: as long as his leg, thick and full to its blunt end, smooth, "
             "plain rust red, with the dark grizzled grey of the neck along its upper side and a black end a "
             "quarter of its length. That black end lies along the brick's top edge at the corner nearest his "
             "chin, beside his cheek, its long hairs spread on the clay. A small pale ridge of grit is pushed up "
             "along that edge where it has just passed, the black hairs are grey with clay dust, and dust still "
             "hangs round it in the low light. Both gloved hands stay on the brick as they are, and the whole "
             "print is in clear view."),
        edit=(   # the whole frame: the head itself changes, so the figure is the painter's (refs.WHOLE)
            ((0, 0), (1536, 0), (1536, 864), (0, 864)),
        ),
    ),
)
