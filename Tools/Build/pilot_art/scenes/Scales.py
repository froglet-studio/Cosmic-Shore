"""Scales: egret and koi, a diplomat. One of the few scenes that show live play in a hologram with depth (Garrett,
2026-11-11: "I want to see some where the pilot's are on the bridge of the ark or in a vessel. and they are looking
at a 3D holographic display ... that 3d rendering should look like gameplay footage"): on the bridge, the morning a
treaty takes effect, watching two sides that used to fight fly their first patrol with their trails braided.

His note on it (2026-11-11, 23:01 UTC): "scales is great if we could get the hypersea better represented, but the
gyroid flora was better than other aspects. we will want more where they have the hypersea outside, but some like
this could tell a different story. that should be the except though." So the scene, the clouds in its windows and
the figure stay, and only the display is painted again (`patch`), from the real frames: the first painting filled
the dome with sea urchins, strings of beads and craft like fish, none of which the game has.

Painted 2026-11-12 in one run, by the painter that takes pictures (four frames of the game). The room came back
nearly to the pixel (its answer moved 6.09 of 255 outside the region) and only the dome was taken from it: the far
net of six-sided panes, a coral of bricks holding a twelve-sided crystal, a ring of shards, and six craft laying
ribbons of bricks, three jade teal and three ruby magenta, with one of each side by side on the near side."""
SCENE = dict(
    treatment="Formal state-occasion photograph down the length of a bridge, soft daylight",
    shot=("A formal photograph of a state occasion, made after the room has emptied: an 85mm lens down the length "
          "of a ship's bridge, soft daylight from a band of windows, exact and composed, quiet colour. The display "
          "is as sharp as the face. The figure is seen from the knees up, standing."),
    side="left",
    chart=("volume", "cell"),
    source="profession: a diplomat watches the first joint patrol fly under the treaty just signed",
    seen=("the plumes of the face are fine and white and the bill long and yellow; the koi fins sweep back from the "
          "temples, translucent white splashed with orange, and two barbels hang at the corners of the mouth; the "
          "hair is dark, short and swept back; the seal is a plain bronze disc on a chain",),
    doing=("Scales stands at the rail of the projector well, very straight, the long neck drawn up, one "
           "white-gloved hand on the brass and the other holding the plain bronze seal on its chain against the "
           "chest. Scales looks down the long bill into the display, at the place where a teal trail and a magenta "
           "trail run side by side: proud and unbowed, the bill level, the eyes bright. The face is turned "
           "three-quarters to the camera. The koi tail fin fans up behind one shoulder."),
    place=("The bridge of a great ship on the morning a treaty takes effect: a long room of dark polished wood and "
           "brass, with a band of tall windows down one side full of pale haze. Down the middle runs a table with "
           "two rows of chairs pushed back, a dozen half-empty glasses and two pens laid side by side. At the "
           "table's head is a round projector well ringed by a brass rail, and over it stands {chart} The display "
           "is as wide as a cartwheel and floats at the height of the rail. The bridge is empty of people."),
    light=("Soft daylight from the windows is the main light, cool and even, white on the plumes of the face. The "
           "display adds its own colours from below and in front: teal and magenta on the bill, the white gloves "
           "and the charcoal coat, and a bright edge through the translucent fins."),
    wear=("The charcoal coat is pressed, but its cuffs are shiny with wear and one button hangs by a thread; the "
          "white gloves are grey at the fingertips; the polished shoes are scuffed at the toes; one long fin has a "
          "kink where it was slept on."),
    close=(350, 0, 700, 350),
    patch=dict(
        what="the display of light over the round well",
        ask=("THE DISPLAY. Over the well stands one low dome of light, in the same place and with the same outline "
             "as now: its round foot sits inside the brass rail, and it is no wider and no higher than now. All "
             "that the old display held is gone, and the dome is filled again from nothing with what the reference "
             "pictures show. It is a hologram with real depth: a small solid-looking scene with a near side and a "
             "far side. Inside it is {view}. On the near side, where {name} looks, one jade teal ribbon and one "
             "ruby magenta ribbon run side by side."),
        edit=(   # the dome, a little wider than its own edge on the room's side, and down to the top of the rail:
                 # the rail in front of it and the glove on the rail are outside it
            ((700, 578), (716, 544), (750, 508), (805, 473), (861, 449), (929, 429), (1011, 412), (1093, 403),
             (1173, 399), (1255, 402), (1337, 412), (1418, 429), (1486, 449), (1536, 466), (1536, 838), (1480, 845),
             (1400, 852), (1300, 856), (1200, 855), (1080, 848), (1040, 845), (1000, 841), (960, 835), (920, 827),
             (880, 816), (840, 804), (800, 789), (760, 766), (748, 757), (734, 740), (724, 720), (716, 700),
             (710, 680), (706, 660), (702, 640), (698, 612)),
        ),
        refs=("sea_cell_outside", "sea_flora_approach", "sea_flora_crystal", "sea_flight_trail"),
    ),
)
