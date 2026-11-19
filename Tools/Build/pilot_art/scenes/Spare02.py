"""Lunari Hollowreach, Spare02: opossum and lynx, a hunter. The season is over and the storm is early: Lunari walks
the one-plank path home across the bog with a dozen carved decoys roped on the back, the way an opossum carries
young, and does not hurry."""
SCENE = dict(
    treatment="One frame lit by a lightning bolt, the shutter left open on a tripod in the dark",
    shot=("A night photograph made with the shutter left open on a tripod in the dark until one bolt of lightning "
          "lit the frame: a 40mm lens, everything caught hard and still in a single blue-white instant, the rain "
          "stopped in the air as short bright threads, deep black beyond. The figure is seen from the waist up, "
          "walking toward the camera along a plank path."),
    side="left",
    source="profession: a hunter carries the season's decoys home across the bog ahead of a storm, on the back",
    seen=("the face is white and pointed with a pink nose and long white whiskers, and the fur round the eyes is "
          "smoke grey",
          "the lynx ears are tall, tawny and tufted in black, and the pale beard-ruff flares wide from both sides "
          "of the jaw",
          "the jacket is russet leather with wide lapels, laced at the chest"),
    doing=("Lunari walks the plank path toward the camera with the back straight under a load: a dozen carved "
           "wooden decoys of long-necked marsh fowl, roped together and riding high on the back and shoulders the "
           "way an opossum carries young, their plain wooden heads and necks crowding up behind the tufted ears. "
           "One has slipped round and hangs at the chest. Both pink bare-fingered hands grip the rope where it "
           "passes over the chest. The chin is up, the hazel eyes level and the mouth shut and straight, rain "
           "streaming off the whiskers: proud and unbowed. The face is turned three-quarters to the camera, both "
           "eyes open."),
    place=("A black bog at night under a storm: a single line of grey planks on low posts running away behind "
           "Lunari to the horizon, with pools of still dark water on both sides, some of them glowing faintly "
           "green from below. Out of the bog stand low leafless trees shaped like fans of coral, and tall seed "
           "heads like lamps on stalks, bent by the wind. Far back along the path is the small square shape of a "
           "hide on stilts, shuttered. One great branching bolt stands in the sky to one side, top to bottom, and "
           "the cloud round it is lit from inside. The bog is empty of other people."),
    light=("The bolt is the only light: blue-white and hard from one side, it lights the white face plainly, edges "
           "the tufted ears, the wide ruff, the decoys' heads and every plank in white, throws long black shadows "
           "and stops the rain as bright threads. Its glare comes back off the wet planks and the pools. The glow "
           "in the pools is faint beside it."),
    wear=("The russet jacket is black with rain across the shoulders and rubbed pale where the ropes ride; one "
          "lace has come undone at the chest; the breeches are caked with peat to the knee and the tall boots to "
          "the ankle; the decoys' paint is worn to bare wood at the bills; a twist of marsh weed has caught in "
          "the rope."),
    close=(340, 0, 700, 360),
    patch=dict(   # Garrett 2026-11-18 on the review page: "what are these ducks? decoys? dead?"
        what="the birds roped on the back and the one at the chest, which become carved wooden decoys",
        ask=("THE DECOYS. Every bird {name} carries is a carved wooden decoy, plainly made by hand, and none is a "
             "real bird. Each is one solid piece of carved wood: a smooth simplified body with no separate "
             "feathers, the head and the long neck carved in one piece with it, the feather pattern only painted "
             "on in a few flat strokes of dull brown, grey and cream, worn away to bare pale wood at the bill, the "
             "crown of the head and every edge, with the grain of the wood showing through and fine cracks along "
             "it. The eyes are small painted dots. The heads are all held at the same stiff upright angle, the "
             "bills shut, because they are carved. A brass screw-eye is set in the breast of each, and the rope "
             "runs through the screw-eyes from one decoy to the next. They are roped in the same places as the "
             "birds now, as many and the same size, riding high on the back and shoulders behind the ears."
             "\n\n"
             "The one at the chest hangs upside down on its rope, so its underside faces the camera: a flat "
             "wooden base with a dark lead keel weight screwed along it. Rain beads on the painted wood and runs "
             "off it, and the lightning "
             "glints on the wet paint as it does now."),
        edit=(   # the whole frame: the decoys sit round the figure (refs.WHOLE)
            ((0, 0), (1536, 0), (1536, 864), (0, 864)),
        ),
    ),
)
