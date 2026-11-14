#!/usr/bin/env python3
"""The SIGNATURE BUST: the Cosmic Shore pilot portrait made for the avatar crop.

Garrett, 2026-11-10: "pilot images that really show both clades in the avatar bust ... when people see it they know it
is cosmic shore because only we have triple hybrid people with that unique and high quality style". Round 1 painted
all 50 avatar pilots on one navy plate with a ring halo, human eyes, human hair and a human hand in every frame.
Garrett, 2026-11-11: "a good step forward, but the backgrounds need much more variance. they don't all need a hand
... any creature that already has a hand or tentacle like a frog, primate, or octopus it doesn't need a hand. the
human side is the least important to express when it is wearing clothes". A blind read of round 1 (seven readers
who were told nothing, graded by blind.py) named both animals in 26 of 50 at full size and 9 of 50 at icon size,
found a human hand in all 50 and found that hand to be the commonest painting flaw. This file is round 2, whose
blind read named both in 44 of 50 and 25 at icon size.

Why a bust brief at all: brief.py (the full-body portrait) gives the second animal ONE feature on the body (a tail,
wings, a shell), so the avatar's head-and-shoulders cut loses it. The bust is painted AS a bust and gives each
heritage its own ground inside the crop:

  * FIRST animal  -> the FACE and skull (FACE[a]), and its own eyes where the eye is the animal's tell (EYES[a]: a
                     tree frog's red eyes; human eyes in a frog's face read as a goblin).
  * SECOND animal -> the CROWN (CROWN[b]: what grows from the head) AND everything behind the face: its coat (MARK[b])
                     covers the back of the head, the nape, the throat and the neck, blending into the face along
                     the jaw (round 1's patch on one cheek read as pasted on); plus, where it has one, ONE MORE piece
                     of its body in the frame (SECOND[b]: a tail over the shoulder, folded wings, a shell rim) or its
                     hand (LIMB_TELL[b]: an otter's paws around a pebble). When the face already shows the crown's
                     organ (ears on a hare), ALT[b] is used instead of a second pair. Wings, tails and tail fans grow
                     from the BACK and stand behind the clothes, never from the head (round 3).
  * HUMAN         -> the bearing, the CLOTHES (GEAR: three outfits per faction type) and the gaze. Human hair for
                     some pilots only (never over a crown that covers the scalp); a human hand almost never, and never
                     beside hair or on a pilot whose animal has a hand or a tentacle of its own (LIMB), which can show
                     that instead. A pilot's hand can always hold and use things: never a claw, a pincer or a club.

Round 3 (Garrett, 2026-11-11, on round 2: "These are great. only a few need changing"): "please never make crab hands
(how would they interact with things?)", "butterfly Wings on a head, as ears, is nonsensical", "the turkey tail
feathers are in a very weird spot with respect to the dog's hood", "the chameleon transitioning to jellyfish looks a
bit abrupt", and "The backgrounds that don't look like they could be earth are the best". Each is a rule in a table
below, held by --self-test, so every pilot rolled from now on inherits it; eight busts were repainted.

What every bust shares, so the set reads as one: the framing (the head in the upper middle, cropped at mid-chest),
the painterly finish, the faction's DOMAIN colour as the clothes' trim, the rim light and a thin glowing ring around
each eye (the pilot's sight: a pilot is a pair of eyes, CALLSIGNS.md section 1), and a few crystal motes in the air.
What differs: the BACKDROP (BACKDROPS: places of the HyperSea and each faction type's home ground, each with its own
light, hue and value), the outfit, the pose and the expression. Square, 1024, painted with its background.

The animals: the 48 AnimalSpecies the game rolls (held to the enum by --self-test) plus the animals of the 32 legacy
painted pilots, so every pilot in the avatar catalog can be painted from one table.

    python3 Tools/Build/pilot_art/bust.py --callsign TALON --heritage Lynx Jackal --faction Hunter --name "..."
    python3 Tools/Build/pilot_art/bust.py --backdrops                # the backdrop table
    python3 Tools/Build/pilot_art/bust.py --self-test
The roster of every avatar pilot, what each was painted with (creator.CAST) and the painting loop: creator.py.
"""
import argparse
import functools
import hashlib
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import brief  # noqa: E402  (DOMAIN_OF, ACCENT, the enum readers)

# ---------------------------------------------------------------------------------------------------------------
# FACE: what an animal owns as the FIRST heritage, seen in a bust. No eyes here: EYES or the human gaze adds them.
# ---------------------------------------------------------------------------------------------------------------
FACE = {
    # the 48 AnimalSpecies
    "Axolotl": "an axolotl's broad smooth face, soft pale-pink skin, a wide flat gently smiling mouth, a rounded snout, and three feathery crimson gill fronds fanning out from each side of the head",
    "Heron": "a grey heron's narrow white face, a long yellow dagger beak, a black stripe sweeping back from the eye",
    "Otter": "a sea otter's round face, a pale grizzled muzzle and cheeks, long white whiskers, a broad dark nose, dense brown fur",
    "Pangolin": "a pangolin's small conical face covered in overlapping brown keratin scales, a long narrow snout",
    "Gecko": "a leopard gecko's broad flat head, pale yellow skin with bold black spots, a wide lipless smile, fine beaded scales",
    "Lynx": "a lynx's broad face, wide fluffy pale cheek ruffs, a short broad muzzle with a pink nose, tawny fur with dark spots on the brow and cheeks, and two tall ears tipped with long black tufts",
    "Narwhal": "a narwhal's large rounded whale head with no neck, a blunt bulbous melon brow, mottled grey-and-white skin, and one long thick spiral-grooved ivory tusk jutting forward from the upper lip",
    "Capybara": "a capybara's big square blunt face, a deep boxy muzzle, coarse russet-brown fur",
    "Octopus": "an octopus's bulbous mantle for a head, smooth ochre-and-rust skin with fine texture, a fringe of short suckered arms curling at the jaw like a beard",
    "Raven": "a raven's glossy black face, a heavy curved black beak, shaggy throat hackles with a violet sheen",
    "Tortoise": "a tortoise's blunt beaked face, leathery olive skin in fine scales, a hooked horny upper lip",
    "Fennec": "a fennec fox's small cream face, a tiny black nose, a delicate pointed muzzle, and its two huge cream ears",
    "Mantis": "a praying mantis's triangular green head with a narrow pointed chin, fine mouthparts and smooth pale-green chitin",
    "Walrus": "a walrus's broad heavy face, a thick bristled moustache, two long ivory tusks hanging down to the chest, wrinkled cinnamon skin",
    "Ibis": "a scarlet ibis's bare pink face, a long down-curved red bill, vivid scarlet feathers",
    "Tapir": "a Malayan tapir's long face, a short drooping prehensile trunk, a black head with white-rimmed ears",
    "Jellyfish": "a translucent jellyfish bell for a head, softly glowing lilac and pale blue, with fine frilled oral arms hanging at the jaw",
    "Badger": "a European badger's wedge-shaped face, a bold white head with two black stripes running over the eyes, a black nose",
    "Chameleon": "a veiled chameleon's narrow wedge-shaped face, a wide thin-lipped mouth, a tall bony casque rising from the back of the skull, bright green granular skin with turquoise and yellow bands",
    "Owl": "a great horned owl's flat round facial disc of barred brown and cream feathers, a small hooked dark beak, a white throat bib",
    "Seahorse": "a seahorse's face, a long tubular snout, a bony ridged brow, armoured coral-orange plates",
    "Wolf": "a grey wolf's long face, a pale muzzle, thick grey and cream fur, a dark nose, tall thick-furred ears",
    "Lemur": "a ring-tailed lemur's face, a narrow black muzzle, a white mask with dark rings around the eyes, soft grey fur",
    "Cuttlefish": "a cuttlefish's broad head with a short crown of arms at the mouth, skin rippling with bands of zebra-striped colour",
    "Moose": "a moose's very long heavy face ending in a broad, drooping, overhanging bulbous muzzle with wide nostrils, dark chocolate-brown fur paling to grey at the muzzle, a furry dewlap bell under the throat, and a pair of small flat palmate antlers like open hands spreading sideways from the brow",
    "Hummingbird": "a hummingbird's small iridescent face, a long needle-thin bill, a shimmering emerald and ruby throat gorget",
    "Armadillo": "an armadillo's long narrow face, a pointed snout, a banded leathery head shield",
    "Koi": "a koi carp's face, two barbels at the mouth, white skin patched with orange and black, fine scales",
    "Hyena": "a spotted hyena's broad heavy face, a blunt black muzzle, round ears, sandy fur with dark spots",
    "Platypus": "a platypus's face: a broad, flat, soft dark-grey duck bill with two nostril dots near its tip, and sleek dense brown fur",
    "Crane": "a whooping crane's white face, a long pointed yellow bill, a bare red crown patch, black whisker marks",
    "Beetle": "a stag beetle's head, glossy black-brown chitin plates, two short mandible tusks at the jaw",
    "Bison": "an American bison's massive face, a shaggy dark-brown beard and forelock, two short curved black horns",
    "Newt": "a fire-bellied newt's smooth face, dark green-brown skin above and a bright orange throat, small dark spots",
    "Puffin": "a puffin's face, a big triangular bill striped red, yellow and slate, a white face, a black cap",
    "Jackal": "a black-backed jackal's narrow fox-like face, a tan muzzle, a pale throat, rusty fur, tall pointed ears",
    "Sloth": "a three-toed sloth's round face, a dark eye mask, a gentle smile, shaggy greenish-brown fur",
    "Lobster": "a lobster's armoured head shield, a pointed rostrum between the eyes, deep blue-red shell, and two very long antennae",
    "Ocelot": "an ocelot's face, golden fur with black-ringed rosettes, white cheek stripes, a pink nose",
    "Pelican": "a brown pelican's face, a very long flat bill with a pale throat pouch, a white head",
    "Hedgehog": "a hedgehog's small pointed face, a shiny black nose on a tapering snout, soft pale-brown face fur, and a thick fringe of brown-and-cream spines bristling out around the brow and cheeks",
    "Mole": "a star-nosed mole's face, a pink fleshy star of small tentacles at the tip of the snout, velvety charcoal fur, no visible ears",
    "Toucan": "a toco toucan's face, a huge orange bill with a black tip, a white throat, a blue eye ring",
    "Yak": "a yak's long broad black-brown face under a shaggy fringe, a pale grey muzzle, long straight hair hanging from the cheeks and jaw like a skirt, and two long slender smooth yak horns rising from the sides of the head, sweeping out wide and then curving up to sharp dark tips",
    "Tardigrade": "a tardigrade's plump segmented head, translucent cream skin, a round tube mouth ringed with tiny teeth",
    "Firefly": "a firefly's head, a black face with a red-and-yellow pronotum shield above the eyes, two segmented antennae",
    "Orangutan": "an orangutan's wide disc-shaped face of dark leather, wide cheek flanges, a ring of long orange hair",
    "Eel": "a moray eel's long face, an open toothy mouth, mottled green-gold skin, small tube nostrils",
}
# The legacy painted pilots' animals that are not AnimalSpecies (Docs/Pilots/PORTRAITS.md section 2).
LEGACY_FACE = {
    "Snail": "a sea snail's soft rounded head with no nose and no eyes on the face, glossy blue-grey skin, a small gentle mouth, two long eye stalks rising from the top of the head, and the snail's own big spiral shell, cream banded with brown, carried on the back and rising behind one shoulder",
    "Horned lizard": "a horned lizard's flat broad head, a crown of short sharp horns at the back of the skull, sandy scaled skin",
    "Clownfish": "a clownfish's blunt rounded fish head with no nose and no ears: a short bulging snout, a small thick-lipped fish mouth, smooth scaled vivid orange skin, one broad white band edged in black wrapping the head behind the eyes like a headband, gill covers at the jaw, and a small rounded orange fin edged in black along the top of the head",
    "Tree frog": "a red-eyed tree frog's wide flat face, a broad mouth that runs from cheek to cheek, two small nostril dots, smooth bright leaf-green skin and a pale cream throat",
    "Mouse": "a white mouse's small face, a pink nose, fine whiskers, two large round pink ears",
    "Cheetah": "a cheetah's lean face, golden fur with black spots, black tear lines running from the eyes",
    "Oryx": "a gemsbok oryx's long narrow antelope face, white with a black blaze down the nose and a black band running from each eye down to the jaw, fawn-grey fur on the brow and cheeks, a dark muzzle, and two very long slender ringed horns rising straight up and back in a tall narrow V, each as long as the head",
    "Orca": "an orca's large rounded whale head with no neck and no beak: a smooth blunt glossy black dome, a short wide snout, a long wide mouth showing a row of small conical teeth, an oval white patch behind each eye, and a white chin and throat",
    "Coyote": "a coyote's narrow face, a long slim muzzle with a black nose, grizzled grey-tan fur, a pale throat, tall pointed ears",
    "Black hare": "a black hare's long face, sleek black fur, a white muzzle with a cleft upper lip, and two very long ears standing taller than the head",
    "Ostrich": "an ostrich's small flat head on a long bare pink-grey neck, a wide flat pale beak, sparse pale fuzz on the crown, long dark eyelashes",
    "Zebra": "a zebra's long face, bold black-and-white stripes, a dark muzzle, an upright striped mane",
    "Egret": "a great egret's slim white face, a long yellow bill, fine white plumes",
    "Caracal": "a caracal's face, sleek tawny fur, black and white face markings, two tall black-tufted ears",
    "Flamingo": "a flamingo's face, a bent black-tipped pink bill, soft rose-pink feathers",
    "Shoebill": "a shoebill's face, a huge clog-shaped bill, pale horn-yellow mottled with grey and hooked at the tip, slate-blue feathers, a small tuft at the back of the head",
    "Tiger": "a tiger's broad face, orange fur with black stripes, white cheeks and brows",
    "Parrot": "a scarlet macaw's face, a big curved bill, bare white cheek skin, scarlet feathers",
    "Basilisk": "a green basilisk lizard's face, a tall sail-like crest on the back of the head, bright green scales",
    "Booby": "a blue-footed booby's face, a long pointed blue-grey bill, a white-flecked brown head and a white breast",
    "Mandrill": "a mandrill's face, a long bright red nose ridge flanked by ridged blue cheeks, a golden beard",
    "Opossum": "an opossum's pointed white face, a long narrow snout ending in a pink nose, long white whiskers",
    "Fox": "a red fox's face, russet fur, a white muzzle and throat, black-backed ears",
    "Hare": "a brown hare's face, a rounded muzzle with a cleft upper lip and long whiskers, tawny grizzled fur, and two very long black-tipped ears standing taller than the head",
    "Kangaroo": "a red kangaroo's long gentle face, soft russet fur, a broad dark nose, tall upright ears",
}
FACE.update(LEGACY_FACE)

# EYES: the face animals whose own eye is a tell (cold-blooded and many-legged faces, and three birds). Round 1 put
# human eyes in every face: on a frog, a chameleon, a gecko or a mantis the blind readers saw "a goblin", "lizardfolk",
# "an alien". Every other face gets the person's gaze.
EYES = {
    "Tree frog": "huge round bulging tree-frog eyes, vivid red with narrow vertical pupils",
    "Chameleon": "cone-shaped chameleon turret eyes, their scaly lids closed down to a small round opening",
    "Gecko": "large lidless gecko eyes, silver-gold with a vertical slit pupil",
    "Owl": "enormous round owl eyes, golden yellow with wide black pupils",
    "Octopus": "heavy-lidded octopus eyes, gold with a horizontal bar pupil",
    "Cuttlefish": "cuttlefish eyes with a curved W-shaped pupil",
    "Mantis": "two large bulging mantis eyes at the upper corners of the head, pale green, each with a small dark pupil spot",
    "Newt": "small round newt eyes, gold-flecked with a dark pupil",
    "Axolotl": "small round lidless axolotl eyes, dark with a thin gold ring, set wide apart",
    "Tortoise": "small dark tortoise eyes under heavy wrinkled lids",
    "Horned lizard": "small dark lizard eyes under scaly brow ridges",
    "Basilisk": "round golden basilisk eyes with a round dark pupil",
    "Beetle": "small glossy black beetle eyes",
    "Firefly": "large rounded black compound eyes",
    "Lobster": "black bead eyes on short stalks",
    "Tardigrade": "two tiny black eye spots",
    "Jellyfish": "two soft glowing eye-lights inside the bell",
    "Seahorse": "small round seahorse eyes on raised turrets",
    "Koi": "round lidless koi eyes rimmed in gold",
    "Clownfish": "round lidless fish eyes, dark with an orange rim, set wide on the sides of the head",
    "Eel": "small round pale moray eyes",
    "Orca": "small dark gentle whale eyes set low, ahead of the white eye patch",
    "Narwhal": "small dark gentle whale eyes",
    "Shoebill": "pale yellow shoebill eyes with a hard unblinking stare under heavy brow ridges",
    "Booby": "pale yellow booby eyes with a small dark pupil",
    "Lemur": "round amber-orange lemur eyes",
    "Mole": "tiny dark bead eyes almost lost in the fur",
    "Snail": "small round dark snail eyes at the tips of the two tall eye stalks, the face below them smooth and eyeless",
}
# Faces with a beak or a bill: it stays rigid (round 1 bent beaks into a smile line).
# FACE_EARS: the ears a face shows only when the second animal brings none of its own. An opossum's bare black ears
# are part of its look, but under a lynx crown the lynx's ears are the only pair (round 2: the opossum's FACE line
# named its ears and the lynx crown said "the only ears on the head" in the same brief). A cat's or a badger's face
# needs them said: under a heron's plumes, a tortoise's shell cap and an armadillo's head shield the ocelot, the tiger
# and the badger were painted with no ears at all, and the blind readers called the heads odd. The animals whose
# ears ARE the tell keep them whatever the crown is: KEEPS.
FACE_EARS = {
    "Opossum": "thin bare black-edged ears",
    "Capybara": "small round ears set far back",
    "Ocelot": "two rounded ocelot ears, black on the backs with a white spot",
    "Tiger": "two rounded tiger ears, black on the backs with a white spot",
    "Cheetah": "two small rounded cheetah ears",
    "Badger": "two small round black badger ears tipped with white",
}
# A face with no ears says so, and stops saying so under a crown that brings ears (a fuzz of every pair composed a
# mole's "no visible ears" beside a fennec's two enormous ones): the words face_of cuts from FACE[a] then.
FACE_NO_EARS = {"Mole": ", no visible ears", "Clownfish": " and no ears"}
BEAKED = {"Heron", "Raven", "Ibis", "Owl", "Hummingbird", "Crane", "Puffin", "Pelican", "Toucan", "Ostrich", "Egret",
          "Flamingo", "Shoebill", "Parrot", "Booby", "Tortoise", "Platypus"}
# What a FACE already shows on its own head, so a crown of the same organ would be a second pair (ALT is used).
KEEPS = {
    "Fennec": ("ears",), "Mouse": ("ears",), "Hare": ("ears",), "Black hare": ("ears",), "Caracal": ("ears",),
    "Lynx": ("ears",), "Kangaroo": ("ears",), "Fox": ("ears",), "Coyote": ("ears",), "Jackal": ("ears",),
    "Wolf": ("ears",), "Tapir": ("ears",), "Hyena": ("ears",),
    "Oryx": ("horns",), "Yak": ("horns",), "Bison": ("horns",), "Moose": ("horns",), "Horned lizard": ("horns",),
}

# ---------------------------------------------------------------------------------------------------------------
# CROWN: what an animal grows from the head as the SECOND heritage:
#   (the part's word, its organ kind, whether it covers the scalp (no human hair over it), the line).
# Round 1's lessons, from the blind read: a crown reads when it is a structure only that animal has (a jellyfish
# bell, butterfly wings, ram horns, a lionfish's spines) and fails when it is a generic organ (pointed ears on a cat,
# a feather crest, plain horns, a plain fin, antennae): those lines now name the animal's own pattern and SECOND or
# LIMB_TELL adds another piece of the body. A narwhal's tusk on the brow reads as a unicorn's horn.
# ---------------------------------------------------------------------------------------------------------------
CROWN = {
    "Axolotl": ("gills", "other", False, "three feathery crimson axolotl gill fronds fanning out from each side of the head"),
    "Heron": ("plumes", "other", True, "a grey heron's crown, smooth white feathers on top of the head between two broad black stripes that run back from above each eye and meet at the nape in two thin black plumes, no longer than the head, lying flat down the back of the neck"),
    "Otter": ("fur", "other", True, "a sea otter's sleek head fur, dense chocolate brown and slicked back as if wet, with a fan of long stiff white whiskers at each cheek"),
    "Pangolin": ("scales", "other", True, "large overlapping pangolin scales, each a pointed tan plate, covering the top of the head like a pinecone helm"),
    "Gecko": ("skin", "other", True, "bumpy pale-yellow leopard gecko skin with bold black spots over the whole top of the head, and a ridge of fine beaded scales along each brow"),
    "Lynx": ("ears", "ears", False, "two tall lynx ears with long black tufts at the tips, and a wide pale lynx beard-ruff barred with black flaring from the jaw on both sides"),
    "Narwhal": ("tusk", "other", True, "a narwhal's smooth domed brow of mottled grey-and-white whale skin over the top of the head, hairless and glossy, and one long straight spiral-grooved ivory tusk jutting forward and down from the left side of the upper jaw, the way a narwhal carries it"),
    "Capybara": ("fur", "other", True, "coarse, sparse russet-brown capybara fur brushed back over a broad flat-topped head"),
    "Octopus": ("arms", "other", True, "suckered ochre-and-rust octopus arms growing from the crown and the back of the head in place of hair, curling down over the shoulders with their pale suckers showing"),
    "Raven": ("feathers", "other", True, "glossy black raven feathers with a blue-violet sheen sweeping back over the whole crown, and a shaggy ruff of pointed raven throat hackles around the neck"),
    "Tortoise": ("shell", "other", True, "a domed cap of tortoise-shell plates on top of the head, each olive-brown scute ringed with golden growth lines"),
    "Fennec": ("ears", "ears", False, "two enormous cream fennec fox ears, pink inside, standing up and out wider than the head"),
    "Mantis": ("antennae", "other", False, "two long fine green mantis antennae sweeping back from the brow"),
    "Walrus": ("tusks", "other", False, "two long ivory walrus tusks curving down from the corners of the jaw to the chest, and a broad pad of stiff pale walrus whiskers above them"),
    "Ibis": ("feathers", "other", True, "sleek vivid scarlet ibis feathers covering the whole crown and nape"),
    "Tapir": ("ears", "ears", False, "two oval tapir ears rimmed in white, and a short stiff tapir crest of dark hair along the top of the neck"),
    "Jellyfish": ("veil", "other", True, "a translucent glowing lilac jellyfish bell that the top and the back of the head turn into by degrees, with no rim where it begins, the {a}'s own skin growing paler and clearer toward the crown until it is glowing jelly, and fine frilled tentacles trailing down behind the head"),
    "Badger": ("stripes", "other", True, "the badger's head pattern over the whole top of the head, a white cap from the brow to the nape split by two bold black stripes that start above each eye and run back over the crown, three white bands and two black"),
    "Chameleon": ("casque", "other", True, "a tall pointed green chameleon casque rising from the back of the head, banded turquoise and yellow"),
    "Owl": ("ear tufts", "other", True, "two tall feathered owl ear tufts, barred brown and cream, and a ruff of fine radiating owl disc feathers framing the face"),
    "Seahorse": ("coronet", "other", False, "a seahorse's bony coronet of coral-orange spikes on top of the head, with small translucent fan fins at the temples"),
    "Wolf": ("ears", "ears", False, "two tall thick-furred grey wolf ears, and a heavy grey-and-cream wolf ruff standing out around the neck and jaw"),
    "Lemur": ("ears", "ears", False, "two round fluffy white lemur ears edged in black"),
    "Cuttlefish": ("fin", "other", True, "skin rippling with moving zebra stripes over the whole crown, edged from temple to temple by a soft translucent undulating cuttlefish fin"),
    "Moose": ("antlers", "horns", False, "broad palmate moose antlers, flat as open hands with a row of points, spreading wide from the sides of the head"),
    "Hummingbird": ("gorget", "other", True, "tiny scale-like hummingbird crown feathers flashing emerald and gold, and a glittering ruby-magenta gorget of throat feathers flaring at the neck like a jewelled collar"),
    "Armadillo": ("bands", "other", True, "an armadillo's grey head shield, a smooth cap of small flat bony plates fitted edge to edge like a mosaic over the top of the head, like the cap of a helmet"),
    "Koi": ("fins", "other", False, "long flowing translucent koi fins, white splashed with orange, sweeping back from the temples and from behind the jaw, and two koi barbels at the corners of the mouth"),
    "Hyena": ("mane", "ears", True, "a coarse upright hyena mane running from the crown down the nape, and two large rounded hyena ears"),
    "Platypus": ("bill shield", "other", True, "a broad flat platypus bill shield lying over the brow like the peak of a cap, soft dark blue-grey leathery skin rounded at the front with two nostril dots, and sleek dense brown platypus fur over the rest of the head"),
    "Crane": ("crown", "other", True, "a crane's bare red crown patch on top of a white-feathered head, with a black stripe running back from each eye"),
    "Beetle": ("carapace", "other", True, "a glossy black-brown beetle carapace over the top and back of the head like a helm, split down the middle by the wing-case seam, with a pair of short clubbed antennae at the brow"),
    "Bison": ("wool", "horns", True, "a bison's huge shaggy forelock, a mound of dark curly wool covering the crown and brow, with two short curved black horns just showing through it at the sides and a long dark beard under the chin"),
    "Newt": ("crest", "other", True, "a great crested newt's tall ragged crest standing along the midline from the brow over the crown and down the nape, dark olive with black spots"),
    "Puffin": ("cap", "other", True, "a puffin's head pattern, a glossy black cap over the crown and nape with large pale grey-white cheek patches, and a small triangular brow plate striped in the puffin bill's red, yellow and slate"),
    "Jackal": ("ears", "ears", False, "two tall pointed black-backed jackal ears, rusty orange with dark edges, standing straight up"),
    "Sloth": ("fur", "other", True, "a three-toed sloth's long coarse grey-brown head fur tinged green with algae, parted in the middle and hanging over the brow, with a dark sloth mask stripe running back from the outer corner of each eye"),
    "Lobster": ("antennae", "other", True, "a red armoured lobster head shield over the crown with a short spiked rostrum at the brow, and two very long red lobster antennae sweeping back from it"),
    "Ocelot": ("ears", "ears", False, "two rounded ocelot ears, black on the backs with a bright white spot, and two bold black stripes running back from the inner corner of each eye over the crown"),
    "Pelican": ("pouch", "other", True, "a pelican's long soft throat pouch hanging beneath the jaw from chin to collar, pale orange-pink and finely veined, and a white-feathered pelican head with a pale yellow wash on the crown and a shaggy tuft at the nape"),
    "Hedgehog": ("spines", "other", True, "a rounded dome of short dense hedgehog spines, brown banded with cream at the tips, covering the top and back of the head like a pincushion"),
    "Mole": ("velvet", "other", True, "sleek velvety charcoal-black mole fur over the whole head{no ears}, and the star-nosed mole's pink star of small fleshy rays at the very tip of the nose"),
    "Toucan": ("casque", "other", True, "glossy black toucan feathers over the crown and nape, a ring of bare blue skin around each eye, and a casque on the brow shaped like the ridge of a toucan's bill, orange shading to yellow with a black tip"),
    "Yak": ("horns", "horns", True, "two long slender smooth yak horns rising from the sides of the head, sweeping out wide and then curving up and back to sharp dark tips, and very long straight silky black-brown yak hair hanging from the crown in a heavy curtain over the nape and shoulders"),
    "Tardigrade": ("folds", "other", True, "soft translucent cream tardigrade segment folds, plump and rounded, stacked over the crown and down the nape like the rolls of a quilted hood"),
    "Firefly": ("antennae", "other", False, "two long segmented black firefly antennae, and the firefly's red-and-yellow shield plate over the brow"),
    "Orangutan": ("flanges", "other", True, "long shaggy orange orangutan hair over the whole head and shoulders, and wide dark leathery orangutan cheek flanges framing the face on both sides"),
    "Eel": ("fin", "other", True, "a moray eel's long ribbon fin standing along the midline from the brow over the crown and down the nape, its edge golden"),
}
LEGACY_CROWN = {   # the legacy painted pilots' second animals that are not AnimalSpecies
    "Scorpion": ("tail", "other", False, "a segmented scorpion tail, glossy dark red, rising from behind the shoulders and arching forward over the head with its curved stinger poised above the brow"),
    "Orca": ("fin", "other", True, "a tall glossy black orca dorsal fin rising from the crown like a blade, and a crisp white orca eye patch swept back from behind each eye"),
    "Eagle": ("mane", "other", False, "a mane of snow-white bald eagle feathers sweeping back from behind the face, over the nape and down the neck to the shoulders, leaving the top of the head bare of feathers"),
    "Turkey": ("wattle", "other", False, "a long red turkey wattle hanging at the throat, and a row of small red turkey caruncles down each side of the neck"),
    "Butterfly": ("antennae", "other", False, "two slender black butterfly antennae with clubbed tips rising from the brow"),
    "Porcupine": ("quills", "other", True, "a great crest of long porcupine quills banded black and white, sweeping back from the brow over the whole head"),
    "Lionfish": ("spines", "other", False, "a crown of long lionfish spines banded red and white, fanning out from the back of the head, each trailing a strip of striped fin"),
    "Red panda": ("ears", "ears", False, "two large pointed red panda ears rimmed in thick white fur, with white cheek patches and russet tear tracks below them"),
    "Mantis shrimp": ("carapace", "other", True, "a peacock mantis shrimp's carapace over the crown, olive green spotted like a leopard and edged in electric blue, with flat red-fringed antennal paddles at the temples and fine red feelers sweeping back"),
    "Crocodile": ("scutes", "other", True, "rows of square armoured crocodile scutes, olive-brown and keeled, covering the crown and running down the nape in a double ridge"),
    "Hawk": ("mantle", "other", False, "a mantle of streaked brown-and-cream red-tailed hawk feathers sweeping back from behind the face, over the nape and down the neck, leaving the top of the head bare of feathers"),
    "Peacock": ("crest", "other", False, "a peacock's upright crest of fine bare feather stems, each tipped with a small blue fan, standing on top of the head"),
    "Ram": ("horns", "horns", False, "two heavy ridged ram horns curling down and around the sides of the head"),
    "Tiger": ("ears", "ears", False, "two rounded tiger ears, black on the backs with a bold white spot, a wide orange-and-white tiger cheek ruff, and black tiger stripes running back over the crown"),
    "Deer": ("antlers", "horns", False, "a pair of branching stag antlers rising from the top of the head"),
    "Skunk": ("stripes", "other", True, "the striped skunk's head pattern over the whole top of the head: a thin white blaze from the bridge of the nose up between the eyes, widening on the crown into a snow-white cap that divides at the nape into two broad white stripes running on down the back of the neck, all of it in long glossy jet-black fur"),
}
for _k, _v in LEGACY_CROWN.items():
    CROWN.setdefault(_k, _v)

# EARS_TOO: the small ears a second animal adds to a crown that is not ears, where the face does not keep ears of its
# own (KEEPS): the one mammal tell a hedgehog can put on a lizard, whose spines otherwise read as the lizard's. A
# CROWN line whose organ is not ears never names ears itself (--self-test): on a lynx's face the otter's line asked
# for a second pair. A crown that shows NO ears says so in the slot {no ears}, which crown_of fills only on a head
# with no ears of its own (a mole's "no visible ears" contradicted a wolf's face).
EARS_TOO = {
    "Hedgehog": "two small round furry hedgehog ears showing at the front edge of the spines",
    "Badger": "two small round black badger ears tipped with white",
    "Otter": "two tiny round otter ears set low on the sides of the head",
    "Capybara": "two small rounded capybara ears set far back",
}
NO_EARS = " with no visible ears"

# ALT: the crown when the FACE already shows that organ (KEEPS): (part, covers the scalp, line; {a} is the face animal).
ALT = {
    "Lynx": ("ear tufts", False, "long black lynx tufts tipping the {a}'s own ears, and a wide pale lynx beard-ruff barred with black flaring from the jaw on both sides"),
    "Fennec": ("coat", True, "the fennec's pale cream coat over the whole crown, soft and dense, and long cream fennec fur lining the {a}'s own ears"),
    "Wolf": ("ruff", False, "a heavy grey-and-cream wolf ruff standing out around the neck and jaw like a mane"),
    "Lemur": ("mask", True, "the ring-tailed lemur's white fur over the crown with a dark widow's peak at the brow"),
    "Jackal": ("saddle", True, "the black-backed jackal's saddle, a thick mantle of black-and-silver grizzled fur over the crown and the nape, sharply edged against rusty-orange fur at the sides"),
    "Ocelot": ("rosettes", True, "the ocelot's golden coat over the crown, marked with chains of black-ringed rosettes, and two bold black stripes running back from the inner corner of each eye"),
    "Hyena": ("mane", True, "a coarse upright hyena mane running from the crown down the nape and standing along the shoulders"),
    "Red panda": ("mask", False, "white red panda rims on the {a}'s own ears, with white cheek patches and russet tear tracks at the edges of the face"),
    "Tiger": ("stripes", True, "bold black tiger stripes running back over the crown, and a wide orange-and-white tiger cheek ruff flaring from the jaw"),
    "Tapir": ("crest", False, "a short stiff tapir crest of dark hair along the top of the head and neck, and white tapir rims on the {a}'s own ears"),
    "Bison": ("wool", True, "a bison's huge shaggy forelock, a mound of dark curly wool covering the crown and brow, and a long dark beard under the chin"),
    "Yak": ("hair", True, "very long straight silky black-brown yak hair hanging from the crown in a heavy curtain over the nape and shoulders"),
    "Ram": ("fleece", True, "tight cream ram fleece curls covering the crown and the nape"),
    "Moose": ("bell", False, "a dark hump of coarse moose hair behind the neck and the moose's furry dewlap bell hanging at the throat"),
    "Deer": ("spots", True, "the fawn's coat over the crown, soft tawny fur dappled with rows of white spots, and a white deer throat patch"),
}

# SECOND: one more piece of the second animal's body inside the frame (kind, line). The lemur's ringed tail, the
# scorpion's tail and the turkey's fan were what the blind readers named those animals from; a hand no longer fills
# that part of the frame.
# Where it grows (Garrett, 2026-11-11: "butterfly Wings on a head, as ears, is nonsensical", "the turkey tail feathers
# are in a very weird spot with respect to the dog's hood"): WINGS grow from the back between the shoulder blades, a
# TAIL and a tail FAN from the base of the back, and all of them stand BEHIND the clothes. None grows from the head,
# and a fan never stands between a hood and the head: a fan or wings line says "behind the back" or "behind ...
# shoulders" and never "behind the head" (--self-test), and the composer lays a hood in front of them (BEHIND_HOOD).
SECOND = {
    "Heron": ("wings", "broad blue-grey heron wings with black flight feathers and a black patch at the bend, folded high behind both shoulders"),
    "Pangolin": ("tail", "a heavy pangolin tail armoured in the same overlapping scales, curled up around one shoulder"),
    "Gecko": ("tail", "a fat gecko tail ringed in black and white, curled over one shoulder"),
    "Narwhal": ("tail", "a narwhal's grey crescent tail fluke rising behind one shoulder"),
    "Raven": ("wings", "glossy black raven wings folded high behind both shoulders"),
    "Tortoise": ("shell", "the high domed rim of a tortoise carapace rising behind the neck and shoulders like a great collar"),
    "Fennec": ("tail", "a bushy cream fennec tail with a black tip curled over one shoulder"),
    "Ibis": ("wings", "scarlet ibis wings with black tips folded behind both shoulders"),
    "Chameleon": ("tail", "a green chameleon tail curled in a tight flat spiral, resting on one shoulder"),
    "Owl": ("wings", "broad soft owl wings, barred brown and cream, folded behind both shoulders"),
    "Seahorse": ("tail", "a seahorse's ridged prehensile tail curled in a spiral over one shoulder"),
    "Wolf": ("tail", "a long bushy grey wolf tail with a dark tip, held up straight behind one shoulder like a brush, not curled"),
    "Lemur": ("tail", "a long lemur tail ringed black and white, raised behind the head and curled over one shoulder"),
    "Hummingbird": ("wings", "two small narrow hummingbird wings blurred in mid-beat behind the shoulders"),
    "Armadillo": ("shell", "an armadillo's domed grey shell arching up behind the neck and shoulders, crossed by a row of narrow flexible bands like the plates of a cuirass"),
    "Koi": ("tail", "a wide translucent koi tail fin, white splashed with orange, fanning up behind one shoulder"),
    "Platypus": ("tail", "a broad flat furry platypus paddle tail raised behind one shoulder"),
    "Crane": ("wings", "white crane wings with black tips folded behind both shoulders"),
    "Beetle": ("shell", "two glossy beetle wing cases raised behind the shoulders, amber translucent flight wings unfolding beneath them"),
    "Bison": ("body", "a towering hump of shaggy dark-brown bison wool rising behind the neck and shoulders"),
    "Newt": ("tail", "a flat paddle-shaped newt tail with a wavy fin edge and an orange underside, curled over one shoulder"),
    "Jackal": ("tail", "a long bushy jackal tail, rusty with a black tip, held up straight behind one shoulder like a fox's brush, not curled"),
    "Ocelot": ("tail", "a long ocelot tail ringed in black, curled over one shoulder"),
    "Pelican": ("wings", "big white pelican wings with black flight feathers folded behind both shoulders"),
    "Firefly": ("body", "the firefly's lantern, a soft yellow-green glow rising from under the collar at the nape, and dark firefly wing cases edged in yellow folded behind the shoulders"),
    "Eel": ("tail", "the eel's long ribbon tail, edged with the same fin, looped once around the shoulders like a scarf"),
    "Orca": ("tail", "a black orca tail fluke, white beneath, rising behind one shoulder"),
    "Eagle": ("wings", "dark brown eagle wings folded high behind both shoulders"),
    "Red panda": ("tail", "a thick red panda tail ringed russet and cream, curled over one shoulder"),
    "Crocodile": ("tail", "a heavy crocodile tail with a double row of ridged scutes, curled around one shoulder"),
    "Hawk": ("fan", "barred hawk wings folded behind both shoulders, and a red-tailed hawk's tail feathers, rust-red with a dark band near the tips, fanned behind the back where a tail grows, their tips showing behind one shoulder and no higher than the jaw"),
    "Tiger": ("tail", "a thick tiger tail ringed in orange and black, curled over one shoulder"),
    "Mantis shrimp": ("tail", "a mantis shrimp's fanned tail plates in electric blue, green and red, raised behind one shoulder"),
    "Lionfish": ("fins", "wide striped lionfish fins spread behind the shoulders like fans"),
    "Tardigrade": ("body", "two pairs of stubby tardigrade legs, each ending in a ring of tiny claws, peeking over the shoulders"),
    "Lobster": ("tail", "a lobster's broad armoured tail, red segmented plates ending in a fan of flat blades, curled up behind one shoulder"),
    "Mantis": ("wings", "long narrow leaf-green mantis wings, veined like leaves, folded flat behind both shoulders like a cloak"),
    "Butterfly": ("wings", "two great monarch butterfly wings, orange veined in black and edged with white dots, growing from the back between the shoulder blades and standing open behind both shoulders, behind the clothes and well clear of the head and the neck"),
    "Turkey": ("fan", "a wide fan of bronze turkey tail feathers, banded and white-tipped, spread behind the back where a tail grows: it rises from below the frame behind both shoulders and behind the clothes, its tips no higher than the jaw, well clear of the head and the neck"),
    "Peacock": ("fan", "a peacock's train of iridescent green-gold tail feathers with blue eye-spots, fanned wide behind the back where a tail grows: it rises from below the frame behind both shoulders and behind the clothes, well clear of the head and the neck"),
    "Skunk": ("tail", "a huge bushy skunk tail as long as the body, a plume of long coarse hair as wide as the shoulders, black with the two white stripes of the back running on into it and white at the tip, standing up behind one shoulder"),
}
# The kinds of SECOND that stand up behind the shoulders: under a hooded outfit the composer says the hood lies in
# front of them (Krush's fan was painted between his hood and his head).
BEHIND_HOOD = ("fan", "wings", "fins", "shell")
HOODED = re.compile(r"\b(?:hood|hooded|hoodie|cowl)\b")

# LIMB: the animals that have a hand or a tentacle of their own (Garrett, 2026-11-11: "any creature that already has
# a hand or tentacle like a frog, primate, or octopus it doesn't need a hand"). A pilot with one never gets a human
# hand; about half of them show the limb instead.
# A pilot's hand can HOLD AND USE THINGS (Garrett, 2026-11-11: "please never make crab hands (how would they interact
# with things?)"). So a line here is a hand with fingers or a tentacle, and never a claw, a pincer, a club, a blade or
# a hook (--self-test): round 2's lobster claws, scorpion pincer, mantis forelegs and mantis shrimp clubs were
# repainted. The armoured animals (ARMOURED) wear their shell on a five-fingered hand like a gauntlet, and a pilot
# who has a true hand as well (a tree frog with a scorpion's tail) shows the true hand.
LIMB = {
    "Tree frog": "a tree frog's long-fingered green hand, each finger ending in a round orange sticky pad",
    "Octopus": "one long suckered octopus arm, ochre above and pale beneath, curling up from below",
    "Orangutan": "an orangutan's long dark leathery hand with hooked fingers, fringed with orange hair",
    "Mandrill": "a mandrill's dark long-fingered hand with olive fur on its back",
    "Lemur": "a lemur's slender black leathery hand with long thin fingers",
    "Gecko": "a gecko's small hand, five splayed toes with wide flat pads",
    "Chameleon": "a chameleon's gripping hand, its toes fused into two opposed green mittens",
    "Axolotl": "an axolotl's small soft pink hand with four slim fingers",
    "Newt": "a newt's small damp four-fingered hand, dark above and orange beneath",
    "Otter": "a sea otter's small dark webbed paw with nimble fingers",
    "Opossum": "an opossum's pink hand with five slender bare fingers",
    "Badger": "a badger's broad dark forepaw, the palm toward the viewer, with five toes and one long pale digging claw on each toe",
    "Mouse": "a mouse's small forepaw grown to the size of a person's hand, covered in short white fur to the "
             "fingertips and bare pink only at the pads, its four fingers and its thumb each ending in a small pale "
             "claw",
    "Kangaroo": "a kangaroo's small dark forepaw with five clawed fingers",
    "Mole": "a mole's broad pink hand, its five thick fingers ending in long pale nails",
    "Sloth": "a sloth's long shaggy arm ending in a slow long-fingered hand, a curved pale claw at the tip of each finger",
    "Cuttlefish": "one slim cuttlefish tentacle with a flat suckered club at its tip, curling up from below",
    "Lobster": "a hand with four fingers and a thumb, armoured in red lobster shell, every finger jointed in small overlapping plates like the fingers of a gauntlet",
    "Scorpion": "a hand with four fingers and a thumb, plated in glossy dark-red scorpion armour, every finger jointed like the fingers of a gauntlet",
    "Mantis": "a slender long-fingered hand of smooth leaf-green mantis chitin, four fingers and a thumb, with a row of fine thorns along the forearm",
    "Mantis shrimp": "a hand with four fingers and a thumb, armoured in a mantis shrimp's shell, red with white spots across the knuckles, every finger jointed like the fingers of a gauntlet",
    "Platypus": "a platypus's webbed forepaw, dark skin stretched between clawed fingers",
    "Tardigrade": "a soft translucent cream tardigrade hand with five plump stubby fingers",
    "Skunk": "a skunk's black forepaw with five slim fingers, each ending in a long pale curved digging claw",
    "Jellyfish": "",   # its tentacles trail from the bell (CROWN, FACE): no human hand, and nothing more to raise
}
# The animals whose own limb is a claw, a pincer, a blade or a club: their LIMB line is their shell on a hand with
# fingers. A pilot whose other animal has a true hand shows that one instead (human_tells).
ARMOURED = {"Lobster", "Scorpion", "Mantis", "Mantis shrimp"}
# What a limb line may never be, and what it must name (--self-test).
NOT_A_HAND = re.compile(r"\b(?:pincers?|crusher|striking clubs?|forelegs?|jack-kni(?:fe|ves)|hooks?)\b")
A_HAND = re.compile(r"\b(?:fingers?|fingered|toes|paws?|forepaws?|tentacle|suckered)\b")
# Animals that bring a mane or long hair of their own: human hair beside it is one head of hair too many.
OWN_HAIR = {"Zebra", "Yak", "Bison", "Orangutan", "Sloth"}
# LIMB_TELL: second animals whose limb is how people know them; it is always in the frame (the whole sentence).
LIMB_TELL = {
    "Sloth": "One arm is raised into the frame and it is the sloth's: a long shaggy grey-brown arm ending in a slow long-fingered hand, a curved pale claw at the tip of each finger, the fingers curled over the collar",
    "Mole": "Both hands are raised into the frame and they are the mole's: broad pink hands with five thick fingers and long pale nails, held up at the chest",
    "Otter": "Both hands are raised into the frame and they are the otter's: small dark webbed paws held together at the chest, cradling a smooth pebble the way an otter does",
}

# BLEND: how a second animal's coat meets the face, where the usual seam along the jaw is not enough ({a}: the face
# animal). A translucent animal against an opaque one was painted as a helmet with a hard edge (Garrett, 2026-11-11:
# "the chameleon transitioning to jellyfish looks a bit abrupt"), so its seam is a gradient that is written out.
SEAM = "meeting the {a}'s face along the jawline and the temples in a soft natural blend"
BLEND = {
    "Jellyfish": "and there is no line where the {a} ends: past the cheeks and the temples its skin thins and turns glassy by degrees, its colours fading out into the jelly over a hand's width",
}

# MARK: the second animal's coat, which covers everything behind the face (the composer says where).
MARK = {
    "Axolotl": "smooth pale-pink axolotl skin with tiny gold flecks",
    "Heron": "sleek blue-grey heron feathers on a long slender heron neck, with a narrow white stripe down the front of the throat marked by a row of black dashes, and a fringe of long thin grey plumes hanging from the base of the neck over the chest",
    "Otter": "dense chocolate-brown otter fur, silver-grizzled at the throat",
    "Pangolin": "pangolin armour scales, tan and umber overlapping plates",
    "Gecko": "yellow gecko skin with bold black spots",
    "Lynx": "tawny lynx fur with dark spots",
    "Narwhal": "mottled grey-and-white narwhal skin, speckled dark like a leopard seal",
    "Capybara": "coarse russet capybara fur",
    "Octopus": "smooth ochre octopus skin mottled rust and cream",
    "Raven": "glossy black raven feathers with a violet sheen",
    "Tortoise": "wrinkled, scaly olive tortoise skin",
    "Fennec": "fennec fur, soft cream and sand",
    "Mantis": "smooth leaf-green mantis chitin plates",
    "Walrus": "thick wrinkled cinnamon walrus hide in heavy folds",
    "Ibis": "vivid scarlet ibis feathers",
    "Tapir": "the tapir's two-tone coat, black at the head and neck with a crisp white saddle beginning at the collarbone",
    "Jellyfish": "translucent glowing lilac jellyfish skin",
    "Badger": "coarse grizzled silver-grey badger fur, black at the throat",
    "Chameleon": "green chameleon skin with turquoise and yellow bands",
    "Owl": "soft barred brown-and-cream owl feathers",
    "Seahorse": "seahorse ring-plates, coral orange and gold bony segments",
    "Wolf": "thick grey-and-cream wolf fur",
    "Lemur": "soft grey lemur fur with a white throat",
    "Cuttlefish": "cuttlefish skin rippling with zebra stripes and blue-green shimmer",
    "Moose": "dark brown moose fur",
    "Hummingbird": "iridescent emerald hummingbird feathers",
    "Armadillo": "grey banded armadillo armour, narrow smooth bony bands ringing the neck like the hoops of a barrel, with pink-grey leathery skin between them",
    "Koi": "koi skin patterning, bold white patches with vivid orange and black blotches and fine scales",
    "Hyena": "coarse sandy-grey hyena fur blotched with dark brown spots, on a thick powerful neck",
    "Platypus": "dense brown waterproof platypus fur",
    "Crane": "white crane feathers on a long neck",
    "Beetle": "glossy black-brown beetle chitin",
    "Bison": "shaggy dark-brown bison wool",
    "Newt": "smooth wet glossy newt skin, dark olive above with a vivid fire-orange throat blotched with black",
    "Puffin": "black puffin feathers above a white breast",
    "Jackal": "the jackal's coat, rusty-orange fur under a black-and-silver saddle",
    "Sloth": "long coarse grey-brown sloth fur tinged green with algae",
    "Lobster": "segmented red lobster shell",
    "Ocelot": "golden ocelot fur with black-ringed rosettes",
    "Pelican": "white pelican feathers shading to chestnut brown down the back of the neck",
    "Hedgehog": "brown-and-cream hedgehog spines above soft pale-brown fur at the throat",
    "Mole": "velvety charcoal mole fur",
    "Toucan": "glossy black toucan feathers with a white-and-yellow throat bib",
    "Yak": "very long straight shaggy black-brown yak hair, hanging from the neck and shoulders down over the chest like a skirt",
    "Tardigrade": "translucent cream tardigrade segments",
    "Firefly": "black firefly chitin with small glowing yellow-green spots",
    "Orangutan": "long orange orangutan hair",
    "Eel": "smooth scaleless moray skin, olive green with a honeycomb of golden-yellow blotches",
    # the legacy second animals
    "Scorpion": "segmented dark-red scorpion plates",
    "Orca": "smooth glossy black orca skin with a white throat",
    "Eagle": "dark brown eagle feathers edged in gold, beginning at the shoulders",
    "Turkey": "bronze iridescent turkey feathers and bare red-and-blue turkey neck skin",
    "Butterfly": "orange butterfly-wing scales with black veins",
    "Porcupine": "short banded porcupine quills over coarse dark fur",
    "Lionfish": "red-and-white lionfish stripes",
    "Red panda": "russet red panda fur, black at the throat",
    "Mantis shrimp": "a mantis shrimp's segmented armour in electric green and blue edged with red",
    "Crocodile": "olive-brown armoured crocodile hide with a pale cream throat of small smooth scales",
    "Hawk": "cream hawk breast feathers streaked with dark brown",
    "Peacock": "iridescent blue peacock neck feathers",
    "Ram": "thick cream ram fleece",
    "Tiger": "orange-and-black tiger stripes with a white throat",
    "Deer": "soft fawn deer fur with white spots and a white throat patch",
    "Skunk": "long glossy jet-black skunk fur with two broad white stripes running down the back of the neck",
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
    "BANDIT": "a brass eyepiece hanging on a cord at the chest, its lens etched with fine sighting lines, and a plain dark bandana knotted to the cord",
    "NEEDLE": "a compass-needle pendant on a fine chain at the throat",
    "GALLEON": "a ship's-wheel badge on the chest and a captain's braided epaulettes",
    "FLUME": "a water-ripple visor hanging on the chest and a coil of tunnel line over one shoulder",
}

# The faction type's clothes from the chest up: THREE outfits each, so two hunters do not wear one jacket. The
# clothes are the human side (Garrett, 2026-11-11), so each names its cut, its cloth and its own colour.
GEAR = {
    "Merchant": ["a long travelling coat of mustard wool worn open over a quilted vest with brass toggles",
                 "a cream linen trader's shirt under a bottle-green brocade waistcoat, a ledger satchel strap across the chest",
                 "a heavy indigo cloak pinned at one shoulder over a striped silk tunic, strings of trade tokens on the chest"],
    "Hunter": ["a weathered trapper's jacket of russet leather with wide lapels, laced at the chest and open at the throat",
               "a moss-green oilskin stalking cape over a laced leather jerkin, a coil of line across the chest",
               "a quilted grey tracker's vest over a rough cream wool shirt open at the throat, a bone toggle on a cord"],
    "Aristocrat": ["a tailored coat of ivory brocade with polished buttons, its collar folded open at the throat",
                   "a midnight-blue velvet court jacket with a low lace collar and a jewelled chain of office",
                   "a pale grey silk mantle draped over one shoulder above an embroidered waistcoat, a signet brooch at the throat"],
    "Pirate": ["a patched sea coat of faded blue with a wide lapel, a striped scarf hanging loose from one shoulder",
               "an open-necked cream shirt under a scuffed black leather baldric, a knotted red sash over one shoulder",
               "a salt-stained oxblood greatcoat with mismatched brass buttons and a necklace of sea-glass and teeth"],
    "BountyHunter": ["a long sand-coloured duster coat worn open at the throat, a bandolier strap across the chest",
                     "a plated shoulder guard over a dark canvas jacket, a coil of binding cord and a stack of blank warrant tags on the lapel",
                     "a slate-grey armoured vest over a rust-red undershirt open at the throat, a tracker's lens clipped to the strap"],
    "Explorer": ["a many-pocketed expedition jacket of sun-bleached khaki with goggles hanging on the chest",
                 "a padded sky-blue survey parka worn unzipped with the hood pushed back, a brass compass on a lanyard",
                 "a canvas chart-maker's smock with rolled sleeves and an open neck, map tubes strapped across the back"],
    "Activist": ["a worn denim work jacket covered in small round pins and plain cloth patches, a strip of bright plain cloth knotted around one sleeve",
                 "a poncho in clashing bright painted stripes and zigzags over a plain shirt, a megaphone strap across the chest",
                 "a patched olive field coat worn open, a hand-knitted scarf hanging loose over one shoulder"],
    "Thief": ["a close-fitting charcoal jacket with a hood pushed back on the shoulders",
              "an ink-blue wrap tunic crossed low on the chest, a row of lockpicks along the lapel and a half-mask hanging on its cord",
              "a soft grey cloak with a deep cowl thrown back over a dark quilted vest, a ring of stolen charms on a cord"],
    "Gatherer": ["a canvas harvest apron over a rolled-sleeve shirt of undyed linen open at the throat, a straw-coloured kerchief in the pocket",
                 "a quilted patchwork jacket in faded reds and greens, the straps of a wicker pack frame over both shoulders",
                 "an ochre knitted fisherman's sweater with a wide loose neck under oilcloth braces, a string of dried kelp pods on the chest"],
    "Contractor": ["a heavy tan work vest over a rolled-sleeve shirt open at the throat, welding goggles hanging on the chest",
                   "a rust-orange shipwright's coverall unzipped at the collar over a grey undershirt, a rivet gauge clipped to the strap",
                   "a scorched leather forge apron over a thick blue shirt open at the throat, a measuring cord looped over one shoulder"],
    "Sport": ["a sleek white racing flight suit unzipped at the throat, with a blank number patch and a bold speed stripe",
              "a bright two-tone racing jacket unzipped at the throat, with plain colour-block patches",
              "a light pit-crew hoodie with the hood down under a broad plain satin sash worn from one shoulder, racing goggles hanging on the chest"],
    "Researcher": ["a long pale field coat with pens in the breast pocket and a lens on a cord",
                   "a chalk-dusted plum cardigan over a buttoned shirt, a bandolier of specimen jars across the chest",
                   "an archivist's grey robe open at the throat, with ink-stained cuffs and a brass reading glass on a chain"],
    "Preservationist": ["a ranger's jacket of faded green with a seed-pouch strap across the chest",
                        "a moss-and-cream woven shawl pinned with a carved leaf over a plain tunic, sprigs tucked in the weave",
                        "a waxed brown warden's coat worn open, a string of bloom tags on the chest"],
    "FirstResponder": ["a reflective rescue jacket in signal orange with a chest harness",
                       "a plain white rescue tabard with one broad red band across the chest over a padded blue undersuit, a coiled lifeline at the shoulder",
                       "a storm-yellow oilskin with the hood down, a buoy light clipped at the collar and a whistle on a cord"],
    "Priest": ["a long layered robe of deep indigo over a flight suit, a shell pendant at the chest",
               "a pale sand-coloured vestment with a wide embroidered stole and a string of nine small shells",
               "a hooded grey choir mantle thrown back, fastened with a spiral shell clasp, over a plain white tunic"],
    "Diplomat": ["a formal long coat of charcoal with a treaty seal on a chain",
                 "a wine-red envoy's sash across a cream tunic unbuttoned at the throat, a small plain silver clasp on the chest",
                 "a layered ceremonial stole in two colours over a dove-grey jacket, a folded treaty ribbon pinned at the chest"],
}

# ---------------------------------------------------------------------------------------------------------------
# BACKDROPS: where the pilot stands. key: (value, the domains whose accent it would swallow, the faction types it
# belongs to (empty: anyone), the scene, its light). Places are the HyperSea's (Docs/Voyages/voyage_slice.json
# locations) and each faction type's home ground (FactionCatalog). Round 1 was one navy plate fifty times.
# ---------------------------------------------------------------------------------------------------------------
BACKDROPS = {
    # the HyperSea and the Ark: anyone
    "open-sea-dusk": ("dark", (), (), "an open sea at dusk, deep indigo water scattered with distant glowing spheres like paper lanterns, one far vessel drawing a thin trail of light", "cool blue dusk light with a soft warm glow from one side"),
    "cell-membrane": ("mid", ("Jade",), (), "the inside of a living cell, a vast translucent membrane wall curving overhead, veined and softly glowing sea-green, pale flora drifting in bright water", "soft green-white light filtering down from above"),
    "nucleus-sun": ("mid", ("Gold",), (), "a cell's nucleus burning far off like a small sun in green-gold water, its rays breaking through drifting haze", "warm backlight from the nucleus behind one shoulder and a cool fill on the face"),
    "crystal-shallows": ("light", (), (), "a shallow bloom of pale faceted crystal in milky water under bright haze, ice white and palest blue", "bright soft overcast light from above"),
    "ark-bridge": ("dark", (), (), "the bridge of a great ship, a wide curved viewport onto dark water, dim brass instruments and small red and amber indicator lights", "low instrument glow from below and a cold light from the viewport"),
    "ark-hangar": ("mid", (), (), "a great ship's hangar deck, the faceted hull of a moored vessel under work lamps, cables and gantries fading into steel-grey haze", "warm work-lamp light from one side with cool steel shadow"),
    "ark-greenhouse": ("mid", ("Jade",), (), "a great ship's greenhouse gallery, tiers of leafy flora under arched glass ribs, mist and leaf-green light", "soft diffuse daylight through misted glass"),
    "storm-front": ("dark", (), (), "a pressure storm over open water, towering slate-grey walls of dark water and one far flicker of lightning", "hard cold storm light from one side"),
    "dawn-mist": ("light", ("Gold",), (), "dawn mist on calm water, pearl and pale peach haze, the faint masts of moored vessels dissolving into fog", "soft pale dawn light from behind, low and even"),
    "wreck-ring": ("mid", ("Gold",), (), "the rusted ribs of old wrecks ringing a trade lane, rust and verdigris, shafts of murky light", "murky shafts of light from above"),
    "lantern-reach": ("dark", ("Gold",), (), "great sealed vault shells drifting in dark water, each lit amber from inside like a paper lantern", "warm amber glow from the lit shells behind and cool shadow on the near side"),
    "glassmere": ("mid", (), (), "enormous shells like blown glass hanging in thick water, smoky silver and pale blue, bending the light", "cool silver light refracted through glass"),
    "nine-wells": ("dark", (), (), "a ring of sunken shells far below, thin shafts of pale light falling through blue-black water", "one thin pale shaft of light from directly above"),
    "the-maw": ("dark", ("Ruby",), (), "thick dark water the colour of old wine, the long vague shadows of predators circling far behind", "dim red-brown ambient light and a pale key from the front"),
    "cinder-shoal": ("dark", ("Gold",), (), "black water full of drifting embers and ash-grey crystal, a dull red glow on the horizon", "ember light from below and behind"),
    "spindrift": ("light", (), (), "bright shallow water, white crystal spray and the tiny far shapes of a grazing herd", "brilliant high daylight, crisp and clear"),
    "lattice-pens": ("mid", (), (), "a vast geometric lattice of bone-white struts receding into warm grey haze, herd shapes moving far behind it", "flat pale light through haze"),
    "derelict-ark": ("dark", (), (), "the huge dark hull of a stranded great ship listing in cold pale water, one row of ports still lit", "cold blue-grey light with small warm points from the ports"),
    "tollmoor-market": ("mid", ("Gold",), (), "a crowded market, awnings in many colours, hanging lamps and a blurred far crowd, steam rising from food stalls", "warm lamplight from several sides"),
    "night-harbour": ("dark", (), (), "a harbour at night, black water doubling strings of small coloured lights, mooring posts and furled sails in silhouette", "cool moonlight from above and small coloured glints"),
    # each faction type's home ground
    "aurum-quay": ("dark", (), ("Merchant",), "a trading harbour at blue hour, cargo cranes and stacked crates against a deep blue evening, counting-house windows lit", "deep blue evening light with warm window glow"),
    "counting-house": ("mid", ("Jade",), ("Merchant",), "a counting house, shelves of ledgers and brass scales, green-shaded lamps, dust in a slanting beam", "one slanting beam of daylight through dust and a green-shaded lamp"),
    "hunters-lodge": ("dark", ("Gold",), ("Hunter",), "a hunters' lodge, dark timber beams hung with coiled lines and lures, a low fire glowing off to one side", "low firelight from one side and deep warm shadow"),
    "reed-blind": ("light", (), ("Hunter", "Gatherer"), "tall pale reeds and still water at first light, cold mist, a stalker's blind", "cold pale first light, soft and even"),
    "court-windows": ("light", (), ("Aristocrat", "Diplomat"), "a court hall, tall windows of stained crystal throwing coloured panes of light across pale stone", "coloured window light falling across pale stone"),
    "water-garden": ("light", ("Ruby",), ("Aristocrat",), "a formal water garden, still mirror pools, white balustrades and clipped silver-and-lilac flora under a soft overcast sky", "soft overcast light, shadowless"),
    "gullbone-landing": ("mid", ("Gold",), ("Pirate",), "a pirate landing, bone-white reef arches, patched sails and the smoke of deck fires under a bruised orange sunset", "low orange sunset light from behind and a cool fill"),
    "storm-deck": ("dark", (), ("Pirate",), "a ship's deck at night in rain, wet planks, a swinging lantern, torn rigging against storm cloud", "swinging lantern light and rain glints"),
    "warrant-wall": ("mid", ("Jade",), ("BountyHunter",), "a bounty office wall, rows of pinned blank portrait sheets, one bare lamp, smoke haze, cold grey-green", "one bare overhead lamp, hard and cool"),
    "rooftop-stakeout": ("dark", (), ("BountyHunter", "Thief"), "a rooftop over a port at night, wet tiles, steam vents, the port's lights far below", "cool night light with a faint glow rising from the port below"),
    "chart-room": ("mid", ("Gold",), ("Explorer",), "a chart room, great hanging charts of cells and lanes, brass instruments, lamplight on rolled maps", "warm lamplight on parchment"),
    "uncharted-edge": ("light", ("Jade",), ("Explorer",), "the edge of the chart, a vast unknown cell looming in pale green fog, one tiny survey vessel's light against it", "pale diffuse fog light"),
    "rally-wall": ("light", (), ("Activist",), "a painted rally wall, bold brush-painted murals of rolling waves and surf in yellow, blue and white, plain unmarked banners, drifting flare smoke", "bright open daylight"),
    "night-march": ("dark", ("Gold",), ("Activist",), "a night march, a river of small handheld lights flowing down a harbour street between tall dark houses, a far crowd in silhouette holding lanterns high, nothing above the crowd but lanterns and the night sky", "many small warm lights from below"),
    "magpie-hollow": ("dark", (), ("Thief",), "a dim cavern heaped with glittering stolen things, cold coins of light off glass and jewels", "cold scattered glints and one thin shaft of light"),
    "kelp-alley": ("dark", ("Jade",), ("Thief",), "a narrow back alley of a port at night, hanging kelp lines and laundry, one doorway spilling green light", "green light from a doorway on one side"),
    "harvest-lines": ("light", (), ("Gatherer",), "harvest lines at midday, long rows of olive and russet kelp hung to dry under a wide pale-blue sky, baskets and floats", "high clear midday light"),
    "driftnet-dawn": ("light", ("Ruby",), ("Gatherer",), "drift nets hauled at dawn, glittering wet nets full of small crystals, rose-pink light on calm water", "rose-pink dawn light, low and soft"),
    "shipyard": ("dark", (), ("Contractor",), "a shipyard, the bare ribs of a hull under scaffolding, showers of white-blue welding sparks, blue smoke", "the white-blue flash of welding from one side"),
    "dry-dock": ("light", ("Jade",), ("Contractor",), "a dry dock in hard noon light, the white flank of a freshly plated hull, chalk marks, ropes and pulleys", "hard bright noon light"),
    "race-gates": ("light", (), ("Sport",), "a race course, a line of great glowing gate rings receding into bright sky-blue water, pennants and a blurred far crowd", "bright clear light from above"),
    "grandstand-night": ("dark", (), ("Sport",), "a floodlit grandstand at night, confetti in the air, camera flashes as soft discs of violet and white light", "hard white floodlight from above and behind"),
    "tidal-archive": ("mid", (), ("Researcher",), "an archive hall, towering dark shelves of specimen jars, each glowing a different pale colour, amber, rose and pale blue, a rolling ladder", "soft many-coloured glow from the jars"),
    "lumen-lab": ("light", (), ("Researcher",), "a laboratory, tall glass columns of luminous plankton, chalkboards of plain diagrams, cool white light", "cool white laboratory light"),
    "bloom-garden": ("light", ("Ruby",), ("Preservationist",), "a nursery in bloom, clouds of tiny coral-pink and white flora drifting like blossom in soft sunlit water", "soft warm sunlight through blossom"),
    "old-growth-reef": ("dark", (), ("Preservationist",), "an old-growth reef of towering lattice flora in deep blue-green shade, cut by shafts of pale light", "shafts of pale light through deep shade"),
    "rescue-bay": ("mid", ("Gold",), ("FirstResponder",), "a rescue bay, orange lifeboats in their cradles, a turning beacon throwing red-orange light through spray", "a turning red-orange beacon and cold spray"),
    "storm-rescue": ("dark", (), ("FirstResponder",), "a night storm at sea, a searchlight beam cutting through rain and spray, a foundering hull far behind", "one white searchlight beam through rain"),
    "deepreed-shrine": ("dark", ("Ruby",), ("Priest",), "a shrine in deep water, a tall beacon flame in a shell-shaped lantern, nine smaller lamps receding into violet dark", "warm flame light from one side in violet dark"),
    "ninefold-shell": ("mid", (), ("Priest",), "nested pearly shell vaults, lit by candle-like points, a mother-of-pearl sheen", "soft pearly light with small warm points"),
    "treaty-hall": ("light", (), ("Diplomat", "Aristocrat"), "a treaty hall, a long table between tall pale columns, three tall plain curtains of ruby, gold and jade cloth drawn back at the far windows", "even pale hall light"),
    "harbour-terrace": ("mid", ("Gold",), ("Diplomat",), "a harbour terrace at golden evening, a marble balustrade, far vessels at anchor, long warm light", "long warm evening light from one side"),
    # round 3: places that could not be on Earth (UNEARTHLY), one home ground for every faction type and five for anyone
    "geode-bazaar": ("dark", (), ("Merchant",), "a night market moored inside a hollow geode as wide as a harbour, barges and stalls on black water, every violet crystal face of the walls throwing back the stall lamps in broken points", "cool violet light off the crystal walls with small warm lamp points"),
    "herd-canopy": ("mid", ("Jade",), ("Hunter",), "a hide slung high in a forest of giant translucent stalks, far below it a herd of slow glowing creatures drifting through blue-green haze like lanterns on a river", "dim blue-green light from below and a pale glow through the stalks"),
    "spire-terrace": ("light", (), ("Aristocrat",), "a palace terrace grown from pale living crystal, slender spires like frozen spray, two small pale suns low over a sea of white cloud", "soft double light from two low suns, with two faint shadows"),
    "leviathan-ribs": ("dark", (), ("Pirate",), "a hidden anchorage inside the skeleton of some vast dead creature, its ribs arching overhead like the vault of a great hall, patched vessels moored between them, deck fires doubled in black water", "low firelight from below and cold blue light falling between the ribs"),
    "gulf-waystation": ("dark", (), ("BountyHunter",), "a waystation bolted to the underside of a drifting rock, a row of caged lamps hanging over a bottomless gulf that glows faintly blue far below", "one caged lamp overhead and a faint blue glow from far below"),
    "spiral-sink": ("light", ("Jade",), ("Explorer",), "the lip of a colossal sinkhole whose pale stone walls spiral down out of sight, three small moons in a green twilight sky, one survey line dropping into the dark", "pale green twilight with a cool glow from the moons"),
    "raft-city": ("mid", ("Jade",), ("Activist",), "a city of a thousand small boats lashed together, drifting under a sky that is one enormous translucent membrane veined like a leaf, lanterns strung between the masts", "soft green-white light through the membrane and warm lantern points"),
    "drift-vault": ("dark", (), ("Thief",), "a looted vault where gravity has let go, coins, keys and cut gems hanging in the air and turning slowly in one shaft of violet-white light, the vault door standing open on black", "one shaft of violet-white light and scattered glints"),
    "sphere-terraces": ("light", ("Jade",), ("Gatherer",), "harvest terraces climbing the inside of a hollow sphere as big as a valley, rows of blue-green kelp curving up and overhead, a small white sun hanging at its centre", "bright even light from the small sun at the centre, with no horizon"),
    "crystal-slip": ("mid", (), ("Contractor",), "a slipway where a hull is grown and not built, a half-formed vessel of living blue-grey crystal in a cradle of scaffolds, new facets still creeping along its edges, cold sparks drifting upward like slow stars", "cool blue light from the growing crystal and white work lamps"),
    "ring-canyon": ("mid", ("Ruby",), ("Sport",), "a race line threading a canyon of rose-coloured crystal, glowing gate rings hanging in the air between the walls and receding out of sight, streaks of light where vessels have passed", "rose light off the crystal walls and white glare from the nearest ring"),
    "sphere-observatory": ("dark", (), ("Researcher",), "an observatory gallery under a clear dome, the dark beyond it crowded with enormous translucent spheres, each with a tiny bright sun at its heart, brass instruments in silhouette", "the cold many-pointed light of the far spheres and one small desk lamp"),
    "bell-grove": ("dark", ("Gold",), ("Preservationist",), "a grove of giant bell-shaped flora that glow amber from within, their stems as thick as towers, pale spores rising between them like slow snow", "warm amber glow from the bells and deep blue shadow"),
    "maelstrom-light": ("dark", ("Ruby",), ("FirstResponder",), "a rescue tower on a shard of rock above a maelstrom of violet cloud, a slow spiral of light turning in its depths, rescue craft circling like sparks", "a turning white beacon from above and a violet glow from below"),
    "hollow-pearl": ("light", (), ("Priest",), "a sanctuary inside a hollow pearl the size of a hall, its curved wall glowing milk-white and faintly rainbowed, nine small flames floating in the air with no lamps beneath them", "soft shadowless pearl light with nine small warm points"),
    "twin-spheres": ("mid", (), ("Diplomat",), "a round council floor hanging in open space between two enormous translucent spheres that almost touch, one glowing rose and one glowing blue, a thin bridge of light where they meet", "rose light from one side and blue light from the other"),
    "prism-field": ("mid", (), (), "a field of great drifting prisms, each as big as a house and clear as water, splitting the light into slow bands of colour that drift through the haze", "white light broken into soft coloured bands"),
    "ring-tunnel": ("dark", (), (), "the mouth of a tunnel of light bored through dark water, ring after glowing ring receding to a point, fine bright dust streaming into it", "cold white-blue light from the tunnel behind"),
    "membrane-dawn": ("light", ("Ruby",), (), "the inside of a vast living cell at its dawn, the far membrane wall glowing rose and pearl like the inside of a shell, veined with pale light", "soft rose-pearl light from the whole far wall, shadowless"),
    "spore-plain": ("light", (), (), "a white plain of fallen spores under a pale sky full of drifting seed-globes, each trailing a thread, a far line of crystal trees", "flat bright light from above and from the white ground"),
    "giant-wake": ("dark", ("Jade",), (), "the wake of something vast that has just passed, a wall of churned dark water full of green sparks, and one enormous fin sliding out of sight", "green sparks from the wake and a cold key light from above"),
}
# UNEARTHLY: the places that could not be on Earth (Garrett, 2026-11-11: "The backgrounds that don't look like they
# could be earth are the best"). A new pilot's place is rolled three times as readily from these (weighted_backdrops,
# creator.propose_cast), and every faction type has at least one of its own (--self-test). The busts already painted
# keep the places they stand in.
UNEARTHLY = frozenset({
    "open-sea-dusk", "cell-membrane", "nucleus-sun", "crystal-shallows", "lantern-reach", "glassmere", "nine-wells",
    "the-maw", "cinder-shoal", "lattice-pens", "uncharted-edge", "race-gates", "old-growth-reef", "ninefold-shell",
    "bloom-garden", "deepreed-shrine",
    "geode-bazaar", "herd-canopy", "spire-terrace", "leviathan-ribs", "gulf-waystation", "spiral-sink", "raft-city",
    "drift-vault", "sphere-terraces", "crystal-slip", "ring-canyon", "sphere-observatory", "bell-grove",
    "maelstrom-light", "hollow-pearl", "twin-spheres", "prism-field", "ring-tunnel", "membrane-dawn", "spore-plain",
    "giant-wake",
})

# The person's human tells, rolled from the name when the caller does not set them (a name always paints the same).
HAIRS = [   # over a crown that pokes through hair (ears, horns, antennae, fins)
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
    "a cloud of tight black human coils",
    "long honey-brown human waves swept to one side",
    "shoulder-length dark human locs gathered in a band",
    # round 3: more of the human side, for the pilots who show it (the list grows; the painted busts keep theirs)
    "a shaved side and a long sweep of black human hair falling over the other eye",
    "two thick chestnut human braids hanging forward over the shoulders",
    "a short iron-grey human crop, thinning at the temples",
    "a loose bun of sun-bleached human hair with strands escaping",
    "a high rounded puff of tight dark human curls",
    "a blunt jaw-length cut of white-blond human hair",
    "long dark-red human hair tied back with a strip of plain cloth",
    "a mane of salt-and-pepper human waves combed straight back",
]
HAIRS_LOW = [   # under a crown that covers the scalp: the hair shows only below it
    "a long silver-white human braid falling over one shoulder",
    "a thick dark human plait hanging down the back",
    "loose copper-red human curls spilling out at the nape",
    "long straight black human hair falling behind the shoulders",
    "a short grey human queue tied at the nape with a cord",
    "two thin dark human braids hanging forward over the collar",
    "soft pale-gold human curls gathered low at the nape",
    "a long rope of tight black human braids over one shoulder",
]
# A person's own touch on an animal's face (Garrett, 2026-11-11: "it is fine to have some that express more of their
# human side ... I would expand the list vs. change that"): each is what {name} does or has, a full clause. Rolled for
# about a quarter of NEW pilots (roll_more); the busts already painted have none, and none hides a crown.
TOUCHES = [
    "wears round wire-rimmed spectacles low in front of the eyes",
    "wears a line of dark kohl drawn under each eye",
    "wears a streak of bright paint under each eye",
    "has a thin old scar running down through one brow",
    "holds a sprig of dried kelp in the corner of the mouth",
    "wears a thin plain gold ring through one brow",
    "wears half-moon reading glasses on a cord, pushed down to the end of the nose",
    "has a small healed nick at the edge of the lip",
]
TOUCHES_FUR = [   # human facial hair, only on a face that has fur of its own (FACE[a] names it)
    "wears a neat human moustache waxed to two points",
    "wears a short trimmed human beard along the jaw",
    "wears long human sideburns brushed forward",
]
TOUCH_ODDS = 25                     # percent of new pilots with a human touch
BARE_HAND_ODDS = 50                 # percent of the human hands shown that are bare, a person's own skin, not gloved
IRISES = ["amber-brown", "hazel-green", "steel-grey", "ice-blue", "deep brown", "warm hazel", "pale green", "dark amber"]
ACTIONS = [   # what a hand or a limb in the frame is doing
    "raised to adjust the collar",
    "holding a small glowing crystal shard up beside the face",
    "curled under the chin in thought",
    "hooked over the lapel",
    "giving a small wave",
    "resting flat over the heart",
]
EXPRESSIONS = [
    "calm and faintly amused", "stern and watchful", "bright, open and curious", "wry and unimpressed",
    "warm and kind", "proud and unbowed", "serene, almost sleepy", "fierce and determined",
    "mischievous, holding back a laugh", "weary but steady", "shy and gentle", "cool and assessing",
    "delighted, on the edge of laughing", "solemn and far-seeing",
]
POSES = [   # the three-quarter views are listed twice: they are the avatar's bread and butter
    "Three-quarter view turned slightly to the left, looking at the viewer",
    "Three-quarter view turned slightly to the right, looking at the viewer",
    "Facing the viewer almost square on, the head tilted a little to one side",
    "Three-quarter view turned slightly to the left, looking at the viewer",
    "Three-quarter view turned slightly to the right, looking at the viewer",
    "Three-quarter view turned to the right, the chin lifted, looking down at the viewer",
    "Three-quarter view turned to the left, the head dipped, looking up at the viewer from under the brow",
    "Facing the viewer almost square on, the shoulders level",
    "The body turned away to the left and the head looking back over the shoulder at the viewer",
    "The body turned away to the right and the head looking back over the shoulder at the viewer",
]
HAIR_ODDS = {False: 60, True: 25}   # percent of pilots with human hair, by whether the crown covers the scalp
LIMB_ODDS = 50                      # percent of pilots with a limb of their own who show it
HAND_ODDS = 10                      # percent of the rest (no hair, no own limb) who show one gloved human hand

RENDER = (
    "Rendering: a premium painterly digital character portrait, semi-realistic, rich texture in fur, feathers, scales "
    "or skin, crisp detail in the eyes and the face, looser brushwork in the clothes, and the background soft, "
    "painterly and out of focus, lower in contrast than the head, with a few tiny faceted crystal motes drifting in "
    "the air; like a collectible character card. Everything that grows from the head is living anatomy, not a hat, a "
    "headdress or an ornament. One figure only. No text, no lettering, no watermark, no frame border, no firearm, no "
    "human ears."
)


def pick(table, name, salt):
    """A deterministic pick from a table by the person's name (a name always paints with the same tells)."""
    h = int(hashlib.md5(f"{salt}/{name}".encode("utf-8")).hexdigest(), 16)
    return table[h % len(table)]


def roll100(name, salt):
    return int(hashlib.md5(f"{salt}/{name}".encode("utf-8")).hexdigest(), 16) % 100


def brings_ears(a, b):
    """Whether crown animal b puts ears of its own on animal a's head. Never on a face that keeps its ears (KEEPS):
    there an ears crown gives way to its ALT and EARS_TOO stays out."""
    return "ears" not in KEEPS.get(a, ()) and (CROWN[b][1] == "ears" or b in EARS_TOO)


def face_of(a, b):
    """The face line under this crown: the face's ears shown, left out or no longer denied, so the brief asks for
    one pair of ears at most and never denies the pair it asks for."""
    if brings_ears(a, b):
        return FACE[a].replace(FACE_NO_EARS.get(a, "\0"), "")
    return FACE[a] + (f", {FACE_EARS[a]}" if a in FACE_EARS else "")


def crown_of(a, b):
    """(part, covers the scalp, line): the crown animal b grows on animal a's face."""
    part, organ, cap, line = CROWN[b]
    clash = organ in KEEPS.get(a, ())
    if clash and b in ALT:
        part, cap, line = ALT[b]
        line = line.replace("{a}", a.lower())
    elif organ == "ears":
        line += ", the only ears on the head"
    elif brings_ears(a, b):
        line += f", and {EARS_TOO[b]}, the only ears on the head"
    own_ears = "ears" in KEEPS.get(a, ()) or a in FACE_EARS
    return part, cap, line.replace("{no ears}", "" if own_ears else NO_EARS).replace("{a}", a.lower())


def backdrops_for(faction):
    """The backdrop keys a pilot of this faction type can stand in: its type's home grounds first, then anyone's."""
    domain = brief.DOMAIN_OF[faction]
    ok = [k for k, v in BACKDROPS.items() if domain not in v[1]]
    return [k for k in ok if faction in BACKDROPS[k][2]], [k for k in ok if not BACKDROPS[k][2]]


def weighted_backdrops(faction):
    """The pool a new pilot's place is rolled from: a home ground twice as likely as any one other, and a place that
    could not be on Earth (UNEARTHLY) three times as likely as one that could."""
    home, anyone = backdrops_for(faction)
    pool = home + home + anyone
    return pool + [k for k in pool if k in UNEARTHLY] * 2


def roll_tells(a, b, name):
    """What a name rolls for the human side beyond the clothes and the gaze: dict(hair, limb, hand).

    hair: the hair's text or "none". limb: True when an animal's own limb is in the frame. hand: what the limb or
    one gloved human hand is doing, or "none". The rules (Garrett, 2026-11-11): hair makes a hand unnecessary, an
    animal's own hand or tentacle makes a human hand unnecessary, and the human side matters least once the pilot is
    dressed, so most pilots roll neither.
    """
    _part, cap, _line = crown_of(a, b)
    hair = pick(HAIRS_LOW if cap else HAIRS, name, "hair") if roll100(name, "hair?") < HAIR_ODDS[cap] else "none"
    if a in OWN_HAIR or b in OWN_HAIR:
        hair = "none"
    if b in LIMB_TELL:
        return dict(hair=hair, limb=True, hand="none")
    if a in LIMB or b in LIMB:
        show = bool(LIMB.get(a) or LIMB.get(b)) and roll100(name, "limb?") < LIMB_ODDS
        return dict(hair=hair, limb=show, hand=pick(ACTIONS, name, "action") if show else "none")
    hand = pick(ACTIONS, name, "action") if hair == "none" and roll100(name, "hand?") < HAND_ODDS else "none"
    return dict(hair=hair, limb=False, hand=hand)


def roll_more(a, b, name):
    """What a NEW pilot's name rolls for more of the human side: dict(touch, glove). touch: a clause of TOUCHES (or
    of TOUCHES_FUR too, on a furred face) or "none"; glove: whether a human hand, when one shows, is gloved. The
    pilots already painted were cast without these and keep their looks (compose's defaults are none and gloved)."""
    pool = TOUCHES + (TOUCHES_FUR if re.search(r"\bfur\b", FACE[a]) and a not in BEAKED else [])
    touch = pick(pool, name, "touch") if roll100(name, "touch?") < TOUCH_ODDS else "none"
    return dict(touch=touch, glove=roll100(name, "glove?") >= BARE_HAND_ODDS)


def human_tells(a, b, name, hair=None, hand=None, limb=None, glove=True):
    """(the hair sentence, the hand sentence).

    Each of hair, hand and limb is rolled from the name when None (roll_tells), and a tell that is set wins over a
    rolled one: hair that is set drops a rolled human hand, and a human hand that is set drops rolled hair. A caller
    that sets them must keep the rules, and a look that breaks one raises ValueError: no human hand beside human
    hair, none on a pilot whose animal has a limb of its own, no human hair beside an animal's own mane (OWN_HAIR),
    and a LIMB_TELL animal's limb always in the frame.
    """
    rolled = roll_tells(a, b, name)
    hair_set = hair is not None
    hair = rolled["hair"] if hair is None else hair
    limb = rolled["limb"] if limb is None else limb
    if hand is None:    # the rolled hand stands only with the limb it was rolled for, and gives way to hair that was set
        hand = rolled["hand"] if limb == rolled["limb"] and (limb or hair == "none") else "none"
    elif hand != "none" and not limb and not hair_set:
        hair = "none"   # a human hand that was set: the rolled hair gives way to it
    for what, text in (("hair", hair), ("hand", hand)):
        if not isinstance(text, str) or not text.strip():
            raise ValueError(f"the {what} is {text!r}: give its text, or 'none'")
    maned = [x for x in (a, b) if x in OWN_HAIR]
    if hair != "none" and maned:
        raise ValueError(f"{maned[0]} brings a mane of its own: human hair beside it is one head of hair too many")
    if b in LIMB_TELL and not limb:
        raise ValueError(f"a {b.lower()}'s limb is how people know it: it is always in the frame (bust.LIMB_TELL)")
    part, cap, _line = crown_of(a, b)
    bl = b.lower()
    if hair != "none" and cap:
        hair_line = f"Human hair shows only below the {bl}'s crown, never over it: {hair}."
    elif hair != "none":
        hair_line = f"Human hair: {hair}, leaving the {part} clear."
    else:
        hair_line = "No human hair anywhere on the head."

    owners = [x for x in (b, a) if x in LIMB]
    if limb and b in LIMB_TELL:
        return hair_line, LIMB_TELL[b] + ". No other hand is in the frame."
    if limb:   # a true hand where the pilot has one, an armoured animal's gauntlet hand only where it has not
        shown = [x for x in owners if LIMB[x] and x not in ARMOURED] or [x for x in owners if LIMB[x]]
        if not shown:
            raise ValueError(f"neither {a} nor {b} has a limb of its own to raise (bust.LIMB)")
        x = shown[roll100(name, "limb owner") % len(shown)]
        action = hand if hand != "none" else pick(ACTIONS, name, "action")
        return hair_line, (f"One limb is raised into the frame and it is the {x.lower()}'s own: {LIMB[x]}, "
                           f"{action}. No human hand is in the frame.")
    if hand != "none":
        if owners:
            raise ValueError(f"{owners[0]} has a limb of its own: it does not need a human hand")
        if hair != "none":
            raise ValueError("human hair and a human hand: the hair is enough")
        if not glove:
            return hair_line, (f"One bare human hand is raised into the frame, {hand}: a person's hand, four fingers "
                               f"and a thumb, with a person's skin and short nails.")
        return hair_line, f"One gloved hand is raised into the frame, {hand}."
    return hair_line, "Both arms are down and out of the frame: no hands."


def compose(animals, faction, name, callsign=None, hair=None, iris="", hand=None, limb=None, expression="",
            extra="", backdrop="", gear=None, pose=None, touch="none", glove=True):
    """The bust brief, verbatim what the painter gets. animals = (face animal, crown animal).

    backdrop: a BACKDROPS key (rolled from the name among the faction's when empty). gear: the outfit's index in
    GEAR[faction] or its text. pose: the index in POSES or its text. touch: a clause of TOUCHES, "none", or None to
    roll it from the name (roll_more: a new pilot); glove: a human hand that shows is gloved, or bare. The rest:
    human_tells.
    """
    a, b = animals
    if not name.strip():
        raise ValueError("a bust is a person: the brief needs the pilot's name")
    if a not in FACE:
        raise KeyError(f"no FACE line for {a!r}: add it to bust.FACE")
    if b not in CROWN or b not in MARK:
        raise KeyError(f"no CROWN/MARK line for {b!r}: add it to bust.CROWN and bust.MARK")
    if callsign is not None and callsign not in MOTIF:
        raise KeyError(f"no MOTIF line for callsign {callsign!r}")
    domain = brief.DOMAIN_OF[faction]
    acc = brief.ACCENT[domain]
    word = acc["word"]
    al, bl = a.lower(), b.lower()
    part, cap, crown = crown_of(a, b)
    face = face_of(a, b)

    if not backdrop:
        backdrop = pick(weighted_backdrops(faction), name, "backdrop")
    if backdrop not in BACKDROPS:
        raise KeyError(f"no BACKDROPS entry {backdrop!r}")
    value, _avoid, _factions, scene, light = BACKDROPS[backdrop]
    outfit = GEAR[faction]
    outfit = (pick(outfit, name, "gear") if gear is None else outfit[gear % len(outfit)] if isinstance(gear, int)
              else gear)
    stance = pick(POSES, name, "pose") if pose is None else POSES[pose % len(POSES)] if isinstance(pose, int) else pose
    iris = iris or pick(IRISES, name, "iris")
    expression = expression or pick(EXPRESSIONS, name, "expression")
    motif = f", and {MOTIF[callsign]}" if callsign else ""

    if touch is None:
        touch = roll_more(a, b, name)["touch"]
    if not isinstance(touch, str) or not touch.strip():
        raise ValueError(f"the touch is {touch!r}: give its clause, or 'none'")
    hair_line, hand_line = human_tells(a, b, name, hair, hand, limb, glove)
    if touch != "none":
        hand_line += f" {name.split()[0]} {touch}."
    kind, second = SECOND.get(b, ("", ""))
    seam = BLEND.get(b, SEAM).replace("{a}", al)
    hood = (f" The hood lies flat on the back of the shoulders, in front of the {bl}'s {kind}: nothing grows between "
            f"the hood and the head." if kind in BEHIND_HOOD and HOODED.search(outfit) else "")
    if a in EYES:
        eyes = (f"the eyes are the {al}'s own, {EYES[a]}, bright and alert and looking at the viewer, each rimmed by "
                f"a thin glowing {word} line (the pilot's sight)")
    else:
        eyes = (f"the eyes are a person's, forward-facing and expressive with a little white showing at the corners, "
                f"{iris} irises each ringed by a thin glowing {word} line (the pilot's sight)")
    mood = (f"The beak is rigid and does not bend into a smile: the mood shows in the eyes and the tilt of the head, "
            f"{expression}." if a in BEAKED else f"Expression: {expression}.")
    rim = (f"and a thin {word} edge light on the shoulders and the {part}" if value == "light"
           else f"and {'an' if word[0] in 'aeiou' else 'a'} {word} rim light tracing one edge of the head, the {part} "
                f"and the shoulders so the silhouette stands clear of the background")
    return (
        f"Signature pilot portrait: a head-and-shoulders bust of {name}, a triple-hybrid person, {al} "
        f"and {bl} and human in one body. Each heritage owns its own part, so both animals read at a glance even as "
        f"a tiny icon. {a.upper()} owns the face and skull: {face}. {b.upper()} owns the {part} and everything "
        f"behind the face: {crown}; the back of the head, the nape, the throat and the neck down into the collar are "
        f"all {bl}, {MARK[b]}, {seam}"
        + (f"; and {second}" if second else "") + ". "
        f"HUMAN is the bearing, the clothes and the gaze: {name} sits upright like a person, dressed, and {eyes}. "
        f"{hair_line} {hand_line} {mood} "
        f"Wearing {outfit}, trimmed in {word} ({acc['hex']}) with thin glowing {word} seam lines{motif}; the collar "
        f"sits low and open so the {bl}'s throat and neck show above it.{hood} "
        + (f"{extra.rstrip('.')}. " if extra else "")
        + f"{stance}, the head in the upper middle of a square frame with clear space above everything that grows "
        f"from it, cropped at mid-chest. Background: {scene}. Lighting: {light}, {rim}. " + RENDER)


WORLD_JSON = "Docs/Voyages/voyage_slice.json"
# A word can summon an emblem. "a medic's tabard" was painted with a red cross on white (a protected emblem), "a
# bright armband" as a band with a disc and a star on it, "raised fists in far silhouette" as fist emblems on
# banners, "a bandana knotted around one upper arm" beside "a crosshair-marked eyepiece" as an armband with a ring
# and a cross on it, "murals of waves and open hands" as a yellow hand on a banner, and "a champion's ribbon sash"
# with a gold laurel wreath on black, which is a clothing brand's mark. So nothing a painter could draw as a symbol
# says one of these words: not an outfit, a callsign's mark, a place or its light, what a hand is doing, or free
# text in a pilot's look. Banners, pennants and murals are plain, and a hall has curtains, never banners ("three
# plain hanging banners" in a treaty hall came back with a winged sword, a sun and a star on them); a crowd holds
# lights, and nothing is tied on an arm. A band or strip of cloth on a SLEEVE is the one such thing a line may carry, and only when
# the line calls it plain (SLEEVE_BAND): "a strip of bright plain cloth knotted around one sleeve" was painted
# with nothing on it. (A callsign's own mark is a small object: a brooch, a pendant, a badge.)
EMBLEM = re.compile(r"\b(?:fists?|flags?|cross|crosses|crosshairs?|skulls?|slogans?|placards?|armbands?|insignia|"
                    r"emblems?|sigils?|open[- ]hands?|hand ?prints?|champion(?:'?s)?|laurels?|wreaths?)\b|medic|red cross|"
                    r"\b(?:around|on) (?:one|an|the|each|either) (?:upper )?arm\b")
SLEEVE_BAND = re.compile(r"\b(?:bands?|strips?|ribbons?|bandanas?|kerchiefs?|scar(?:f|ves)|rags?|sash(?:es)?)\b[^,.;]*?"
                         r"(?P<on>\b(?:around|round|about|on) (?:one|a|the|each|either|both|its)(?: [a-z-]+)? sleeves?\b)")
_PLAIN_BEFORE = re.compile(r"\bplain(?: [a-z-]+){0,2} $")


def emblem_word(text):
    """The words in a line that a painter draws as an emblem or as lettering, or None: a word of EMBLEM, or a band
    of cloth on a sleeve that the line does not call plain (SLEEVE_BAND)."""
    t = text.lower()
    m = EMBLEM.search(t)
    if m:
        return m.group(0)
    for m in SLEEVE_BAND.finditer(t):
        if "plain" not in t[m.start():m.start("on")] and not _PLAIN_BEFORE.search(t[:m.start()]):
            return m.group(0)
    return None


@functools.lru_cache(maxsize=None)
def world_names():
    """The proper names of the game's world, which a brief never carries: its places and rival crews (WORLD_JSON)
    and every founding faction (FactionCatalog.cs), as the game writes them, less a leading "The"."""
    import json
    world = json.load(open(os.path.join(brief.ROOT, WORLD_JSON), encoding="utf-8"))
    names = [x["name"] for k in ("locations", "rivals") for x in world[k]]
    text = open(os.path.join(brief.ROOT, brief.FACTION_CATALOG_CS), encoding="utf-8").read()
    for body in re.findall(r"\[FactionType\.\w+\]\s*=\s*new\[\]\s*\{(.*?)\}", text, re.S):
        names += re.findall(r'"([^"]+)"', body)
    return tuple(sorted({re.sub(r"^The ", "", n) for n in names}))


@functools.lru_cache(maxsize=None)
def _named():
    return re.compile(r"(?i:cosmic shore|froglet|hyper ?sea|etherion|\bark\b)|\b(?:"
                      + "|".join(re.escape(n) for n in sorted(world_names(), key=len, reverse=True)) + r")\b")


def world_name_in(text):
    """The name of the game, the studio or the world, or of a place, a crew or a faction in it, that a text
    carries, or None. The brief goes to an outside painter and describes a picture: it names none of them. The
    world's names are matched as NAMES, capitalised as the game writes them ("rust and verdigris" is a metal's
    patina, "Verdigris" is a place); the game's and the studio's in any case."""
    m = _named().search(text)
    return m.group(0) if m else None


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
        if len(GEAR.get(f, ())) != 3:
            errors.append(f"GEAR needs three outfits for {f}")
        home, anyone = backdrops_for(f)
        if len(home) < 1 or len(anyone) < 8:
            errors.append(f"{f} has {len(home)} home backdrops and {len(anyone)} others")
    texts = list(FACE.values()) + list(MARK.values()) + list(MOTIF.values()) + list(EYES.values()) + \
        [v for v in LIMB.values() if v] + list(LIMB_TELL.values()) + [x for v in GEAR.values() for x in v] + \
        [v[3] for v in CROWN.values()] + [v[2] for v in ALT.values()] + [v[1] for v in SECOND.values()] + \
        list(EARS_TOO.values()) + list(FACE_EARS.values()) + list(BLEND.values()) + [SEAM] + TOUCHES + TOUCHES_FUR + \
        [x for v in BACKDROPS.values() for x in v[3:]] + HAIRS + HAIRS_LOW + ACTIONS + EXPRESSIONS + POSES + IRISES
    for t in texts:
        if not t.isascii():
            errors.append(f"non-ASCII line: {t[:40]}")
        if t.rstrip().endswith("."):
            errors.append(f"a table line ends with a full stop (the composer adds it): {t[:40]}")
    # nothing a painter could draw as a symbol says a word that summons one (EMBLEM)
    drawn = [(f"GEAR[{f}]", o) for f, v in GEAR.items() for o in v] + [(f"MOTIF[{c}]", t) for c, t in MOTIF.items()] + \
        [(f"backdrop {k}", x) for k, v in BACKDROPS.items() for x in v[3:]] + [("ACTIONS", x) for x in ACTIONS] + \
        [("TOUCHES", x) for x in TOUCHES + TOUCHES_FUR]
    for where, t in drawn:
        if emblem_word(t):
            errors.append(f"{where} says {emblem_word(t)!r}, which is painted as an emblem or as lettering: describe "
                          f"plain cloth, plain shapes and lights")
    for t, want in (("a bandana knotted around one upper arm", "around one upper arm"), ("a crosshair-marked lens", "crosshair"),
                    ("murals of waves and open hands", "open hands"), ("a medic's tabard", "medic"),
                    ("a bright armband", "armband"), ("raised fists in far silhouette", "fists"),
                    ("a champion's ribbon sash", "champion's"), ("a laurel on the sash", "laurel"),
                    ("a hand print on the wall", "hand print"), ("a plain strip of cloth knotted around one sleeve", None),
                    ("a strip of bright plain cloth knotted around one sleeve", None),
                    ("a plain red band on each sleeve", None), ("a shirt with rolled sleeves", None),
                    ("a striped scarf hanging loose from one shoulder", None),
                    ("a strip of bright cloth knotted around one sleeve", "strip of bright cloth knotted around one sleeve"),
                    ("a strip of cloth knotted around one plain sleeve", "strip of cloth knotted around one plain sleeve"),
                    ("plain cloth patches and a ribbon on each sleeve", "ribbon on each sleeve"),
                    ("a plain coat, a bandana tied round the upper sleeve", "bandana tied round the upper sleeve"),
                    ("a hand-knitted scarf", None), ("arms crossed", None), ("one hand raised to the collar", None)):
        if emblem_word(t) != want:
            errors.append(f"the emblem gate reads {t!r} as {emblem_word(t)!r}, not {want!r}")
    for k, line in FACE.items():
        named = line.replace("no visible ears", "").replace("no ears", "").replace("no nose and", "")
        if "ears" in named and "ears" not in KEEPS.get(k, ()):
            errors.append(f"FACE[{k}] names ears: KEEPS (always its own) or FACE_EARS (only when the crown has none)")
    for k, (_part, organ, _cap, line) in CROWN.items():
        named = line.replace("{no ears}", "")
        if re.search(r"\bears\b", named) and organ != "ears":
            errors.append(f"CROWN[{k}] names ears and is not an ears crown: its ears go in EARS_TOO, and a head "
                          f"with no ears says so in the slot {{no ears}}")
    for k in FACE_EARS:
        if "ears" in KEEPS.get(k, ()):
            errors.append(f"{k} is in KEEPS and in FACE_EARS: its ears show always, or only when the crown has none")
    denial = re.compile(r"\bno (?:visible )?ears\b")
    for k, line in FACE.items():
        if bool(denial.search(line)) != (k in FACE_NO_EARS) or line.count(FACE_NO_EARS.get(k, line)) != 1:
            errors.append(f"FACE[{k}]: a face that says it has no ears has those words, once, in FACE_NO_EARS "
                          f"(they are cut under a crown that brings ears)")
    # every pair of animals: one pair of ears at most, never shown by one line and denied by the other
    for a in FACE:
        for b in CROWN:
            crown = crown_of(a, b)[2]
            if "{" in crown:
                errors.append(f"{a} + {b}: a slot is left in the crown line: {crown[:60]}")
            shows = "ears" in KEEPS.get(a, ()) or (a in FACE_EARS and not brings_ears(a, b))
            rest = crown.replace(f"the {a.lower()}'s own ears", "")
            if shows and re.search(r"\bears\b", rest):
                errors.append(f"{a} + {b}: the face shows its own ears and the crown line names ears as well")
            if a in FACE_EARS and not shows and "the only ears on the head" not in crown:
                errors.append(f"{a} + {b}: the face's ears are dropped and the crown brings none")
            if denial.search(face_of(a, b)) and re.search(r"\bears\b", rest.replace(NO_EARS, "")):
                errors.append(f"{a} + {b}: the face line says the head has no ears and the crown line brings ears")
    for table, n in ((EYES, "EYES"), (KEEPS, "KEEPS"), (BEAKED, "BEAKED"), (FACE_EARS, "FACE_EARS")):
        for k in table:
            if k not in FACE:
                errors.append(f"{n} names {k}, which has no FACE")
    for table, n in ((ALT, "ALT"), (SECOND, "SECOND"), (LIMB_TELL, "LIMB_TELL"), (EARS_TOO, "EARS_TOO")):
        for k in table:
            if k not in CROWN:
                errors.append(f"{n} names {k}, which has no CROWN")
    for k, v in CROWN.items():
        if v[1] in ("ears", "horns") and k not in ALT:
            errors.append(f"{k}'s crown is {v[1]} and it has no ALT for a face that already has them")
    # round 3. A hand can hold and use things: a limb line names fingers, toes, a paw or a tentacle, and is never a
    # claw, a pincer, a club, a blade or a hook ("please never make crab hands (how would they interact with things?)")
    for n, table in (("LIMB", LIMB), ("LIMB_TELL", LIMB_TELL)):
        for k, line in table.items():
            if line and NOT_A_HAND.search(line):
                errors.append(f"{n}[{k}] says {NOT_A_HAND.search(line).group(0)!r}: a pilot's hand can hold and use "
                              f"things, so a limb is a hand with fingers or a tentacle")
            if line and not A_HAND.search(line):
                errors.append(f"{n}[{k}] names no fingers, toes, paw or tentacle: say what the hand holds things with")
    for k in ARMOURED:
        if not LIMB.get(k) or "four fingers and a thumb" not in LIMB[k]:
            errors.append(f"{k} is ARMOURED: its LIMB line is its shell on a hand with four fingers and a thumb")
        if k in LIMB_TELL:
            errors.append(f"{k} is ARMOURED and in LIMB_TELL: an armoured hand is never how people know a pilot")
    # wings, tails and fans grow from the back and stand behind the clothes: never from the head, never at the head
    for k, (_part, _organ, _cap, line) in CROWN.items():
        if re.search(r"\bwings\b|\bwing\b(?!-case)|\btail feathers\b|\bfan of [^,]*feathers\b", line):
            errors.append(f"CROWN[{k}] grows wings or a tail fan from the head: they grow from the back (SECOND)")
    for k, (kind, line) in SECOND.items():
        if kind in ("fan", "wings"):
            if "behind the head" in line:
                errors.append(f"SECOND[{k}] puts its {kind} behind the head: it stands behind the back or the shoulders")
            if not re.search(r"behind (?:the back|both shoulders|the shoulders)", line):
                errors.append(f"SECOND[{k}] does not say its {kind} stands behind the back or the shoulders")
    for k in BLEND:
        if k not in MARK:
            errors.append(f"BLEND names {k}, which has no MARK")
    t = compose(("Chameleon", "Jellyfish"), "Merchant", "X")
    if "no line where the chameleon ends" not in t or SEAM.replace("{a}", "chameleon") in t or "{a}" in t:
        errors.append("a jellyfish's coat must meet the face in its written gradient (BLEND), not at the usual seam")
    if SEAM.replace("{a}", "owl") not in compose(("Owl", "Lynx"), "Merchant", "X"):
        errors.append("a coat with no BLEND line must meet the face at the usual seam (SEAM)")
    t = compose(("Coyote", "Turkey"), "Thief", "X", gear=0)
    if "The hood lies flat on the back of the shoulders, in front of the turkey's fan" not in t:
        errors.append("a hooded outfit over a tail fan must say the hood lies in front of the fan (BEHIND_HOOD)")
    if "The hood lies" in compose(("Coyote", "Turkey"), "Thief", "X", gear=1):
        errors.append("an outfit with no hood was given the hood sentence")
    if "The hood lies" in compose(("Caracal", "Red panda"), "Thief", "X", gear=2):
        errors.append("a tail curled over a shoulder needs no hood sentence (BEHIND_HOOD)")
    t = compose(("Tree frog", "Scorpion"), "Contractor", "X", limb=True)
    if "it is the tree frog's own" not in t or "scorpion armour" in t:
        errors.append("a pilot with a true hand and an armoured animal must show the true hand")
    t = compose(("Heron", "Lobster"), "Sport", "X", limb=True)
    if "it is the lobster's own: a hand with four fingers and a thumb" not in t:
        errors.append("a lobster pilot who shows a hand must show one with fingers")
    # more of the human side: nothing is added to a pilot who was cast without it, a touch is rolled only when asked
    plain = compose(("Lynx", "Jackal"), "Hunter", "Wyvendo Waveline", "TALON", hair="none", hand=ACTIONS[0], limb=False)
    if "One gloved hand is raised" not in plain or any(x in plain for x in TOUCHES + TOUCHES_FUR):
        errors.append("a pilot cast without a touch or a bare hand must be painted without them")
    more = {f"Pilot {i}": roll_more("Lynx", "Jackal", f"Pilot {i}") for i in range(400)}
    share = sum(1 for r in more.values() if r["touch"] != "none")
    if not 60 <= share <= 140:
        errors.append(f"{share} of 400 new pilots roll a human touch: TOUCH_ODDS is {TOUCH_ODDS} percent")
    if not any(r["touch"] in TOUCHES_FUR for r in more.values()):
        errors.append("no furred face in 400 rolled human facial hair (TOUCHES_FUR)")
    if any(roll_more(a, "Lynx", f"Pilot {i}")["touch"] in TOUCHES_FUR for a in ("Heron", "Tree frog", "Koi")
           for i in range(400)):
        errors.append("human facial hair was rolled for a face with a beak or without fur")
    touched = next(n for n, r in more.items() if r["touch"] != "none")
    t = compose(("Lynx", "Jackal"), "Hunter", touched, touch=None)
    if f"Pilot {more[touched]['touch']}." not in t:
        errors.append(f"{touched} rolls a touch: compose(touch=None) must write it")
    t = compose(("Lynx", "Jackal"), "Hunter", "X", hair="none", hand=ACTIONS[0], limb=False, glove=False)
    if "One bare human hand is raised into the frame" not in t or "gloved" in t:
        errors.append("a bare human hand must be painted bare")
    try:
        compose(("Owl", "Lynx"), "Hunter", "X", touch="")
        errors.append("compose accepted an empty touch")
    except ValueError:
        pass
    # places that could not be on Earth: every faction type has one of its own, and a new pilot rolls them more
    for k in UNEARTHLY:
        if k not in BACKDROPS:
            errors.append(f"UNEARTHLY names {k}, which is not a backdrop")
    for f in brief.DOMAIN_OF:
        home, anyone = backdrops_for(f)
        if not any(k in UNEARTHLY for k in home):
            errors.append(f"{f} has no home ground that could not be on Earth (UNEARTHLY)")
        pool = weighted_backdrops(f)
        plain_share = sum(1 for k in home + home + anyone if k in UNEARTHLY) / len(home + home + anyone)
        if sum(1 for k in pool if k in UNEARTHLY) / len(pool) <= plain_share + 0.1:
            errors.append(f"{f}: an unearthly place is not rolled more readily than an earthly one")
    for k, (value, avoid, factions, scene, light) in BACKDROPS.items():
        if value not in ("dark", "mid", "light"):
            errors.append(f"backdrop {k}: value {value!r}")
        for d in avoid:
            if d not in brief.ACCENT:
                errors.append(f"backdrop {k}: unknown domain {d}")
        for f in factions:
            if f not in brief.DOMAIN_OF:
                errors.append(f"backdrop {k}: unknown faction type {f}")
            elif brief.DOMAIN_OF[f] in avoid:
                errors.append(f"backdrop {k} belongs to {f} and swallows its accent")
    for k, v in BACKDROPS.items():
        if v[3][:1].isupper() or v[4][:1].isupper():
            errors.append(f"backdrop {k} opens with a name: describe the place instead")
    values = [v[0] for v in BACKDROPS.values()]
    for want in ("dark", "mid", "light"):
        if values.count(want) < len(values) // 5:
            errors.append(f"only {values.count(want)} of {len(values)} backdrops are {want}")

    # the brief goes to an outside painter: it describes a picture and never names the game, the studio or the
    # world, nor a place, a crew or a faction of that world. Every table line is read, and one whole brief per
    # backdrop for the composer's own words. The world's names are read from the game and matched as NAMES,
    # capitalised as the game writes them: "rust and verdigris" is a metal's patina, "Verdigris" is a place
    world = world_names()
    if len(world) < 60:
        errors.append(f"only {len(world)} world names were read from {WORLD_JSON} and FactionCatalog.cs")
    one_word = next(n for n in world if n.isalpha())
    if world_name_in(f"the {one_word} flats") != one_word or world_name_in(f"the {one_word.lower()} flats"):
        errors.append(f"the name gate must refuse {one_word!r} as a name and let the same letters pass as a word")
    words = [v[0] for v in CROWN.values()] + [v[0] for v in ALT.values()] + [a["word"] for a in brief.ACCENT.values()] + \
        [NO_EARS] + list(FACE_NO_EARS.values())
    scanned = [compose(("Owl", "Lynx"), "Merchant", "X", "TALON", backdrop=k) for k in BACKDROPS] + [
        # between them these hold every sentence the composer writes itself, and all the accent words
        compose(("Owl", "Lynx"), "Hunter", "X", hair=HAIRS[0], hand="none", limb=False),
        compose(("Lynx", "Jackal"), "Hunter", "X", hair=HAIRS_LOW[0], hand="none", limb=False),
        compose(("Lynx", "Jackal"), "Hunter", "X", hair="none", hand=ACTIONS[0], limb=False),
        compose(("Heron", "Lobster"), "Sport", "X", limb=True),
        compose(("Heron", "Otter"), "Sport", "X"),
        compose(("Chameleon", "Jellyfish"), "Merchant", "X"),
        compose(("Coyote", "Turkey"), "Thief", "X", gear=0),
        compose(("Lynx", "Jackal"), "Hunter", "X", hair="none", hand=ACTIONS[0], limb=False, glove=False,
                touch=TOUCHES[0]),
        compose(("Tree frog", "Owl"), "Researcher", "X", limb=True),
        compose(("Horned lizard", "Hedgehog"), "Gatherer", "X")]
    for must in ("Human hair: ", "Human hair shows only below", "No human hair anywhere", "One gloved hand is raised",
                 "One bare human hand is raised", "The hood lies flat", "no line where the chameleon ends",
                 "Both arms are down", "No other hand is in the frame", "No human hand is in the frame",
                 "the only ears on the head") + tuple(a["word"] for a in brief.ACCENT.values()):
        if not any(must in t for t in scanned):
            errors.append(f"no brief the name gate reads holds {must!r}: add one to the briefs it scans")
    for t in texts + words + [RENDER] + scanned:
        if world_name_in(t):
            errors.append(f"the brief names {world_name_in(t)!r} (describe it, do not name it): {t[:50]}")
    b = compose(("Lynx", "Jackal"), "Hunter", "Wyvendo Waveline", "TALON")
    for must in ("LYNX owns the face", "JACKAL owns the saddle", "black-and-silver", "#C800C3", MOTIF["TALON"],
                 "Background: ", "jackal tail"):
        if must not in b:
            errors.append(f"the composed brief lacks {must!r}")
    if "#222645" in b or "ring halo" in b:
        errors.append("the brief still paints round 1's navy plate")
    # the human rules
    frog = compose(("Tree frog", "Scorpion"), "Contractor", "Dozer")
    if "human hand" in frog.replace("No human hand", "") or "gloved hand" in frog:
        errors.append("a tree frog pilot was given a human hand")
    if "tree-frog eyes" not in frog:
        errors.append("a tree frog face lost its own eyes")
    for nm in ("A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L"):
        t = compose(("Parrot", "Deer"), "Preservationist", nm)
        if "Human hair:" in t and "gloved hand" in t:
            errors.append(f"{nm}: hair and a hand together")
        t = compose(("Heron", "Lobster"), "Sport", nm)
        if NOT_A_HAND.search(t) or re.search(r"\bclaws?\b", t) or "gloved hand" in t:
            errors.append(f"{nm}: a lobster second animal never shows a claw, and needs no human hand")
        t = compose(("Heron", "Tortoise"), "Sport", nm)
        if "Human hair:" in t:
            errors.append(f"{nm}: human hair painted over a crown that covers the scalp")
    # a tell that is set wins over a rolled one: set hair drops a rolled hand, a set hand drops rolled hair
    rolls = {f"Pilot {i}": roll_tells("Owl", "Lynx", f"Pilot {i}") for i in range(400)}
    handed = next(n for n, r in rolls.items() if r["hand"] != "none" and not r["limb"])
    haired = next(n for n, r in rolls.items() if r["hair"] != "none")
    t = compose(("Owl", "Lynx"), "Merchant", handed, hair=HAIRS[0])
    if "Human hair: " not in t or "gloved hand" in t:
        errors.append(f"{handed} rolls a gloved hand: hair that is set must drop it")
    t = compose(("Owl", "Lynx"), "Merchant", haired, hand=ACTIONS[0])
    if "No human hair anywhere" not in t or "One gloved hand is raised" not in t:
        errors.append(f"{haired} rolls hair: a human hand that is set must drop it")
    try:
        compose(("Owl", "Lynx"), "Merchant", "X", hair=HAIRS[0], hand=ACTIONS[0], limb=False)
        errors.append("hair and a human hand, both set, must be refused")
    except ValueError:
        pass
    if "hedgehog ears" not in compose(("Horned lizard", "Hedgehog"), "Gatherer", "X"):
        errors.append("a hedgehog on a lizard must add its small ears (EARS_TOO)")
    if "hedgehog ears" in compose(("Hare", "Hedgehog"), "Gatherer", "X"):
        errors.append("a hedgehog on a hare must leave the hare's own ears alone")
    if "the only ears on the head" not in compose(("Heron", "Fennec"), "Hunter", "X"):
        errors.append("an ear crown must be the only ears")
    if "own ears" not in compose(("Hare", "Lynx"), "Hunter", "X"):
        errors.append("a lynx crown on a hare must use the hare's own ears (ALT)")
    if "black-edged ears" in compose(("Opossum", "Lynx"), "Hunter", "X"):
        errors.append("an opossum under a lynx crown must not keep a second pair of ears (FACE_EARS)")
    if "black-edged ears" not in compose(("Opossum", "Peacock"), "Hunter", "X"):
        errors.append("an opossum under a crown without ears must show its own (FACE_EARS)")
    if "otter ears" in compose(("Lynx", "Otter"), "Hunter", "X"):
        errors.append("an otter on a lynx must leave the lynx's own ears alone (EARS_TOO)")
    if "otter ears set low on the sides of the head, the only ears on the head" not in \
            compose(("Heron", "Otter"), "Hunter", "X"):
        errors.append("an otter on a heron must add its small ears (EARS_TOO)")
    if "no visible ears" in compose(("Wolf", "Mole"), "Gatherer", "X"):
        errors.append("a mole crown on a wolf must not say the head has no ears")
    if "over the whole head with no visible ears, and" not in compose(("Heron", "Mole"), "Gatherer", "X"):
        errors.append("a mole crown on a head without ears must say it has none")
    t = compose(("Tiger", "Mole"), "Gatherer", "X")
    if "tiger ears" not in t or "no visible ears" in t:
        errors.append("a tiger under a mole crown must show its own ears (FACE_EARS)")
    t = compose(("Mole", "Fennec"), "Hunter", "X")
    if "no visible ears" in t or "velvety charcoal fur. FENNEC owns" not in t:
        errors.append("a mole under a fennec's ears must not say the head has no ears (FACE_NO_EARS)")
    if "velvety charcoal fur, no visible ears. NEWT owns" not in compose(("Mole", "Newt"), "Hunter", "X"):
        errors.append("a mole under a crown without ears must say it has none")
    if "fish head with no nose: a short" not in compose(("Clownfish", "Lynx"), "Hunter", "X"):
        errors.append("a clownfish under a lynx's ears must not say the head has no ears (FACE_NO_EARS)")
    if "small round ears set far back" not in compose(("Capybara", "Butterfly"), "Hunter", "X"):
        errors.append("a capybara under a butterfly crown must show its own ears: the crown brings none (FACE_EARS)")
    if "beak is rigid" not in compose(("Parrot", "Narwhal"), "Preservationist", "Tender"):
        errors.append("a beaked face must keep a rigid beak")
    seen = {compose(("Owl", "Lynx"), "Aristocrat", f"Pilot {i}").split("Background: ")[1].split(".")[0] for i in range(60)}
    if len(seen) < 8:
        errors.append(f"60 aristocrats stand in only {len(seen)} backdrops")
    if compose(("Owl", "Lynx"), "Aristocrat", "X") != compose(("Owl", "Lynx"), "Aristocrat", "X"):
        errors.append("compose is not deterministic")
    for bad in ((("Dragon", "Owl"), "Hunter"), (("Owl", "Dragon"), "Hunter")):
        try:
            compose(bad[0], bad[1], "X")
            errors.append(f"compose accepted {bad[0]}")
        except KeyError:
            pass
    try:
        compose(("Owl", "Lynx"), "Hunter", "X", backdrop="nowhere")
        errors.append("compose accepted an unknown backdrop")
    except KeyError:
        pass
    for animals, look, what in (
            (("Zebra", "Yak"), dict(hair=HAIRS[0]), "human hair beside a yak's own mane"),
            (("Heron", "Sloth"), dict(limb=False), "a sloth pilot without its arm in the frame"),
            (("Tree frog", "Owl"), dict(hand=ACTIONS[0], limb=False), "a human hand on a tree frog pilot"),
            (("Owl", "Lynx"), dict(hair=HAIRS[0], hand=ACTIONS[0], limb=False), "human hair and a human hand"),
            (("Owl", "Lynx"), dict(limb=True), "a limb for a pilot whose animals have none")):
        try:
            compose(animals, "Merchant", "X", **look)
            errors.append(f"compose accepted {what}")
        except ValueError:
            pass
    try:
        compose(("Owl", "Lynx"), "Hunter", " ")
        errors.append("compose accepted a brief with no name")
    except ValueError:
        pass
    print("bust.py self-test:", "FAIL" if errors else
          f"ok ({len(species)} species, {len(FACE)} faces, {len(CROWN)} crowns, {len(BACKDROPS)} backdrops, "
          f"{sum(len(v) for v in GEAR.values())} outfits)")
    for e in errors:
        print("  -", e)
    return 1 if errors else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--callsign")
    ap.add_argument("--heritage", nargs=2)
    ap.add_argument("--faction")
    ap.add_argument("--name", default="")
    ap.add_argument("--backdrop", default="")
    ap.add_argument("--backdrops", action="store_true", help="print the backdrop table")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    if args.backdrops:
        for k, (value, avoid, factions, scene, light) in BACKDROPS.items():
            print(f"{k:18} {value:5} {'/'.join(factions) or 'anyone':24} {scene}")
        return 0
    if not (args.heritage and args.faction and args.name.strip()):
        ap.error("a brief needs --heritage FACE CROWN --faction TYPE --name NAME (or --backdrops, --self-test)")
    print(compose(tuple(args.heritage), args.faction, args.name, args.callsign, backdrop=args.backdrop))
    return 0


if __name__ == "__main__":
    sys.exit(main())
