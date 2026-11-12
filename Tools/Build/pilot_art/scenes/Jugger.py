"""Jugger: orca and eagle, a shipwright. One of the few scenes that show live play in a hologram with depth (Garrett,
2026-11-11: "I want to see some where the pilot's are on the bridge of the ark or in a vessel. and they are looking
at a 3D holographic display ... that 3d rendering should look like gameplay footage"): on the bridge of the great
ship Jugger re-plated, watching creatures take the plates off it in the display, one hand held over the gap.
Garrett, 22:49 UTC: "Jugger is a great concept. I love that there is parity between the windows and the hologram.
however the hologram boundary was bleeding into the window and didn't contain everything on left. both the hypersea
outside and the hypersea in the rendering are very different than the hypersea in our gameplay." And 23:01: "the dual
perspectives of jugger is a better standard for windows". So the scene stays and two things in it are painted again
from real frames of the game (the patch): what the windows show, and the display, which is now one closed dome
inside the rail and below the sills.

Painted 2026-11-12 in one run, by the painter that takes pictures (four of them: three frames of the sea and the
ship's four views). The windows hold the game's bricks, needles, trails and shards, and the dome holds the ship
itself. The painter repainted the whole room a little while it worked (its answer moved 14.36 of 255 outside the
region: the figure 40 px to the right, the windows taller), so only the region was taken (refs.apply) and Jugger is
the Jugger of the first painting. Its answer also hung a second orca head in a window, above the old frames and so
outside the region: the words had asked the glass to carry "faint reflections of the bridge". That sentence is gone
from the ask below (LESSONS 84); patches/Jugger.json keeps the words as they were sent."""
SCENE = dict(
    treatment="65mm epic film still, deep focus, a bridge lit by its display",
    shot=("A film still in the manner of a 65mm epic: a wide lens low on the deck of a ship's bridge at night, deep "
          "focus, the dark room lit by the display at its centre, rich blacks that still hold detail, fine grain. "
          "The display is as sharp as the face. The figure is seen from the knees up, standing."),
    side="right",
    chart=("volume", "siege"),
    source="history: \"grew up in the shadow of a dead Ark\" (the game's Upbringings)",
    seen=("the folded wings are dark brown",),
    doing=("Jugger stands at the rail of the projector well, one heavy gauntlet on the brass, and looks down into "
           "the display at the small ship in it, which is this ship: at one place on its hull where the plates are "
           "gone. The other gauntlet has come off the rail and is held over that place, as if to cover it. The "
           "small dark eyes are solemn and far-seeing, the long mouth shut. The great head is turned three-quarters "
           "to the camera, the white patch behind each eye in the light. The wings are folded tight."),
    place=("The bridge of a great ship at night, in the middle of a long fight: a wide low room of dark worn metal, "
           "patched and re-plated in a dozen different years, with a band of thick windows. Outside them is the "
           "fight at its true size: black space sown with fine white dust, a coral of indigo bricks of light "
           "drifting past, two ribbons of small amber gold bricks curving away, a swarm of tiny violet shards, "
           "and far off a wall of six-sided panes. In the middle of the deck is "
           "a round projector well ringed by a scuffed brass rail, and over it stands {chart} The display is as "
           "wide as a cartwheel and stands inside the rail like a model under a glass bell, its top below the "
           "window sills. Round the well stand worn consoles of blank dark glass and two empty seats; one deck "
           "plate has been lifted and left, with a box of rivets and a work lamp beside it. The bridge is empty of "
           "people."),
    light=("The display is the main light: its amber-gold ribbons and violet swarms light the white chin and "
           "throat from below and in front, and put small moving points in the wet black skin of the head. The "
           "windows give a faint indigo fill, and the work lamp on the deck warms the backs of the legs."),
    wear=("The coverall is black with soot to the elbows and has a fresh scorch on one knee; its zip is pulled down "
          "to the collarbone over the grey undershirt; the rivet gauge swings on its strap; weld spatter has "
          "pitted the gauntlets; one wing feather is bent out of line."),
    close=(930, 0, 1370, 440),
    refs=("sea_flora_approach", "sea_flora_crystal", "sea_heart_beams", "ship_views"),
    patch=dict(
        what="what is seen through the bridge windows, and the display of light over the round well",
        ask=("THE WINDOWS. Through every window is the sea this ship sails, at its true size, exactly as the "
             "reference pictures show it: black space sown with fine white dust, and far off the wall of a vast "
             "hollow sphere, a net of thin dim blue lines in six-sided panes. Close by, drifting past the ship, "
             "stands a coral of bricks of light, flat-sided rectangular blocks with deep indigo and violet faces "
             "and thin bright edges, set edge to edge in rings round open holes with a spray of white needles in "
             "every hole. It is one thing seen through all the windows, running on behind the frames from one pane "
             "to the next. Two long ribbons of small amber gold bricks curve away past it, the trails of small "
             "craft flying guard, and a swarm of tiny violet shards streams toward the ship. The window frames "
             "stay where they are. The glass is thick and scratched."
             "\n\n"
             "THE DISPLAY. The old display is gone, and all of its light with it. Over the well stands one closed "
             "dome of light, like a model under a glass bell: its "
             "round foot sits inside the brass rail, its top is lower than the window sills, and no part of it "
             "reaches a window, a console or {name}. Everything it shows is inside it, and nothing glows outside "
             "its edge. It is a hologram with real depth: a small solid-looking scene with a near side and a far "
             "side. Inside it, small, is the same sea as in the windows: {view}. The raised gauntlet is held just "
             "above the dome, over the place on the ship where plates are gone."),
        edit=(   # the six panes of the window band, left to right (the fifth in two pieces round the head), then
                 # the old display with everything it spilled over
            ((0, 30), (125, 40), (142, 58), (170, 254), (0, 254)),
            ((214, 56), (428, 72), (436, 82), (450, 256), (234, 256)),
            ((476, 80), (664, 94), (672, 100), (684, 258), (488, 258)),
            ((698, 98), (856, 98), (862, 104), (864, 260), (704, 260)),
            ((890, 98), (982, 98), (980, 128), (992, 152), (1030, 166), (1005, 180), (960, 214), (925, 246),
             (904, 262), (896, 262)),
            ((1238, 95), (1292, 92), (1292, 138), (1272, 146), (1250, 122)),
            ((1345, 62), (1536, 40), (1536, 252), (1380, 252), (1362, 235), (1352, 180), (1345, 140)),
            ((285, 330), (330, 280), (420, 235), (560, 210), (700, 202), (840, 210), (960, 240), (1040, 290),
             (1085, 360), (1097, 460), (1095, 520), (1060, 600), (900, 645), (700, 662), (480, 645), (330, 612),
             (262, 575), (268, 470)),
        ),
        keep=(   # Jugger's reaching gauntlet and forearm, which the display's region runs under. The old display's
                 # creatures were painted over the edge of one thigh, so that strip is painted again with the rest.
            ((810, 348), (826, 305), (875, 290), (920, 286), (940, 276), (962, 261), (982, 248), (1045, 290),
             (1100, 298), (1100, 380), (1046, 380), (1042, 340), (1020, 346), (980, 359), (940, 366), (900, 378),
             (850, 374)),
        ),
    ),
)
