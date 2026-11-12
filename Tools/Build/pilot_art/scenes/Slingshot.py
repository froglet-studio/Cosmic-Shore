"""Slingshot: flamingo and mantis shrimp, a racer. The evening before a winter heat, on one leg on a frozen slip:
one rap of an armoured hand on the rim of the intake and the whole crust of rime comes off in the flash.

Read blind on 2026-11-12, the avatar gave a stranger the flamingo, "mahi-mahi (dolphinfish)" and "lobster or shrimp":
"a tropical fish's scaly skin and fin, with shrimp or lobster feelers", and "the fish fins behind the shoulder have no
clear attachment". The whole scene was named a mantis shrimp, with "the tail fan sits behind the left shoulder like a
wing, not at the base of the spine", a shell that "reads somewhat like a helmet" and "glossy plastic-looking hands and
shell". The bust had been named a mantis shrimp at full size. The patch below designs Slingshot again as one creature
(Garrett, 2026-11-11: "we can do a lot better job of expressing the animals together"): the bust's shell in
overlapping plates and bands, dull, pitted and frosted, the pink feathers lying over its first edge; a paddle at each
temple, swung forward as guards against the ice; hands in the same shell with four fingers and a thumb; and the tail
grown from the base of the spine in six bands that end in the tail plates, held out low behind the standing leg. Not
painted yet: this is one of the eight weakest reads, and painting them waits for Garrett's go-ahead."""
SCENE = dict(
    treatment="Rear-curtain flash at dusk with the shutter dragged, ice in the air",
    shot=("A photograph made at dusk with the shutter dragged and a flash fired at its end: a 28mm lens close in, "
          "the last daylight smeared a little into streaks, the subject and the flying ice frozen hard and bright "
          "by the flash. Cold blue and warm pink. The figure is seen from the knees up, balanced on one leg."),
    side="right",
    source="profession: a racer clears the intake the evening before a winter heat, with one rap of the hand",
    seen=("the face is soft rose pink, and the bent bill is pink at the base and black at the tip",
          "the hoodie is pale and light, the sash broad and golden, and the goggles have amber rims"),
    doing=("Slingshot stands balanced on one leg on the frozen slip with the other knee drawn up, as a flamingo "
           "rests, and has just struck: one armoured red hand, its fingers folded, has rapped the rim of the "
           "craft's intake once, and the whole crust of rime is coming off it in a sheet and bursting into "
           "crystals that hang in the flash. The other hand hangs loose. The long neck is drawn into a tight "
           "curve and the bent bill is clamped shut, the brows low: fierce and determined. The face is turned "
           "three-quarters to the camera, both eyes open. The fanned tail plates stand raised behind one "
           "shoulder."),
    place=("A pit slip on the shore of a frozen bay at dusk: a ramp of ribbed concrete glazed with ice, running "
           "down to black water, and on it a slim one-seat racing craft on its cradle, white with frost, its "
           "round intake rimmed with rime as thick as a wrist. Round it stand a heater cart with a dull red "
           "glow, hoses stiff with cold and a row of marker poles with plain frozen pennants. Far across the bay "
           "are the low lights of the other pits, under a sky of deep rose going to violet. The slip is empty "
           "of other people."),
    light=("The flash freezes what is near: hard and white on the pink feathers, the spotted green carapace and "
           "every flying crystal, each one a spark. Behind it the dusk has burned in: the rose sky, the red glow "
           "of the heater cart and the far lights are drawn out into short soft streaks."),
    wear=("The pale hoodie is spattered with meltwater and has frost in its folds; the satin sash has slipped "
          "toward the hip; the goggles on the chest are fogged; the leggings are scuffed at the padded knee; ice "
          "has built up on the boot soles; a few pink feathers at the neck are stiff with rime."),
    close=(610, 0, 1070, 460),
    patch=dict(
        what=("the shell over the head and the neck, the paddles and feelers at the temples, the two hands, and "
              "the fan behind the shoulder, which becomes a tail"),
        bust="the spotted shell over the crown, with its two paddles and the banded neck below it",
        changes=("the hands",),
        ask=("THE HEAD AND NECK. {name} is feathered pink as far back as the eyes. Behind them, where the shining "
             "green dome is now, the head and the neck wear a mantis shrimp's shell, the shell of picture 2: hard "
             "plates, each lying over the edge of the next, a dull matte olive green, with dark spots ringed in "
             "white low on each side of the head and a thin bright blue line along the edge of every plate. The "
             "last pink feathers lie over the front edge of the first plate. Down the back of the neck the plates "
             "are bands, each two fingers tall, each with a blue rear edge, and orange-red skin shows in the seam "
             "between one band and the next. The shell is dull, finely pitted and scratched, and frost lies along "
             "every edge of it."
             "\n\n"
             "THE PADDLES. At each temple, on a short jointed stalk, is one flat oval paddle as long as the bill: "
             "orange-red with a darker edge and a fringe of short red bristles. Both are swung forward beside the "
             "eyes, guarding them from the flying ice, and ice is breaking on the near one. From the brow between "
             "them two thin red feelers are thrown back over the head by the blow."
             "\n\n"
             "THE HANDS. Both hands wear the same shell in red: four fingers and a thumb, each finger in three "
             "short jointed pieces of dull red shell, pitted, scuffed pale at the knuckles and frosted. The hand "
             "at the intake is closed, its fingers folded, as it is now."
             "\n\n"
             "THE TAIL. Where the coloured fan stands behind the shoulder now there is the dusk sky and the far "
             "lights. The tail grows from the base of the spine: it comes out under the back hem of the hoodie as "
             "six broad bands of the olive shell, each narrower than the last, and is held out low behind the "
             "standing leg, where it ends in its tail plates: one ridged middle plate and two flat blades at each "
             "side of it, deep blue edged with red."),
        edit=(   # the whole frame: the head itself changes, so the figure is the painter's (refs.WHOLE)
            ((0, 0), (1536, 0), (1536, 864), (0, 864)),
        ),
    ),
)
