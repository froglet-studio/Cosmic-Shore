"""Jorin Gullwing, WINGMAN: puffin and pelican, a pirate. What he wants most is a crew that stays: he pegs out
their washing on the ledge, the spare pegs held in his bill the way a puffin holds fish.

Read blind on 2026-11-12, the avatar gave a stranger the puffin and "some white bird (gull or swan)": the plumes read
as "windblown bird plumage, or straw-blond hair", the throat as bare skin, "possibly human-like", the wing as hung
"from a strap or harness at the shoulder" and the row of pegs as "a posed gag". The bust had been named a pelican by
its pouch. The patch below designs him again as one creature (Garrett, 2026-11-11: "we can do a lot better job of
expressing the animals together"): the bust's pouch grows from the bill and is where he keeps the pegs, the
washing-day light coming through it; one peg stays in the tip of the bill, the puffin's way of carrying beside the
pelican's; the plumes become a pelican's short nape; the wing grows through the coat. Not painted yet: this is one of
the eight weakest reads, and painting them waits for Garrett's go-ahead."""
SCENE = dict(
    treatment="Hard sea light on a cliff ledge, from a burrow's doorway",
    shot=("A photograph taken from just inside a burrow's doorway on a cliff ledge, a little of the dark door "
          "post at one edge of the frame: a 28mm lens, clean hard sea light, strong colour, everything sharp, the "
          "wind in everything. The subject is busy a few paces away and gives the camera one sidelong look. The "
          "figure is seen from the knees up."),
    side="left",
    source="want: \"a crew that stays\" (the game's Wants)",
    seen=("the sea coat is faded navy, patched at both elbows in a lighter blue, and the scarf is striped",
          "the pin on the chest is a small pair of plain brass wings"),
    doing=("Jorin is pegging out the crew's washing on a line strung along the cliff ledge, and holds the spare "
           "pegs the way a puffin holds fish: six wooden pegs clamped sideways in the big striped bill, all in a "
           "row. Both gloved hands are up at the line, pinning a wet shirt far too large to be Jorin's own. A "
           "dozen garments of a dozen sizes already hang along the line, no two alike. Jorin gives the camera one "
           "sidelong look over the pegs, wry and unimpressed. The wings are folded tight, and the face is turned "
           "three-quarters to the camera."),
    place=("A ledge halfway up a sea cliff riddled with burrows: round doorways cut into the turf and rock, each "
           "with a plank door, a step and a lamp hook; a washing line strung on driftwood posts along the lip of "
           "the ledge, a tin tub of grey suds, a basket. Far below the ledge, pale water and a few moored craft "
           "small as seeds; beyond, haze, and tall stacks of rock standing in it. The ledge is empty of people."),
    light=("Clear sea light from high and behind the camera: the wet washing is bright where the sun comes "
           "through it, and the white of the face and the wings is brighter still."),
    wear=("The sea coat's cuffs are dark with suds and one elbow patch has come unstitched at a corner; the "
          "scarf is tucked into the coat to keep it out of the tub; soap has dried white on the gloves; the "
          "trousers are splashed to the knee."),
    close=(250, 0, 710, 460),
    patch=dict(
        what=("the throat under the bill and what the bill holds, the plumes at the back of the head, and the wing "
              "behind the near shoulder with the strap at its root"),
        bust="the pouch that hangs under the bill",
        ask=("THE POUCH. Where the smooth orange throat and the row of wooden pegs are now, under the bill, hangs "
             "a pelican's throat pouch: the pouch {name} has in picture 2. It is one deep fold of thin bare skin. "
             "It grows from the two edges of the bill's lower half, from the tip back to the corners of the mouth, "
             "and from the throat below them down to the scarf, and it is one piece with the bill: the yellow skin "
             "at the corner of the mouth runs into it with no line between them. It is full and it sags, as deep "
             "as the head is tall and widest at the bottom: pale apricot pink, yellower where it meets the bill, "
             "with fine pleats down its length and thin red veins. The hard sea light comes through the thin skin "
             "as it comes through the wet shirt on the line, and the wooden pegs it holds show through it, a dozen "
             "small dark bars lying every way in the bottom, their weight pulling it down. The white feathers of "
             "the cheek thin to wisps along its upper edge."
             "\n\n"
             "THE BILL. The bill is shut on one wooden peg, held sideways in its tip. The bill's stripes and the "
             "eye stay as they are."
             "\n\n"
             "THE BACK OF THE HEAD. Where the long pale plumes sweep back from the head now, the head is a "
             "pelican's: the black cap runs to the back of the head, and below it the nape wears short white "
             "feathers with a pale yellow wash, lying close, and one shaggy tuft of them a thumb long."
             "\n\n"
             "THE WING. The wing behind the near shoulder is {name}'s own: it grows from the shoulder blade and "
             "comes out through a slit in the back of the coat, a slit bound round its edge with the coat's own "
             "navy cloth, and the small white feathers at its root lie over that binding. Where the brown strap "
             "lies over its root now there is the plain patched cloth of the coat. Behind the far shoulder the "
             "bend of the second wing shows, folded the same way."),
        edit=(   # the whole frame: the head itself changes, so the figure is the painter's (refs.WHOLE)
            ((0, 0), (1536, 0), (1536, 864), (0, 864)),
        ),
    ),
)
