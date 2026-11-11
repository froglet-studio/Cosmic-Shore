#!/usr/bin/env python3
"""The SIGNATURE BUST: the Cosmic Shore pilot portrait made for the avatar crop.

Garrett, 2026-11-10: "pilot images that really show both clades in the avatar bust ... when people see it they know it
is cosmic shore because only we have triple hybrid people with that unique and high quality style". Six samples were
painted that evening and Garrett called them "definitely improvements".

Why a new brief: brief.py (the full-body portrait) gives the second animal ONE feature on the body (a tail, wings, a
shell), so the avatar's head-and-shoulders cut loses it and the avatar shows one animal. The bust is painted AS a bust
and splits the head into three OWNED ZONES, so every heritage is inside the crop:

  * FIRST animal  -> the FACE: skull, muzzle or beak, coat on the face (FACE[a]). If the animal's thumbnail tell is
                     something that grows from the head (antlers, horns), FACE keeps a SMALL version of it, so the
                     crown stays the second animal's (LESSONS: Fenlia's moose without antlers read as a horse).
  * SECOND animal -> the CROWN (CROWN[b]: what grows from the top or sides of the head: ears, horns, crest, fins,
                     coronet) and the HERITAGE MARK (MARK[b]): its pattern flowing up from the shoulders over the nape
                     and sweeping across ONE cheek like a birthmark.
  * HUMAN         -> the EYES (white sclera, human irises, human brows), the HAIR, and ONE HAND in frame.

The SIGNATURE every bust shares: the deep-sea navy plate (#222645), a luminous ring halo in the faction's DOMAIN colour
behind the head, that colour as the rim light and as a thin glowing ring around each iris (the pilot's sight: a pilot
is a pair of eyes, CALLSIGNS.md section 1), drifting crystal motes, a warm key from the upper left, premium painterly
rendering. Square, 1024, painted with its background (no matte: the avatar is a square).

The animals: the 48 AnimalSpecies the game rolls (held to the enum by --self-test) plus the animals of the 32 legacy
painted pilots (LEGACY_ANIMALS), so every pilot in the avatar catalog can be painted from one table.

    python3 Tools/Build/pilot_art/bust.py wyvendo                     # the bust brief of a spec (specs/<name>.py)
    python3 Tools/Build/pilot_art/bust.py --callsign TALON --heritage Lynx Jackal --faction Hunter --name "..."
    python3 Tools/Build/pilot_art/bust.py --self-test
The roster of every avatar pilot and the painting loop: creator.py.
"""
import argparse
import hashlib
import importlib
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import brief  # noqa: E402  (DOMAIN_OF, ACCENT, the enum readers)

# ---------------------------------------------------------------------------------------------------------------
# FACE: what an animal owns as the FIRST heritage, seen in a three-quarter bust.
# ---------------------------------------------------------------------------------------------------------------
FACE = {
    # the 48 AnimalSpecies
    "Axolotl": "an axolotl's broad smooth face, soft pale-pink skin, a wide flat gently smiling mouth, a rounded snout, and three feathery crimson gill fronds fanning out from each side of the head",
    "Heron": "a grey heron's narrow white face, a long yellow dagger beak, a black stripe sweeping back from the eye",
    "Otter": "a sea otter's round face, a pale grizzled muzzle and cheeks, long white whiskers, a broad dark nose, dense brown fur",
    "Pangolin": "a pangolin's small conical face covered in overlapping brown keratin scales, a long narrow snout",
    "Gecko": "a leopard gecko's broad flat head, pale yellow skin with bold black spots, a wide lipless smile, fine beaded scales",
    "Lynx": "a lynx's broad face, wide fluffy pale cheek ruffs, a short broad muzzle with a pink nose, tawny fur with dark spots on the brow and cheeks",
    "Narwhal": "a narwhal's large rounded whale head with no neck, a blunt bulbous melon brow, mottled grey-and-white skin, and one long thick spiral-grooved ivory tusk jutting forward from the upper lip",
    "Capybara": "a capybara's big square blunt face, a deep boxy muzzle, small round ears, coarse russet-brown fur",
    "Octopus": "an octopus's bulbous mantle for a head, smooth ochre-and-rust skin with fine texture, a fringe of short suckered arms curling at the jaw like a beard",
    "Raven": "a raven's glossy black face, a heavy curved black beak, shaggy throat hackles with a violet sheen",
    "Tortoise": "a tortoise's blunt beaked face, leathery olive skin in fine scales, a hooked horny upper lip",
    "Fennec": "a fennec fox's small cream face, a tiny black nose, a delicate pointed muzzle, and its two huge cream ears",
    "Mantis": "a praying mantis's triangular green head, two big compound eyes at the corners, fine mouthparts, smooth chitin",
    "Walrus": "a walrus's broad heavy face, a thick bristled moustache, two long ivory tusks hanging down to the chest, wrinkled cinnamon skin",
    "Ibis": "a scarlet ibis's bare pink face, a long down-curved red bill, vivid scarlet feathers",
    "Tapir": "a Malayan tapir's long face, a short drooping prehensile trunk, a black head with white-rimmed ears",
    "Jellyfish": "a translucent jellyfish bell for a head, softly glowing lilac and pale blue, with fine frilled oral arms hanging at the jaw",
    "Badger": "a European badger's wedge-shaped face, a bold white head with two black stripes running over the eyes, a black nose",
    "Chameleon": "a veiled chameleon's face, a tall casque on the head, turret eyes, bright green skin with turquoise and yellow bands",
    "Owl": "a great horned owl's flat round facial disc of barred brown and cream feathers, a small hooked dark beak, a white throat bib",
    "Seahorse": "a seahorse's face, a long tubular snout, a bony ridged brow, armoured coral-orange plates",
    "Wolf": "a grey wolf's long face, a pale muzzle, thick grey and cream fur, a dark nose",
    "Lemur": "a ring-tailed lemur's face, a narrow black muzzle, a white mask around the eyes, soft grey fur",
    "Cuttlefish": "a cuttlefish's broad head with a short crown of arms at the mouth, skin rippling with bands of zebra-striped colour",
    "Moose": "a moose's long heavy face, a drooping bulbous dark muzzle, dark brown fur, a dewlap bell at the throat, and two short velvet antler stubs on the brow",
    "Hummingbird": "a hummingbird's small iridescent face, a long needle-thin bill, a shimmering emerald and ruby throat gorget",
    "Armadillo": "an armadillo's long narrow face, a pointed snout, a banded leathery head shield",
    "Koi": "a koi carp's face, two barbels at the mouth, white skin patched with orange and black, fine scales",
    "Hyena": "a spotted hyena's broad heavy face, a blunt black muzzle, round ears, sandy fur with dark spots",
    "Platypus": "a platypus's face, a broad soft dark-grey duck bill, small dark eyes, dense brown fur",
    "Crane": "a whooping crane's white face, a long pointed yellow bill, a bare red crown patch, black whisker marks",
    "Beetle": "a stag beetle's head, glossy black-brown chitin plates, two short mandible tusks at the jaw",
    "Bison": "an American bison's massive face, a shaggy dark-brown beard and forelock, two short curved black horns",
    "Newt": "a fire-bellied newt's smooth face, dark green-brown skin above and a bright orange throat, small dark spots",
    "Puffin": "a puffin's face, a big triangular bill striped red, yellow and slate, a white face, a black cap",
    "Jackal": "a black-backed jackal's narrow fox-like face, a tan muzzle, a pale throat, rusty fur",
    "Sloth": "a three-toed sloth's round face, a dark eye mask, a gentle smile, shaggy greenish-brown fur",
    "Lobster": "a lobster's armoured head shield, a pointed rostrum between the eyes, deep blue-red shell, and two very long antennae",
    "Ocelot": "an ocelot's face, golden fur with black-ringed rosettes, white cheek stripes, a pink nose",
    "Pelican": "a brown pelican's face, a very long flat bill with a pale throat pouch, a white head",
    "Hedgehog": "a hedgehog's small pointed face, a shiny black nose, soft brown face fur, a short fringe of spines over the brow",
    "Mole": "a star-nosed mole's face, a pink fleshy star of tentacles on the snout, velvety charcoal fur, tiny eyes",
    "Toucan": "a toco toucan's face, a huge orange bill with a black tip, a white throat, a blue eye ring",
    "Yak": "a yak's broad shaggy face, a long dark fringe of hair, a pale muzzle, two short curved horns",
    "Tardigrade": "a tardigrade's plump segmented head, translucent cream skin, a round tube mouth ringed with tiny teeth",
    "Firefly": "a firefly's head, a black face with a red-and-yellow pronotum shield above the eyes, two segmented antennae",
    "Orangutan": "an orangutan's wide disc-shaped face of dark leather, wide cheek flanges, a ring of long orange hair",
    "Eel": "a moray eel's long face, an open toothy mouth, mottled green-gold skin, small tube nostrils",
}
# The legacy painted pilots' animals that are not AnimalSpecies (Docs/Pilots/PORTRAITS.md section 2).
LEGACY_FACE = {
    "Snail": "a sea snail's soft face, glossy blue-grey skin, two long eye stalks rising from the head",
    "Horned lizard": "a horned lizard's flat broad head, a crown of short sharp horns at the back of the skull, sandy scaled skin",
    "Clownfish": "a clownfish's round face, vivid orange skin with bold white black-edged bands across the cheeks",
    "Tree frog": "a red-eyed tree frog's face, huge red eyes, bright green skin, a wide smiling mouth",
    "Mouse": "a white mouse's small face, a pink nose, fine whiskers, two large round pink ears",
    "Cheetah": "a cheetah's lean face, golden fur with black spots, black tear lines running from the eyes",
    "Oryx": "a gemsbok oryx's face, a bold black-and-white mask, a pale muzzle, and two short straight horns",
    "Orca": "an orca's rounded black head, a glossy black face with the white eye patch and a white chin",
    "Coyote": "a coyote's narrow face, grizzled grey-tan fur, a pale throat, alert eyes",
    "Black hare": "a black hare's long face, sleek black fur, a white muzzle, two tall ears",
    "Ostrich": "an ostrich's small flat-billed face, sparse pale down on the head, a long bare neck",
    "Zebra": "a zebra's long face, bold black-and-white stripes, a dark muzzle, an upright striped mane",
    "Egret": "a great egret's slim white face, a long yellow bill, fine white plumes",
    "Caracal": "a caracal's face, sleek tawny fur, black and white face markings, two tall black-tufted ears",
    "Flamingo": "a flamingo's face, a bent black-tipped pink bill, soft rose-pink feathers",
    "Shoebill": "a shoebill's face, a huge clog-shaped grey bill with a hooked tip, a pale staring eye, slate-blue feathers",
    "Tiger": "a tiger's broad face, orange fur with black stripes, white cheeks and brows",
    "Parrot": "a scarlet macaw's face, a big curved bill, bare white cheek skin, scarlet feathers",
    "Basilisk": "a green basilisk lizard's face, a tall sail-like crest on the back of the head, bright green scales",
    "Booby": "a blue-footed booby's face, a long pointed blue-grey bill, a white-flecked brown head, a pale yellow eye",
    "Mandrill": "a mandrill's face, a long bright red nose ridge flanked by ridged blue cheeks, a golden beard",
    "Opossum": "an opossum's pointed white face, a pink nose, dark eyes, thin black-edged ears",
    "Fox": "a red fox's face, russet fur, a white muzzle and throat, black-backed ears",
    "Hare": "a brown hare's face, tawny fur, a soft muzzle, two tall black-tipped ears",
    "Kangaroo": "a red kangaroo's long gentle face, soft russet fur, a broad dark nose, tall upright ears",
}
FACE.update(LEGACY_FACE)

# ---------------------------------------------------------------------------------------------------------------
# CROWN: what an animal grows from the head as the SECOND heritage: (the part's word, the line).
# MARK: its pattern or texture, a bold band; the composer says where it flows.
# ---------------------------------------------------------------------------------------------------------------
CROWN = {
    "Axolotl": ("gills", "three feathery crimson axolotl gill fronds fanning out from each side of the head"),
    "Heron": ("crest", "two long black heron crest plumes sweeping back from the crown"),
    "Otter": ("whiskers", "long white otter whiskers and small round dark otter ears set low on the head"),
    "Pangolin": ("crown", "a crown of overlapping tan pangolin scales covering the top of the head and the back of the neck like a helm"),
    "Gecko": ("frill", "a frill of beaded yellow gecko scales with black spots along the brow and behind the jaw"),
    "Lynx": ("ears", "two tall lynx ears with long black tufts at the tips"),
    "Narwhal": ("horn", "one long spiral-grooved ivory narwhal tusk rising from the brow like a horn"),
    "Capybara": ("ears", "small round russet capybara ears and a coarse russet crest of fur"),
    "Octopus": ("arms", "a few suckered ochre octopus arms curling up behind the head like hair"),
    "Raven": ("crest", "a crest of glossy black raven feathers with a violet sheen sweeping back from the brow and down the nape"),
    "Tortoise": ("crown", "a domed crown of olive tortoise-shell plates with gold growth rings on top of the head"),
    "Fennec": ("ears", "two enormous cream fennec fox ears, pink inside, standing up and out wider than the head"),
    "Mantis": ("antennae", "two long green mantis antennae and a pair of small green compound eye bumps on the brow"),
    "Walrus": ("tusks", "two short ivory walrus tusks curving down from the cheekbones and a bristled moustache"),
    "Ibis": ("crest", "a crest of vivid scarlet ibis feathers sweeping back from the crown"),
    "Tapir": ("ears", "two white-rimmed tapir ears and a black-and-white tapir saddle behind the head"),
    "Jellyfish": ("veil", "a translucent glowing lilac jellyfish bell worn over the crown like a veil, fine tentacles trailing behind the head"),
    "Badger": ("stripes", "two bold black badger stripes over a white crown, running from the brow back over the head"),
    "Chameleon": ("casque", "a tall green chameleon casque rising from the back of the head, banded turquoise and yellow"),
    "Owl": ("ear tufts", "two tall feathered owl ear tufts, barred brown and cream"),
    "Seahorse": ("coronet", "a seahorse's bony ridged coronet of coral-orange spikes on top of the head, with small fan fins behind the jaw"),
    "Wolf": ("ears", "two tall grey wolf ears and a thick grey wolf ruff behind the jaw"),
    "Lemur": ("ears", "two pale fluffy lemur ears and a ringed black-and-white lemur tail curled up over one shoulder"),
    "Cuttlefish": ("fin", "a frilled cuttlefish fin running over the crown and down the nape, rippling with zebra-striped colour"),
    "Moose": ("antlers", "broad palmate moose antlers spreading from the top of the head"),
    "Hummingbird": ("crest", "a short crest of iridescent emerald hummingbird feathers and a shimmering ruby gorget patch at the throat"),
    "Armadillo": ("crown", "a banded armadillo head shield over the crown, its plates curving down the nape"),
    "Koi": ("fins", "long flowing translucent koi fins, white and orange, sweeping back from behind the jaw and the temples, and two koi barbels at the corners of the mouth"),
    "Hyena": ("mane", "a bristling dark hyena mane rising along the top of the head and two round hyena ears"),
    "Platypus": ("spurs", "a soft brown platypus fur cap and two small dark platypus spurs on the cheekbones"),
    "Crane": ("crown", "a bare red crane crown patch and a fan of long white crane plumes behind the head"),
    "Beetle": ("horn", "one curved glossy black beetle horn rising from the brow"),
    "Bison": ("horns", "two short curved black bison horns and a shaggy dark-brown bison forelock"),
    "Newt": ("crest", "a low wavy newt crest running over the crown, dark green on top and bright orange beneath"),
    "Puffin": ("crown", "a black puffin cap with a white puffin face patch and a striped red-yellow puffin bill plate on the brow"),
    "Jackal": ("ears", "two tall, oversized black-backed jackal ears, rusty orange on the backs with dark edges, standing straight up"),
    "Sloth": ("mask", "a shaggy greenish sloth crown of fur and a dark sloth eye-mask stripe"),
    "Lobster": ("antennae", "two very long red lobster antennae sweeping back from the brow and small red shell plates on the temples"),
    "Ocelot": ("ears", "two rounded ocelot ears, black-backed with a white spot"),
    "Pelican": ("crest", "a pale yellow pelican crown and a fan of pelican feathers down the nape"),
    "Hedgehog": ("spines", "a crown of brown-and-cream hedgehog spines bristling back over the head"),
    "Mole": ("star", "a small pink fleshy mole star of tentacles at the tip of the nose and velvety charcoal mole fur on the crown"),
    "Toucan": ("crest", "a crest of glossy black toucan feathers with an orange-and-yellow toucan bill plate on the brow"),
    "Yak": ("horns", "two long upcurved yak horns and a long shaggy dark yak fringe"),
    "Tardigrade": ("folds", "soft translucent cream tardigrade segment folds over the crown"),
    "Firefly": ("antennae", "two segmented black firefly antennae with softly glowing yellow-green tips"),
    "Orangutan": ("flanges", "wide orangutan cheek flanges and a ring of long orange orangutan hair"),
    "Eel": ("fin", "a ribbon of green-gold moray eel fin running over the crown and down the nape"),
}
LEGACY_CROWN = {
    "Hedgehog": CROWN["Hedgehog"],
    "Scorpion": ("tail", "a segmented red scorpion tail curling up from behind the shoulders and arching over the head, its stinger above the brow"),
    "Hyena": CROWN["Hyena"],
    "Orca": ("fin", "a tall glossy black orca dorsal fin rising from the crown like a crest"),
    "Eagle": ("crest", "a crest of white-and-brown eagle feathers sweeping back from the brow"),
    "Turkey": ("fan", "a fanned crest of bronze turkey tail feathers behind the head and a red turkey wattle at the throat"),
    "Butterfly": ("wings", "two small orange butterfly wings with dark eyespots rising from behind the head like a headdress, and two clubbed antennae"),
    "Porcupine": ("quills", "a crown of long banded black-and-white porcupine quills sweeping back from the head"),
    "Lionfish": ("spines", "a crown of long venomous striped red-and-white lionfish spines fanning back from the head"),
    "Red panda": ("ears", "two white-rimmed red panda ears and a ringed russet red panda tail curled over one shoulder"),
    "Mantis shrimp": ("crest", "rainbow mantis shrimp eye stalks and a frilled rainbow crest of shell plates on top of the head"),
    "Crocodile": ("scutes", "a ridge of armoured crocodile scutes running from the brow back over the crown"),
    "Hawk": ("crest", "a crest of rust-red hawk feathers sweeping back from the brow"),
    "Peacock": ("crest", "a crest of peacock feathers on top of the head and a fan of iridescent peacock eye-feathers behind it"),
    "Ram": ("horns", "two heavy curled ram horns spiralling around the ears"),
    "Tiger": ("ears", "black-backed tiger ears with white spots and tiger stripes over the brow"),
    "Deer": ("antlers", "a pair of small branched deer antlers rising between the ears"),
}
for k, v in LEGACY_CROWN.items():
    CROWN.setdefault(k, v)

MARK = {
    "Axolotl": "smooth pale-pink axolotl skin with tiny gold flecks",
    "Heron": "slate-grey heron feathers with black streaks",
    "Otter": "dense chocolate-brown otter fur",
    "Pangolin": "pangolin armour scales, tan and umber overlapping plates",
    "Gecko": "yellow gecko skin with bold black spots",
    "Lynx": "tawny lynx fur with dark spots",
    "Narwhal": "mottled grey-and-white narwhal skin",
    "Capybara": "coarse russet capybara fur",
    "Octopus": "ochre octopus skin with a row of small suckers",
    "Raven": "glossy black raven feathers with a violet sheen",
    "Tortoise": "olive tortoise scales and gold-ringed shell plates",
    "Fennec": "fennec fur, soft cream and sand",
    "Mantis": "smooth green mantis chitin plates",
    "Walrus": "wrinkled cinnamon walrus hide",
    "Ibis": "vivid scarlet ibis feathers",
    "Tapir": "the tapir's black-and-white saddle, a bold two-tone band",
    "Jellyfish": "translucent glowing lilac jellyfish skin",
    "Badger": "black-and-white badger stripes",
    "Chameleon": "green chameleon skin with turquoise and yellow bands",
    "Owl": "barred brown-and-cream owl feathers",
    "Seahorse": "seahorse ring-plates, coral orange and gold bony segments",
    "Wolf": "thick grey-and-cream wolf fur",
    "Lemur": "black-and-white lemur rings",
    "Cuttlefish": "cuttlefish skin rippling with zebra stripes",
    "Moose": "dark brown moose fur",
    "Hummingbird": "iridescent emerald hummingbird feathers",
    "Armadillo": "banded armadillo plates",
    "Koi": "koi skin patterning, bold white patches with vivid orange and black blotches and fine scales",
    "Hyena": "sandy hyena fur with dark spots",
    "Platypus": "dense brown platypus fur",
    "Crane": "white crane feathers with black wingtips",
    "Beetle": "glossy black-brown beetle chitin",
    "Bison": "shaggy dark-brown bison wool",
    "Newt": "dark green newt skin with an orange edge",
    "Puffin": "black puffin feathers with a white edge",
    "Jackal": "the jackal's black-and-silver saddle fur, a bold dark band with silver flecks",
    "Sloth": "shaggy greenish-brown sloth fur",
    "Lobster": "segmented red lobster shell",
    "Ocelot": "golden ocelot fur with black-ringed rosettes",
    "Pelican": "grey-brown pelican feathers",
    "Hedgehog": "brown-and-cream hedgehog spines",
    "Mole": "velvety charcoal mole fur",
    "Toucan": "glossy black toucan feathers with an orange edge",
    "Yak": "long shaggy dark yak hair",
    "Tardigrade": "translucent cream tardigrade segments",
    "Firefly": "black firefly chitin with small glowing yellow-green spots",
    "Orangutan": "long orange orangutan hair",
    "Eel": "mottled green-gold eel skin",
    # the legacy second animals
    "Scorpion": "segmented red scorpion plates",
    "Orca": "glossy black orca skin with a crisp white patch",
    "Eagle": "brown eagle feathers with white tips",
    "Turkey": "bronze iridescent turkey feathers",
    "Butterfly": "orange butterfly-wing scales with black veins",
    "Porcupine": "short banded porcupine quills",
    "Lionfish": "red-and-white lionfish stripes",
    "Red panda": "russet red panda fur with dark rings",
    "Mantis shrimp": "rainbow mantis shrimp shell plates",
    "Crocodile": "dark armoured crocodile scales",
    "Hawk": "rust-red hawk feathers",
    "Peacock": "iridescent blue peacock feathers",
    "Ram": "thick cream ram fleece",
    "Tiger": "orange-and-black tiger stripes",
    "Deer": "soft fawn deer fur with white spots",
}

# A bust shows only the chest up: the callsign's mark sits there (brief.MOTIFS can sit at the belt or the wrist).
MOTIF = {
    "ENSIGN": "a small forked pennant badge on the shoulder and a sash across the chest in the accent colour",
    "MANGER": "a leather feed-satchel strap across the chest, a few crystal grains caught in its buckle",
    "ANVIL": "a small anvil-shaped brooch at the collar and a forge hammer pin",
    "VOLT": "a monocle over one eye crackling with a thin arc of electric light",
    "ZODIAC": "a brass star-chart astrolabe pendant and faint constellation marks on the collar",
    "SUNDIAL": "a small brass sundial brooch pinned at the collar, its gnomon casting a tiny shadow",
    "OPAL": "an iridescent opal pendant at the throat",
    "RAMPART": "a crenellated shield-shaped shoulder plate like a castle wall",
    "CITADEL": "a breastplate at the chest embossed with a fortress tower",
    "HORNET": "black-and-yellow hazard bands on the collar",
    "IVY": "living vines and small leaves grown over one shoulder",
    "TALON": "a predator's fang on a cord at the throat",
    "DOE": "a small brass shepherd's bell at the collar",
    "WINGMAN": "a winged squadron badge on the chest",
    "BANDIT": "a bandana pulled down around the neck and a crosshair-marked eyepiece",
    "NEEDLE": "a compass-needle pendant on a fine chain at the throat",
    "GALLEON": "a ship's-wheel badge on the chest and a captain's high coat collar",
    "FLUME": "a water-ripple visor pushed up on the brow and a coil of tunnel line over one shoulder",
}
# The faction type's clothes from the chest up (brief.GEAR is head to boots).
GEAR = {
    "Merchant": "a long travelling coat over a quilted vest with a high collar",
    "Hunter": "a weathered trapper's jacket with a thick fur-lined collar",
    "Aristocrat": "a tailored high-collared coat with brocade panels and polished buttons",
    "Pirate": "a patched sea coat with a loose scarf and a wide lapel",
    "BountyHunter": "a long duster coat with a raised collar and a bandolier strap across the chest",
    "Explorer": "a many-pocketed expedition jacket with goggles hanging at the neck",
    "Activist": "a worn work jacket pinned with badges and a bright armband",
    "Thief": "a close-fitting dark jacket with a hood pushed back on the shoulders",
    "Gatherer": "a canvas harvest apron strap over a rolled-sleeve shirt",
    "Contractor": "a heavy work vest over a rolled-sleeve shirt, welding goggles at the neck",
    "Sport": "a sleek racing flight suit with a number patch and a speed stripe",
    "Researcher": "a long field coat with pens in the breast pocket and a lens on a cord",
    "Preservationist": "a ranger's jacket with a seed pouch strap across the chest",
    "FirstResponder": "a reflective rescue jacket with a chest harness",
    "Priest": "a long layered robe over a flight suit and a shell pendant",
    "Diplomat": "a formal long coat with a treaty seal on a chain",
}

# The person's human tells, rolled from the name when a spec does not set them (so a name always paints the same).
HAIRS = [
    "a short swept-back undercut of dark human hair",
    "a long silver-white human braid falling over one shoulder",
    "a neat high knot of copper-red human hair",
    "loose shoulder-length black human hair",
    "a cropped sandy-blond human crew cut",
    "a tight crown of dark human braids",
    "a wild mop of auburn human curls",
    "a sleek ink-blue human bob",
    "a long grey human ponytail tied with a cord",
    "short spiky white human hair",
]
IRISES = ["amber-brown", "hazel-green", "steel-grey", "ice-blue", "deep brown", "warm hazel", "pale green", "dark amber"]
HANDS = [
    "raised to grip the lapel of the jacket",
    "raised, two fingers touching the badge at the collar",
    "resting on the collar",
    "raised to the chin in thought",
    "adjusting the collar",
]
EXPRESSIONS = [
    "a knowing, confident half-smile", "a warm, shrewd smile", "a calm, composed, faintly amused look",
    "a sly, roguish grin", "a cool, poised, assessing look", "a gentle, steady, watchful expression",
    "a fierce, determined look", "a bright, curious, open expression",
]

SIGNATURE = (
    "Three-quarter view turned slightly to the left, eyes looking straight at the viewer, the head in the upper "
    "middle of a square frame with clear space above everything that grows from the head, cropped at mid-chest. Background: "
    "a deep-sea navy (#222645) gradient, darker at the corners, with a soft luminous ring halo of {word} light "
    "behind the head and a few faint drifting crystal motes. Lighting: a warm soft key light from the upper left, "
    "and a strong {word} rim light tracing the right edge of the head, {crown_part} and shoulders so the "
    "silhouette separates from the dark background. Rendering: a premium painterly digital character portrait, "
    "semi-realistic, rich texture in fur, feathers, scales or skin, crisp detail in the eyes and face, soft "
    "painterly brushwork in the clothing and background, like a collectible character card. No text, no "
    "lettering, no watermark, no frame border, no second character, no firearm."
)


def pick(table, name, salt):
    """A deterministic pick from a table by the person's name (a name always paints with the same human tells)."""
    h = int(hashlib.md5(f"{salt}/{name}".encode("utf-8")).hexdigest(), 16)
    return table[h % len(table)]


def compose(animals, faction, name, callsign=None, hair="", iris="", hand="", expression="", extra=""):
    """The bust brief, verbatim what the painter gets. animals = (face animal, crown animal)."""
    a, b = animals
    if a not in FACE:
        raise KeyError(f"no FACE line for {a!r}: add it to bust.FACE")
    if b not in CROWN or b not in MARK:
        raise KeyError(f"no CROWN/MARK line for {b!r}: add it to bust.CROWN and bust.MARK")
    if callsign is not None and callsign not in MOTIF:
        raise KeyError(f"no MOTIF line for callsign {callsign!r}")
    domain = brief.DOMAIN_OF[faction]
    acc = brief.ACCENT[domain]
    part, crown = CROWN[b]
    hair = hair or pick(HAIRS, name, "hair")
    iris = iris or pick(IRISES, name, "iris")
    hand = hand or pick(HANDS if callsign else HANDS[:1] + HANDS[2:], name, "hand")
    expression = expression or pick(EXPRESSIONS, name, "expression")
    motif = f", and {MOTIF[callsign]}" if callsign else ""
    al, bl = a.lower(), b.lower()
    return (
        f"Signature Cosmic Shore pilot portrait: a head-and-shoulders bust of {name}, a triple-hybrid person: one "
        f"face made of three heritages, human, {al} and {bl}, each owning its own part so all three read at a glance "
        f"even as a tiny icon. {a.upper()} owns the face and skull: {FACE[a]}. {b.upper()} owns the {part} and the "
        f"heritage mark: {crown}; and {MARK[b]}, flowing up from the shoulders over the back of the neck and sweeping "
        f"forward across one cheek like a birthmark where the {bl} heritage shows through the {al}. HUMAN owns the "
        f"eyes, the hair and the hand: forward-facing almond-shaped human eyes with white sclera, natural {iris} "
        f"irises each ringed by a thin glowing {acc['word']} line (the pilot's sight), expressive human eyebrows; "
        f"{hair}; and one human hand with five slender fingers, its skin matching the {al}'s, {hand}. "
        f"{expression[:1].upper() + expression[1:]}. Wearing {GEAR[faction]}, trimmed in {acc['word']} "
        f"({acc['hex']}) with thin glowing {acc['word']} seam lines{motif}. "
        + (f"{extra.rstrip('.')}. " if extra else "")
        + SIGNATURE.format(word=acc["word"], crown_part=part))


def self_test():
    species = [a for a in brief.enum_members(brief.ANIMAL_SPECIES_CS, "AnimalSpecies") if a != "None"]
    errors = []
    for s in species:
        for table, n in ((FACE, "FACE"), (CROWN, "CROWN"), (MARK, "MARK")):
            if s not in table:
                errors.append(f"{n} has no line for AnimalSpecies.{s}")
    for c in brief.MOTIFS:
        if c not in MOTIF:
            errors.append(f"MOTIF has no bust line for {c}")
    for f in brief.DOMAIN_OF:
        if f not in GEAR:
            errors.append(f"GEAR has no bust line for {f}")
    for tbl in (FACE, MARK, MOTIF, GEAR):
        for k, v in tbl.items():
            if not v.isascii():
                errors.append(f"non-ASCII line for {k}")
    for k, (part, line) in CROWN.items():
        if not (part.isascii() and line.isascii()):
            errors.append(f"non-ASCII CROWN line for {k}")
    b = compose(("Lynx", "Jackal"), "Hunter", "Wyvendo Waveline", "TALON")
    for must in ("LYNX owns the face", "JACKAL owns the ears", "HUMAN owns the eyes", "#C800C3", "#222645", MOTIF["TALON"]):
        if must not in b:
            errors.append(f"the composed brief lacks {must!r}")
    if compose(("Owl", "Lynx"), "Aristocrat", "X") != compose(("Owl", "Lynx"), "Aristocrat", "X"):
        errors.append("compose is not deterministic")
    for bad in ((("Dragon", "Owl"), "Hunter"), (("Owl", "Dragon"), "Hunter")):
        try:
            compose(bad[0], bad[1], "X")
            errors.append(f"compose accepted {bad[0]}")
        except KeyError:
            pass
    print("bust.py self-test:", "FAIL" if errors else f"ok ({len(species)} species, {len(FACE)} faces, {len(CROWN)} crowns)")
    for e in errors:
        print("  -", e)
    return 1 if errors else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("spec", nargs="?")
    ap.add_argument("--callsign")
    ap.add_argument("--heritage", nargs=2)
    ap.add_argument("--faction")
    ap.add_argument("--name", default="")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    if args.spec:
        p = importlib.import_module(f"specs.{args.spec}").PORTRAIT
        print(compose(tuple(p["heritage"]), p["faction"], p["person"], p["callsign"], **p.get("bust", {})))
    else:
        print(compose(tuple(args.heritage), args.faction, args.name, args.callsign))
    return 0


if __name__ == "__main__":
    sys.exit(main())
