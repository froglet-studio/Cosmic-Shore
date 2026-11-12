#!/usr/bin/env python3
"""The SCENE: a pilot photographed in their world. One widescreen picture a pilot, and its close-up is their avatar.

Garrett, 2026-11-11, asked for a second, realistic style ("make a full scene piece of art that show them in a hyper
realistic setting. they still have their scratches, imperfections, a tear in their clothing, some dirt on their knees,
etc. they don't need to have any of these things, but if they fit then it shouldn't look like a pristine commercial
set, it should show them raw in their environment doing something that is appropriate to their background, history,
hobbies, profession, etc."), saw four samples, and decided (19:58 UTC): "All four of these are knocked out of the
park. This is an excellent direction. drop what we had before. the scene close ups are categorically better than the
previous avatar pictures and should be used in their place. All four scenes are excellent choices and I like that
each choice suits the personality of the pilot. SO I would continue to extend your reach and treat each pilot as a
new bespoke exercise in that regard. As far as the sizing and aspect ratio, we want something like 2.- (cinematic
film still) because then we have a fullscreen piece of art we can crop etc. for different platforms and
presentations. the main use will be a full screen profile for each pilot accessible through the ark app shell menu."

So:
  * EVERY PILOT HAS ONE SCENE, and it is a bespoke exercise: the shot, the place and what the pilot is doing are
    chosen for that person. A pilot's record is one file, scenes/<WORD>.py (SCENE = dict(...)); no two records
    share a treatment.
  * THE FRAME IS WIDE: 16:9, asked of the painter as ASK and returned as FRAME. It is fullscreen art that other
    presentations are cropped from (crop_box, the `crops` sheet, and the manifest scenes.json, which gives whoever
    builds a screen each scene's size, the head's box and the side the pilot stands on).
  * THE AVATAR IS THE SCENE'S CLOSE-UP: the record's `close` box, a square of the head AND whatever of the second
    animal the scene carries near it (a wing at the shoulder, a shell on the back, a tail, a fin), at least AVATAR
    px wide so an avatar is never enlarged. A square of the face alone loses the second animal: in a scene much of
    it rides on the body, where a bust kept it at the neck. So the box is head and shoulders wherever that brings
    the second animal in, and tight on the head where the head already carries both. Tools/Build/
    author_pilot_avatar_assets.py cuts every avatar from it; a pilot without a painted scene keeps the cut from
    their bust until they have one (avatar_source).
  * A FEW SCENES SHOW THE SEA THE PILOTS SAIL, on a chart panel or a chart of light in the place (20:22 UTC: "Make a
    few of them render the hypersea in their scene on either a panel or holographic display"): a record's `chart`,
    drawn as the game's own map draws that sea and never named (CHART_VIEWS, CHART_KINDS).
  * AND A FEW SHOW LIVE PLAY, in a hologram with depth, on the bridge of the pilot's great ship or in a vessel
    (21:34 UTC, on the first four: "these are all just view of the galactic scale hypersea. I want to see some where
    the pilot's are on the bridge of the ark or in a vessel. and they are looking at a 3D holographic display (not a
    2d hologram embedded in 3d) that 3d rendering should look like gameplay footage. prisms, trails, flora, fauna,
    gyroid flora, crystals, cells, etc."): the kind "volume" and its PLAY_VIEWS. The pilot looks INTO it: of the
    sheet of light in a tower room he said it "looks out of place in that scene and not aligned to his perspective",
    so a display belongs to its place (a ship has one, a writing desk does not) and the pilot's eyes are on it.
  * THE TWO ANIMALS ARE ONE CREATURE (23:01 UTC, on a booby under a jellyfish's bell: "looks like a bad and lazy
    photoshop with a bird head some hair and jellyfish stuff. she does not look like a thoughtfully conceived hybrid
    animal person. we can do a lot better job of expressing the animals together"; and on a hare in a badger's
    stripes: "otherwise he is just a rabbit man with skunk coloring"). A bust's brief gives each animal a part of
    its own so both can be named at icon size, and a painter paints that as parts set side by side. So a record
    may carry a `fused` line: the pilot written bespoke as ONE animal (the second animal's traits grown through
    the first one's own structures, one pattern over the whole body, an outline neither animal has alone, and a
    body the pilot is seen USING), which takes the place of the two sentences that hand out the parts (`who`,
    fused_errors). How to write one: .claude/skills/pilot-creator/SKILL.md, "One creature".
  * A PILOT'S ANIMAL CAN BE RECAST after their bust was painted (the hare's badger became a skunk): `recast`
    freezes today's words (scenes/identity/<WORD>.txt), and every later picture starts from them.

How a scene keeps the character (the painter takes words only):

1. THE SAME WORDS. A scene's description of the person is cut from the brief their bust was painted from
   (`busts/briefs/<WORD>.txt`, `identity`), not from today's tables: the tables move on, a painting does not. A new
   pilot, who never had a bust, takes the bust brief today's tables compose, and capture freezes it
   (scenes/identity/<WORD>.txt) so every later picture of that pilot starts from the same words. A frozen file
   wins over a bust's brief, which is how a recast pilot leaves the old animal behind.
2. WHAT THE PAINTING SHOWS BEYOND ITS BRIEF (`seen`), AND AGAINST IT (`swap`). A painter decides things no brief
   asked for: the colour of a glove, that the brooch is brass, that the shell cap hides the ears. Whoever writes a
   record LOOKS at the pilot's earlier picture first and writes those down. Where that painting departs from the
   words it was painted from, the record swaps the phrase for what the picture shows. Two colour words for one eye
   let the painter choose, so a correction replaces, never adds.
3. THE REST OF THE BODY IS RULES, NOT ROLLS: the build (BUILD), the hands (`hands_of`), where the second animal's
   piece grows from (CARRY) and the lower half of the clothes (LOWER), the same in every picture of a pilot.
4. NOTHING PERMANENT IS ADDED. Wear is mud, soot, a tear, a mended seam, a scuff: things a day gives and a wash or a
   needle takes away. A scene never gives the face a scar, a chipped tusk or a missing ear (PERMANENT).

What the person is DOING comes from who they are, and each record names its source: their profession (the faction
type), a hobby (the game's own `Quirks` in PilotGenerator.cs), their history (its `Upbringings`) or what they want
most (its `Wants`). A pilot the game rolled has all of these already (`creator.py roll`).

The brief goes to an outside painter, so it keeps bust.py's two gates: it never names the game, the studio, the world
or a place, a crew or a faction in it (`bust.world_name_in`; the home ship is "a great ship"), and it never invites an
emblem or lettering (`bust.emblem_word`).

    python3 Tools/Build/pilot_art/scene.py list                 # every pilot: treatment, source, painted or not
    python3 Tools/Build/pilot_art/scene.py who <WORD>           # a pilot's identity lines and hands
    python3 Tools/Build/pilot_art/scene.py draft <WORD>         # the facts and an empty record for a new scene
    python3 Tools/Build/pilot_art/scene.py recast <WORD>        # freeze today's words for a pilot whose animals changed
    python3 Tools/Build/pilot_art/scene.py brief <WORD>         # the brief, verbatim what the painter gets
    python3 Tools/Build/pilot_art/scene.py capture <WORD> <url> # save a painting and the brief it was painted from
    python3 Tools/Build/pilot_art/scene.py grid <WORD|file> <out.jpg>   # the picture under a ruled grid, to set `close`
    python3 Tools/Build/pilot_art/scene.py sheet <out.png> [WORD...]    # scenes beside their avatars, ten a page
    python3 Tools/Build/pilot_art/scene.py crops <out.png> [WORD...]    # one scene cut for other presentations
    python3 Tools/Build/pilot_art/scene.py manifest [--check]   # scenes.json: what a screen needs to place a scene
    python3 Tools/Build/pilot_art/scene.py stale                # scenes today's kit would paint differently
    python3 Tools/Build/pilot_art/scene.py sweep-prep <dir> [WORD...]   # the readers' pictures, shuffled; key beside <dir>
    python3 Tools/Build/pilot_art/scene.py sweep-grade <dir>    # grade <dir>/result.json against the key
    python3 Tools/Build/pilot_art/scene.py --self-test

THE SWEEP is how a finished scene is checked by someone who did not write its brief. sweep-prep writes, into a NEW
scratch folder outside the repository, every painted scene under a shuffled number: sNN.jpg whole, sNN_close.jpg the
avatar's square, sNN_left.jpg and sNN_right.jpg its two halves enlarged, with the key at <dir>.key.json, outside the
folder the readers open. The readers are the workflow .claude/skills/pilot-creator/references/scene_sweep.js: told
nothing, they say what kind of image it is, which animals they see, what the person is doing, how many figures there
are, and list any lettering, any broken anatomy and any bare human skin. Save what it returns as <dir>/result.json
and grade it. Whether a stranger names both animals from the AVATAR is blind.py's read, which reads the close-ups.
"""
import collections
import functools
import hashlib
import importlib.util
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import brief    # noqa: E402
import bust     # noqa: E402
import creator  # noqa: E402

ROOT = brief.ROOT
SCENES = "Docs/Pilots/art/scenes"

# ---------------------------------------------------------------------------------------------------------------
# The frame. Every scene is the wide frame of a cinema still, and the pilot's avatar is cut from it.
# ---------------------------------------------------------------------------------------------------------------
ASK = (2048, 1152)      # what the painter is asked for
FRAME = (1536, 864)     # what gpt-image-2.5-sunburst returns for 16:9: the size of every scene
AVATAR = 256            # the avatar's size (author_pilot_avatar_assets.SIZE): a close-up is never smaller
RECORDS = os.path.join(HERE, "scenes")   # one record a pilot: <WORD>.py, SCENE = dict(...)

# What every shot shares, after its own words: where the pilot stands in the wide frame, so a screen can lay text
# over the other side and other presentations can be cropped from it, and what the avatar needs of the head. A
# record whose shot already frames itself says frame="" (the two samples kept from before this line existed).
FRAMING = ("The frame is wide, sixteen by nine, with {name} a third of the way in from the {side} edge and the place "
           "filling the rest. Clear space above all that grows from the head. The head is sharp and lit, the whole "
           "face easy to read, the place behind it soft.")
SIDES = ("left", "right")

FINISH = (
    "All of it is photographed as it is, with a real camera: living anatomy with the fur, feathers, hide and scales of "
    "real animals, every hair resolved, wet where it is wet; real cloth, leather and metal with weight and wear; real "
    "dirt. The head and everything that grows from it are {name}'s own living anatomy, never a mask, a costume, a hat "
    "or a headdress. The eye rings and the piping glow no brighter than a reflection. One person only: nobody else is "
    "anywhere in the frame, near or far. Nothing in the frame carries writing, numbers or a symbol: every label, "
    "sign, crate and screen is blank. No weapon, no watermark, no border, no human ears."
)
STYLE_WORDS = re.compile(r"\b(?:painterly|illustrat\w+|render\w*|concept art|digital art|airbrush\w*|glamour|"
                         r"cartoon\w*|anime|collectible|3d)\b", re.I)   # words that pull a photograph back to a painting

# ---------------------------------------------------------------------------------------------------------------
# The sea on a display. Garrett, 2026-11-11 (20:22 UTC): "Make a few of them render the hypersea in their scene on
# either a panel or holographic display". So a FEW scenes, never most (CHART_MOST), show the sea the pilots sail on
# something that stands in the place: a flat chart panel, or a chart of light hanging in the air. It is drawn as the
# game's own map draws it (VoyageMapGraphic and the Voyage Board, coloured by HyperSea.SeaColor: slate spray, teal
# froth, blue water, violet thick water, coral reef; places as small rings and diamonds, lanes as thin pale lines,
# the course one lime-yellow line). The brief may not name that sea or a place on it (bust.world_name_in), so the
# chart is told by what it looks like, and it carries no writing. A record asks for one with chart=(kind, view) and
# writes the token {chart} in its `place` where the display's own words begin ("On the bulkhead to the right hangs
# {chart} Under it, ..."): the token becomes the display, what it shows, how dim it is and that nothing is written
# on it, ending in a full stop. A lit chart is the one thing in a scene that glows, and a loud glow turns a
# photograph into a 3D render (LESSONS 67), so both kinds are dim by their own words; the record's `light` says
# what little the chart lights.
# ---------------------------------------------------------------------------------------------------------------
CHART_VIEWS = {   # what the chart shows: the whole sea, or the waters round a course
    "galaxy": ("a whole spiral galaxy seen from above, drawn as a chart of a sea: long arms of soft sea blue flecked "
               "with violet wind out from a short pale-peach bar at the centre, the gulfs between them nearly black, "
               "small gold points scattered round it; one small amber ring near the centre, another far out along "
               "a spiral arm, and a thin lime-yellow line between them in short straight legs"),
    "waters": ("a sea chart of deep space seen from straight above: dark blue water dusted with fine white points, "
               "drifts of violet where it thickens, islands of coral orange with violet shores, one wide grey shoal "
               "inside a thin teal shoreline, small ring and diamond markers joined by thin pale lines, and one "
               "lime-yellow course line stepping from marker to marker"),
}
# Live play in a hologram with depth. Garrett, 2026-11-11 (21:34 UTC), on the four charts: "these are all just view
# of the galactic scale hypersea. I want to see some where the pilot's are on the bridge of the ark or in a vessel.
# and they are looking at a 3D holographic display (not a 2d hologram embedded in 3d) that 3d rendering should look
# like gameplay footage. prisms, trails, flora, fauna, gyroid flora, crystals, cells, etc." So a third kind of
# display, "volume", shows play. Its first words were written from the arcade cards and painted as a soap bubble
# holding strings of beads, and he said so (22:49 UTC: "both the hypersea outside and the hypersea in the rendering
# are very different than the hypersea in our gameplay"). These words are written from REAL FRAMES of the game
# (Docs/Pilots/art/refs, refs.json; SEA holds the look once, for a window as well as a display):
#   - space is black and sown with fine white dust, each mote a short pale dash;
#   - a cell's wall is a net of thin dim blue lines in six-sided panes, like a wire dome seen from inside;
#   - a prism is a brick of light: a flat-sided rectangular block, its faces a deep clear colour, its edges thin
#     bright lines; a trail is a ribbon of them laid end to end; far off it reads as a row of dashes;
#   - the gyroid flora is a coral of such bricks set edge to edge in rings round open holes, with a spray of white
#     needles in every hole; a crystal is a pale lilac twelve-sided die; fauna are swarms of tiny violet shards;
#   - the great ship is a closed scallop shell: a swept fan of ribbed plates behind a domed bridge, engines along
#     the stern edge (Docs/VesselModels/ArkReverse.md).
# None of it is named, since the brief names nothing of the game's: it is told by what it looks like. {own} is the
# pilot's own domain colour (the accent their clothes are piped in), {other} and {third} the other two. A volume
# shows play and nothing else, and a chart never shows play.
SEA = dict(   # the look, once: a record's own words for a window use these same nouns (LESSONS 76)
    dark="black space sown with fine white dust",
    wall="the wall of a vast hollow sphere, a net of thin dim blue lines in six-sided panes",
    brick="flat-sided rectangular blocks with deep clear faces and thin bright edges",
    coral="set edge to edge in rings round open holes",
    crystal="shaped like a twelve-sided die",
    swarm="tiny violet shards",
)
PLAY_VIEWS = {
    "cell": ("the inside of one vast hollow sphere whose wall is a net of thin dim blue lines in six-sided panes, "
             "holding black space sown with fine white dust. Everything in it is built of small bricks of light: "
             "flat-sided rectangular blocks with deep clear faces and thin bright edges. Six tiny bright craft fly "
             "inside it, and each lays behind it a long curving ribbon of such bricks set end to end, hundreds to a "
             "ribbon: three ribbons {own}, three {other}, looping over and under each other. At the sphere's heart "
             "grows a coral of the same bricks in deep indigo and violet, set edge to edge in rings round open "
             "holes so that it makes curving walls full of round windows, with a spray of white needles in every "
             "hole. A pale lilac crystal shaped like a twelve-sided die glows in the coral's wall, a swarm of tiny "
             "violet shards flows past it in the shape of a ring, and long rows of small cyan dashes run through "
             "the dark"),
    "race": ("a racing course strung through black space sown with fine white dust: a line of thick glowing rings, "
             "pale blue-white, each drawn with a fine grid, set one beyond another at odd angles like hoops, and "
             "three tiny bright craft threading them. Each craft lays behind it a long curving ribbon of small "
             "bricks of light set end to end, flat-sided rectangular blocks with deep clear faces and thin bright "
             "edges, hundreds to a ribbon: one ribbon {own}, one {other}, one {third}. Where two ribbons meet, "
             "bricks have been knocked loose and tumble away. Beside the course stands a coral of indigo bricks set "
             "edge to edge in rings round open holes, pale lilac crystals shaped like twelve-sided dice hang along "
             "the racing line, and far behind it all curves the wall of a vast hollow sphere, a net of thin dim "
             "blue lines in six-sided panes"),
    "siege": ("one great dark ship, no longer than a forearm in the display, shaped like a closed scallop shell: a "
              "low swept fan of ribbed plates spreading back from a domed bridge at its point, with a row of round "
              "engine mouths glowing along its wide stern edge and thin bright seams between its plates. Swarms of "
              "tiny violet shards are closing on it from every side, each swarm flowing like a shoal, and where "
              "they have fed, plates are gone from the fan and the frame shows. Three tiny bright craft circle the "
              "ship, and each lays behind it a long curving ribbon of small glowing {own} blocks set end to end, "
              "flat-sided, with deep clear faces and thin bright edges, the three ribbons winding round the ship "
              "like a cage. A coral of indigo blocks set edge to edge in rings round open holes drifts beside it, "
              "pale lilac crystals shaped like twelve-sided dice hang in the dark, and far behind curves the wall "
              "of a vast hollow sphere, a net of thin dim blue lines in six-sided panes, in black space sown with "
              "fine white dust"),
    "garden": ("a coral growing through black space sown with fine white dust: many hundreds of bricks of light, "
               "flat-sided rectangular blocks with deep clear faces and thin bright edges, set edge to edge in "
               "rings round open holes, ring joined to ring into curving walls and tunnels full of round windows, "
               "{own} at its heart and deep indigo and violet at its growing edges, with a spray of white needles, "
               "thin as hairs, in every hole. A pale lilac crystal shaped like a twelve-sided die glows where it "
               "is held in the coral's wall. One tiny bright craft flies through a tunnel of it and lays behind it "
               "a ribbon of small bricks set end to end. A swarm of tiny violet shards flows past in the shape of "
               "a ring, long rows of small cyan dashes run through the dark, and far behind curves the wall of a "
               "vast hollow sphere, a net of thin dim blue lines in six-sided panes"),
}
PLAY_COLOURS = ("jade teal", "ruby magenta", "amber gold")   # the three accents a pilot's clothes are piped in
CHART_KINDS = {   # what the sea or the play is shown on
    "panel": ("a flat chart panel, a sheet of dark glass in a scuffed metal frame. It shows {view}. The panel is old "
              "and dim, no brighter than a lamp turned low, its glass smeared with fingerprints."),
    "hologram": ("a chart made of faint light that hangs in the air on its own, a thin flat sheet of it. It shows "
                 "{view}. Its light is grainy and thin as smoke in a torch beam, and the place shows through it."),
    "volume": ("a hologram with real depth: not a flat picture but a small solid-looking scene made of light, "
               "standing in the air, with a near side and a far side like a model, the near things in front of the "
               "far. It shows {view}. Its colours are clear and bright against its own dark, bright enough to read "
               "a face by and no brighter, and they fall softly on whatever is near."),
}
CHART_SHOWS = {"panel": CHART_VIEWS, "hologram": CHART_VIEWS, "volume": PLAY_VIEWS}   # which views a kind may show
CHART_NOUN = {"panel": "chart", "hologram": "chart", "volume": "display"}   # what a record's own words call it
CHART_RULE = "The chart is colour, lines and plain marks only: no letter, number, label or icon anywhere on it."
PLAY_RULE = "The display is light and shape only: no letter, number, label or icon anywhere in it."
CHART_RULES = {"chart": CHART_RULE, "display": PLAY_RULE}
CHART_TOKEN = "{chart}"
CHART_MOST = 0.2   # "a few of them": at most one scene in five carries a display of either sort
FINISH_BLANK = "every label, sign, crate and screen is blank"                                  # FINISH's own words,
FINISH_CHART = "every label, sign and crate is blank, and the chart is the only screen that is lit"   # and a chart's
FINISH_PLAY = "every label, sign and crate is blank, and the display is the only screen that is lit"
FINISH_LIT = {"chart": FINISH_CHART, "display": FINISH_PLAY}


def chart_pairs():
    """Every (kind, view) a record may ask for."""
    return [(kind, view) for kind, views in CHART_SHOWS.items() for view in views]


def chart_of(chart, own=None):
    """The words a record's {chart} token becomes. `own` is the pilot's accent colour, which a view of play gives
    the pilot's own side's trails (the first of PLAY_COLOURS when none is given)."""
    if not (isinstance(chart, (tuple, list)) and len(chart) == 2 and chart[0] in CHART_KINDS
            and chart[1] in CHART_SHOWS[chart[0]]):
        raise ValueError(f"chart={chart!r}: give (kind, view), one of "
                         + ", ".join(f"({k}, {v})" for k, v in chart_pairs()))
    kind, view = chart
    shown = CHART_SHOWS[kind][view]
    own = own if own in PLAY_COLOURS else PLAY_COLOURS[0]
    other, third = [c for c in PLAY_COLOURS if c != own]
    shown = shown.replace("{own}", own).replace("{other}", other).replace("{third}", third)
    return CHART_KINDS[kind].replace("{view}", shown) + " " + CHART_RULES[CHART_NOUN[kind]]


def place_of(rec, own=None):
    """(the record's place with its chart written in, the brief's last paragraph before the name is filled in)."""
    place, chart = rec["place"], rec.get("chart")
    if not chart:
        if CHART_TOKEN in place:
            raise ValueError(f"the place says {CHART_TOKEN} and the record has no chart=(kind, view)")
        return place, FINISH
    if place.count(CHART_TOKEN) != 1:
        raise ValueError(f"a record with a chart writes {CHART_TOKEN} once in its place, where the display's words "
                         f"begin")
    words = chart_of(chart, own)
    return place.replace(CHART_TOKEN, words), FINISH.replace(FINISH_BLANK, FINISH_LIT[CHART_NOUN[chart[0]]])

# ---------------------------------------------------------------------------------------------------------------
# The body below the bust's crop: decided once, here, the same for every picture of a pilot.
# ---------------------------------------------------------------------------------------------------------------
BUILD_DEFAULT = "an adult's build, upright and proportioned like a person"
BUILD = {   # by the face animal: the body under the clothes follows the head it carries (BUILD_DEFAULT for a new one)
    "Armadillo": "short, round-backed and solid, low to the ground",
    "Axolotl": "small and compact, soft-shouldered and quick",
    "Badger": "short, broad and low-slung, thick in the neck and heavy in the forearm",
    "Basilisk": "light, lean and quick, long in the leg, always about to move",
    "Beetle": "broad, compact and heavy for its height, slow to start and hard to stop",
    "Bison": "huge in the chest and shoulder, heavy-necked, narrow in the hip",
    "Black hare": "lean and long-legged, light on the feet, quick to startle",
    "Booby": "of middle height and plain, a little pigeon-toed, steady",
    "Capybara": "barrel-bodied and placid, short in the leg, never in a hurry",
    "Caracal": "slim and light, long in the leg, a jumper's build",
    "Chameleon": "slim and deliberate, long in the arm, every movement slow and exact",
    "Cheetah": "tall and whip-lean, deep-chested and narrow-waisted, a sprinter's build",
    "Clownfish": "small, compact and wiry, a jockey's build",
    "Coyote": "rangy and loose-limbed, lean from going without",
    "Crane": "tall and slender, long-necked and long-legged, with a dancer's straight back",
    "Cuttlefish": "soft-shouldered and fluid, of middle height, never quite still",
    "Eel": "long, narrow and supple, sloping in the shoulder",
    "Egret": "tall and very slender, long-necked, with an exact upright carriage",
    "Fennec": "small and slight, fine-boned, quick and light",
    "Firefly": "slight and narrow, light on the feet",
    "Flamingo": "tall and thin, all leg and neck, balanced like a dancer",
    "Fox": "slight, neat and quick",
    "Gecko": "small, slim and supple, at ease on any surface",
    "Hare": "lean and long-legged, light on the feet, quick to startle",
    "Hedgehog": "small and round-shouldered, a head shorter than most people",
    "Heron": "tall, thin and stooped a little, long-legged and patient",
    "Horned lizard": "squat and broad, short-legged and wide across the back",
    "Hummingbird": "tiny and fine-boned, a head shorter than most people, quick and never still",
    "Hyena": "heavy in the shoulder and neck, sloping to lean hips",
    "Ibis": "lean and long-legged, a wader's build",
    "Jackal": "lean, light and wiry, a trotter's build",
    "Jellyfish": "slight and soft, of middle height, drifting more than walking",
    "Kangaroo": "tall and long-legged, strong in the haunch, narrow in the shoulder",
    "Koi": "smooth and solid, of middle height, calm in every movement",
    "Lemur": "small, light and long-limbed, a climber's build",
    "Lobster": "broad and armoured-looking, heavy in the forearm",
    "Lynx": "lean and spring-legged, a climber's build",
    "Mandrill": "powerful and compact, heavy in the shoulder and long in the arm",
    "Mantis": "very tall and thin, long-limbed and narrow-waisted, with a held stillness",
    "Mole": "short, stout and strong, broad in the hand",
    "Moose": "very tall and rangy, long-legged and big-shouldered, a little stooped",
    "Mouse": "small and slight, narrow-shouldered, a head shorter than most people",
    "Narwhal": "tall, heavy and smooth, deep-chested, with a slow straight carriage",
    "Newt": "small, slim and soft, cool and unhurried",
    "Ocelot": "lithe and light on the feet, with a cat's easy balance",
    "Octopus": "soft-shouldered and fluid, of middle height, never quite still",
    "Opossum": "small, wiry and sharp-shouldered",
    "Orangutan": "big and heavy-shouldered, with arms so long the hands hang to the knees",
    "Orca": "huge and smooth, broad as a doorway, heavy and unhurried",
    "Oryx": "lean and long-legged, a runner's build, with a straight proud neck",
    "Ostrich": "very tall, long-necked and long-legged, with powerful thighs",
    "Otter": "long-bodied and supple, short in the leg, quick as water",
    "Owl": "short and stout, deep-chested, the head carried high",
    "Pangolin": "stocky and round-backed, strong in the forearm",
    "Parrot": "of middle height and compact, quick in the hands, the head often cocked",
    "Pelican": "big-chested and heavy, short in the leg",
    "Platypus": "broad and low, thick through the body, with a slow rolling walk",
    "Puffin": "short and barrel-chested, with a seaman's wide stance",
    "Raven": "lean, dark and deliberate, of middle height",
    "Seahorse": "narrow and upright, held very straight",
    "Shoebill": "tall, gaunt and long-legged, standing very still",
    "Sloth": "long-armed and slow, hung loosely together",
    "Snail": "soft and unhurried, of middle height, sloping in the shoulder",
    "Tapir": "solid and barrel-chested, short in the neck",
    "Tardigrade": "plump, short-limbed and unbothered",
    "Tiger": "tall, broad-shouldered and powerful",
    "Tortoise": "stocky and slow, short in the leg, built to last",
    "Toucan": "of middle height and sturdy, a little top-heavy under the great bill",
    "Tree frog": "slight and long-limbed, long in the finger and the shin, folded up when at rest",
    "Walrus": "huge and heavy, barrel-chested and thick-necked, broad as a doorway, a big man's bulk carried upright "
              "and gently",
    "Wolf": "tall, rangy and deep-chested, an easy tireless walker",
    "Yak": "massive and slow, deep-chested and heavy-shouldered, a head taller than most people",
    "Zebra": "strong and deep-chested, heavy in the neck, straight-backed",
}

GLOVES = {   # by faction type: the gloves a pilot wears when no animal hand of their own shows
    "Merchant": "soft tan kid gloves, creased at the knuckles",
    "Hunter": "worn russet leather field gloves",
    "Aristocrat": "pale grey doeskin gloves",
    "Pirate": "dark brown leather sea gloves, creased and scuffed",
    "BountyHunter": "black leather gloves with padded knuckles",
    "Explorer": "scuffed tan expedition gloves of canvas and leather",
    "Activist": "knitted wool gloves gone thin at the fingertips",
    "Thief": "thin black close-fitting gloves",
    "Gatherer": "earth-stained canvas work gloves",
    "Contractor": "heavy scorched hide gauntlets",
    "Sport": "close-fitting padded racing gloves",
    "Researcher": "thin pale specimen gloves",
    "Preservationist": "green gardening gloves with mud in the seams",
    "FirstResponder": "heavy orange-and-black rescue gloves, scuffed grey at the palms",
    "Priest": "plain white linen gloves",
    "Diplomat": "fine white dress gloves",
}

LOWER = {   # by faction type: the lower half of the clothes, worded to go under any of the type's three outfits
    "Merchant": "tailored dark trousers and polished ankle boots with worn heels",
    "Hunter": "mud-brown canvas breeches and tall laced leather boots, resoled more than once",
    "Aristocrat": "pressed pale breeches and tall riding boots",
    "Pirate": "dark sea trousers tucked into heavy sea boots folded down at the knee",
    "BountyHunter": "dark reinforced trousers with a strapped thigh pouch and scuffed black boots",
    "Explorer": "patched canvas trousers with a mended knee and dusty trail boots",
    "Activist": "faded work trousers and old boots with odd laces",
    "Thief": "close dark trousers and soft-soled boots",
    "Gatherer": "stained canvas dungarees and short rubber boots",
    "Contractor": "heavy scorched work trousers with knee pads and steel-capped boots",
    "Sport": "close-fitting racing leggings with padded knees and light boots",
    "Researcher": "plain dark trousers, bagged at the knees, and soft-soled deck shoes",
    "Preservationist": "green canvas trousers, damp to the knee, and rubber wading boots",
    "FirstResponder": "heavy waterproof over-trousers with grey reflective bands and black rescue boots",
    "Priest": "a long plain under-robe to the ankle and sandals",
    "Diplomat": "sharply pressed dark trousers and polished shoes",
}

CARRY = {   # by the kind of the second animal's piece (bust.SECOND): where it grows from, seen whole
    "tail": "seen whole, the {bl}'s tail grows from the base of the spine and comes out under the hem of the clothes "
            "at the back",
    "fan": "seen whole, the {bl}'s fan grows from the base of the spine, where a tail grows, and stands behind the "
           "back, behind the clothes",
    "wings": "seen whole, the {bl}'s wings grow from the shoulder blades, pass through two slits in the back of the "
             "clothes and fold down the back to the hips",
    "fins": "seen whole, the {bl}'s fins grow from the shoulder blades through two slits in the back of the clothes",
    "shell": "seen whole, the {bl}'s shell covers the back from the shoulders to the hips, and the clothes are cut to "
             "close around it",
    "body": "seen whole, it runs on down the back under the clothes, which are cut wide to take it",
}

# Wear is what a day gives and a wash or a needle takes away. These words name something that stays, on a body:
# a scene never adds one the avatar does not have, or the pilot's next picture would have to carry it.
PERMANENT = re.compile(r"\b(?:scars?|scarred|tattoo\w*|eye ?patch\w*|one-eyed|branded|missing (?:an? )?(?:ear|eye|"
                       r"tusk|horn|finger|tooth)|(?:chipped|broken|cracked|snapped|notched|torn|split) (?:tusks?|"
                       r"horns?|beaks?|bills?|ears?|antlers?|shell|teeth|tooth))\b", re.I)

# What a fused line may not say: the words that give one animal the head and the other what is behind it. And what
# it must say when the second animal brings a piece of its body (bust.SECOND's kinds): the fused line is the whole
# creature, so compose adds no "also gives" sentence after it and the record gives no `carry`.
FUSED_PIECE = {"tail": "tail", "fan": "fan", "wings": "wing", "fins": "fin", "shell": "shell"}
FUSED_NOT = re.compile(r"\b(?:owns|everything behind the face|is the face and the shape of the head|"
                       r"the head of an? \w+ (?:on|with) the body of)\b", re.I)

# ---------------------------------------------------------------------------------------------------------------
# The records: scenes/<WORD>.py, one a pilot. treatment: a few words that name the shot (no two pilots share one).
# shot: the camera, the lens and the moment, written for this pilot. side: which side of the frame the pilot stands
# on. source: what in the pilot's life the scene comes from. seen / swap: what the pilot's earlier painting shows
# beyond and against its brief (look at the picture, then write). doing, place, light, wear: the scene. chart: a
# display in the place that shows the sea (above). close: the square in the painted scene that is the avatar: the
# head, and the nearest piece of the second animal when the scene carries one near it (set it after painting: `grid`).
# ---------------------------------------------------------------------------------------------------------------
SCENE_KEYS = ("treatment", "shot", "source", "seen", "doing", "place", "light", "wear")
SCENE_MORE = ("frame", "side", "carry", "lower", "build", "hands", "swap", "chart", "close", "fused", "refs",
              "patch")   # refs, patch: the pictures a painter that takes pictures is shown (refs.py)
SOURCES = ("profession", "hobby", "history", "want")
SWAPPED = ("face", "coat", "second", "eyes", "hair", "outfit", "motif")   # the bust's lines a swap may correct
WORD_CAP = 1000   # briefs of 870 to 890 words were painted faithfully; past a thousand a line is one too many.
#                   A chart's own sentences (chart_of) are counted apart: they are the same in every record.


@functools.lru_cache(maxsize=None)
def _load(path, stamp):
    spec = importlib.util.spec_from_file_location("scene_record_" + os.path.basename(path)[:-3], path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.SCENE


def record(word):
    """A pilot's scene record (a copy), or None when the pilot has none yet."""
    path = os.path.join(RECORDS, f"{word}.py")
    if not os.path.exists(path):
        return None
    return dict(_load(path, os.path.getmtime(path)))


def records():
    """{word: record} for every catalog pilot that has one, in the catalog's order."""
    out = {}
    for w, *_ in creator.catalog():
        rec = record(w)
        if rec is not None:
            out[w] = rec
    return out

# ---------------------------------------------------------------------------------------------------------------
# The identity: cut from the brief the avatar was painted from.
# ---------------------------------------------------------------------------------------------------------------
PAINTED = re.compile(
    r"owns the face and skull: (?P<face>.*?)\. (?P<B>[A-Z][A-Z -]*?) owns the (?P<part>.*?) and everything behind the "
    r"face: (?P<behind>.*?)\. HUMAN is the bearing, the clothes and the gaze: (?P<name>.*?) sits upright like a "
    r"person, dressed, and (?P<eyes>.*?)\. (?P<hair>No human hair anywhere on the head\.|Human hair[^.]*\.) "
    r"(?P<hand>.*?) (?:The beak is rigid|Expression:).*?Wearing (?P<outfit>.*?), trimmed in (?P<word>[a-z ]+) "
    r"\((?P<hex>#[0-9A-Fa-f]{6})\) with thin glowing [a-z ]+ seam lines(?:, and (?P<motif>.*?))?"
    r"(?:; the collar sits low[^.]*)?\.", re.S)
GAZE = "bright and alert and looking at the viewer, "   # the bust's gaze; a scene sets its own
OWN_LIMB = re.compile(r"it is the (?P<x>[a-z -]+?)'s(?: own)?:")


def identity_brief_path(word):
    """Where a pilot who never had a bust keeps the words their first scene was painted from."""
    return os.path.join(ROOT, SCENES, "identity", f"{word}.txt")


def identity_text(word):
    """(the bust brief a pilot's pictures start from, its path): the words frozen for this pilot (a pilot who never
    had a bust, at their first scene; a pilot recast since their bust, by `recast`); else the brief their bust was
    painted from; else, for a pilot with no picture yet, what today's tables compose (path None)."""
    for path in (identity_brief_path(word), creator.painted_brief_path(word)):
        if os.path.exists(path):
            with open(path, encoding="utf-8") as fh:
                return fh.read(), path
    return creator.brief_of(word), None


def recast(word):
    """Freeze today's words for a pilot whose animals changed after their bust was painted (creator.LEGACY, a spec's
    heritage): the bust's brief still says the old animal, so every later picture starts from this file instead.
    Returns its path. The record's `seen` and `swap` were written against the old words: read them again."""
    w = next((w for w, *_ in creator.catalog() if w.lower() == word.lower()), None)
    if w is None:
        raise SystemExit(f"{word} is not in the catalog (creator.py list)")
    path = identity_brief_path(w)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(creator.brief_of(w) + "\n")
    return path


def identity(word):
    """A cast pilot as their first picture was painted: dict(word, name, a, b, faction, callsign, look, face, part, coat,
    second, kind, eyes, hair, hand, outfit, accent, hex, motif), every text line verbatim from that brief (identity_text)."""
    for w, person, (a, b), faction, callsign, look in creator.catalog():
        if w.lower() == word.lower():
            break
    else:
        raise SystemExit(f"{word} is not in the catalog (creator.py list)")
    text, path = identity_text(w)
    m = PAINTED.search(text)
    if not m:
        raise ValueError(f"{path or 'the brief bust.compose writes for ' + w} does not read as a bust brief: "
                         f"scene.PAINTED no longer matches bust.compose")
    d = m.groupdict()
    behind = d["behind"]
    cut = behind.find("; and ", behind.find("the back of the head, the nape"))
    coat, second = (behind, "") if cut < 0 else (behind[:cut], behind[cut + len("; and "):])
    return dict(word=w, name=person, a=a, b=b, faction=faction, callsign=callsign, look=look, face=d["face"],
                part=d["part"], coat=coat, second=second, kind=bust.SECOND.get(b, ("", ""))[0] if second else "",
                eyes=d["eyes"].replace(GAZE, ""), hair=d["hair"], hand=d["hand"], outfit=d["outfit"],
                accent=d["word"], hex=d["hex"], motif=d["motif"] or "")


def as_seen(idn, swap=()):
    """The identity with a record's swaps applied: where the PAINTING departs from the words it was painted from, the
    scene follows the painting. A swap whose phrase is in none of the avatar's lines is refused."""
    out = dict(idn)
    for old, new in swap:
        hit = [k for k in SWAPPED if old in out[k]]
        if not hit:
            raise ValueError(f"{idn['word']}: the swap {old!r} is in none of the avatar's lines")
        for k in hit:
            out[k] = out[k].replace(old, new)
    return out


POSED = (", the palm toward the viewer", ", curling up from below")   # bust.LIMB's words that pose a bust's one limb
ARMS = ("Octopus", "Cuttlefish")   # animals whose own limb is an arm with no fingers: it holds by curling round


def hands_of(idn):
    """The pilot's two hands, the same in every picture: the hand the avatar shows (an animal's own, or gloves);
    else the true hand of one of their animals; else the faction type's gloves. A bust's limb line poses the one
    limb the bust shows (POSED); a scene poses its own hands, so those words are left out."""
    a, b, look = idn["a"], idn["b"], idn["look"]
    holds = "Each hand has fingers and a thumb and holds things the way a person's hand does."

    def own(x):
        limb = bust.LIMB[x]
        for posed in POSED:
            limb = limb.replace(posed, "")
        if x in ARMS:
            return (f"Both arms are the {x.lower()}'s own from the elbow down: each sleeve ends in {limb}, in place "
                    f"of a forearm and a hand. Each arm holds things by curling round them, as surely as a "
                    f"person's hand does.")
        return (f"Both hands are the {x.lower()}'s own: each is {limb}, and the wrist above it wears the "
                f"{x.lower()}'s own coat. {holds}")
    if look.get("limb"):
        m = OWN_LIMB.search(idn["hand"])
        x = next((k for k in (b, a) if m and k.lower() == m.group("x")), None)
        if not x or not bust.LIMB.get(x):
            raise ValueError(f"{idn['word']} shows a limb but its brief does not say whose (scene.OWN_LIMB)")
        return own(x)
    if look.get("hand", "none") != "none" and not look.get("glove", True):
        return "Both hands are a person's bare hands: four fingers and a thumb, a person's skin and short nails."
    if look.get("hand", "none") != "none":
        return f"Both hands are a person's hands in gloves: {GLOVES[idn['faction']]}."
    owners = [x for x in (b, a) if bust.LIMB.get(x)]
    true = [x for x in owners if x not in bust.ARMOURED] or owners
    if true:
        return own(true[0])
    return f"Both hands are a person's hands in gloves: {GLOVES[idn['faction']]}."


ONE = "one creature, born this way, and no part of {name} looks added to another"   # what a fused pilot's brief opens on
FUSED_CONTROL = ("One animal: the {a}'s face in the {b}'s coat, the one growing out of the other with no line "
                 "between them, and its tail, wing, fan, fin and shell all the same creature's.")   # the self-test's


def fused_errors(idn, fused):
    """What is wrong with a record's `fused` line, as sentences (none: []). It is the pilot's two animals written as
    ONE creature (the docstring's ONE CREATURE), so it names both, and it does not hand the head to one animal and
    what is behind it to the other, which is the collage it replaces."""
    out = []
    for x in (idn["a"], idn["b"]):
        if not re.search(r"\b%s" % re.escape(x.split()[-1].lower()), fused.lower()):
            out.append(f"{idn['word']}: the fused line never says {x.split()[-1].lower()}: it is both animals as one "
                       f"creature")
    m = FUSED_NOT.search(fused)
    if m:
        out.append(f"{idn['word']}: the fused line says {m.group(0)!r}: that hands a part to each animal, which is "
                   f"the collage a fused line replaces")
    piece = FUSED_PIECE.get(idn["kind"])
    if piece and not re.search(r"\b%s" % piece, fused.lower()):
        out.append(f"{idn['word']}: the fused line never says {piece}: it tells the whole creature, the "
                   f"{idn['b'].lower()}'s {idn['kind']} and where it grows from included")
    if not fused.rstrip().endswith("."):
        out.append(f"{idn['word']}: the fused line ends in a full stop")
    return out


def painted_as_one(fused, painted, name, patched=False):
    """Whether the brief a scene was painted from is this pilot's one-creature brief. It holds the record's `fused`
    line word for word; or, when a patch has since redrawn the creature (its note lies beside its mask, and the
    fused line now follows the picture), it still opens as that pilot's one creature (ONE)."""
    return bool(fused) and (fused in painted or (patched and ONE.replace("{name}", name) in painted))


def who(word, seen=(), swap=(), fused=""):
    """The identity paragraph of a scene: the person, in the words their avatar was painted from; or, when the
    record has a `fused` line, as the one creature that line describes."""
    i = as_seen(identity(word), swap)
    al, bl = i["a"].lower(), i["b"].lower()
    opening = (f"The subject is {i['name']}, a real living person in a world where people like this are ordinary: "
               f"{al} and {bl} and human in one body")
    if fused:
        problems = fused_errors(i, fused)
        if problems:
            raise ValueError("; ".join(problems))
        lines = [f"{opening}: {ONE.replace('{name}', i['name'])}. {fused}"]
    else:
        lines = [f"{opening}. {i['a'].upper()} is the face and the shape of the head: {i['face']}. "
                 f"{i['b'].upper()} is the {i['part']} and everything behind the face: {i['coat']}."]
    lines.append(f"{i['eyes'][0].upper()}{i['eyes'][1:]}. {i['hair']}")
    if seen:
        lines.append("In detail: " + "; ".join(seen) + ".")
    return " ".join(lines)


def shot_of(rec, name):
    """The camera paragraph: the record's own shot, then what every shot shares (FRAMING) unless the record frames
    itself (frame="")."""
    frame = rec.get("frame", FRAMING)
    if not frame:
        return rec["shot"]
    if rec.get("side") not in SIDES:
        raise ValueError(f"the record says side={rec.get('side')!r}: one of {', '.join(SIDES)}")
    return rec["shot"] + " " + frame.replace("{name}", name).replace("{side}", rec["side"])


def compose(word, _rec=None, **over):
    """The scene brief, verbatim what the painter gets, from the pilot's record (scenes/<WORD>.py)."""
    i = identity(word)
    rec = dict(record(i["word"]) or {} if _rec is None else _rec, **over)
    missing = [k for k in SCENE_KEYS if not rec.get(k)]
    if missing:
        raise ValueError(f"{i['word']} has no scene record: give {', '.join(missing)} "
                         f"(Tools/Build/pilot_art/scenes/{i['word']}.py)")
    i = as_seen(i, rec.get("swap", ()))
    name, bl = i["name"], i["b"].lower()
    for what in ("doing", "wear"):
        m = PERMANENT.search(rec[what])
        if m and not PERMANENT.search(i["hand"] + " " + " ".join(rec["seen"])):
            raise ValueError(f"{i['word']}'s {what} says {m.group(0)!r}: a scene adds nothing permanent that the "
                             f"pilot does not have")
    build = rec.get("build") or BUILD.get(i["a"], BUILD_DEFAULT)
    hands = rec.get("hands") or hands_of(i)
    lower = rec.get("lower") or LOWER[i["faction"]]
    second = ""
    if rec.get("fused") and rec.get("carry"):
        raise ValueError(f"{i['word']}: a record with a fused line tells the whole creature there, where its "
                         f"{i['kind'] or 'second piece'} grows from included: it gives no carry")
    if i["second"] and not rec.get("fused"):
        carry = rec.get("carry") or CARRY[i["kind"]].replace("{bl}", bl)
        second = f" The {bl} also gives {i['second']}: {carry}."
    motif = f", and {i['motif']}" if i["motif"] else ""
    place, finish = place_of(rec, i["accent"])
    text = (
        f"{shot_of(rec, name)}\n\n"
        f"{who(i['word'], rec['seen'], rec.get('swap', ()), rec.get('fused', ''))}\n\n"
        f"From the neck down {name} is built like a person and stands, sits and moves like one: {build}. {hands} "
        f"The clothes cover the body from the collar to the wrists and the ankles, and no bare human skin shows "
        f"between them.{second}\n\n"
        f"{name}'s clothes, seen whole and lived in: {i['outfit']}, piped in {i['accent']} ({i['hex']}), the piping "
        f"a thin cord that glows faintly{motif}; below it, {lower}. {rec['wear']}\n\n"
        f"{rec['doing']}\n\n"
        f"{place} A few tiny faceted crystal grains drift in the air like dust in a sunbeam. {rec['light']}\n\n"
        + finish.replace("{name}", name))
    # The emblem gate reads the scene's own words. The animals' anatomy lines were painted before, as they stand
    # ("a casque rising from the back of the skull" is a chameleon's head, not a skull on a sleeve).
    own = text
    for k in ("face", "coat", "second"):
        own = own.replace(i[k], "") if i[k] else own
    for gate, what, of in ((bust.world_name_in, "names the world", text), (bust.emblem_word, "invites an emblem", own),
                           (lambda t: (STYLE_WORDS.search(t) or [None])[0], "pulls back to a painting", text)):
        hit = gate(of)
        if hit:
            raise ValueError(f"{i['word']}'s scene brief {what}: {hit!r}")
    return text


# ---------------------------------------------------------------------------------------------------------------
# The painted scene, its brief, its close-up.
# ---------------------------------------------------------------------------------------------------------------
def scene_path(word):
    return os.path.join(ROOT, SCENES, f"{word}.jpg")


def painted_brief_path(word):
    """The brief a scene was painted from, kept beside it: the kit moves on, the painting does not."""
    return os.path.join(ROOT, SCENES, "briefs", f"{word}.txt")


def close_of(word):
    """The avatar's square in a pilot's painted scene (left, top, right, bottom), or None while the scene is not
    painted or its record has no `close` yet."""
    rec = record(word)
    if not rec or not rec.get("close") or not os.path.exists(scene_path(word)):
        return None
    return tuple(rec["close"])


def close_errors(word, size=None):
    """What is wrong with a painted scene's `close` box, as sentences (none: [])."""
    rec = record(word) or {}
    box = rec.get("close")
    if size is None:
        from PIL import Image
        size = Image.open(scene_path(word)).size
    if not box:
        return [f"{word}: the painted scene has no `close` box, so the pilot has no avatar from it"]
    if len(box) != 4 or not (0 <= box[0] < box[2] <= size[0] and 0 <= box[1] < box[3] <= size[1]):
        return [f"{word}: the `close` box {tuple(box)} is not inside the scene {size}"]
    out = []
    if box[2] - box[0] != box[3] - box[1]:
        out.append(f"{word}: the `close` box is not square")
    if box[2] - box[0] < AVATAR:
        out.append(f"{word}: the `close` box is {box[2] - box[0]} px wide, and an avatar is {AVATAR}: the head is "
                   f"too small in this scene")
    return out


def avatar_source(word):
    """Where a pilot's avatar is cut from: ("scene", path, close box) once their scene is painted and its close-up is
    set; ("bust", path, None) while they only have a bust; None with neither."""
    box = close_of(word)
    if box:
        return ("scene", scene_path(word), box)
    if os.path.exists(creator.bust_path(word)):
        return ("bust", creator.bust_path(word), None)
    return None


def avatar_image(word):
    """A pilot's avatar before it is sized, a square picture: their scene's close-up, else their bust's cut, else
    None. The blind read (blind.py) reads these."""
    from PIL import Image
    src = avatar_source(word)
    if not src:
        return None
    im = Image.open(src[1]).convert("RGB")
    return im.crop(src[2]) if src[0] == "scene" else creator.avatar_cut(im)


def avatar_sources():
    """{word: (the scene's path from the repository root, its close box)} for every pilot whose avatar is cut from
    their scene. Tools/Build/author_pilot_avatar_assets.py reads this."""
    out = {}
    for w in records():
        box = close_of(w)
        if box:
            out[w] = (f"{SCENES}/{w}.jpg", box)
    return out


def stale():
    """[(word, why)] for the records today's kit would paint differently, or that are not painted."""
    out = []
    for w in records():
        if not os.path.exists(scene_path(w)):
            out.append((w, "not painted"))
        elif not os.path.exists(painted_brief_path(w)):
            out.append((w, "no brief on record"))
        elif open(painted_brief_path(w), encoding="utf-8").read().strip() != compose(w).strip():
            out.append((w, "painted from an older brief"))
    return out


def wide(size):
    """Is a picture the wide frame (16:9 to within a row or two of pixels, and at least FRAME)?"""
    w, h = size
    return w >= FRAME[0] and abs(w * 9 - h * 16) <= 16 * 2


def capture(word, url, **over):
    """Download a painted scene and keep the brief it was painted from beside it. A pilot who never had a bust also
    gets the bust brief of today's tables frozen (identity_brief_path), so later pictures start from the same words."""
    i = identity(word)
    w = i["word"]
    text = compose(w, **over)
    froze = None
    if identity_text(w)[1] is None:
        froze = creator.brief_of(w)
    out = scene_path(w)
    os.makedirs(os.path.dirname(painted_brief_path(w)), exist_ok=True)
    raw = out + ".download"
    subprocess.run(["curl", "-sS", "-f", "-L", "-o", raw, url], check=True)
    from PIL import Image
    im = Image.open(raw).convert("RGB")
    os.remove(raw)
    if not wide(im.size):
        raise SystemExit(f"{url} is {im.size}: a scene is the wide frame, {FRAME[0]} x {FRAME[1]} (ask the painter "
                         f"for {ASK[0]} x {ASK[1]})")
    im.save(out, quality=93, subsampling=0, optimize=True)
    with open(painted_brief_path(w), "w", encoding="utf-8") as f:
        f.write(text + "\n")
    if froze:
        os.makedirs(os.path.dirname(identity_brief_path(w)), exist_ok=True)
        with open(identity_brief_path(w), "w", encoding="utf-8") as f:
            f.write(froze + "\n")
    print(f"captured {out} {im.size}")
    if close_of(w) is None or close_errors(w, im.size):
        print(f"set its close box next: scene.py grid {w} <out.jpg>, then close=(left, top, right, bottom) in "
              f"Tools/Build/pilot_art/scenes/{w}.py")
    return out


def _font(size, bold=False):
    from PIL import ImageFont
    name = "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"
    try:
        return ImageFont.truetype(os.path.join("/usr/share/fonts/truetype/dejavu", name), size)
    except OSError:
        return ImageFont.load_default()


def grid(src, out, step=96):
    """A picture (a pilot's scene, or any file) under a ruled grid with its pixel coordinates, for setting `close`
    by eye. The record's close box, when it has one, is drawn too."""
    from PIL import Image, ImageDraw
    rec = record(src) if not os.path.exists(src) else None
    path = scene_path(src) if not os.path.exists(src) else src
    im = Image.open(path).convert("RGB")
    d = ImageDraw.Draw(im)
    f = _font(15, True)
    for x in range(0, im.width, step):
        d.line((x, 0, x, im.height), fill=(255, 235, 0), width=1)
        d.text((x + 3, 2), str(x), fill=(255, 235, 0), font=f, stroke_width=2, stroke_fill=(0, 0, 0))
    for y in range(step, im.height, step):
        d.line((0, y, im.width, y), fill=(255, 235, 0), width=1)
        d.text((3, y + 2), str(y), fill=(255, 235, 0), font=f, stroke_width=2, stroke_fill=(0, 0, 0))
    if rec and rec.get("close"):
        d.rectangle(tuple(rec["close"]), outline=(255, 0, 200), width=3)
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    im.save(out, quality=88)
    print(f"grid {out} {im.size}")


# ---------------------------------------------------------------------------------------------------------------
# Other presentations: a scene is fullscreen art that narrower and squarer frames are cut from.
# ---------------------------------------------------------------------------------------------------------------
CROPS = (("16:9", 16 / 9), ("4:3", 4 / 3), ("1:1", 1.0), ("4:5", 4 / 5), ("9:16", 9 / 16))


def crop_box(size, close, aspect):
    """The largest box of an aspect (width over height) inside a scene, placed to hold the pilot: centred on the
    head from side to side, with the head a little above the middle. Returns (left, top, right, bottom)."""
    W, H = size
    if W / H > aspect:
        w, h = min(W, round(H * aspect)), H
    else:
        w, h = W, min(H, round(W / aspect))
    cx, cy = (close[0] + close[2]) / 2, (close[1] + close[3]) / 2
    left = int(min(max(round(cx - w / 2), 0), W - w))
    top = int(min(max(round(cy - h * 0.4), 0), H - h))
    return (left, top, left + w, top + h)


def side_of(size, close):
    """Which side of the frame the pilot stands on, measured from the painting: left, right or centre."""
    x = (close[0] + close[2]) / 2 / size[0]
    return "left" if x < 0.45 else "right" if x > 0.55 else "centre"


def manifest():
    """What a screen needs to place a scene, for every pilot whose scene is painted and has its close box."""
    from PIL import Image
    scenes = {}
    for w, rec in records().items():
        box = close_of(w)
        if not box:
            continue
        size = Image.open(scene_path(w)).size
        i = identity(w)
        scenes[w] = dict(name=i["name"], file=f"{SCENES}/{w}.jpg", size=list(size), close=list(box),
                         side=side_of(size, box), treatment=rec["treatment"], source=rec["source"],
                         crops={name: list(crop_box(size, box, a)) for name, a in CROPS})
        if rec.get("chart"):
            scenes[w]["chart"] = " of the ".join(rec["chart"])   # e.g. "panel of the waters": the sea is in this one
    return dict(about="Pilot scenes: one widescreen picture a pilot. close is the avatar's square (the head, with "
                      "the second animal's nearest piece); side "
                      "is where the pilot stands, so text goes on the other side; crops are the largest box of "
                      "each aspect that keeps the pilot; chart, on a few, says the scene shows the sea on a display. "
                      "Written by Tools/Build/pilot_art/scene.py manifest.",
                frame=list(FRAME), scenes=scenes)


MANIFEST = f"{SCENES}/scenes.json"


def manifest_text():
    text = json.dumps(manifest(), indent=1, ensure_ascii=False)
    # a row of numbers reads better on one line: [490, 155, 860, 525]
    return re.sub(r"\[\s+((?:-?\d+,\s+)*-?\d+)\s+\]", lambda m: "[" + re.sub(r"\s+", " ", m.group(1)) + "]", text) + "\n"


def manifest_errors():
    path = os.path.join(ROOT, MANIFEST)
    if not os.path.exists(path):
        return [f"{MANIFEST} is missing (scene.py manifest)"] if avatar_sources() else []
    with open(path, encoding="utf-8") as fh:
        if fh.read() != manifest_text():
            return [f"{MANIFEST} drifted from the records and the paintings (scene.py manifest)"]
    return []


# ---------------------------------------------------------------------------------------------------------------
# Sheets, for looking.
# ---------------------------------------------------------------------------------------------------------------
def _wrap(d, text, font, width):
    lines, line = [], ""
    for word_ in text.split():
        if line and d.textlength(f"{line} {word_}", font=font) > width:
            lines.append(line)
            line = word_
        else:
            line = f"{line} {word_}".strip()
    return lines + [line]


def sheet(out, words=None, per_page=10):
    """Review sheets: each painted scene with its avatar (the close-up at 256, 128 and 64 px) and a line saying who
    it is and where the scene comes from; ten scenes a page (out.png, then out-2.png and so on). Returns the files."""
    from PIL import Image, ImageDraw
    want = {w.lower() for w in words or ()}
    rows = [(w, rec) for w, rec in records().items()
            if os.path.exists(scene_path(w)) and (not want or w.lower() in want)]
    if not rows:
        raise SystemExit("no painted scene to put on a sheet")
    plate, ink, dim = (20, 23, 43), (236, 238, 246), (160, 166, 190)
    SW, SH, AV, M, CAP = 960, 540, 256, 32, 78
    cell_w, cell_h = SW + 16 + AV, CAP + SH
    cols = 2 if len(rows) > 1 else 1
    files = []
    stem, ext = os.path.splitext(out)
    for page in range((len(rows) + per_page - 1) // per_page):
        part = rows[page * per_page:(page + 1) * per_page]
        lines_n = (len(part) + cols - 1) // cols
        im = Image.new("RGB", (M + cols * (cell_w + M), M + lines_n * (cell_h + M)), plate)
        d = ImageDraw.Draw(im)
        for k, (w, rec) in enumerate(part):
            i = identity(w)
            x, y = M + (k % cols) * (cell_w + M), M + (k // cols) * (cell_h + M)
            who_ = i["name"] if i["name"] == w else f"{i['name']} ({w})"
            d.text((x, y), f"{who_}: {i['a'].lower()} and {i['b'].lower()}", fill=ink, font=_font(26, True))
            cap = _wrap(d, f"{rec['treatment']}. From their {rec['source']}", _font(17), cell_w)
            for n, line in enumerate(cap[:2]):
                d.text((x, y + 34 + 21 * n), line, fill=dim, font=_font(17))
            scene = Image.open(scene_path(w)).convert("RGB")
            im.paste(scene.resize((SW, SH), Image.LANCZOS), (x, y + CAP))
            box = close_of(w)
            if box:
                av = scene.crop(box)
                ax = x + SW + 16
                im.paste(av.resize((AV, AV), Image.LANCZOS), (ax, y + CAP))
                im.paste(creator.octagon(av, 128), (ax, y + CAP + AV + 16))
                im.paste(av.resize((64, 64), Image.LANCZOS), (ax + 128 + 16, y + CAP + AV + 16))
                im.paste(av.resize((44, 44), Image.LANCZOS), (ax + 128 + 16, y + CAP + AV + 16 + 64 + 12))
        name = out if page == 0 else f"{stem}-{page + 1}{ext}"
        os.makedirs(os.path.dirname(os.path.abspath(name)), exist_ok=True)
        im.save(name)
        files.append(name)
        print(f"sheet {name} {im.size}: {len(part)} scenes")
    return files


def crops_sheet(out, words=None):
    """One row a pilot: the scene whole, then cut to each aspect of CROPS around the pilot, then the avatar."""
    from PIL import Image, ImageDraw
    want = {w.lower() for w in words or ()}
    rows = [w for w in avatar_sources() if not want or w.lower() in want]
    if not rows:
        raise SystemExit("no painted scene with a close box to cut")
    plate, ink = (20, 23, 43), (236, 238, 246)
    H, M = 360, 24
    widths = [round(H * a) for _, a in CROPS] + [H]
    im = Image.new("RGB", (M + sum(w + M for w in widths), M + len(rows) * (H + 40 + M)), plate)
    d = ImageDraw.Draw(im)
    for r, w in enumerate(rows):
        scene = Image.open(scene_path(w)).convert("RGB")
        box = close_of(w)
        y = M + r * (H + 40 + M)
        x = M
        for (name, a), width in zip(CROPS, widths):
            cut = crop_box(scene.size, box, a)
            d.text((x, y), f"{w}  {name}  {cut[2] - cut[0]} x {cut[3] - cut[1]}", fill=ink, font=_font(18, True))
            im.paste(scene.crop(cut).resize((width, H), Image.LANCZOS), (x, y + 30))
            x += width + M
        d.text((x, y), f"avatar  {box[2] - box[0]} px", fill=ink, font=_font(18, True))
        im.paste(scene.crop(box).resize((H, H), Image.LANCZOS), (x, y + 30))
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    im.save(out)
    print(f"crops {out} {im.size}: {len(rows)} scenes")


def draft(word):
    """What whoever writes a new scene needs in front of them: the pilot's facts, the body rules that will be
    applied, what the other records already use, and an empty record to fill."""
    i = identity(word)
    w = i["word"]
    lines = [f"# {w}: {i['name']}, {i['a'].lower()} and {i['b'].lower()}, a {brief.faction_words(i['faction'])}"
             + (f", callsign {i['callsign']}" if i["callsign"] else "")]
    look = i["look"]
    if look.get("expression"):
        lines.append(f"# mood: {look['expression']}")
    if look.get("backdrop") in bust.BACKDROPS:
        lines.append(f"# the place their bust stood in: {bust.BACKDROPS[look['backdrop']][3]}")
    lines += [f"# face: {i['face']}", f"# {i['part']}, and behind the face: {i['coat']}"]
    if i["second"]:
        lines.append(f"# the {i['b'].lower()} also gives: {i['second']} ({CARRY[i['kind']].replace('{bl}', i['b'].lower())})")
    lines += [f"# eyes: {i['eyes']}", f"# hair: {i['hair']}", f"# clothes: {i['outfit']}"
              + (f"; {i['motif']}" if i["motif"] else ""), f"# below: {LOWER[i['faction']]}",
              f"# build: {BUILD.get(i['a'], BUILD_DEFAULT)}", f"# hands: {hands_of(i)}", "#",
              "# Treatments taken (yours is a new one):"]
    for other, rec in records().items():
        if other != w:
            lines.append(f"#   {other}: {rec['treatment']} | {rec['source']}")
    lines += ["#", "# Sources: profession: | hobby: (the game's Quirks) | history: (its Upbringings) | want: (its Wants).",
              "# `creator.py roll` prints the lines a rolled pilot already has.",
              f"# If the face and coat lines above read as a {i['a'].lower()}'s head with {i['b'].lower()} parts "
              "added, write a",
              "# `fused=` line: the two animals as ONE creature (it replaces both lines; the skill's \"One creature\").",
              "",
              f'"""{i["name"]}' + (f", {w}" if i["name"] != w else "") + f": {i['a'].lower()} and {i['b'].lower()}, "
              f"a {brief.faction_words(i['faction'])}.\"\"\"",
              "SCENE = dict(", '    treatment="",', '    shot=(""),', '    side="left",', '    source="profession: ",',
              "    seen=(),", '    doing=(""),', '    place=(""),', '    light=(""),', '    wear=(""),', ")"]
    return "\n".join(lines)


# ---------------------------------------------------------------------------------------------------------------
# The sweep: what does someone who was told nothing see in each finished scene?
# ---------------------------------------------------------------------------------------------------------------
def _md5(path):
    with open(path, "rb") as fh:
        return hashlib.md5(fh.read()).hexdigest()


def sweep_key_path(out):
    """The key's path: beside the readers' folder, never in it."""
    return os.path.abspath(out) + ".key.json"


def _in_repo(path):
    return os.path.realpath(path).startswith(os.path.realpath(ROOT) + os.sep)


HALVES = ((0, 0, 864, 864), (672, 0, 1536, 864))   # a FRAME's two squares, overlapping by 192 px
TILE = 1072


def sweep_prep(out, words=None, salt=None):
    """Write the readers' pictures into out/ and the key beside it; returns the key. Every painted scene that has a
    close box (or the words given) gets a number in an order shuffled by the folder's name (or `salt`): sNN.jpg the
    whole frame, sNN_close.jpg the avatar's square, sNN_left.jpg and sNN_right.jpg its halves enlarged. A folder in
    the repository, or one that already holds a read, is refused."""
    from PIL import Image
    out = os.path.realpath(out)
    if _in_repo(out) or _in_repo(sweep_key_path(out)):
        raise ValueError("a sweep's pictures and key are scratch: give a folder outside the repository")
    if os.path.exists(os.path.join(out, "result.json")):
        raise ValueError(f"{out} already holds a read (result.json): prep a new folder, so a read stays with the "
                         f"pictures it was made on")
    salt = salt or os.path.basename(out)
    have = avatar_sources()
    want = {w.lower() for w in words or ()}
    strangers = sorted(want - {w.lower() for w in have})
    if strangers:
        raise ValueError(f"no painted scene with a close box: {', '.join(strangers)}")
    painted = sorted((w for w in have if not want or w.lower() in want),
                     key=lambda w: hashlib.md5(f"{salt}/scene/{w}".encode("utf-8")).hexdigest())
    if not painted:
        raise ValueError("no painted scene to read")
    os.makedirs(out, exist_ok=True)
    for name in os.listdir(out):   # the pictures of an earlier shuffle
        if re.fullmatch(r"s\d+(?:_close|_left|_right)?\.jpg", name):
            os.remove(os.path.join(out, name))
    key = {}
    for n, w in enumerate(painted, 1):
        sid = f"s{n:02d}"
        path = scene_path(w)
        im = Image.open(path).convert("RGB")
        if im.size != FRAME:
            im = im.resize(FRAME, Image.LANCZOS)
            box = tuple(round(v * FRAME[0] / Image.open(path).size[0]) for v in have[w][1])
        else:
            box = have[w][1]
        im.save(os.path.join(out, f"{sid}.jpg"), quality=92)
        im.crop(box).resize((768, 768), Image.LANCZOS).save(os.path.join(out, f"{sid}_close.jpg"), quality=92)
        for half, tag in zip(HALVES, ("left", "right")):
            im.crop(half).resize((TILE, TILE), Image.LANCZOS).save(os.path.join(out, f"{sid}_{tag}.jpg"), quality=92)
        i = identity(w)
        key[sid] = dict(word=w, face=i["a"], crown=i["b"], scene_md5=_md5(path),
                        chart=list((record(w) or {}).get("chart") or ()))
    with open(sweep_key_path(out), "w", encoding="utf-8") as fh:
        json.dump(key, fh, indent=1)
    return key


PHOTO_KINDS = ("photograph", "film still")
CHART_SEEN = re.compile(r"\b(?:galax\w*|spiral|star ?(?:map|chart|field)s?|maps?|charts?|nebula\w*|sea chart|"
                        r"navigation\w*|constellation\w*)\b", re.I)   # a reader's words for the sea on a display
PLAY_SEEN = re.compile(r"\b(?:hologra\w*|projection\w*|projected|miniature|model|trails?|bubble|sphere|orb|"
                       r"lattice|creatures?|craft|ships?|rings?)\b", re.I)   # and for play in a hologram with depth
NOTHING = re.compile(r"\s*(?:no|none|nothing|not any|n/a)\b", re.I)   # a reader's "none seen" is not a report
LISTS = ("lettering_or_symbols", "anatomy_problems", "bare_human_skin", "permanent_marks", "realism_breaks")


def _reported(entries):
    if isinstance(entries, str):
        entries = [entries]
    return [str(e) for e in entries or () if str(e).strip() and not NOTHING.match(str(e))]


def sweep_grade(key, result):
    """Grade what scene_sweep.js returned against the key: (rows, tally). rows is [(id, word, [lines])], worst
    first; tally the counts the docs quote. An answer for an id the key does not have is counted and left out."""
    import blind
    tally = collections.Counter()
    by_id = collections.defaultdict(list)
    for reader in result.get("readers") or ():
        for s in (reader or {}).get("scenes") or ():
            sid = str(s.get("id", "")).strip().lower().replace(".jpg", "")
            if sid in key:
                by_id[sid].append(s)
            else:
                tally["answers for an id that is not in the key"] += 1
    rows = []
    for sid in sorted(key):
        k = key[sid]
        lines, score = [], 0
        if not by_id[sid]:
            tally["scenes no reader answered for"] += 1
            lines.append("NO READER ANSWERED")
            score += 100
        for s in by_id[sid]:
            tally["answers"] += 1
            said = [str(x) for x in [s.get("face_animal"), s.get("second_animal")] + list(s.get("other_animals") or ())
                    if x and not NOTHING.match(str(x))]
            f_lv, c_lv = blind.read_levels(k["face"], k["crown"], said)
            face, crown = (("no", "its kind", "named")[lv] for lv in (f_lv, c_lv))
            both = f_lv == blind.SPECIES and c_lv == blind.SPECIES
            kinds = bool(f_lv and c_lv)
            photo = s.get("kind_of_image") in PHOTO_KINDS
            figures = int(s.get("figures_visible") or 1)
            tally["both animals named"] += both
            tally["both animals named, at least by their kind"] += kinds
            tally["called a photograph or a film still"] += photo
            tally["more than one figure"] += figures > 1
            tally["face not easy to read"] += s.get("face_readable") is False
            score += (not kinds) * 4 + (not photo) * 3 + (figures > 1) * 5 + (s.get("face_readable") is False) * 3
            shown = "; ".join(_reported(s.get("displays")))
            if k.get("chart"):
                saw = bool((PLAY_SEEN if k["chart"][0] == "volume" else CHART_SEEN).search(shown))
                tally["chart scenes read"] += 1
                tally["chart scenes where the reader saw the chart"] += saw
                score += (not saw) * 3
                lines_chart = f"    the {k['chart'][0]} ({k['chart'][1]}): " + (shown or "NOT SEEN")
            else:
                lines_chart = f"    a display nobody asked for: {shown}" if shown else ""
            lines.append(f"saw {s.get('face_animal')} and {s.get('second_animal')} "
                         f"({k['face']}: {face}; {k['crown']}: {crown}); a {s.get('kind_of_image')} "
                         f"({s.get('kind_why', '')}); {figures} figure(s); doing: {s.get('doing', '')}")
            if lines_chart:
                lines.append(lines_chart)
            for what in LISTS:
                hits = _reported(s.get(what))
                tally[f"answers listing {what.replace('_', ' ')}"] += bool(hits)
                if hits:
                    score += 2 if what != "realism_breaks" else 1
                    lines.append(f"    {what.replace('_', ' ')}: " + "; ".join(hits))
        rows.append((score, sid, k["word"], lines))
    rows.sort(key=lambda r: (-r[0], r[1]))
    return [(sid, word, lines) for _, sid, word, lines in rows], tally


def sweep_notes(key):
    """What changed since the sweep was prepared: scenes repainted since (the read is of the earlier pictures)."""
    return [f"{sid} ({k['word']}) was repainted since this read" for sid, k in sorted(key.items())
            if not os.path.exists(scene_path(k["word"])) or _md5(scene_path(k["word"])) != k["scene_md5"]]


def print_sweep(out):
    with open(sweep_key_path(out), encoding="utf-8") as fh:
        key = json.load(fh)
    with open(os.path.join(out, "result.json"), encoding="utf-8") as fh:
        result = json.load(fh)
    rows, tally = sweep_grade(key, result)
    for sid, word, lines in rows:
        print(f"{sid}  {word}")
        for line in lines:
            print("   " + line)
    n = tally["answers"]
    print(f"scenes read                                   {len(rows)} ({n} answers)")
    for what in ("both animals named", "both animals named, at least by their kind",
                 "called a photograph or a film still", "more than one figure", "face not easy to read"):
        print(f"{what:45} {tally[what]} of {n}")
    if tally["chart scenes read"]:
        print(f"{'the chart seen, where a scene has one':45} {tally['chart scenes where the reader saw the chart']} "
              f"of {tally['chart scenes read']}")
    for what in LISTS:
        label = f"answers listing {what.replace('_', ' ')}"
        print(f"{label:45} {tally[label]} of {n} (read the lines)")
    for what in ("scenes no reader answered for", "answers for an id that is not in the key"):
        if tally[what]:
            print(f"WARNING: {what}: {tally[what]}")
    for note in sweep_notes(key):
        print("NOTE:", note)
    return 0


# ---------------------------------------------------------------------------------------------------------------
# Self-test.
# ---------------------------------------------------------------------------------------------------------------
def self_test():
    errors = []
    words = [w for w, *_ in creator.catalog()]

    # 1. Every pilot's bust brief still reads, and what is cut from it is in it, word for word.
    for w in words:
        try:
            i = identity(w)
        except (ValueError, SystemExit) as e:
            errors.append(str(e))
            continue
        text = identity_text(w)[0]
        for k in ("face", "coat", "second", "hair", "outfit", "motif"):
            if i[k] and i[k] not in text:
                errors.append(f"{w}: the {k} line is not the painted brief's own words")
        if GAZE in i["eyes"] or "looking at the viewer" in i["eyes"]:
            errors.append(f"{w}: the eyes line still carries the bust's gaze")
        if bool(i["second"]) != bool(i["kind"]):
            errors.append(f"{w}: the second piece and its kind disagree")
        try:
            hands = hands_of(i)
        except ValueError as e:
            errors.append(str(e))
            continue
        if bust.NOT_A_HAND.search(hands):
            errors.append(f"{w}: the scene's hands are not hands ({bust.NOT_A_HAND.search(hands).group(0)})")
        try:
            who(w)
        except Exception as e:   # noqa: BLE001
            errors.append(f"{w}: who() raised {e!r}")
    # ... and so does the brief TODAY's tables write, which is where a pilot who never had a bust starts
    for w, person, animals, faction, callsign, look in creator.catalog():
        if not PAINTED.search(bust.compose(animals, faction, person, callsign, **look)):
            errors.append(f"{w}: scene.PAINTED no longer reads the brief bust.compose writes today")
            break

    # 2. The tables cover every faction type and every kind of second piece, and name nothing they must not.
    for f in bust.GEAR:
        for table, what in ((GLOVES, "GLOVES"), (LOWER, "LOWER")):
            if f not in table:
                errors.append(f"{what} has no line for {f}")
    for kind in {k for k, _ in bust.SECOND.values()}:
        if kind not in CARRY:
            errors.append(f"CARRY has no line for a {kind}")
    for a in BUILD:
        if a not in bust.FACE:
            errors.append(f"BUILD names {a}, which is not a face animal")
    for a in bust.FACE:
        if a not in BUILD:
            errors.append(f"BUILD has no line for a {a}'s body (a new face animal takes BUILD_DEFAULT until it has one)")
    texts = ([FINISH, FRAMING, BUILD_DEFAULT, CHART_RULE, PLAY_RULE, FINISH_CHART, FINISH_PLAY] + list(BUILD.values())
             + list(GLOVES.values()) + list(LOWER.values()) + list(CARRY.values()) + list(CHART_VIEWS.values())
             + list(PLAY_VIEWS.values()) + list(CHART_KINDS.values()))
    for t in texts:
        for gate, what in ((bust.world_name_in, "names the world"), (bust.emblem_word, "invites an emblem")):
            if gate(t):
                errors.append(f"a scene table line {what} ({gate(t)!r}): {t[:60]}...")
    if FINISH.count(FINISH_BLANK) != 1:
        errors.append("FINISH no longer has the words a chart's scene swaps (FINISH_BLANK)")
    for kind, line in CHART_KINDS.items():
        if line.count("{view}") != 1 or not line.rstrip().endswith("."):
            errors.append(f"CHART_KINDS[{kind!r}] takes its view once and ends in a full stop")
        if STYLE_WORDS.search(line):
            errors.append(f"CHART_KINDS[{kind!r}] pulls back to a painting")
    for view, line in list(CHART_VIEWS.items()) + list(PLAY_VIEWS.items()):
        if STYLE_WORDS.search(line):
            errors.append(f"the view {view!r} pulls back to a painting")
    for view, line in PLAY_VIEWS.items():
        if "{own}" not in line:
            errors.append(f"PLAY_VIEWS[{view!r}] gives no trail the pilot's own colour ({{own}})")
    if set(CHART_SHOWS) != set(CHART_KINDS) or set(CHART_NOUN) != set(CHART_KINDS):
        errors.append("CHART_SHOWS and CHART_NOUN name every kind of display, and no other")
    if not wide(FRAME) or not wide(ASK) or wide((1536, 1024)) or wide((1024, 576)):
        errors.append("wide() does not tell the wide frame from a 3:2 picture or a small one")

    # 3. Every record is a catalog pilot's, complete, names its source, composes, keeps the identity lines and
    #    passes the gates; no two share a treatment.
    recs = records()
    for name in sorted(os.listdir(RECORDS)):
        if name.endswith(".py") and name[:-3] not in words:
            errors.append(f"scenes/{name} is no catalog pilot's record (creator.py list)")
    taken = collections.Counter(rec.get("treatment", "").strip().lower() for rec in recs.values())
    for w, rec in recs.items():
        for k in SCENE_KEYS:
            if not rec.get(k):
                errors.append(f"{w}: the record has no {k}")
        for k in rec:
            if k not in SCENE_KEYS + SCENE_MORE:
                errors.append(f"{w}: the record has an unknown field {k}")
        if not re.match(r"(?:%s): \S" % "|".join(SOURCES), rec.get("source", "")):
            errors.append(f"{w}: the source starts with one of {', '.join(x + ':' for x in SOURCES)}")
        if taken[rec.get("treatment", "").strip().lower()] > 1:
            errors.append(f"{w}: another pilot has the treatment {rec.get('treatment')!r}: each scene is its own")
        if rec.get("frame", FRAMING) and rec.get("side") not in SIDES:
            errors.append(f"{w}: the record needs side= one of {', '.join(SIDES)}")
        noun = CHART_NOUN.get((rec.get("chart") or ("",))[0])
        if rec.get("chart") and noun and noun not in (rec.get("doing", "") + rec.get("light", "")).lower():
            errors.append(f"{w}: the scene has a {noun} that neither its doing nor its light mentions: a display "
                          f"belongs to what the pilot is doing or to the light, or it is set dressing")
        if noun == "display" and not ("display" in rec.get("doing", "").lower()
                                      and "display" in rec.get("light", "").lower()):
            errors.append(f"{w}: a display of play is looked into and lights the face: both the doing and the "
                          f"light say so (\"not aligned to his perspective\" was a display nobody looked at)")
        try:
            text = compose(w)
        except Exception as e:   # noqa: BLE001
            errors.append(f"{w} does not compose: {e}")
            continue
        i = as_seen(identity(w), rec.get("swap", ()))
        fused = rec.get("fused", "")
        if fused and (fused not in text or ONE.replace("{name}", i["name"]) not in text):
            errors.append(f"{w}: the scene brief lost the pilot's fused line")
        for k in ("outfit",) if fused else ("face", "coat", "outfit"):
            if i[k] not in text:
                errors.append(f"{w}: the scene brief lost the pilot's {k} line")
        if i["name"] not in text:
            errors.append(f"{w}: the scene brief does not name the pilot")
        if re.search(r"(?:\bthe|'s) portrait\b", text):
            errors.append(f"{w}: the brief points the painter at a portrait it cannot see")
        count = len(text.split()) - (len(chart_of(rec["chart"], i["accent"]).split()) if rec.get("chart") else 0)
        if count > WORD_CAP:
            errors.append(f"{w}: the scene brief is {count} words"
                          + (" beside its chart" if rec.get("chart") else "") + f" (the cap is {WORD_CAP})")
        if not os.path.exists(scene_path(w)):
            continue
        from PIL import Image
        size = Image.open(scene_path(w)).size
        if not wide(size):
            errors.append(f"{w}: the painted scene is {size}, not the wide frame")
        errors += close_errors(w, size)
        if not os.path.exists(painted_brief_path(w)):
            errors.append(f"{w}: the painted scene has no brief on record beside it")
        else:   # whatever the kit writes today, the painting's own brief carries the pilot's own words,
                # as the bust's brief had them or as the record swaps them for what the bust shows
            painted = open(painted_brief_path(w), encoding="utf-8").read()
            raw = identity(w)
            patched = os.path.exists(os.path.join(ROOT, SCENES, "patches", f"{w}.json"))   # refs.py apply's note
            as_one = painted_as_one(fused, painted, i["name"], patched)
            for k in ("face", "coat", "outfit"):
                if raw[k] not in painted and i[k] not in painted and not (k != "outfit" and as_one):
                    errors.append(f"{w}: the brief it was painted from lacks the pilot's {k} line")
    errors += manifest_errors()
    import refs   # the pictures a record shows a painter, and its patch: refs.py says what is wrong with them
    for w, rec in recs.items():
        errors += refs.record_errors(w, rec)
    charts = [w for w, rec in recs.items() if rec.get("chart")]
    cast = len(creator.catalog())   # of the whole cast, so the few may be written and painted first
    if len(charts) > max(1, int(cast * CHART_MOST + 0.999)):
        errors.append(f"{len(charts)} scenes of a cast of {cast} carry a chart of the sea: a few do, never most "
                      f"(CHART_MOST)")

    # 4. Negative controls: each gate fires on what it is for.
    base = recs.get("MANGER") or next(iter(recs.values()), None)
    who_ = "MANGER" if "MANGER" in recs else next(iter(recs), None)
    if base is None:
        errors.append("no record to test the gates on")
    else:
        def refuses(over, why):
            try:
                compose(who_, **over)
            except ValueError as e:
                return why in str(e)
            return False
        controls = (
            (dict(place="The hangar of the Ark, at night."), "names the world"),
            (dict(doing="Tamako sits cross-legged and waves a flag."), "invites an emblem"),
            (dict(wear="A long scar runs down the bill."), "nothing permanent"),
            (dict(wear="One chipped tusk and a muddy hem."), "nothing permanent"),
            (dict(place="A painterly marsh at dawn."), "pulls back to a painting"),
            (dict(swap=(("a phrase the bust's brief never had", "anything"),)), "is in none of the avatar's lines"),
            (dict(frame=FRAMING, side="middle"), "side="),
            (dict(chart=("panel", "waters")), "writes {chart} once"),
            (dict(place="A chart room. On the wall hangs {chart} Nothing else."), "has no chart="),
            (dict(chart=("window", "waters"), place="A chart room. On the wall hangs {chart}"), "give (kind, view)"),
            (dict(chart=("volume", "waters"), place="A chart room. On the wall hangs {chart}"), "give (kind, view)"),
            (dict(chart=("panel", "race"), place="A chart room. On the wall hangs {chart}"), "give (kind, view)"),
            (dict(chart=("panel", "waters"), place="The bridge of the HyperSea liner. There hangs {chart}"),
             "names the world"),
            (dict(fused=f"A creature with the {identity(who_)['a'].lower()}'s face and nothing else.", carry=""),
             "the fused line never says"),
            (dict(fused=f"The {identity(who_)['a'].lower()} owns the head and the {identity(who_)['b'].lower()} "
                        f"the rest, tail, wing, fan, fin and shell.", carry=""), "hands a part to each animal"),
            (dict(fused=FUSED_CONTROL.replace("{a}", identity(who_)["a"].lower())
                  .replace("{b}", identity(who_)["b"].lower()), carry="seen whole, it grows from the hip"),
             "it gives no carry"),
        )
        for over, why in controls:
            if not refuses(over, why):
                errors.append(f"the gate '{why}' did not fire on {over}")
        try:
            compose(who_, _rec={})
            errors.append("compose made a scene from an empty record")
        except ValueError:
            pass
        one = FUSED_CONTROL.replace("{a}", identity(who_)["a"].lower()).replace("{b}", identity(who_)["b"].lower())
        told = compose(who_, fused=one, carry="")
        raw = identity(who_)
        if (one not in told or ONE.replace("{name}", raw["name"]) not in told or raw["coat"] in told
                or " also gives " in told or "is the face and the shape of the head" in told):
            errors.append("a fused line does not replace the two animals' own sentences in the brief")
        later = one + " Its back has since been painted again."   # the fused line after a patch redrew the creature
        if (not painted_as_one(one, told, raw["name"]) or painted_as_one(later, told, raw["name"])
                or not painted_as_one(later, told, raw["name"], patched=True)
                or painted_as_one(later, compose(who_), raw["name"], patched=True)):
            errors.append("painted_as_one does not tell a patched creature's first brief from a brief that never "
                          "was the one creature's")
        if FUSED_CONTROL.count("{a}") < 1 or FUSED_CONTROL.count("{b}") < 1:
            errors.append("FUSED_CONTROL names both animals")
        framed = compose(who_, frame=FRAMING, side="right")
        if "a third of the way in from the right edge" not in framed or "{" in framed:
            errors.append("FRAMING does not reach the brief with its side and the pilot's name filled in")
        for kind, view in chart_pairs():
            charted = compose(who_, chart=(kind, view), place="A chart room. On the wall hangs {chart} A stool.")
            noun = CHART_NOUN[kind]
            whole = CHART_SHOWS[kind][view].split("{")[0]   # a view of play carries the pilot's colours after this
            if (whole not in charted or CHART_RULES[noun] not in charted or FINISH_LIT[noun] not in charted
                    or FINISH_BLANK in charted or "{" in charted):
                errors.append(f"a {kind} of the {view} does not reach the brief whole")
        mine = identity(who_)["accent"]
        if PLAY_VIEWS and f"small glowing {mine} blocks" not in compose(
                who_, chart=("volume", "siege"), place="A bridge. Over the well stands {chart} A stool."):
            errors.append("a view of play does not take the pilot's own colour for the pilot's side")
        if FINISH_BLANK not in compose(who_) or FINISH_CHART in compose(who_):
            errors.append("a scene without a chart lost FINISH's blank screens")

    # 5. Cuts for other presentations: each is the aspect asked for, inside the frame, and holds the head when a
    #    frame of that shape can.
    for close in ((360, 0, 800, 440), (1100, 300, 1400, 600), (0, 500, 300, 800), (640, 200, 896, 456)):
        for name, a in CROPS:
            l, t, r, b = crop_box(FRAME, close, a)
            if not (0 <= l < r <= FRAME[0] and 0 <= t < b <= FRAME[1]) or abs((r - l) / (b - t) - a) > 0.01:
                errors.append(f"crop_box gives {(l, t, r, b)} for {name} round {close}")
            elif r - l >= close[2] - close[0] and not (l <= close[0] and close[2] <= r):
                errors.append(f"the {name} cut round {close} loses part of the head: {(l, t, r, b)}")
    if (side_of(FRAME, (100, 0, 400, 300)), side_of(FRAME, (1100, 0, 1400, 300)),
            side_of(FRAME, (640, 0, 896, 256))) != ("left", "right", "centre"):
        errors.append("side_of does not tell left, right and centre apart")

    # 6. The sweep: prep shuffles into scratch with the key outside the readers' folder, and grade tells a clean
    #    answer from one that lists a second figure, lettering and the wrong animals.
    import shutil
    import tempfile
    have = sorted(avatar_sources())
    if have:
        tmp = tempfile.mkdtemp(prefix="scene_sweep_")
        try:
            out = os.path.join(tmp, "sweep")
            key = sweep_prep(out)
            if sorted(k["word"] for k in key.values()) != have:
                errors.append("sweep-prep did not number every painted scene once")
            if os.path.dirname(sweep_key_path(out)) != tmp or "key" in " ".join(os.listdir(out)):
                errors.append("the sweep key is inside the folder the readers open")
            if any(w.lower() in name.lower() for name in os.listdir(out) for w in words):
                errors.append("a file the readers open gives a pilot away by its name")
            if len(os.listdir(out)) != 4 * len(have):
                errors.append("sweep-prep did not write four pictures a scene")
            first = [k["word"] for _, k in sorted(key.items())]   # a few scenes can fall in one order by chance,
            if len(have) > 3 and all(                              # so three more folders are tried
                    [k["word"] for _, k in sorted(sweep_prep(os.path.join(tmp, other)).items())] == first
                    for other in ("another", "third", "fourth")):
                errors.append("sweeps in different folders share one shuffle")

            def answers(clean):
                return dict(readers=[dict(scenes=[dict(
                    id=sid, kind_of_image="photograph" if clean else "3D render", kind_why="",
                    face_animal=k["face"].lower() if clean else "a dragon",
                    second_animal=k["crown"].lower() if clean else "none", doing="works",
                    figures_visible=1 if clean else 2, face_readable=clean,
                    displays=["a glowing map of a spiral galaxy"] if clean else [],
                    lettering_or_symbols=["None seen"] if clean else ["a number on a crate"],
                    anatomy_problems=[] if clean else ["six fingers on the left hand"],
                    bare_human_skin=[], permanent_marks=[], realism_breaks=[]) for sid, k in key.items()])])
            n = len(key)
            charted = {sid: dict(k, chart=["hologram", "galaxy"]) for sid, k in key.items()}
            seen = [sweep_grade(charted, answers(c))[1]["chart scenes where the reader saw the chart"]
                    for c in (True, False)]
            if seen != [n, 0]:
                errors.append(f"sweep-grade does not tell a chart that was seen from one that was not: {seen}")
            _, tally = sweep_grade(key, answers(True))
            if (tally["both animals named"], tally["called a photograph or a film still"], tally["more than one figure"],
                    tally["answers listing lettering or symbols"], tally["face not easy to read"]) != (n, n, 0, 0, 0):
                errors.append(f"sweep-grade miscounts a clean read: {dict(tally)}")
            _, tally = sweep_grade(key, answers(False))
            if (tally["both animals named, at least by their kind"], tally["called a photograph or a film still"],
                    tally["more than one figure"], tally["answers listing lettering or symbols"],
                    tally["answers listing anatomy problems"], tally["face not easy to read"]) != (0, 0, n, n, n, n):
                errors.append(f"sweep-grade miscounts a bad read: {dict(tally)}")
            with open(os.path.join(out, "result.json"), "w", encoding="utf-8") as fh:
                fh.write("{}")
            for folder, what in ((out, "a folder that already holds a read"),
                                 (os.path.join(ROOT, SCENES, "_sweep"), "the repository")):
                try:
                    sweep_prep(folder)
                    errors.append(f"sweep-prep wrote into {what}")
                except ValueError:
                    pass
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    painted = sum(os.path.exists(scene_path(w)) for w in recs)
    if errors:
        print("scene.py self-test FAILED:")
        for e in errors:
            print("  -", e)
        return 1
    print(f"scene.py self-test: ok ({len(words)} pilots read, {len(recs)} records, {painted} painted, "
          f"{len(avatar_sources())} avatars cut from a scene)")
    return 0


def main():
    args = sys.argv[1:]
    if not args or args[0] in ("-h", "--help"):
        print(__doc__)
        return 0
    if args[0] == "--self-test":
        return self_test()
    if args[0] == "list":
        recs = records()
        sources = avatar_sources()
        for w, person, (a, b), *_ in creator.catalog():
            rec = recs.get(w)
            state = "-" if not rec else "avatar" if w in sources else "painted" if os.path.exists(scene_path(w)) \
                else "record"
            print(f"{w:10} {person:22} {a + '/' + b:26} {state:8} "
                  + (f"{rec['treatment']} | {rec['source']}" if rec else ""))
        print(f"{len(recs)} records, {sum(os.path.exists(scene_path(w)) for w in recs)} painted, {len(sources)} "
              f"avatars cut from a scene, of {len(creator.catalog())} pilots")
        return 0
    if args[0] == "who" and len(args) == 2:
        i = identity(args[1])
        rec = record(i["word"]) or {}
        print(who(args[1], rec.get("seen", ()), rec.get("swap", ()), rec.get("fused", "")))
        print(rec.get("hands") or hands_of(i))
        return 0
    if args[0] == "draft" and len(args) == 2:
        print(draft(args[1]))
        return 0
    if args[0] == "recast" and len(args) == 2:
        path = recast(args[1])
        print(f"froze today's words for {args[1]} in {os.path.relpath(path, ROOT)}: the record's seen and swap were "
              f"written against the old words, so read them again (scene.py who {args[1]})")
        return 0
    if args[0] == "brief" and len(args) == 2:
        print(compose(args[1]))
        return 0
    if args[0] == "stale":
        rows = stale()
        for key, why in rows:
            print(f"{key:20} {why}")
        print(f"{len(rows)} of {len(records())} scenes differ from what the kit would paint today")
        return 0
    if args[0] == "sheet" and len(args) >= 2:
        sheet(args[1], args[2:] or None)
        return 0
    if args[0] == "crops" and len(args) >= 2:
        crops_sheet(args[1], args[2:] or None)
        return 0
    if args[0] == "grid" and len(args) == 3:
        grid(args[1], args[2])
        return 0
    if args[0] == "capture" and len(args) == 3:
        capture(args[1], args[2])
        return 0
    if args[0] == "manifest":
        if "--check" in args:
            problems = manifest_errors()
            for e in problems:
                print("DRIFT", e)
            print("manifest:", "ok" if not problems else f"{len(problems)} problem(s)")
            return 1 if problems else 0
        with open(os.path.join(ROOT, MANIFEST), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(manifest_text())
        print(f"wrote {MANIFEST}: {len(avatar_sources())} scenes")
        return 0
    if args[0] == "sweep-prep" and len(args) >= 2:
        try:
            key = sweep_prep(args[1], args[2:] or None)
        except ValueError as e:
            print(f"scene.py sweep-prep: {e}")
            return 2
        ids = sorted(key)
        print(f"{len(key)} scenes in {os.path.realpath(args[1])}; the key is {sweep_key_path(args[1])}")
        print("readers: .claude/skills/pilot-creator/references/scene_sweep.js with args "
              f"{{\"dir\": \"{os.path.realpath(args[1])}\", \"ids\": {json.dumps(ids)}}}")
        return 0
    if args[0] == "sweep-grade" and len(args) == 2:
        return print_sweep(args[1])
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())
