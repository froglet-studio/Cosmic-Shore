"""Slingshot: flamingo and mantis shrimp, a racer. The evening before a winter heat, on one leg on a frozen slip:
one rap of an armoured hand on the rim of the intake and the whole crust of rime comes off into the air.

Read blind on 2026-11-12, the avatar gave a stranger the flamingo, "mahi-mahi (dolphinfish)" and "lobster or shrimp":
"a tropical fish's scaly skin and fin, with shrimp or lobster feelers", and "the fish fins behind the shoulder have no
clear attachment". The whole scene was named a mantis shrimp, with "the tail fan sits behind the left shoulder like a
wing, not at the base of the spine", a shell that "reads somewhat like a helmet" and "glossy plastic-looking hands and
shell". The bust had been named a mantis shrimp at full size. The patch below designs Slingshot again as one creature
(Garrett, 2026-11-11: "we can do a lot better job of expressing the animals together"): one smooth shield over the
crown and bands down the back of the neck, dull, pitted and frosted, the pink feathers lying over its first edge; a
paddle at each side of the brow, swung out toward the flying ice; hands in the same shell with four fingers and a
thumb; and the tail grown from the base of the spine in six bands that end in the tail plates, held out for balance.
Three critics then read the first draft of these words beside the picture (an art director's eye, a painter's, a
stranger's), and this is the second draft: the shell is one shield and then bands (plates lying over one another are
scales, the fish a stranger sees now), and four bands come round to the camera's side, since bands are the one pattern
a fish cannot have; the paddles are half as long as the bill and the near one stops behind the eye, so they are not
ears and do not cover the face; nothing is worn (a shell that is worn is a helmet, and hands that wear shell are
gloves); and the bust is not sent, since its gloss and its rainbow are the fish's colours and it shows the fan this
patch takes away. The same three then read the second draft against their first notes, and this is the third: the near
paddle grows where the red fin grows now and points the other way, toward the eye, and the fin's place is given back
to the shell and the sky (told both to swing forward and to stand where a fin that points back stands, the painter
would keep the fin, and the fin is the fish a stranger sees); the tail is held level with the hips, where the second
draft had it both level and just above the ground, and its plates are turned flat to the camera. The three then read
only the sentences that draft had rewritten, beside the picture once more: all three would send it as written. In a
painting, look first at the near paddle: orange-red on pink feathers behind the eye is low in contrast, and its stalk,
its darker edge and the ice breaking on it are what keep it from being a red cheek. A lobster or a shrimp, which
counts as its kind, named from the avatar: about one in two, and a mantis shrimp by name one in five (the critics'
estimate: nothing is painted or tested). Not painted yet: this is one of the eight weakest reads, and painting them
waits for Garrett's go-ahead."""
SCENE = dict(
    treatment="Afterglow on a frozen slip, a burst of rime in the air",
    shot=("A photograph made in the afterglow: a 28mm lens close in at a fast shutter, the flying ice "
          "caught in the air, the last low light of the sky the only light. Cold violet shade and warm rose rims. "
          "The figure is seen from the knees up, balanced on one leg."),
    side="right",
    source="profession: a racer clears the intake the evening before a winter heat, with one rap of the hand",
    seen=("the face is soft rose pink, and the bent bill is pink at the base and black at the tip",
          "the hoodie is pale and light, the sash broad and golden, and the goggles have amber rims"),
    doing=("Slingshot stands balanced on one leg on the frozen slip with the other knee drawn up, as a flamingo "
           "rests, and has just struck: one armoured red hand, its fingers folded, has rapped the rim of the "
           "craft's intake once, and the whole crust of rime is coming off it in a sheet and bursting into "
           "crystals that hang in the air. The other hand hangs loose. The long neck is drawn into a tight "
           "curve and the bent bill is clamped shut, the brows low: fierce and determined. The face is turned "
           "three-quarters to the camera, both eyes open. The fanned tail plates stand raised behind one "
           "shoulder."),
    place=("A pit slip on the shore of a frozen bay at dusk: a ramp of ribbed concrete glazed with ice, running "
           "down to black water, and on it a slim one-seat racing craft on its cradle, white with frost, its "
           "round intake rimmed with rime as thick as a wrist. Round it stand a heater cart with a dull red "
           "glow, hoses stiff with cold and a row of marker poles with plain frozen pennants. Far across the bay "
           "are the low lights of the other pits, under a sky of deep rose going to violet. The slip is empty "
           "of other people."),
    light=("The dusk alone: the last rose-gold light low from the left rims the pink feathers, the bill and the "
           "raised knee, and the violet sky fills the shade with a cool blue where every colour still reads; each "
           "flying crystal catches a rose or violet glint; the heater cart's glow lights only the ice at its feet."),
    wear=("The pale hoodie is spattered with meltwater and has frost in its folds; the satin sash has slipped "
          "toward the hip; the goggles on the chest are fogged; the leggings are scuffed at the padded knee; ice "
          "has built up on the boot soles; a few pink feathers at the neck are stiff with rime."),
    close=(610, 0, 1070, 460),
    patch=dict(   # Garrett 2026-11-18 on the review page: "i don't like the mix of the flash and the sunset lighting"
        what=("the light: the flash is gone, and the dusk alone lights the whole picture"),
        changes=("every light that falls on them",),
        ask=("THE LIGHT. The whole photograph is lit by the dusk alone: the last light of a sun "
             "already set behind the far shore, coming low from the left, soft and rose-gold, and the deep violet "
             "of the sky above it filling every shadow with a cool blue. That light falls on {name} from the side: "
             "it lays a warm rim along the pink feathers of the head and neck, the bill, the shoulder and the edge "
             "of the raised knee, and leaves the side toward the camera in soft cool shade where every colour still "
             "reads. Nothing is lit hard and white from the front, and no shadow falls hard behind the figure."
             "\n\n"
             "The flying crystals of rime are lit by the same sky: each catches a small rose or violet glint of it "
             "and they are a little soft with their own motion, a burst of ice in the air rather than white sparks. "
             "The frost on the craft is pale lilac in shadow and rose where the low light reaches it. The heater "
             "cart's dull red glow lights only the ice at its feet and the near edge of the slip. The far lights of "
             "the other pits are small points, not streaks. The sky, the bay and everything in the picture stay "
             "where they are now."),
        edit=(   # the whole frame: the light falls on everything (refs.WHOLE)
            ((0, 0), (1536, 0), (1536, 864), (0, 864)),
        ),
    ),
)

# The one-creature redesign written on 2026-11-12 (the docstring above), not painted yet: Garrett, 2026-11-12, held the
# eight redesigns for the next pass. Swap it back in as SCENE's patch= when that pass paints it.
REDESIGN = dict(
    what=("the shell over the head and the neck, the red fin at the temple, which becomes one of two paddles "
          "at the brow, the feelers, the two hands, and the fan behind the shoulder, which becomes a tail"),
    changes=("the hands",),
    ask=("THE HEAD AND NECK. {name} is feathered pink as far back as the eyes. Behind them, where the shining "
         "green dome is now, the back of the head and the back of the neck are a mantis shrimp's own shell, "
         "grown there: one smooth hard shield over the crown and broad bands behind it, a dull matte olive "
         "green, with dark spots ringed in white low on each side of the shield and a thin bright blue line "
         "along the edge of the shield and of every band. From the back of the crown down to the hood of the "
         "hoodie the shell is bands, four of them in view, each running from the back of the neck round the "
         "side that faces the camera as far forward as the corner of the jaw, each with a blue rear edge, and "
         "orange-red skin shows in the seam between one band and the next. The front of the neck is "
         "pink-feathered, as it is now. The last pink feathers lie over the front edge of the shield, single "
         "pink feathers grow from the seams between the first bands, and the shield's dark spots begin as dark "
         "flecks among the pink feathers of the brow. The shell is dull, finely pitted and scratched, and "
         "frost lies along every edge of it."
         "\n\n"
         "THE PADDLES. Low at each side of the brow, where the shield's front edge meets the pink feathers, on "
         "a short jointed stalk that is seen, is one flat oval paddle of smooth hard shell, half as long as "
         "the bill: orange-red with a darker edge and short red bristles along it. Both point forward, toward "
         "the flying ice, and a little outward. The far one stands clear against the sky in front of the brow. "
         "Where the red fin stands at the temple now, and where its blade lies back past the head, there is "
         "the olive shell of the head and the dusk sky behind it. The near paddle grows where that fin grows "
         "now and points the other way: toward the eye, along the side of the head and a little out toward the "
         "camera. Its tip stops level with the back corner of the eye, so the eye, the cheek and the whole "
         "bill are in full view in front of it, and ice is breaking on its outer face. From the brow between "
         "them two thin red feelers are thrown back over the head by the blow."
         "\n\n"
         "THE HANDS. Both hands are of the same shell, grown from their own skin: dull red, four fingers and a "
         "thumb, each finger in three short jointed pieces, pitted, scuffed pale at the knuckles and frosted. "
         "The hand at the intake is closed, its fingers folded, as it is now."
         "\n\n"
         "THE TAIL. Where the coloured fan stands behind the shoulder now there is the dusk sky, the far "
         "lights and the frozen field. The tail grows from the base of the spine. It comes into view from "
         "under the hoodie's hem at the back of the hips, on the side away from the frosted intake, as six "
         "broad bands of the olive shell, each narrower than the last. It passes behind the hanging hand and "
         "is held straight out behind the body, level with the hips, for balance, clear against the snow and "
         "the wet ground beyond it, where it ends in its tail plates: one ridged middle plate and two flat "
         "blades at each side of it, deep blue edged with red, turned flat to the camera."),
    edit=(   # the whole frame: the head itself changes, so the figure is the painter's (refs.WHOLE)
        ((0, 0), (1536, 0), (1536, 864), (0, 864)),
    ),
)
