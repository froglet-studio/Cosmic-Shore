"""Ivelia Starreach, ZODIAC: ocelot and heron, a merchant. One of the few scenes that show the sea the pilots sail
(Garrett, 2026-11-11: "Make a few of them render the hypersea in their scene on either a panel or holographic
display"): a chart of light over a chart table, measured with a paper chart's brass dividers.

His notes on the second painting (2026-11-11, 23:01 UTC): "the dual perspectives of jugger is a better standard for
windows, which applies to zodiac as well. zodiacs hands are also an old man's which doesn't fit." So the windows no
longer hold a dusk over dark water: they hold the sea the chart shows from above, at its true size (the reef outside
is the chart's nearest coral-orange island), and the creased kid gloves are gone: the hands are the ocelot's own,
young, spotted and slim.

Three paintings. The first two called what is outside "a reef of coral-orange bricks of light with violet edges" and
got a reef: glowing rock, once as a spiral with dark boulders and once as a lava plain under a horizon. The third
says "a wall of bricks of light, each as big as a door, ... coral orange deepening to violet", the words that built
Trace's port, and got bricks, with the same rings of bricks drawn small on the chart (LESSONS 78: the painter paints
the noun, so a growth of bricks is called bricks, never a reef, a coral or an island).

Read blind on 2026-11-12, the avatar gave a stranger the ocelot and "some bird (owl or hawk)", with "cat ears, with a
feather-like crest between them"; the whole scene's reader saw a "bird of prey with grey-white and black feathers
(white-and-black feather crest on the head, folded grey wings on the back); exact bird unclear". Three paintings have
not been named a heron: a heron's tell is its bill, and the ocelot owns the face. The patch below designs Ivelia again
as one creature (Garrett, 2026-11-11: "we can do a lot better job of expressing the animals together"), by the one
shape of a heron the face leaves free: the long neck drawn back in its S, slate blue-grey with the streaked white
stripe, that holds the head still over the chart as a heron holds its head over water; the crown's black stripes leave
the head as two thin feathers; and the wings fold low, so that nothing stands over the shoulders like a hawk's. A
wading bird by its kind is the honest target. Not painted yet: this is one of the eight weakest reads, and painting
them waits for Garrett's go-ahead."""
SCENE = dict(
    treatment="Travel reportage on slow slide film, a chart house lit by the reef outside",
    shot=("A travel-reportage photograph on slow colour slide film: a 35mm lens nearly wide open. Saturated colour, "
          "deep indigo shadows that still hold detail. The figure is seen from the thighs up, leaning over a "
          "table."),
    side="right",
    chart=("hologram", "waters"),
    source="history: \"was apprenticed to a cartographer\" (the game's Upbringings)",
    # Garrett, 2026-11-11, on the first painting: "zodiac's feathers look too rendered". The first line is the answer:
    # what a living bird's feathers look like to a camera.
    seen=("the heron's feathers are a wild bird's, soft, matte and a little untidy: fine loose filaments at their "
          "edges, the grey neck feathers lying in overlapping rows, the two thin black plumes at the nape parted "
          "by the draught",
          "the cloak is deep indigo wool edged with dull gold, pinned at one shoulder; the tunic is cream silk "
          "striped pale gold; the tokens are wooden and brass beads on cords"),
    hands=("Both hands are the ocelot's own, young and strong: a person's hands in shape, four fingers and a thumb, "
           "in short golden fur with small black spots, slim-fingered, with dark pads at the tips. The wrists wear "
           "the same spotted fur."),
    doing=("Ivelia leans over the chart table on one braced arm and walks a pair of brass dividers down the "
           "chart's lime-yellow course line, point over point. The lips are parted and the ears pricked: bright, "
           "open curiosity. The face is turned three-quarters to the camera, both eyes on the dividers. The wings "
           "are folded high behind the shoulders."),
    place=("The chart house of a trade barge under way through deep space: a small deckhouse of oiled wood with "
           "windows on three sides. Over the sloping chart table floats {chart} The sheet lies level, a hand above "
           "the table and as wide as it. Outside all three windows is the place the chart shows from above: black "
           "space sown with fine white dust; close outside, a wall of bricks of light, each as big as a door, set "
           "edge to edge in rings round open holes, coral orange deepening to violet; and far off three vast "
           "hollow spheres, each a net of thin dim blue lines in six-sided panes, hanging like paper lanterns."),
    light=("The bricks outside light the room low through the windows, coral orange and violet. A brass oil lamp "
           "on a hook, turned low, warms the near side of the face and the white of the crown. The chart gives a "
           "thin cold tint to the chin, the dividers and the spotted fingers."),
    wear=("The cloak's hem is frayed and one corner singed; the gold edging is rubbed dull; pencil dust greys the "
          "fur of two fingers; two flight feathers are out of line."),
    close=(800, 0, 1320, 520),
    patch=dict(
        what=("the throat and the neck under the head, the stripes at the top of the head, and the wings"),
        ask=("THE NECK. Where the white furred throat is now, {name} has a heron's neck, drawn back in its S: it "
             "leaves the collar of the cloak, swings forward in one deep bend below the chin and comes up to the "
             "head behind the jaw, so the head is where it is now, held quite still over the chart. The neck is "
             "half as wide as the head and wears small flat feathers in close rows, slate blue-grey, with a narrow "
             "white stripe down its front that carries two rows of short black streaks. Under the jaw the ocelot's "
             "fur turns to feather over two finger-widths, and the fur's black spots draw out into the first "
             "streaks. From the base of the neck a fringe of long narrow pale grey feathers, each a hand long, "
             "hangs over the neckline of the cloak, and the chart lights them from below."
             "\n\n"
             "THE CROWN. The two black stripes on the top of the head run back past the ears and leave the head as "
             "two thin black feathers, as long as the head, lying along the back of the neck."
             "\n\n"
             "THE WINGS. Where the wings stand above the shoulders now there is the room: the window, its frame "
             "and the dark wood. The wings are folded low. Each grows from a shoulder blade and comes out through "
             "a slit in the back of the cloak, bound in the cloak's own dull gold edging, and lies closed against "
             "the back with its bend below the shoulder: blue-grey, with black flight feathers and a black patch "
             "at the bend."),
        edit=(   # the whole frame: the head itself changes, so the figure is the painter's (refs.WHOLE)
            ((0, 0), (1536, 0), (1536, 864), (0, 864)),
        ),
    ),
)
