#!/usr/bin/env python3
"""The SCENE brief: a pilot photographed in their world, the same person as their signature bust.

STATUS: THE STYLE IS NOT LOCKED. This file holds the mechanism and four samples, each a different approach, made for
Garrett to choose from. Until he does, the scene tables below cover what the samples need plus one line per faction
type, and a scene for any other pilot is a draft. `Docs/Pilots/PORTRAITS.md` section 8 says where it stands.

Garrett, 2026-11-11, after round 2 of the busts: "These also look like airbrushed glamour shots, which is fine for
avatar pictures. now lets see how realistic we can get when we take the same character from the avatar picture and make
the art needed for the ark. and elsewhere. make a full scene piece of art that show them in a hyper realistic setting.
they still have their scratches, imperfections, a tear in their clothing, some dirt on their knees, etc. they don't
need to have any of these things, but if they fit then it shouldn't look like a pristine commercial set, it should show
them raw in their environment doing something that is appropriate to their background, history, hobbies, profession,
etc." And the goal: "lock in this second style so for each new character the skill is equipped to make all the needed
assets and keep the character consistent across them."

How a scene keeps the character (the painter takes words only, never the avatar itself):

1. THE SAME WORDS. A scene's description of the person is cut from the very brief their avatar was painted from
   (`busts/briefs/<WORD>.txt`, `identity`), not from today's tables: the tables move on, the painting does not. The
   face, the crown, the coat behind the face, the second animal's piece, the eyes, the hair, the outfit, the callsign's
   mark and the accent colour reach the scene's painter word for word.
2. WHAT THE AVATAR SHOWS BEYOND ITS BRIEF (`seen`), AND AGAINST IT (`swap`). A painter decides things no brief asked
   for: the colour of a glove, that the brooch is brass, that the shell cap hides the ears. Whoever writes a scene
   LOOKS at the avatar first and writes those down, and the scene repeats them. And where the painting departs from
   the words it was painted from (the brief asked for hazel eyes and the painter gave amber-gold; "shaggy" wool was
   painted as tight curls), the record swaps that phrase for what the picture shows: a scene follows the painting,
   not the request. Two colour words for one eye let the painter choose, so a correction replaces, never adds.
3. THE REST OF THE BODY IS RULES, NOT ROLLS. Below the bust's crop nothing was ever decided, so it is decided once,
   here, the same way for every picture of a pilot: the build (BUILD), the hands (`hands_of`: the hand the avatar
   shows; else the animal's own true hand; else gloves), where the second animal's piece grows from (CARRY) and the
   lower half of the clothes (LOWER).
4. NOTHING PERMANENT IS ADDED. Wear is mud, soot, a tear, a mended seam, a scuff: things a day gives and a wash or a
   needle takes away. A scene never gives the face a scar, a chipped tusk or a missing ear that the avatar does not
   have (PERMANENT), because the next picture of that pilot would have to carry it.

What the person is DOING comes from who they are, and each scene names its source: their profession (the faction
type), a hobby (the game's own `Quirks` in PilotGenerator.cs) or their history (its `Upbringings`).

The brief goes to an outside painter, so it keeps bust.py's two gates: it never names the game, the studio, the world
or a place, a crew or a faction in it (`bust.world_name_in`; the home ship is "a great ship"), and it never invites an
emblem or lettering (`bust.emblem_word`).

    python3 Tools/Build/pilot_art/scene.py list                 # the samples and their approaches
    python3 Tools/Build/pilot_art/scene.py who <WORD>           # any avatar pilot's identity lines, as painted
    python3 Tools/Build/pilot_art/scene.py brief <WORD> [approach]
    python3 Tools/Build/pilot_art/scene.py capture <WORD> <url> [approach]
    python3 Tools/Build/pilot_art/scene.py sheet <out.png>       # each sample beside its avatar
    python3 Tools/Build/pilot_art/scene.py stale                 # scenes today's kit would paint differently
    python3 Tools/Build/pilot_art/scene.py --self-test
"""
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
# The four approaches (samples for the style decision). `shot` is the camera, `frame` the width and height asked for.
# ---------------------------------------------------------------------------------------------------------------
APPROACHES = {
    "field": dict(
        name="Field documentary",
        frame=(1536, 1024),
        shot=("A documentary photograph taken in the field by a photojournalist who waited hours for it, in the manner "
              "of the best wildlife and expedition photography. A 400mm lens at f/4 from a long way off, the camera low, "
              "at the subject's own height; one blurred stem leaning through the near foreground, the subject sharp, "
              "the distance soft. Natural light only. The subject is at work and has not noticed the camera. The whole "
              "figure is in the frame, head to foot, a little over half the frame's height and off-centre, with the "
              "place filling the rest. True-to-life colour, a little grain, nothing retouched."),
    ),
    "film": dict(
        name="Cinematic film still",
        frame=(1536, 864),   # asked for as 2048 x 1152; this is what the painter returns for 16:9
        shot=("A single frame from a feature film shot on 35mm stock with anamorphic lenses. A medium-wide shot: the "
              "figure from the knees up, placed a third of the way in from one side, deep space behind, out-of-focus "
              "lights stretched into soft ovals, fine film grain, a restrained colour grade with true deep blacks. "
              "The lighting is motivated by what is in the place. It is a quiet moment between scenes of action, held "
              "a beat longer than is comfortable. The subject does not look at the camera."),
    ),
    "candid": dict(
        name="Candid, available light",
        frame=(1024, 1280),
        shot=("A candid photograph taken by a crewmate without asking. A 35mm lens wide open at arm's length in a small "
              "room, available light only, high ISO with real grain, the white balance a little too warm, the frame "
              "slightly off level, something soft and out of focus intruding at one near corner, a trace of motion "
              "blur in one hand. The subject is off duty and absorbed in something of their own, unposed, not looking "
              "at the camera. The whole seated figure is in the frame. Nothing has been tidied for the picture."),
    ),
    "sitting": dict(
        name="Aftermath portrait",
        frame=(1024, 1280),
        shot=("An environmental portrait by a documentary photographer, made in the first quiet minute after the work "
              "was done. Medium format, an 80mm lens at f/2.8, the camera at the subject's eye height and two steps "
              "away, close enough to see the rim of each iris, a thin line of colour where an animal's would be "
              "dark. The subject has stopped "
              "where they were, sits, and looks straight into the lens and holds the look. Framed from the knees up, "
              "the place soft behind. Every trace of the last hours is kept: wet fur in clumps, grime in the creases, "
              "a tired and steady face. Nothing retouched."),
    ),
}

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
# The body below the bust's crop: decided once, here, the same for every picture of a pilot.
# ---------------------------------------------------------------------------------------------------------------
BUILD_DEFAULT = "an adult's build, upright and proportioned like a person"
BUILD = {   # by the face animal; an animal not listed gets BUILD_DEFAULT
    "Walrus": "huge and heavy, barrel-chested and thick-necked, broad as a doorway, a big man's bulk carried upright "
              "and gently",
    "Mouse": "small and slight, narrow-shouldered, a head shorter than most people",
    "Tiger": "tall, broad-shouldered and powerful",
    "Ibis": "lean and long-legged, a wader's build",
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

# ---------------------------------------------------------------------------------------------------------------
# The samples. source: what in the pilot's life the scene comes from. seen: what the painted avatar shows beyond its
# brief (look at the picture, then write). The rest is the scene.
# ---------------------------------------------------------------------------------------------------------------
SAMPLES = {
    "MANGER": dict(
        approach="field",
        source="profession: a hunter who wades the feeding flats sets a lure line",
        seen=("the whole face is scarlet and the long bill rose-red, greyer toward its tip",
              "the cape has a broad turned-down collar with magenta piping on every edge",
              "the jerkin is dark brown, the line is a coil of pale hemp rope, and the strap's buckle is square brass"),
        carry="seen whole, the paddle tail is as long as a forearm and a hand, grows from the base of the spine and "
              "comes out under the back of the cape",
        doing=("Tamako kneels on one knee at the edge of a shallow channel on a feeding flat and sets a lure. One "
               "webbed hand pays out wet line from the coil worn at the chest; the other lowers a small hand-carved "
               "wooden float with one glinting crystal bead into the water. The long bill is tipped down over the "
               "work and the eyes are fixed on the line. The paddle tail lies flat on the mud behind, as a brace."),
        place=("The feeding flat lies inside a living cell the size of a valley: grey silt and mirror-still water a "
               "hand deep, stands of pale glassy reeds whose seed heads are clusters of small clear crystals, and far "
               "behind, through mist, the wall of the cell, a translucent sea-green membrane veined like a leaf and "
               "curving up out of sight."),
        light=("Soft, shadowless green-white daylight filtering down through the membrane, the mist brighter behind "
               "the figure, the wet mud shining."),
        wear=("Both knees and shins are plastered with wet grey mud; the hem of the cape is soaked dark and spattered; "
              "a three-cornered tear near the hem has been mended with coarse pale stitches; the leather is cracked "
              "at its creases; the fur of the neck is damp and spiked; there is silt under the claws, and water "
              "stands in beads on the oilskin."),
    ),
    "GALLEON": dict(
        approach="film",
        source="hobby: \"ties a ribbon on the hull for every friend lost\" (the game's Quirks)",
        swap=(("a bison's huge shaggy forelock", "a bison's huge woolly forelock"),
              ("just showing through it at the sides", "standing clear of it at each side of the head,"),
              ("shaggy dark-brown bison wool", "curly dark-brown bison wool")),
        seen=("the skin is deep cinnamon brown, the whiskers pale and stiff, the tusks yellowed ivory, finely crazed "
              "with age",
              "the wool is tight springy curls like a lamb's fleece, growing from the scalp itself and running "
              "without a break into the beard and the wool of the neck",
              "each epaulette is a coil of rope, the ship's wheel is a small badge of yellow brass, and the "
              "sea-glass is pale green"),
        doing=("Haradek stands at the nose of a small single-seat craft and ties a strip of plain faded red cloth to "
               "its landing strut, the big gloved fingers slow and careful with the knot. A dozen older strips already "
               "hang from the same strut, sun-bleached, frayed and stiff with salt: one for every friend lost. The "
               "eyes are lowered to the knot, and the heavy face is shy and gentle."),
        place=("The hangar deck of a great ship: a long vaulted bay of dark ribbed metal, scuffed deck plates with "
               "worn coloured lane stripes, hoses and chains, a row of small single-seat craft parked nose-out on "
               "their pads, each hull made of dark glassy facets like a cut gemstone, and at the far end the open "
               "mouth of the bay, a wall of dim blue-green haze where pale faceted crystals drift slowly past. A thin "
               "mist of coolant lies at knee height. The bay is empty of people."),
        light=("One warm work lamp on a stand just out of frame is the key light, low and from the side; the cold "
               "blue-green of the open bay rims the wool, the horns and the shoulders from behind; the rest falls "
               "away into shadow."),
        wear=("The greatcoat is salt-stained in tide lines, its oxblood red still plain under them, and its hem is "
              "dark with oil; one shoulder seam has split and shows the lining; a button hangs by a thread; the "
              "magenta piping is rubbed through at the cuffs; the gloves are creased and the boots scuffed grey; "
              "the curls are flecked with salt."),
    ),
    "Echo": dict(
        approach="candid",
        source="hobby: \"keeps a jar of every crystal they have ever missed\" (the game's Quirks)",
        seen=("the face is white from the brow down, and the barred feathers begin above the brow, between the two "
              "mouse ears, where the ear tufts stand",
              "the coat is pale stone-grey, edged in teal piping along the lapels, the pocket and the cuffs, and "
              "worn over a dark teal brocade waistcoat",
              "the lens is a round brass-rimmed magnifying glass on a thin brown cord, and one small glass vial "
              "stands in the pocket beside the pens"),
        lower="plain dark trousers, bagged at the knees, and grey wool socks pulled up under their cuffs, one darned "
              "at the heel, the deck shoes kicked off under the bunk",
        doing=("Echo sits on the bunk with both knees drawn up and a big glass jar wedged between them, three-quarters "
               "full of small crystals: every crystal Echo ever missed. One small pink hand lifts the brass lens on "
               "its cord up to one eye; the other holds a single pale crystal the size of a thumbnail up to the desk "
               "lamp in a pair of tweezers. The whiskers are pushed forward and the whole face is bent to the "
               "crystal, cool and assessing. Both wings show behind the shoulders: one is folded, the other hangs "
               "half open over the edge of the bunk, and two shed barred feathers lie on the blanket."),
        place=("A crew cabin aboard a great ship, late at night: a narrow bunk built into a metal wall, a fold-down "
               "desk crowded with a chipped enamel mug, a bent desk lamp, coils of wire and a closed notebook, a "
               "small round window full of dark blue-green haze, a shelf of odd blank jars and tins above, a jacket "
               "on a hook."),
        light=("The desk lamp's warm bulb is the only real light, close and low: it glows through the thin ears and "
               "catches the edge of every feather. The window gives a faint cold fill, and the corners of the room "
               "are nearly black."),
        wear=("The coat is unbuttoned and creased from being slept in, its sleeves a little too long and down over "
              "the wrists, one cuff frayed, an old ink stain spread under the breast pocket, a small burn hole near "
              "the hem, a loose thread at a button; a few feathers are out of place, and there are crumbs on the "
              "blanket."),
    ),
    "Tenacious": dict(
        approach="sitting",
        source="profession: a first responder, in the first minute after a rescue",
        swap=(("warm hazel irises", "amber-gold irises, a tiger's own eye colour,"),),
        seen=("the shell cap is one smooth unbroken dome, a tortoise's own carapace in small, fitting close over the "
              "whole top of the head, down to the brow and down over the temples where ears would be",
              "the nose is pink, the whiskers are long and white, and the rim of each amber-gold iris is teal where "
              "a tiger's would be dark",
              "the tortoise skin of the neck is olive green",
              "the jacket has a stand collar lined in teal and plain grey reflective bands on the sleeves, and the "
              "harness is black webbing with steel buckles"),
        lower="signal-orange over-trousers to match the jacket, with grey reflective bands, and heavy black rescue "
              "boots",
        doing=("Tenacious has just come off a long rescue and sits on an upturned equipment case at the edge of the "
               "deck, elbows on knees, a dented steel mug steaming between both gloved hands, and looks straight into "
               "the lens: bone tired, calm and faintly amused."),
        place=("The open deck of a rescue station built on a shard of rock, at first light after a storm: wet steel "
               "plates, coiled lines and blank crates of gear stacked against a rail, two orange rescue boats in "
               "their cradles soft in the background, and a beacon lamp still turning far behind, laying a "
               "red-orange glow into the lifting rain. Beyond the rail there is no sea, no shore and no horizon: only "
               "a vast slow whirlpool of violet-grey storm cloud, dim and far, seen through the rain like weather, "
               "and pale faceted crystals as big as houses drifting past."),
        light=("Flat grey daylight from the overcast above on the face, soft and even; the far beacon's red-orange as "
               "a faint warm edge on the wet shell and one shoulder; a faint violet glow from the cloud behind; fine "
               "rain still falling, a few drops caught sharp."),
        wear=("The jacket is soaked and streaked with soot; one forearm of it is ripped open in a ragged tear with "
              "the grey lining showing; one reflective band is half peeled away; the harness webbing is frayed at a "
              "buckle; the gloves are blackened; the shell cap and the shell behind the shoulders are scuffed, with "
              "pale scrapes across the olive-brown plates, their golden growth rings still plain; the black hair "
              "hangs wet in ropes; rain runs down the face, and the fur of the cheeks and the brow is soaked dark, "
              "stuck in points and smudged with soot."),
    ),
}
CLOSE = {   # painted scene -> the square around the figure's head, for the sheet's close-up beside the avatar
    "MANGER-field": (650, 195, 1010, 555),
    "GALLEON-film": (330, 0, 800, 470),
    "Echo-candid": (300, 60, 720, 480),
    "Tenacious-sitting": (220, 20, 720, 520),
}
SCENE_KEYS = ("approach", "source", "seen", "doing", "place", "light", "wear")
SCENE_MORE = ("carry", "lower", "build", "hands", "swap")
SWAPPED = ("face", "coat", "second", "eyes", "hair", "outfit", "motif")   # the avatar's lines a swap may correct
WORD_CAP = 1000   # briefs of 870 to 890 words were painted faithfully; past a thousand a line is one too many

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


def identity(word):
    """A cast pilot as their avatar was painted: dict(word, name, a, b, faction, callsign, look, face, part, coat,
    second, kind, eyes, hair, hand, outfit, accent, hex, motif), every text line verbatim from the painted brief."""
    for w, person, (a, b), faction, callsign, look in creator.catalog():
        if w.lower() == word.lower():
            break
    else:
        raise SystemExit(f"{word} is not in the catalog (creator.py list)")
    path = creator.painted_brief_path(w)
    if not os.path.exists(path):
        raise SystemExit(f"{w} has no painted brief on record ({path}): paint the bust first")
    text = open(path, encoding="utf-8").read()
    m = PAINTED.search(text)
    if not m:
        raise ValueError(f"{path} does not read as a bust brief: scene.PAINTED no longer matches bust.compose")
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


def hands_of(idn):
    """The pilot's two hands, the same in every picture: the hand the avatar shows (an animal's own, or gloves);
    else the true hand of one of their animals; else the faction type's gloves."""
    a, b, look = idn["a"], idn["b"], idn["look"]
    holds = "Each hand has fingers and a thumb and holds things the way a person's hand does."

    def own(x):
        return (f"Both hands are the {x.lower()}'s own: each is {bust.LIMB[x]}, and the wrist above it wears the "
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


def who(word, seen=(), swap=()):
    """The identity paragraph of a scene: the person, in the words their avatar was painted from."""
    i = as_seen(identity(word), swap)
    al, bl = i["a"].lower(), i["b"].lower()
    lines = [f"The subject is {i['name']}, a real living person in a world where people like this are ordinary: {al} "
             f"and {bl} and human in one body. {i['a'].upper()} is the face and the shape of the head: {i['face']}. "
             f"{i['b'].upper()} is the {i['part']} and everything behind the face: {i['coat']}."]
    lines.append(f"{i['eyes'][0].upper()}{i['eyes'][1:]}. {i['hair']}")
    if seen:
        lines.append("In detail: " + "; ".join(seen) + ".")
    return " ".join(lines)


def compose(word, approach=None, **over):
    """The scene brief, verbatim what the painter gets. A sample's record (SAMPLES) fills what is not given."""
    i = identity(word)
    rec = dict(SAMPLES.get(i["word"], {}), **over)
    missing = [k for k in SCENE_KEYS if k != "approach" and not rec.get(k)]
    if missing:
        raise ValueError(f"{i['word']} has no scene record: give {', '.join(missing)} (scene.SAMPLES)")
    i = as_seen(i, rec.get("swap", ()))
    approach = approach or rec.get("approach")
    if approach not in APPROACHES:
        raise KeyError(f"no approach {approach!r}: one of {', '.join(APPROACHES)}")
    name, bl = i["name"], i["b"].lower()
    for what in ("doing", "wear"):
        m = PERMANENT.search(rec[what])
        if m and not PERMANENT.search(i["hand"] + " " + " ".join(rec["seen"])):
            raise ValueError(f"{i['word']}'s {what} says {m.group(0)!r}: a scene adds nothing permanent that the "
                             f"avatar does not have")
    build = rec.get("build") or BUILD.get(i["a"], BUILD_DEFAULT)
    hands = rec.get("hands") or hands_of(i)
    lower = rec.get("lower") or LOWER[i["faction"]]
    second = ""
    if i["second"]:
        carry = rec.get("carry") or CARRY[i["kind"]].replace("{bl}", bl)
        second = f" The {bl} also gives {i['second']}: {carry}."
    motif = f", and {i['motif']}" if i["motif"] else ""
    text = (
        f"{APPROACHES[approach]['shot']}\n\n"
        f"{who(i['word'], rec['seen'], rec.get('swap', ()))}\n\n"
        f"From the neck down {name} is built like a person and stands, sits and moves like one: {build}. {hands} "
        f"The clothes cover the body from the collar to the wrists and the ankles, and no bare human skin shows "
        f"between them.{second}\n\n"
        f"{name}'s clothes, seen whole and lived in: {i['outfit']}, piped in {i['accent']} ({i['hex']}), the piping "
        f"a thin cord that glows faintly{motif}; below it, {lower}. {rec['wear']}\n\n"
        f"{rec['doing']}\n\n"
        f"{rec['place']} A few tiny faceted crystal grains drift in the air like dust in a sunbeam. {rec['light']}\n\n"
        + FINISH.replace("{name}", name))
    for gate, what in ((bust.world_name_in, "names the world"), (bust.emblem_word, "invites an emblem"),
                       (lambda t: (STYLE_WORDS.search(t) or [None])[0], "pulls back to a painting")):
        hit = gate(text)
        if hit:
            raise ValueError(f"{i['word']}'s scene brief {what}: {hit!r}")
    return text


# ---------------------------------------------------------------------------------------------------------------
# Capture.
# ---------------------------------------------------------------------------------------------------------------
def scene_path(word, approach):
    return os.path.join(ROOT, SCENES, f"{word}-{approach}.jpg")


def painted_brief_path(word, approach):
    """The brief a scene was painted from, kept beside it: the kit moves on, the painting does not."""
    return os.path.join(ROOT, SCENES, "briefs", f"{word}-{approach}.txt")


def stale():
    """[(scene, why)] for the samples today's kit would paint differently, or that are not painted."""
    out = []
    for w, rec in SAMPLES.items():
        key = f"{w}-{rec['approach']}"
        if not os.path.exists(scene_path(w, rec["approach"])):
            out.append((key, "not painted"))
        elif not os.path.exists(painted_brief_path(w, rec["approach"])):
            out.append((key, "no brief on record"))
        elif open(painted_brief_path(w, rec["approach"]), encoding="utf-8").read().strip() != compose(w).strip():
            out.append((key, "painted from an older brief"))
    return out


def capture(word, url, approach=None, **over):
    """Download a painted scene and keep the brief it was painted from beside it."""
    i = identity(word)
    approach = approach or SAMPLES.get(i["word"], {}).get("approach")
    text = compose(i["word"], approach, **over)
    out = scene_path(i["word"], approach)
    os.makedirs(os.path.dirname(painted_brief_path(i["word"], approach)), exist_ok=True)
    raw = out + ".download"
    subprocess.run(["curl", "-sS", "-f", "-L", "-o", raw, url], check=True)
    from PIL import Image
    im = Image.open(raw).convert("RGB")
    os.remove(raw)
    assert max(im.size) >= 1280 and min(im.size) >= 848, f"{url} is {im.size}: smaller than any scene frame"
    im.save(out, quality=92, subsampling=0, optimize=True)
    with open(painted_brief_path(i["word"], approach), "w", encoding="utf-8") as f:
        f.write(text + "\n")
    print(f"captured {out} {im.size}")
    return out


def sheet(out):
    """The review sheet: each sample's avatar, the scene's own close-up under it, and the whole scene beside them."""
    from PIL import Image, ImageDraw, ImageFont

    def font(size, bold=False):
        name = "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"
        try:
            return ImageFont.truetype(os.path.join("/usr/share/fonts/truetype/dejavu", name), size)
        except OSError:
            return ImageFont.load_default()

    plate, ink, dim = (20, 23, 43), (236, 238, 246), (160, 166, 190)
    W, M, GAP = 2200, 40, 16
    probe = ImageDraw.Draw(Image.new("RGB", (8, 8)))
    blocks = []
    for n, (w, rec) in enumerate(SAMPLES.items(), 1):
        i = identity(w)
        ap = APPROACHES[rec["approach"]]
        key = f"{i['word']}-{rec['approach']}"
        scene = Image.open(scene_path(i["word"], rec["approach"])).convert("RGB")
        avatar = Image.open(creator.bust_path(i["word"])).convert("RGB")
        wide = scene.width > scene.height
        side = 440 if wide else 380
        h = side * 2 + GAP
        big = scene.resize((round(scene.width * h / scene.height), h), Image.LANCZOS)
        width = side + GAP + big.width
        who_ = i["name"] if i["name"] == i["word"] else f"{i['name']} ({i['word']})"
        lines, line = [], ""
        for word_ in f"{who_}, {i['a'].lower()} and {i['b'].lower()}. From their {rec['source']}".split():
            if line and probe.textlength(f"{line} {word_}", font=font(22)) > width:
                lines.append(line)
                line = word_
            else:
                line = f"{line} {word_}".strip()
        lines.append(line)
        HEAD = 2 * 30 + 66   # room for two lines under the title, so every block's pictures start level
        block = Image.new("RGB", (width, HEAD + h), plate)
        d = ImageDraw.Draw(block)
        d.text((0, 0), f"{n}. {ap['name']}", fill=ink, font=font(40, True))
        for k, text_ in enumerate(lines[:2]):
            d.text((0, 54 + 30 * k), text_, fill=dim, font=font(22))
        block.paste(avatar.resize((side, side), Image.LANCZOS), (0, HEAD))
        block.paste(scene.crop(CLOSE[key]).resize((side, side), Image.LANCZOS), (0, HEAD + side + GAP))
        d.rectangle((0, HEAD, 150, HEAD + 34), fill=(0, 0, 0))
        d.text((8, HEAD + 4), "the avatar", fill=ink, font=font(22))
        d.rectangle((0, HEAD + side + GAP, 236, HEAD + side + GAP + 34), fill=(0, 0, 0))
        d.text((8, HEAD + side + GAP + 4), "the scene, close up", fill=ink, font=font(22))
        block.paste(big, (side + GAP, HEAD))
        blocks.append((wide, block))
    rows, pending = [], []
    for wide, block in blocks:   # a wide scene takes a row; two tall ones share one
        if wide:
            rows.append([block])
        else:
            pending.append(block)
            if len(pending) == 2:
                rows.append(pending)
                pending = []
    if pending:
        rows.append(pending)
    top = 150
    height = top + sum(max(b.height for b in row) + M for row in rows) + 20
    im = Image.new("RGB", (W, height), plate)
    d = ImageDraw.Draw(im)
    d.text((M, 30), "The second style: realistic scenes, four approaches", fill=ink, font=font(50, True))
    d.text((M, 96), "Each scene is the same pilot as the avatar beside it, painted from words alone. Nothing here is "
                    "locked: which approach, or which mix, is the style?", fill=dim, font=font(24))
    y = top
    for row in rows:
        x = M
        for block in row:
            im.paste(block, (x, y))
            x += block.width + M
        y += max(b.height for b in row) + M
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    im.save(out)
    print(f"sheet {out} {im.size}")


# ---------------------------------------------------------------------------------------------------------------
# Self-test.
# ---------------------------------------------------------------------------------------------------------------
def self_test():
    errors = []
    words = [w for w, *_ in creator.catalog()]

    # 1. Every avatar's painted brief still reads, and what is cut from it is in it, word for word.
    for w in words:
        try:
            i = identity(w)
        except (ValueError, SystemExit) as e:
            errors.append(str(e))
            continue
        text = open(creator.painted_brief_path(w), encoding="utf-8").read()
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
    texts = ([FINISH, BUILD_DEFAULT] + [v["shot"] for v in APPROACHES.values()] + list(BUILD.values())
             + list(GLOVES.values()) + list(LOWER.values()) + list(CARRY.values()))
    for t in texts:
        for gate, what in ((bust.world_name_in, "names the world"), (bust.emblem_word, "invites an emblem")):
            if gate(t):
                errors.append(f"a scene table line {what} ({gate(t)!r}): {t[:60]}...")
    for v in APPROACHES.values():
        w_, h_ = v["frame"]
        if not (848 <= min(w_, h_) and 1280 <= max(w_, h_) <= 2048):
            errors.append(f"approach {v['name']}: frame {v['frame']} is outside what the painter takes")

    # 3. Every sample composes, names its source, keeps the identity lines and passes the gates.
    for w, rec in SAMPLES.items():
        for k in SCENE_KEYS:
            if not rec.get(k):
                errors.append(f"sample {w} has no {k}")
        for k in rec:
            if k not in SCENE_KEYS + SCENE_MORE:
                errors.append(f"sample {w} has an unknown field {k}")
        if not re.match(r"(?:profession|hobby|history): ", rec.get("source", "")):
            errors.append(f"sample {w}: the source says profession:, hobby: or history:")
        try:
            text = compose(w)
        except Exception as e:   # noqa: BLE001
            errors.append(f"sample {w} does not compose: {e}")
            continue
        i = as_seen(identity(w), rec.get("swap", ()))
        for k in ("face", "coat", "outfit"):
            if i[k] not in text:
                errors.append(f"sample {w}: the scene brief lost the avatar's {k} line")
        if i["name"] not in text:
            errors.append(f"sample {w}: the scene brief does not name the pilot")
        if re.search(r"(?:\bthe|'s) portrait\b", text):
            errors.append(f"sample {w}: the brief points the painter at a portrait it cannot see")
        if len(text.split()) > WORD_CAP:
            errors.append(f"sample {w}: the scene brief is {len(text.split())} words (the cap is {WORD_CAP})")
    for w, rec in SAMPLES.items():
        path = scene_path(w, rec["approach"])
        if os.path.exists(path):
            from PIL import Image
            box = CLOSE.get(f"{w}-{rec['approach']}")
            size = Image.open(path).size
            if not box or not (0 <= box[0] < box[2] <= size[0] and 0 <= box[1] < box[3] <= size[1]):
                errors.append(f"sample {w}: CLOSE has no box inside {os.path.basename(path)} {size}")
            elif box[2] - box[0] != box[3] - box[1]:
                errors.append(f"sample {w}: the CLOSE box is not square")
            if not os.path.exists(painted_brief_path(w, rec["approach"])):
                errors.append(f"sample {w}: the painted scene has no brief on record beside it")
            else:   # whatever the kit writes today, the painting's own brief carries the avatar's own words,
                    # as its brief had them or as the record swaps them for what the avatar shows
                painted = open(painted_brief_path(w, rec["approach"]), encoding="utf-8").read()
                raw, seen_ = identity(w), as_seen(identity(w), rec.get("swap", ()))
                for k in ("face", "coat", "outfit"):
                    if raw[k] not in painted and seen_[k] not in painted:
                        errors.append(f"sample {w}: the brief it was painted from lacks the avatar's {k} line")
    if len({r["approach"] for r in SAMPLES.values()}) != len(SAMPLES):
        errors.append("two samples share an approach: each sample is a different one")

    # 4. Negative controls: each gate fires on what it is for.
    def refuses(over, why):
        try:
            compose("MANGER", **over)
        except ValueError as e:
            return why in str(e)
        return False
    controls = (
        (dict(place="The hangar of the Ark, at night."), "names the world"),
        (dict(doing="Tamako sits cross-legged and waves a flag."), "invites an emblem"),
        (dict(wear="A long scar runs down the bill."), "nothing permanent"),
        (dict(wear="One chipped tusk and a muddy hem."), "nothing permanent"),
        (dict(place="A painterly marsh at dawn."), "pulls back to a painting"),
        (dict(swap=(("a phrase the avatar's brief never had", "anything"),)), "is in none of the avatar's lines"),
    )
    for over, why in controls:
        if not refuses(over, why):
            errors.append(f"the gate '{why}' did not fire on {over}")
    try:
        compose("ENSIGN")
        errors.append("compose made a scene for a pilot with no scene record")
    except ValueError:
        pass

    if errors:
        print("scene.py self-test FAILED:")
        for e in errors:
            print("  -", e)
        return 1
    print(f"scene.py self-test: ok ({len(words)} avatars read, {len(SAMPLES)} samples, {len(APPROACHES)} approaches)")
    return 0


def main():
    args = sys.argv[1:]
    if not args or args[0] in ("-h", "--help"):
        print(__doc__)
        return 0
    if args[0] == "--self-test":
        return self_test()
    if args[0] == "list":
        for w, rec in SAMPLES.items():
            i = identity(w)
            ap = APPROACHES[rec["approach"]]
            print(f"{w:10} {i['name']:20} {i['a']}/{i['b']:10} {ap['name']:24} {ap['frame'][0]}x{ap['frame'][1]}  "
                  f"{rec['source']}")
        return 0
    if args[0] == "who" and len(args) == 2:
        i = identity(args[1])
        rec = SAMPLES.get(i["word"], {})
        print(who(args[1], rec.get("seen", ()), rec.get("swap", ())))
        print(hands_of(i))
        return 0
    if args[0] == "brief" and len(args) in (2, 3):
        print(compose(args[1], args[2] if len(args) == 3 else None))
        return 0
    if args[0] == "stale":
        rows = stale()
        for key, why in rows:
            print(f"{key:20} {why}")
        print(f"{len(rows)} of {len(SAMPLES)} scenes differ from what the kit would paint today")
        return 0
    if args[0] == "sheet" and len(args) == 2:
        sheet(args[1])
        return 0
    if args[0] == "capture" and len(args) in (3, 4):
        capture(args[1], args[2], args[3] if len(args) == 4 else None)
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())
