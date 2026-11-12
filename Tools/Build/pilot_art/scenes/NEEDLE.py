"""Ilia Silvergate, NEEDLE: narwhal and seahorse, an aristocrat. One of the few scenes that show the sea the pilots
sail (Garrett, 2026-11-11: "Make a few of them render the hypersea in their scene on either a panel or holographic
display"): the wall chart of a harbour's gate room, watched by the house that keeps the gate.

Read blind on 2026-11-12, the avatar gave a stranger the narwhal and "lionfish (some spiny-finned fish)": "the frill
reads as fish fin spines or a dragon/frilled-lizard crest", and at icon size both readers said "bird". The whole scene
was named a seahorse by the tail at the hip, which the avatar does not hold, and of the tusk a reader said it "comes
straight out of the tip of the snout like a beak or nose spike, not from the upper jaw to one side". The bust had been
named a seahorse: its neck of bony rings is bare above a low lace collar and its tail curls beside the head. The patch
below designs Ilia again as one creature (Garrett, 2026-11-11: "we can do a lot better job of expressing the animals
together"): the head ends blunt and the tusk is a tooth through the lip; the points over the head give way to a
narwhal's smooth skin; the bust's ringed neck shows above the lace; and the tail leaves the handrail, comes round the
hip and up to the collar, and carries the watch's lantern in its curled end, which puts the seahorse's own grip and
its rings beside the face. Not painted yet: this is one of the eight weakest reads, and painting them waits for
Garrett's go-ahead."""
SCENE = dict(
    treatment="Architectural interior with a shift lens, verticals true",
    shot=("An architectural interior photograph made on a tripod with a shift lens: every vertical true, the room "
          "square to the frame, a long exposure at a small aperture so the room is sharp from the floor plates to "
          "the window bars, the one figure in it standing still. Cool, exact and unhurried, the colour muted. The "
          "figure is seen from the knees up, standing."),
    side="left",
    chart=("panel", "waters"),
    source="profession: an aristocrat whose house keeps a harbour's sea gate stands the last watch of the night",
    swap=(("curled in a spiral over one shoulder", "as long as a leg"),),   # the bust's pose; the scene sets its own
    seen=("the skin of the head is pale grey mottled with darker grey and black flecks, darkest over the brow and "
          "palest at the throat",
          "the tusk is pale ivory and as long as the head, no longer",
          "the coronet is a row of coral-orange spikes that runs from the brow over the top of the head and down "
          "the back of the neck like a crest",
          "the jacket has a standing collar, leaf embroidery in dark teal thread down its front and a froth of "
          "white lace at the throat; the chain of office is silver links set with oval sea-green stones"),
    doing=("Ilia stands the last watch of the night at the gate. One gloved hand rests on the long brass lever of "
           "the gate winch and has not moved it; the other is held behind the back. The seahorse tail has come "
           "round from behind and taken two turns round the brass handrail at the hip, the way a seahorse anchors "
           "itself in a current. The great head is turned three-quarters to the camera and tipped toward the "
           "chart on the wall, the near eye fixed on the last ring of its course line: stern, watchful, waiting "
           "for someone who is late."),
    place=("The gate room above a harbour's sea gate, before dawn: a tall narrow room of pale stone and dark iron. "
           "Beside Ilia stands the gate winch, a brass drum and lever worn bright, its chains running down through "
           "slots in the floor plates. On the wall beyond hangs {chart} The panel is as tall as Ilia and twice as "
           "wide. To its side, one tall barred window looks down the harbour channel into blue-green haze; under "
           "the window, a bench, a folded blanket and a cold cup. The room is empty of people."),
    light=("The window is the main light, a cold grey-green before dawn that lies along the tusk, the brow and the "
           "spikes of the coronet. A caged work lamp over the winch makes a small warm pool on the lever and the "
           "glove. The chart is dimmer than either and puts only a faint blue on the near velvet sleeve."),
    wear=("The velvet is rubbed bare at the elbows and the right cuff shines with winch grease; the lace has gone "
          "limp in the damp; the gloves are dark at the palms; dried salt lies in the grooves of the tusk."),
    close=(320, 0, 775, 455),
    patch=dict(
        what=("the front of the head and where the tusk leaves it, the top and back of the head, the neck above "
              "the lace, and the tail, which leaves the handrail and carries a small lit lantern"),
        bust="the blunt round front of the head and the neck of bony rings",
        changes=("the face", "every light that falls on them"),
        ask=("THE FRONT OF THE HEAD. Where the head narrows to a point now, it ends in a blunt round brow, as it "
             "does in picture 2: a narwhal's high bulging forehead, and under it a short straight mouth with a "
             "soft upper lip. The tusk is a tooth. It comes out through the upper lip a little to one side of the "
             "middle, under the bulge of the brow, the lip drawn into a small pucker round its root, and it points "
             "where it points now and is as long as it is now. The eye and the mottled grey skin with its dark "
             "flecks stay as they are."
             "\n\n"
             "THE TOP OF THE HEAD. Where the row of orange points stands now, from the brow over the top of the "
             "head and down its back, the head is bare and smooth: the same mottled grey skin, with the cold light "
             "of the window lying along it."
             "\n\n"
             "THE NECK. The lace sits low, at the collarbone, and above it the neck is bare: the neck {name} has "
             "in picture 2. It is a seahorse's, a column of bony rings under tight skin: each ring a finger tall, "
             "with a raised ridge down each of its corners and a small blunt knob where the ridges of two rings "
             "meet, amber orange with pale gold ridges. Where the head meets the neck the grey skin warms into the "
             "orange over a hand's width, and the head's dark flecks carry on over the first three rings."
             "\n\n"
             "THE TAIL. The tail has let go of the handrail, which stands bare brass. It grows from the base of "
             "the spine, comes out through the vent at the back of the coat and round the hip on the window's "
             "side, and rises in front of the body, close beside the coat's front edge, to the height of the "
             "collar. It is a seahorse's tail: square in section and ringed along its whole length like the neck, "
             "in the same amber orange and gold, as thick as a wrist where it leaves the coat and as thin as a "
             "finger at its end. Its end is wound twice through the ring on top of a small brass lantern and "
             "curled tight on itself, and the lit lantern hangs from it below the line of the tusk. The flame "
             "warms the underside of the tusk, the lip, the jaw and the rings of the neck, and glows through the "
             "thin skin between the tail's ridges. The gloved hand on the lever, the coat and the chain stay as "
             "they are."),
        edit=(   # the whole frame: the head itself changes, so the figure is the painter's (refs.WHOLE)
            ((0, 0), (1536, 0), (1536, 864), (0, 864)),
        ),
    ),
)
