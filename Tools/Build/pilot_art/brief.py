#!/usr/bin/env python3
"""The ART DIRECTION for one pilot portrait: the brief a painter paints from, composed from the game's own data.

A pilot is a unique person on a callsign (Docs/Pilots/INVENTORY.md section 3): a name, human plus two animals
(AnimalSpecies, 48), a faction (FactionType, 16; its domain is Ruby, Gold or Jade) and a backstory. The brief turns
that record into the one paragraph every painter gets - an image model through the Figma connector, an artist, a
future procedural painter - so a portrait is never "an anthropomorphic animal in a jacket" but THIS pilot:

  * the FIRST animal is the head, the hands and the build (its line in SPECIES["head"]);
  * the SECOND animal lends ONE unmistakable feature (its "lend" line: a tail, wings, a shell, fins, markings);
  * the body is human (the heritage is "Human / A / B");
  * the gear is the FACTION TYPE's (GEAR), trimmed in the faction's DOMAIN colour (Ruby freedom, Gold commerce,
    Jade order: FactionCatalog.DomainOf; the hexes are the colour-domain anchors, Docs/VesselModels/COLOR_DOMAINS.md);
  * the one prop or gear mark is the CALLSIGN's (MOTIFS): what holding LB lets this pilot see, worn on the body;
  * the rest is the HOUSE STYLE measured from the 32 painted portraits in Assets/_Graphics/Pilots/
    (.claude/skills/pilot-art/references/STYLE.md): full body, standing three-quarter, painterly, one key light,
    no ground, no backdrop - painted over a flat KEY colour the matte removes (matte.py).

    python3 Tools/Build/pilot_art/brief.py wyvendo                                   # the brief of a spec
    python3 Tools/Build/pilot_art/brief.py --callsign TALON --heritage Lynx Jackal \\
        --faction Hunter --name "Wyvendo Waveline" [--faction-name "Hollowfen Trackers"] [--pose "..."]
    python3 Tools/Build/pilot_art/brief.py --self-test                               # every table complete, ASCII, keyed

The text is printable ASCII only (the same rule as the pilot generator: the tables are read by people and by
models, and a stray glyph is a typo nobody sees). Never a C# literal: the brief is tooling, not player-facing text.
"""
import argparse
import importlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
HERE = os.path.dirname(os.path.abspath(__file__))
SPECS_DIR = os.path.join(HERE, "specs")
ANIMAL_SPECIES_CS = "Assets/_Scripts/Data/Inventory/AnimalSpecies.cs"
FACTION_CATALOG_CS = "Assets/_Scripts/Data/Inventory/FactionCatalog.cs"
VOYAGE_ENUMS_CS = "Assets/_Scripts/Data/Voyages/VoyageEnums.cs"

# ---------------------------------------------------------------------------------------------------------------
# The animals (AnimalSpecies.cs, 48). "head": what the FIRST animal gives: the head, hands and build.
# "lend": the ONE feature the SECOND animal lends. "colour": the coat, for the painter's palette.
# ---------------------------------------------------------------------------------------------------------------
SPECIES = {
    "Axolotl": dict(
        head="an axolotl's head: smooth pale-pink skin, a wide flat smiling mouth, tiny dark eyes, and three feathery crimson gill fronds fanning out from each side of the head",
        lend="feathery pink axolotl gill fronds behind the ears and a pale finned tail",
        colour="pale pink and cream"),
    "Heron": dict(
        head="a grey heron's narrow white head: a long yellow dagger beak, a pale yellow eye, a black stripe sweeping back into black crest plumes",
        lend="long slate-grey heron wing feathers folded like a cape",
        colour="slate grey, white and black"),
    "Otter": dict(
        head="a sea otter's round head: a pale grizzled muzzle and cheeks, long white whiskers, a broad dark nose, small ears, dense brown fur",
        lend="a thick tapering otter tail, sleek and dark brown",
        colour="chocolate brown and cream"),
    "Pangolin": dict(
        head="a pangolin's small conical head covered in overlapping brown keratin scales, a narrow snout and tiny eyes",
        lend="overlapping pangolin armour scales down the back and a broad scaled tail",
        colour="tan and umber"),
    "Gecko": dict(
        head="a leopard gecko's broad head: large gold eyes with slit pupils, a wide grin, spotted yellow skin",
        lend="a fat spotted leopard gecko tail, yellow with black spots",
        colour="yellow and cream with black spots"),
    "Lynx": dict(
        head="a lynx's head: black-tufted ear tips, wide cheek ruffs, amber eyes, a short broad muzzle",
        lend="a lynx's short bobbed black-tipped tail",
        colour="tawny grey with faint spots"),
    "Narwhal": dict(
        head="a narwhal's large rounded whale head with no neck: a blunt bulbous melon, a small eye, mottled grey skin, and one long thick spiral-grooved ivory tusk jutting forward from the upper lip",
        lend="one long spiralled narwhal tusk and mottled grey-blue skin",
        colour="mottled grey and white"),
    "Capybara": dict(
        head="a capybara's blocky head: small rounded ears, a wide blunt muzzle, calm dark eyes, coarse tan fur",
        lend="coarse reddish-tan capybara fur and small rounded ears",
        colour="tan and russet"),
    "Octopus": dict(
        head="an octopus's bulbous mantle for a head: large eyes with horizontal bar pupils, a fringe of short suckered arms at the jaw, skin shifting between ochre and rust",
        lend="a few long suckered octopus arms curling out from the collar",
        colour="ochre, rust and violet"),
    "Raven": dict(
        head="a raven's glossy black head: a heavy curved beak, a bright intelligent eye, a feathered throat",
        lend="glossy black raven wings folded at the back and a fan of black tail feathers",
        colour="iridescent black with a violet sheen"),
    "Tortoise": dict(
        head="a tortoise's wrinkled leathery head: a beaked mouth, patient hooded eyes",
        lend="a domed patterned tortoise shell worn on the back",
        colour="olive and amber"),
    "Fennec": dict(
        head="a fennec fox's small cream face with enormous ears, dark eyes and a tiny black nose",
        lend="a fennec fox's bushy cream tail with a black tip",
        colour="cream and sand"),
    "Mantis": dict(
        head="a praying mantis's triangular green head: huge compound eyes, fine antennae",
        lend="folded raptorial mantis forearms and translucent green wing cases",
        colour="leaf green"),
    "Walrus": dict(
        head="a walrus's broad wrinkled head: two very long thick ivory tusks reaching down past the collar to the chest, a thick bristle moustache",
        lend="two long ivory walrus tusks and a bristled moustache",
        colour="pinkish brown"),
    "Ibis": dict(
        head="a scarlet ibis's head: a long slender down-curved bill, bare pink skin on the face, scarlet feathers",
        lend="a pair of scarlet ibis wings folded at the back, black at the tips",
        colour="scarlet with black wingtips"),
    "Tapir": dict(
        head="a tapir's head with a short flexible trunk-like snout, small eyes and rounded ears",
        lend="a short tapir trunk and a black-and-white saddle pattern on the coat",
        colour="black and white"),
    "Jellyfish": dict(
        head="a translucent bell-shaped jellyfish dome for a head, glowing faintly from within, with fine tendrils trailing like hair",
        lend="long softly glowing jellyfish tentacles trailing from the back of the head like hair",
        colour="translucent blue and lilac"),
    "Badger": dict(
        head="a badger's head: the bold black-and-white striped face, small ears, a strong snout",
        lend="badger face stripes and a coarse grizzled grey coat",
        colour="grey, black and white"),
    "Chameleon": dict(
        head="a chameleon's casqued head: independently swivelling turret eyes, a wide mouth, skin shifting between green and turquoise",
        lend="a chameleon's long prehensile tail curled in a tight spiral at the hip",
        colour="green, turquoise and yellow"),
    "Owl": dict(
        head="a great horned owl's flat facial disc: huge forward-facing amber eyes, feather ear tufts, a small hooked beak",
        lend="broad barred owl wings folded as a cloak and feather ear tufts",
        colour="barred brown and cream"),
    "Seahorse": dict(
        head="a seahorse's head: a tubular snout, a bony ridged crown, a round eye, armoured plates",
        lend="a seahorse's long bony ring-plated tail coiled in a tight curl at the hip",
        colour="coral orange and gold"),
    "Wolf": dict(
        head="a grey wolf's head: a long muzzle, pale yellow eyes, erect ears, a thick ruff",
        lend="a thick bushy grey wolf tail hanging straight, black at the tip",
        colour="grey, white and black"),
    "Lemur": dict(
        head="a ring-tailed lemur's fox-like face: orange eyes in black patches, a white muzzle, grey fur",
        lend="a long black-and-white ringed lemur tail",
        colour="soft grey and cream-white"),
    "Cuttlefish": dict(
        head="a cuttlefish's broad head with W-shaped pupils and a fringe of short tentacles, skin flickering in bands",
        lend="rippling cuttlefish mantle fins along the sides and skin flickering with zebra bands",
        colour="sand with iridescent zebra bands"),
    "Moose": dict(
        head="a bull moose's long heavy head: huge broad palmate antlers spanning wider than the shoulders, a drooping bulbous muzzle, a dewlap bell hanging at the throat",
        lend="broad palmate moose antlers",
        colour="dark brown"),
    "Hummingbird": dict(
        head="a hummingbird's sleek head: a long needle bill, a bright black eye, an iridescent ruby gorget flashing at the throat",
        lend="tiny iridescent hummingbird wings at the back and an emerald gorget at the throat",
        colour="emerald with a ruby throat"),
    "Armadillo": dict(
        head="an armadillo's narrow head: a long pointed snout, tall upright ears, a bony scaled head shield over the crown, small dark eyes",
        lend="a banded armadillo carapace of jointed bony plates wrapped over the back and shoulders",
        colour="grey-brown"),
    "Koi": dict(
        head="a koi carp's head: barbels at the mouth, a round eye, white skin patched with orange and black",
        lend="koi fins along the forearms and a white skin patched with orange and black",
        colour="white, orange and black"),
    "Hyena": dict(
        head="a spotted hyena's broad head: rounded ears, a dark muzzle, a grinning jaw, a spotted coat",
        lend="a bristled hyena mane and a spotted tawny coat",
        colour="tawny with dark spots"),
    "Platypus": dict(
        head="a platypus's head with a flat leathery duck bill, small eyes and dense brown fur",
        lend="a broad flat paddle-shaped platypus tail covered in dense brown fur",
        colour="dark brown"),
    "Crane": dict(
        head="a whooping crane's white head: a bare red crown and red cheek patch, a pale yellow eye, a long dark olive beak",
        lend="long white crane wings with black tips and a red crown patch",
        colour="white, black and red"),
    "Beetle": dict(
        head="a stag beetle's dark head with great antler-like mandibles, compound eyes and antennae",
        lend="glossy bronze-black beetle wing cases folded over the back like a split shell",
        colour="glossy black and bronze"),
    "Bison": dict(
        head="a bison's huge shaggy head: short curved horns, a dark beard",
        lend="a bison's shaggy hump of dark woolly fur rising over the shoulders and upper back",
        colour="dark brown"),
    "Newt": dict(
        head="a fire-bellied newt's small head: rough dark skin, a bright orange underside",
        lend="a flat finned newt tail with a bright orange belly",
        colour="black and orange"),
    "Puffin": dict(
        head="a puffin's white face: a big striped orange-and-yellow bill, dark eye markings",
        lend="a big striped puffin bill and black-and-white plumage",
        colour="black, white and orange"),
    "Jackal": dict(
        head="a black-backed jackal's narrow fox-like head: large ears, a tan muzzle, dark eyes",
        lend="a jackal's black saddle of fur down the back and a black-tipped bushy tail",
        colour="tan and black"),
    "Sloth": dict(
        head="a three-toed sloth's round face: the permanent smile, a dark eye mask, shaggy moss-tinted fur",
        lend="long curved sloth claws and shaggy moss-tinted fur",
        colour="grey-green"),
    "Lobster": dict(
        head="a lobster's armoured head: two long whip antennae swept back, stalked black eyes, a spiked rostrum, a hard glossy shell",
        lend="a pair of lobster claws for hands and a segmented tail",
        colour="deep red and orange"),
    "Ocelot": dict(
        head="an ocelot's small cat head: big eyes lined in black, two black stripes down each cheek, a white muzzle, golden fur dappled with black rosettes",
        lend="an ocelot's ringed tail and a rosette-spotted golden coat",
        colour="gold with black rosettes"),
    "Pelican": dict(
        head="a pelican's head: a long bill with a hanging throat pouch, a pale eye",
        lend="broad white pelican wings with black flight feathers folded at the back",
        colour="white and grey"),
    "Hedgehog": dict(
        head="a hedgehog's head: a crown of short banded spines from the brow back, a small pointed snout with a black nose, bright dark eyes",
        lend="a cape of short brown-and-cream hedgehog spines over the back",
        colour="brown and cream"),
    "Mole": dict(
        head="a mole's velvet black head: a pink pointed snout, tiny hidden eyes, no visible ears",
        lend="broad shovel-like mole hands with long claws",
        colour="velvet black"),
    "Toucan": dict(
        head="a toco toucan's black head: an enormous orange bill with a black tip, a blue eye ring",
        lend="an enormous orange toucan bill and black-and-white plumage",
        colour="black, white and orange"),
    "Yak": dict(
        head="a yak's broad head: long curved horns, a long shaggy fringe over the eyes",
        lend="long sweeping yak horns and a long shaggy coat",
        colour="black and dark brown"),
    "Tardigrade": dict(
        head="a tardigrade's rounded segmented head: a circular puckered mouth, tiny eyespots, plump translucent skin",
        lend="a tardigrade's plump translucent amber back, ringed in soft segmented folds",
        colour="translucent amber"),
    "Firefly": dict(
        head="a firefly's small dark head hooded by a rounded dusky-red pronotum shield with a black spot: big black compound eyes, fine antennae",
        lend="dark firefly wing cases at the back over a short abdomen whose tip glows soft yellow-green",
        colour="black and red with a yellow-green glow"),
    "Orangutan": dict(
        head="an orangutan's wide bare dark leathery face framed by huge flat cheek flanges that make the head a broad disc, small deep-set eyes, rust-orange hair cropped short and close to the skull",
        lend="a fringe of long shaggy rust-orange orangutan hair hanging from the arms and shoulders",
        colour="rust orange"),
    "Eel": dict(
        head="a moray eel's long head: a gaping jaw with needle teeth, small eyes, mottled olive skin",
        lend="a slender smooth eel tail with a thin translucent fin along its length, held up and curling behind the figure",
        colour="mottled olive"),
}

# Species whose coat sits near a key colour: the default key moves off it (see key_for).
GREENISH = {"Mantis", "Chameleon", "Sloth", "Firefly", "Hummingbird", "Gecko"}

# ---------------------------------------------------------------------------------------------------------------
# The callsigns (Tools/Build/author_callsign_assets.py CALLSIGNS; Docs/Pilots/CALLSIGNS.md section 2): the ONE
# gear mark or prop that says what holding LB lets this pilot see. Worn, never held up to the camera.
# ---------------------------------------------------------------------------------------------------------------
MOTIFS = {
    "ENSIGN": "a small forked pennant flag stitched on the shoulder and a sash across the chest in the accent colour",
    "MANGER": "a worn leather feed satchel on the hip, a few crystal grains spilling from its mouth",
    "ANVIL": "a heavy anvil-shaped belt buckle and a small forge hammer hooked at the belt",
    "VOLT": "a monocle lens over one eye crackling with a thin arc of electric light",
    "ZODIAC": "a brass star-chart astrolabe hanging from the belt and faint constellation marks on the visor",
    "SUNDIAL": "a sundial-faced dial strapped to the forearm, its gnomon casting a tiny shadow",
    "OPAL": "an opal pendant at the throat, iridescent, and a faint rainbow sheen on one eye lens",
    "RAMPART": "a crenellated shield-shaped shoulder plate like a castle wall",
    "CITADEL": "a heavy breastplate embossed with a fortress tower",
    "HORNET": "black-and-yellow hazard bands around both sleeves and a stinger-shaped tool at the belt",
    "IVY": "living vines and small leaves grown over one shoulder of the jacket",
    "TALON": "a clawed hunting gauntlet on one hand and a predator's fang on a cord at the neck",
    "DOE": "a small brass shepherd's bell on the belt and a soft wool-lined collar",
    "WINGMAN": "a winged squadron badge on the chest and paired wing marks on the sleeves",
    "BANDIT": "a bandana mask pulled down around the neck and a crosshair-marked eyepiece",
    "NEEDLE": "a compass-needle pendant and a ring-and-needle gauge on the wrist",
    "GALLEON": "a ship's-wheel badge on the chest and a long captain's coat",
    "FLUME": "a water-ripple visor across the eyes and a coil of tunnel line over one shoulder",
}

# ---------------------------------------------------------------------------------------------------------------
# The factions (FactionCatalog.cs): the gear a type wears, and its domain (DomainOf: Ruby freedom, Gold commerce,
# Jade order). The accent hexes are the colour-domain anchors (Docs/VesselModels/colour_domains.json: ruby-0,
# gold-0, jade-0), so a pilot's trim is a colour the game already draws for that team.
# ---------------------------------------------------------------------------------------------------------------
DOMAIN_OF = {
    "Hunter": "Ruby", "Pirate": "Ruby", "Explorer": "Ruby", "Activist": "Ruby", "Thief": "Ruby",
    "Merchant": "Gold", "BountyHunter": "Gold", "Gatherer": "Gold", "Contractor": "Gold", "Sport": "Gold",
    "Aristocrat": "Jade", "Researcher": "Jade", "Preservationist": "Jade", "FirstResponder": "Jade",
    "Priest": "Jade", "Diplomat": "Jade",
}
ACCENT = {
    "Ruby": dict(hex="#C800C3", word="ruby magenta"),
    "Gold": dict(hex="#FFA700", word="amber gold"),
    "Jade": dict(hex="#0EC0B6", word="jade teal"),
}
GEAR = {
    "Merchant": "a long travelling coat over a quilted vest, ledger pouches and a coin purse on a wide belt",
    "Hunter": "a weathered trapper's jacket with a fur-lined collar, rope coiled at the hip, knee guards and heavy boots",
    "Aristocrat": "a tailored high-collared coat with brocade panels, polished buttons and fine gloves",
    "Pirate": "a patched sea coat with a loose scarf, rings, a wide sash and scuffed boots",
    "BountyHunter": "a long duster coat, a bandolier of empty loops, a wrist ledger and dusty boots",
    "Explorer": "a many-pocketed expedition jacket, a rolled map case on the back, goggles pushed up on the brow",
    "Activist": "a worn work jacket pinned with badges, a bright armband, fingerless gloves",
    "Thief": "a close-fitting dark jacket with many hidden pockets, soft boots, a hood pushed back",
    "Gatherer": "a canvas harvest apron over a rolled-sleeve shirt, a net bag at the hip, wading boots",
    "Contractor": "a heavy work vest over a rolled-sleeve shirt, a tool belt, welding goggles on the brow",
    "Sport": "a sleek racing flight suit with number patches, a speed stripe, light boots",
    "Researcher": "a long field coat with pens in the breast pocket, a lens on a cord, soft boots",
    "Preservationist": "a green ranger's jacket with a seed pouch, gaiters, a wide-brimmed hat hung at the back",
    "FirstResponder": "a reflective rescue jacket with a chest harness and a medical kit at the hip",
    "Priest": "a long layered robe over a flight suit, a shell pendant, soft wrapped boots",
    "Diplomat": "a formal long coat with a treaty seal on a chain and clean white gloves",
}

# ---------------------------------------------------------------------------------------------------------------
# The house style, measured from the 32 painted portraits (references/STYLE.md). One paragraph every brief ends
# with; the framing sentence is what finish.py then enforces by measurement.
# ---------------------------------------------------------------------------------------------------------------
STYLE = (
    "Painterly digital concept illustration, semi-realistic, with visible soft brush strokes, crisp detail "
    "concentrated on the face and the gear, a muted earthy base palette with one saturated accent colour and a few "
    "small glowing light lines on the gear, one soft key light from the upper left, a faint cool rim light along "
    "the right edge, no cast shadow on the ground. The figure is lit to read against a dark navy screen: the face "
    "and the nearest gear carry pale highlights, and the darkest fur, feathers or cloth still show their form in "
    "cool grey light rather than sinking to black."
)
FRAMING = (
    "The whole figure is visible from the top of the head to the soles of the boots, standing upright, centred, "
    "with a little empty space above the head and below the boots, portrait orientation."
)
DEFAULT_POSE = (
    "standing in a relaxed three-quarter pose, weight on one leg, turned slightly to the left, looking at the "
    "viewer, one hand resting on the belt and the other relaxed at the side"
)
NEVER = (
    "No text, no lettering, no watermark, no signature, no frame, no second character, no cropped limbs, "
    "no firearm, no floor, no horizon, no backdrop scenery."
)
KEYS = {"green": "#00FF00", "magenta": "#FF00FF", "blue": "#0000FF"}


def background(key_hex):
    name = {v: k for k, v in KEYS.items()}.get(key_hex.upper(), "flat")
    return (f"The background is a perfectly flat, uniform, solid {name} colour {key_hex.upper()} filling the entire "
            f"image edge to edge, with no gradient, no vignette, no texture and nothing else in the scene.")


def key_for(animals, domain, callsign=""):
    """The key colour the painter paints over, chosen so the matte's difference key never eats the pilot: green
    unless the coat is green (GREENISH) or the callsign wears leaves (IVY), then blue. Never magenta on its own:
    it eats Ruby trim (matte.py's negative control); a spec may still name it for a blue-coated green animal."""
    greenish = any(a in GREENISH for a in animals) or callsign.upper() == "IVY"
    return KEYS["blue"] if greenish else KEYS["green"]


def crew_phrase(name):
    """' of the Black Tide', ' of House Verdance', ' of the Rusted Grin', ' of the House of Seven Scales': the founding
    names read as English after 'hunter' or 'aristocrat' (round 2's judges caught 'of the House Verdance')."""
    if not name:
        return ""
    if name.lower().startswith("the "):
        return f" of the {name[4:]}"
    if name.startswith("House ") and " of " not in name:
        return f" of {name}"
    return f" of the {name}"


def compose(callsign, animals, faction, name="", faction_name="", pose="", key=None, extra=""):
    """The brief: dict(prompt, key, accent, lines). animals = (first, second): the head, then the lent feature."""
    callsign = callsign.upper()
    first, second = animals
    for a in (first, second):
        if a not in SPECIES:
            raise KeyError(f"unknown animal {a!r} (AnimalSpecies.cs)")
    if callsign not in MOTIFS:
        raise KeyError(f"unknown callsign {callsign!r} (author_callsign_assets.CALLSIGNS)")
    if faction not in GEAR:
        raise KeyError(f"unknown faction type {faction!r} (FactionType)")
    domain = DOMAIN_OF[faction]
    accent = ACCENT[domain]
    key = (key or key_for(animals, domain, callsign)).upper()
    who = f"{name} ({callsign})" if name else callsign
    crew = crew_phrase(faction_name)
    prompt = (
        f"Full-body character portrait of one anthropomorphic pilot, {who}, {faction.lower()}{crew}: "
        f"a human body and build with {SPECIES[first]['head']}, fur and skin {SPECIES[first]['colour']}, "
        f"and {SPECIES[second]['lend']}. "
        f"{(pose or DEFAULT_POSE).strip().rstrip('.').capitalize()}. "
        f"Wearing {GEAR[faction]}, trimmed in {accent['word']} ({accent['hex']}) with thin glowing "
        f"{accent['word']} seam lights, and {MOTIFS[callsign]}. "
        + (extra.strip().rstrip(".") + ". " if extra.strip() else "")
        + f"{STYLE} {FRAMING} {NEVER} {background(key)}"
    )
    lines = [
        ("pilot", who),
        ("heritage", f"Human / {first} / {second}"),
        ("faction", f"{faction_name or faction} ({faction}, {domain} {accent['hex']})"),
        ("callsign mark", MOTIFS[callsign]),
        ("key", key),
    ]
    return dict(prompt=prompt, key=key, accent=accent["hex"], domain=domain, lines=lines)


def load_spec(name):
    """specs/<name>.py -> its PORTRAIT dict (name, pilot, callsign, heritage, faction, faction_name, source, ...)."""
    if HERE not in sys.path:
        sys.path.insert(0, HERE)
    mod = importlib.import_module(f"specs.{name}")
    spec = dict(mod.PORTRAIT)
    spec.setdefault("name", name)
    return spec


def brief_of(spec):
    return compose(spec["callsign"], tuple(spec["heritage"]), spec["faction"], spec.get("person", ""),
                   spec.get("faction_name", ""), spec.get("pose", ""), spec.get("key"), spec.get("extra", ""))


def enum_members(path, enum):
    text = open(os.path.join(ROOT, path), encoding="utf-8").read()
    body = re.search(r"enum %s\s*\{(.*?)\}" % enum, text, re.S).group(1)
    return [m.group(1) for m in re.finditer(r"^\s*(\w+)\s*=\s*\d+", body, re.M)]


def self_test():
    failures = []
    animals = [a for a in enum_members(ANIMAL_SPECIES_CS, "AnimalSpecies") if a != "None"]
    missing = [a for a in animals if a not in SPECIES]
    extra = [a for a in SPECIES if a not in animals]
    if missing:
        failures.append(f"animals without a line: {missing}")
    if extra:
        failures.append(f"lines for animals the game does not roll: {extra}")
    factions = [f for f in enum_members(VOYAGE_ENUMS_CS, "FactionType") if f != "None"]
    for f in factions:
        if f not in GEAR or f not in DOMAIN_OF:
            failures.append(f"faction type without gear or domain: {f}")
    # The domain table agrees with FactionCatalog.DomainOf (the switch: explicit arms, then the default arm).
    cat = open(os.path.join(ROOT, FACTION_CATALOG_CS), encoding="utf-8").read()
    block = cat[cat.find("DomainOf"):cat.find("TypeName")]
    explicit = {}
    for m in re.finditer(r"((?:FactionType\.\w+(?:\s+or\s+)?)+)\s*=>\s*Domains\.(\w+)", block):
        for f in re.findall(r"FactionType\.(\w+)", m.group(1)):
            explicit[f] = m.group(2)
    default = re.search(r"_\s*=>\s*Domains\.(\w+)", block).group(1)
    for f in factions:
        if DOMAIN_OF.get(f) != explicit.get(f, default):
            failures.append(f"DOMAIN_OF[{f}] = {DOMAIN_OF.get(f)} disagrees with FactionCatalog.DomainOf ({explicit.get(f, default)})")
    sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))
    from author_callsign_assets import CALLSIGNS  # noqa: E402
    words = [w for _, w, _ in CALLSIGNS]
    for w in words:
        if w not in MOTIFS:
            failures.append(f"callsign without a motif: {w}")
    for w in MOTIFS:
        if w not in words:
            failures.append(f"motif for a callsign that does not exist: {w}")
    # Every table line is printable ASCII and ends without a full stop (the composer punctuates).
    for table, name in ((SPECIES, "SPECIES"), (MOTIFS, "MOTIFS"), (GEAR, "GEAR")):
        for k, v in table.items():
            for s in (v.values() if isinstance(v, dict) else [v]):
                if not all(32 <= ord(c) < 127 for c in s):
                    failures.append(f"{name}[{k}] is not printable ASCII")
                if s.endswith("."):
                    failures.append(f"{name}[{k}] ends with a full stop")
    # A brief names the key colour it is painted over, the accent hex, both animals and the motif.
    b = compose("TALON", ("Lynx", "Jackal"), "Hunter", "Wyvendo Waveline", "Hollowfen Trackers")
    for must in ("#00FF00", "#C800C3", "lynx", "jackal", MOTIFS["TALON"], "Hollowfen Trackers", "TALON"):
        if must not in b["prompt"]:
            failures.append(f"the brief does not name {must!r}")
    if key_for(("Chameleon", "Owl"), "Jade") != KEYS["blue"] or key_for(("Owl", "Mantis"), "Ruby") != KEYS["blue"]:
        failures.append("key_for does not move the key off a green coat")
    if key_for(("Owl", "Lynx"), "Gold", "IVY") != KEYS["blue"]:
        failures.append("key_for does not move the key off IVY's leaves")
    if key_for(("Owl", "Lynx"), "Ruby") != KEYS["green"]:
        failures.append("key_for moved the key for no reason")
    if KEYS["magenta"] in (key_for(a, d) for a in (("Mantis", "Owl"), ("Owl", "Lynx")) for d in ("Ruby", "Gold", "Jade")):
        failures.append("key_for chose magenta, which eats Ruby trim")
    for given, want in (("Black Tide", " of the Black Tide"), ("House Verdance", " of House Verdance"),
                        ("The Rusted Grin", " of the Rusted Grin"), ("House of Seven Scales", " of the House of Seven Scales"),
                        ("Order of the Last Lure", " of the Order of the Last Lure"), ("", "")):
        if crew_phrase(given) != want:
            failures.append(f"crew_phrase({given!r}) = {crew_phrase(given)!r}, not {want!r}")
    for bad, kwargs in (("animal", dict(callsign="TALON", animals=("Dragon", "Owl"), faction="Hunter")),
                        ("callsign", dict(callsign="EAGLE", animals=("Owl", "Lynx"), faction="Hunter")),
                        ("faction", dict(callsign="TALON", animals=("Owl", "Lynx"), faction="Baker"))):
        try:
            compose(**kwargs)
            failures.append(f"compose accepted an unknown {bad}")
        except KeyError:
            pass
    for f in failures:
        print("SELF-TEST FAIL:", f)
    print("self-test:", "FAILED" if failures else f"OK ({len(animals)} animals, {len(words)} callsigns, {len(factions)} faction types)")
    return 1 if failures else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("spec", nargs="?", help="a spec name under specs/")
    ap.add_argument("--callsign")
    ap.add_argument("--heritage", nargs=2, metavar=("FIRST", "SECOND"))
    ap.add_argument("--faction")
    ap.add_argument("--faction-name", default="")
    ap.add_argument("--name", default="")
    ap.add_argument("--pose", default="")
    ap.add_argument("--key", default=None)
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    if args.spec:
        b = brief_of(load_spec(args.spec))
    elif args.callsign and args.heritage and args.faction:
        b = compose(args.callsign, tuple(args.heritage), args.faction, args.name, args.faction_name, args.pose, args.key)
    else:
        ap.error("a spec name, or --callsign --heritage FIRST SECOND --faction")
    for k, v in b["lines"]:
        print(f"{k:>14}: {v}")
    print()
    print(b["prompt"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
