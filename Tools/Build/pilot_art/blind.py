#!/usr/bin/env python3
"""The BLIND READ: does a stranger name both animals of a pilot's avatar, at full size and at icon size?

Garrett's bar (2026-11-10) is "pilot images that really show both clades in the avatar bust". Looking at a picture
whose brief you wrote is no test: you see the jackal because you asked for one. So the avatars are shown, shuffled
and unnamed, to readers who are told nothing (agents that may open only the pictures), and what they name is graded
against the key by this file. Round 1 (one navy plate, a hand in every frame) and round 2 (bust.py as it stands)
were both read this way; the numbers are in Docs/Pilots/PORTRAITS.md section 7. Since 2026-11-11 a pilot's avatar is
the close-up of their realistic scene ("the scene close ups are categorically better than the previous avatar
pictures and should be used in their place"), so that close-up is the picture read (scene.avatar_source); a pilot
whose scene is not painted yet is read from their bust, and the key says which picture each pilot was read from.

    python3 Tools/Build/pilot_art/blind.py prep <dir> [WORD...]     # shuffle the avatars into <dir>, key beside it
    python3 Tools/Build/pilot_art/blind.py grade <dir> [--rows]     # grade <dir>/result.json against the key
    python3 Tools/Build/pilot_art/blind.py --self-test

prep writes <dir>/full/pNN.jpg (640 px: the scene's close-up, or the whole bust), <dir>/thumb/sheetN.png (the
avatar at 64 px, shown enlarged with hard pixels, 25 to a sheet) and the key at <dir>.key.json, OUTSIDE the folder
the readers open. The readers are the
workflow .claude/skills/pilot-creator/references/blind_read.js (one at full size for every ten portraits, two at
icon size); save what it returns as <dir>/result.json. <dir> is a NEW scratch folder each time, never in the repo:
prep refuses a folder that already holds a read, and grade says which pictures were repainted since the read.
One full-size reader sees each portrait, and two readers of the same pictures differ by a few portraits in fifty:
read a set twice before reading much into a difference of two or three.

Two levels, because a reader who says "fox" of a jackal has seen the right kind of animal:
  species  the reader named the animal itself, or an animal everyday speech does not tell apart from it (turtle for
           a tortoise, crow for a raven, alligator for a crocodile, rabbit for a hare, buffalo for a bison, heron
           for an egret, salamander for a newt).
  clade    the reader named its kind: a relative people do tell apart (a fox for a jackal, a cheetah for a tiger,
           a stork for an egret), a look-alike (a porcupine for a hedgehog, a lizard for a newt), or the everyday
           word for its family or order (a cat for a lynx, a dog for a fennec, a rodent for a capybara, a monkey
           for a lemur). The families that name one another are lists of their own (CATS, DOGS, LIZARDS, WADERS,
           RAPTORS, RODENTS, PRIMATES, MUSTELIDS, WHALES, GAMEBIRDS): where the family word counts, a named
           relative never counts for less. Not a kind: a word for a whole class (CLASS_WORDS: "some bird" for a
           heron, "fish" for a koi), and a look-alike of one part (a beaver for a platypus's tail, a duck for its
           bill). For a pilot whose two animals are of one family (a lynx and a caracal) the kind rows say
           little: "cat" twice names both kinds.
The rules of a read (each is a case in --self-test):
  * one name counts for one animal. "bison" for a bison-and-yak pilot names the bison; a hedge ("hawk or eagle",
    "lynx, possibly owl", "fox? wolf?") counts when the right animal is one of the alternatives, and for one
    animal; a hedge between the pilot's own two animals goes to the face animal. One name earns both only when it
    holds a word for each animal that are not a hedge's alternatives and not one compound name ("a hawk-headed
    lizard", "lynx with owl feathers"); a word for a kind must not also be a name of the other animal ("a
    wolf-like dog" for a wolf-and-jackal pilot is one animal). Two different names are two animals; the same
    name twice is one.
  * a name marked "guess" by the reader is counted in the "any" columns and left out of "sure or likely".
  * a denied name is no name: a denial ("not", "no", "never", "neither", "nor", "without", "unlike", "than",
    "isn't", "cannot", "instead of") covers the names after it to the end of its clause ("a dog, not a fox or a
    wolf"); "not just", "not only", "not unlike", "not sure" and "can't tell" deny nothing. A question the reader
    answers with no is taken back ("fox? no, a dog").
  * a plural is the name ("foxes", "wolves", "octopi"), and spelling with a hyphen or two spaces is the name
    ("mantis-shrimp").
  * in a compound name the LAST animal word is the animal: a sloth bear is no sloth, a zebra lionfish no zebra, a
    turkey vulture no turkey, a stag beetle no deer. The first word is the animal when the last is a word for
    its own kind ("fennec fox" is the fennec, and so only a kind of fox for a fox pilot; "koi carp", "koi fish",
    "husky dog"). A few compounds are not their last word either (COMPOUNDS: a sea horse is no horse, a prairie
    dog no dog, a flying fox no fox). A name used as a colour or a look is no animal ("raven-black feathers",
    "an eagle-eyed owl", "a fawn coat"), and "<name>-like" is the animal's kind at most ("a wolf-like dog").
  * human hair is counted from the human features a reader listed, not from the free-text hair answer, which
    readers also use to say "none; shaggy fur". "hairy", "hairless" and "hair: none" are not hair.
The matcher reads names, not sentences: it knows the compounds, denials and hedges above and no others, and
--rows prints what every reader said beside its grade so a person can check a row. Every row of one portrait
comes from ONE reading of its names (the one that gives both animals the most), so where a name is a hedge
between the pilot's own two animals an animal can grade higher with the guesses left out than with them: grade
says so in a WARNING when it happens.
The clade names keep the everyday-word list of the full-body stranger test
(.claude/skills/pilot-art/references/PAINTERS.md section 6: a bull for a yak, a leopard for an ocelot, a porcupine
for a hedgehog).
"""
import argparse
import functools
import hashlib
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bust      # noqa: E402
import creator   # noqa: E402

# Families whose members people name for one another. Each member's kind is the rest of its list (_kin): where
# the everyday word for a family counts ("cat", "rodent"), a named relative never counts for less ("cheetah", "rat").
CATS = ["cat", "feline", "wild ?cat", "lynx", "bob ?cat", "caracal", "ocelot", "serval", "margay", "leopard", "jaguar",
        "cheetah", "tiger", "lion", "lioness", "panther", "cougar", "puma", "mountain lion"]
DOGS = ["dog", "canine", "canid", "wolf", "wolves", "coyote", "jackal", "fox", "fennec", "husky", "dingo",
        "malamute", "german shepherd"]
LIZARDS = ["lizard", "iguana", "gecko", "chameleon", "anole", "basilisk", "skink", "agama", "bearded dragon",
           "komodo dragon"]
WADERS = ["heron", "egret", "crane", "stork", "ibis", "spoonbill", "curlew", "bittern", "wading bird", "wader"]
RAPTORS = ["eagle", "hawk", "falcon", "kite", "osprey", "buzzard", "kestrel", "vulture", "harrier", "raptor",
           "birds? of prey"]
RODENTS = ["rodent", "rat", "mouse", "mice", "capybara", "guinea pig", "porcupine", "beaver", "squirrel", "hamster",
           "chipmunk", "gerbil", "vole", "marmot", "groundhog", "nutria", "chinchilla"]
PRIMATES = ["primate", "monkey", "ape", "lemur", "mandrill", "baboon", "orang ?utang?", "gorilla", "chimp(anzee)?",
            "macaque", "gibbon", "marmoset", "tamarin"]
MUSTELIDS = ["mustelid", "badger", "otter", "weasel", "skunk", "wolverine", "mink", "ferret", "stoat", "marten",
             "polecat"]
WHALES = ["whale", "dolphin", "porpoise", "beluga", "narwhal", "orca", "killer whale"]
GAMEBIRDS = ["pheasant", "grouse", "chicken", "rooster", "hen", "turkey", "pea ?cock", "pea ?fowl", "quail",
             "guinea ?fowl"]


def _kin(family, *own):
    """A family's names less the animal's own, which are its species-level names."""
    return [n for n in family if n not in own]


# animal: (species-level names, clade-level names). Regexes, lower case, matched between non-letters, a space for
# a hyphen. A regular plural is added (_frag); an irregular one is a name of its own (wolves, mice, oxen, octopi).
NAMES = {
    "Armadillo": (["armadillo"], ["pangolin"]),
    "Axolotl": (["axolotl"], ["salamander", "newt", "lizard"]),
    "Badger": (["badger"], _kin(MUSTELIDS, "badger")),
    "Basilisk": (["basilisk"], _kin(LIZARDS, "basilisk")),
    "Beetle": (["beetle", "scarab", "ladybug", "ladybird"], ["cockroach", "weevil"]),
    "Bison": (["bison", "buffalo"], ["bull", "ox", "oxen", "cattle", "cow", "yak", "musk ?ox", "highland", "bovine",
                                    "water buffalo", "cape buffalo", "african buffalo"]),
    "Black hare": (["hare", "jack ?rabbit", "rabbit", "bunny"], []),
    "Booby": (["booby", "gannet"], ["sea ?bird", "gull", "sea ?gull", "albatross", "tern", "cormorant"]),
    "Butterfly": (["butterfly", "monarch"], ["moth"]),
    "Capybara": (["capybara"], _kin(RODENTS, "capybara")),
    "Caracal": (["caracal"], _kin(CATS, "caracal")),
    "Chameleon": (["chameleon"], _kin(LIZARDS, "chameleon")),
    "Cheetah": (["cheetah"], _kin(CATS, "cheetah")),
    "Clownfish": (["clown ?fish", "anemone ?fish"], ["damsel ?fish"]),
    "Coyote": (["coyote"], _kin(DOGS, "coyote")),
    "Crane": (["crane"], _kin(WADERS, "crane")),
    "Crocodile": (["crocodile", "alligator", "croc", "gator", "caiman"], []),
    "Cuttlefish": (["cuttle ?fish"], ["squid", "octopus", "octopi"]),
    "Deer": (["deer", "stag", "doe", "elk", "fawn", "buck", "reindeer", "caribou"], ["moose", "antelope", "gazelle"]),
    "Eagle": (["eagle"], _kin(RAPTORS, "eagle")),
    "Eel": (["eel", "moray"], ["lamprey", "sea snake"]),
    "Egret": (["egret", "heron"], _kin(WADERS, "egret", "heron")),
    "Fennec": (["fennec"], _kin(DOGS, "fennec")),
    "Firefly": (["fire ?fly", "lightning bug", "glow ?worm"], ["beetle"]),
    "Flamingo": (["flamingo"], _kin(WADERS)),
    "Fox": (["fox", "vixen"], _kin(DOGS, "fox")),
    "Gecko": (["gecko"], _kin(LIZARDS, "gecko")),
    "Hare": (["hare", "jack ?rabbit", "rabbit", "bunny"], []),
    "Hawk": (["hawk", "sparrowhawk", "goshawk"], _kin(RAPTORS, "hawk")),
    "Hedgehog": (["hedgehog"], ["porcupine", "echidna"]),
    "Heron": (["heron", "egret"], _kin(WADERS, "heron", "egret")),
    "Horned lizard": (["horned lizard", "horned toad", "horny toad", "thorny devil"], _kin(LIZARDS)),
    "Hummingbird": (["humming ?bird"], []),
    "Hyena": (["hyena", "hyaena"], _kin(DOGS) + ["aardwolf"]),
    "Ibis": (["ibis"], _kin(WADERS, "ibis") + ["flamingo"]),
    "Jackal": (["jackal"], _kin(DOGS, "jackal")),
    "Jellyfish": (["jelly ?fish", "jelly", "man o(?:f|')? ?war"], []),
    "Kangaroo": (["kangaroo", "wallaby"], []),
    "Koi": (["koi"], ["gold ?fish", "carp", "betta"]),
    "Lemur": (["lemur"], _kin(PRIMATES, "lemur")),
    "Lionfish": (["lion ?fish"], ["scorpion ?fish"]),
    "Lobster": (["lobster", "crayfish", "crawfish"], ["crab", "shrimp", "prawn"]),
    "Lynx": (["lynx", "bob ?cat"], _kin(CATS, "lynx", "bob ?cat")),
    "Mandrill": (["mandrill"], _kin(PRIMATES, "mandrill")),
    "Mantis": (["mantis", "mantid"], ["grasshopper", "stick insect", "cricket", "locust"]),
    "Mantis shrimp": (["mantis shrimp"], ["shrimp", "lobster", "crab", "crayfish", "prawn"]),
    "Mole": (["mole"], ["shrew"]),
    "Moose": (["moose"], ["elk", "deer", "stag", "reindeer", "caribou"]),
    "Mouse": (["mouse", "mice"], _kin(RODENTS, "mouse", "mice")),
    "Narwhal": (["narwhal"], _kin(WHALES, "narwhal")),
    "Newt": (["newt", "salamander"], ["axolotl", "lizard"]),
    "Ocelot": (["ocelot"], _kin(CATS, "ocelot")),
    "Octopus": (["octopus", "octopi", "octopodes"], ["squid", "cuttle ?fish"]),
    "Opossum": (["opossum", "possum"], []),
    "Orangutan": (["orang ?utang?"], _kin(PRIMATES, "orang ?utang?")),
    "Orca": (["orca", "killer whale"], _kin(WHALES, "orca", "killer whale")),
    "Oryx": (["oryx", "gemsbok"], ["antelope", "gazelle", "addax", "sable", "deer"]),
    "Ostrich": (["ostrich"], ["emu", "rhea"]),
    "Otter": (["otter"], _kin(MUSTELIDS, "otter")),
    "Owl": (["owl", "owlet"], []),
    "Pangolin": (["pangolin", "scaly anteater"], ["armadillo", "anteater"]),
    "Parrot": (["parrot", "macaw", "cockatoo", "cockatiel", "parakeet", "lorikeet", "budgie", "budgerigar", "conure"], []),
    "Peacock": (["pea ?cock", "pea ?fowl"], _kin(GAMEBIRDS, "pea ?cock", "pea ?fowl")),
    "Pelican": (["pelican"], []),
    "Platypus": (["platypus", "platypi"], []),
    "Porcupine": (["porcupine"], _kin(RODENTS, "porcupine") + ["hedgehog", "echidna"]),
    "Puffin": (["puffin", "sea parrot"], ["auk", "sea ?bird", "penguin"]),
    "Ram": (["ram", "sheep", "bighorn", "mouflon"], ["goat", "ibex"]),
    "Raven": (["raven", "crow"], ["corvid", "black ?bird", "magpie", "rook", "jackdaw", "jay"]),
    "Red panda": (["red panda", "lesser panda"], ["racc?oon"]),
    "Scorpion": (["scorpion"], ["lobster", "crayfish", "crawfish"]),
    "Seahorse": (["sea ?horse", "sea ?dragon"], []),
    "Shoebill": (["shoebill"], _kin(WADERS) + ["pelican"]),
    "Skunk": (["skunk", "polecat"], _kin(MUSTELIDS, "skunk", "polecat")),
    "Sloth": (["sloth"], []),
    "Snail": (["snail"], ["slug"]),
    "Tapir": (["tapir"], []),
    "Tardigrade": (["tardigrade", "water bear"], []),
    "Tiger": (["tiger", "tigress"], _kin(CATS, "tiger")),
    "Tortoise": (["tortoise", "turtle"], []),
    "Toucan": (["toucan"], ["hornbill"]),
    "Tree frog": (["tree ?frog", "frog", "bull ?frog"], ["toad"]),
    "Turkey": (["turkey"], _kin(GAMEBIRDS, "turkey")),
    "Walrus": (["walrus"], ["seal", "sea ?lion"]),
    "Wolf": (["wolf", "wolves"], _kin(DOGS, "wolf", "wolves")),
    "Yak": (["yak"], ["bison", "buffalo", "bull", "ox", "oxen", "cattle", "cow", "musk ?ox", "highland", "bovine"]),
    "Zebra": (["zebra"], ["horse", "equine", "donkey", "pony", "mule"]),
}
SPECIES, CLADE = 2, 1
# A word for a whole class (or wider) is no kind: "some bird" is how a missed heron reads, so it cannot count as
# finding one, and "fish" for a koi is the same answer. No list may hold one (--self-test).
CLASS_WORDS = ("bird", "fish", "insect", "bug", "reptile", "amphibian", "mammal", "arachnid", "crustacean",
               "cephalopod", "mollusc", "mollusk", "animal", "creature")
# In a compound the last animal word is the animal ("sloth bear", "zebra finch", "tiger lily" is no animal at
# all). HEADS are last words no list above holds; a word of NAMES is one too. After "fish" or "bird" the first
# word stays the animal only when the animal is one (FISH, BIRDS): a koi fish is a koi, a parrot fish no parrot.
HEADS = ("bear", "terrier", "hound", "shark", "ray", "finch", "dove", "snake", "viper", "spider", "fly", "wasp",
         "bee", "ant", "bat", "pig", "hog", "worm", "squirrel", "bass", "mussel", "clam", "lily", "duck", "goose",
         "swan", "pigeon", "sparrow", "beaver", "mongoose", "trout", "pike", "louse", "flea", "tamarin", "lemming")
FISH = {"Clownfish", "Eel", "Koi", "Lionfish", "Seahorse"}
BIRDS = {"Booby", "Crane", "Eagle", "Egret", "Flamingo", "Hawk", "Heron", "Hummingbird", "Ibis", "Ostrich", "Owl",
         "Parrot", "Peacock", "Pelican", "Puffin", "Raven", "Shoebill", "Toucan", "Turkey"}
# Compounds that are not their last word either. The word in the group "void" does not name that animal; an
# animal that lists the whole name itself still matches it (Seahorse, Walrus's sea lion, the horned toad).
COMPOUNDS = [re.compile(k) for k in (
    r"flying (?P<void>fox(?:es)?)",
    r"prairie (?P<void>dogs?)",
    r"sea (?P<void>horses?|hares?|cows?|mouse|mice|dogs?|lions?|snakes?|parrots?)",
    r"pit (?P<void>bulls?)",
    r"(?P<void>buck) ?(?:teeth|tooth(?:ed)?)",
    r"horn(?:ed|y) (?P<void>toads?)",
    r"(?P<void>alligator) snapping turtles?",
)]
# A denial covers the names after it to the end of its clause: "a dog, not a fox or a wolf", "more wolf than fox".
_DENIAL = re.compile(r"\b(?:not|no|never|neither|nor|without|unlike|than|cannot|instead of|anything but)\b|n't\b")
_NO_DENIAL = re.compile(r"(?:\bnot|n't) (?:just|only|unlike|sure|certain|clear)\b"
                        r"|(?:\bcannot|\bcan not|\bcould not|n't) (?:quite |really )?(?:tell|decide|say)\b"
                        r"|\bno doubt\b|\bwithout (?:a |any )?doubt\b")
_CLAUSE_END = re.compile(r"[,;:()?.!]| - |\b(?:but|though|although|however|yet)\b")
# A question the reader answers with no takes its own names back: "fox? no, a dog" ("fox? no idea" is a hedge).
_TAKEN_BACK = re.compile(r"\? ?(?:no|nope|nah)\b(?! (?:idea|clue))")
# What marks the alternatives of a hedge: "hawk or eagle", "lynx/owl", "lynx, possibly owl", "fox? wolf?". A comma, a
# dash or a parenthesis alone marks none: "lynx, owl" and "lynx (owl feathers)" name both.
_HEDGE = re.compile(r"\bor\b|[/?]|\b(?:maybe|perhaps|possibly|probably|vs|versus|could be|might be|may be)\b")
# A name used as a colour or a look is no animal: "raven-black feathers", "an eagle-eyed owl", "a fawn coat".
_LOOK = re.compile(r" (?:black|blue|red|grey|gray|white|brown|green|pink|orange|yellow|coloured|colored|eyed|coat)\b")
# "<name>-like" says what it looks like: the animal's kind at most ("a wolf-like dog", "fox-like but not a fox").
_LIKE = re.compile(r" like\b")


def _norm(text):
    """A reader's words as the matcher reads them: lower case, plain apostrophes, a dash a spaced dash, a hyphen
    inside a word a space ("mantis-shrimp", "sea-horse", "hawk-headed"), a run of spaces one space."""
    t = str(text or "").lower().replace("’", "'").replace("‘", "'")
    t = re.sub(r"[‐-―]", " - ", t)
    t = re.sub(r"(?<=[a-z])-(?=[a-z])", " ", t)
    return re.sub(r"\s+", " ", t).strip()


def _frag(name):
    """A name with its regular plural: foxes, lynxes, monarchs, tigers, butterflies. "es" only after a sibilant:
    "stages" is no stag and "rates" no rat."""
    if re.search(r"[^aeiou ?)]y$", name):
        return name[:-1] + "(?:y|ies)"
    return name + ("(?:e?s)?" if re.search(r"(?:s|x|z|ch|sh)$", name) else "s?")


@functools.lru_cache(maxsize=None)
def _rx(name):
    """One name as a whole-word pattern."""
    return re.compile(r"(?<![a-z])(?:" + _frag(name) + r")(?![a-z])")


@functools.lru_cache(maxsize=None)
def _head():
    """An animal word standing right after another word: what makes that word the first half of a compound."""
    words = {n for species, clade in NAMES.values() for n in species + clade} | set(HEADS) | {"fish", "bird"}
    return re.compile(r" (" + "|".join(_frag(n) for n in sorted(words, key=lambda n: (-len(n), n))) + r")(?![a-z])")


def _denied(t):
    """[(start, end)]: the stretches of a normalised text a denial covers."""
    spared = [m.span() for m in _NO_DENIAL.finditer(t)]
    out = []
    for m in _DENIAL.finditer(t):
        if any(a <= m.start() < b for a, b in spared):
            continue
        end = _CLAUSE_END.search(t, m.end())
        out.append((m.start(), end.start() if end else len(t)))
    for m in _TAKEN_BACK.finditer(t):
        start = max((c.end() for c in _CLAUSE_END.finditer(t, 0, m.start())), default=0)
        out.append((start, m.start()))
    return out


@functools.lru_cache(maxsize=1 << 16)
def _matches(animal, t):
    """((start, end, level), ...): where a normalised text names the animal. Not counted: a denied name, a name
    used as a colour or a look, the first half of a compound whose last word is another animal, and a word inside
    a compound that is not that animal (COMPOUNDS). "<name>-like" is the animal's kind at most. Where two of the
    animal's own names overlap, the longer is the name ("water buffalo" is no "buffalo"); where one stands right
    before another, the first is the name ("fennec fox" is a fennec: a kind of fox for a fox pilot)."""
    species, clade = NAMES[animal]
    barred = [m.span("void") for k in COMPOUNDS for m in k.finditer(t)] + _denied(t)
    found = []
    for lv, names in ((SPECIES, species), (CLADE, clade)):
        for n in names:
            for m in _rx(n).finditer(t):
                s, e = m.span()
                if any(a <= s and e <= b for a, b in barred) or _LOOK.match(t, e):
                    continue
                head = _head().match(t, e)
                if head:    # "<name> <animal word>": the last word is the animal, unless it is this animal's own kind
                    word = head.group(1)
                    own = any(_rx(x).fullmatch(word) for x in species + clade)
                    if _rx("fish").fullmatch(word):
                        own = animal in FISH
                    elif _rx("bird").fullmatch(word):
                        own = animal in BIRDS
                    if not own:
                        continue
                found.append((s, e, CLADE if _LIKE.match(t, e) else lv))
    found = [x for x in found
             if not any(y[0] <= x[0] and x[1] <= y[1] and y[1] - y[0] > x[1] - x[0] for y in found)]
    return tuple(x for x in found if not any(t[y[1]:x[0]] == " " for y in found if y[1] < x[0]))


def level(animal, text):
    """SPECIES, CLADE or 0: how well one thing a reader said names the animal."""
    return max((lv for _s, _e, lv in _matches(animal, _norm(text))), default=0)


def _both(face, crown, name):
    """What ONE name earns for both animals at once, or None: it holds a word for each animal, the two are not
    the same word and not alternatives of a hedge, and a word for a kind is not also a name of the other animal. "a hawk-headed lizard" names a hawk and a lizard; "jackal or wolf", "bison" for a
    bison-and-yak pilot, "a wolf-like dog" for a wolf-and-jackal pilot and "fennec fox" each name one animal."""
    t = _norm(name)
    cuts = [m.start() for m in _HEDGE.finditer(t)]
    fm, cm = _matches(face, t), _matches(crown, t)

    def clear(x, others):
        return not any(x[0] < o[1] and o[0] < x[1] for o in others)

    best = None
    for f in fm:
        for c in cm:
            lo, hi = min(f[1], c[1]), max(f[0], c[0])
            if hi <= lo or any(lo <= k < hi for k in cuts):
                continue    # the same words, or the alternatives of a hedge (a compound is one match: _matches)
            if (f[2], c[2]) != (SPECIES, SPECIES) and not (clear(f, cm) and clear(c, fm)):
                continue
            if best is None or (min(f[2], c[2]), f[2] + c[2]) > (min(best), sum(best)):
                best = (f[2], c[2])
    return best


def read_levels(face, crown, said):
    """(face level, crown level) from the list of names one reader gave. One name counts for one animal, unless
    that one name really names both (_both). Where two readings tie, the face animal gets the name, whatever the
    order of the names."""
    said = list(dict.fromkeys(_norm(n) for n in said))    # the same name twice is one name
    f = [level(face, n) for n in said]
    c = [level(crown, n) for n in said]
    options = [(0, 0)]
    for i in range(len(said)):
        options += [(f[i], 0), (0, c[i])]
        both = _both(face, crown, said[i])
        if both:
            options.append(both)
        options += [(f[i], c[j]) for j in range(len(said)) if j != i]
    return max(options, key=lambda t: (min(t), sum(t), t[0], t[1]))


def _md5(path):
    with open(path, "rb") as fh:
        return hashlib.md5(fh.read()).hexdigest()


def prep(out, words=None, salt=None):
    """Shuffle the catalog's avatar pictures into out/ for the readers and write the key beside it. Returns the key.
    A pilot is read from the close-up of their scene once it is painted, else from their bust (scene.avatar_source).
    The words are taken in any case; a word that is no catalog pilot, or has neither picture, raises ValueError, and
    so does a folder that already holds a read (result.json). Pictures of an earlier shuffle in the folder are
    removed."""
    from PIL import Image, ImageDraw, ImageFont
    import scene
    out = os.path.realpath(out)
    salt = salt or os.path.basename(out)
    cat = creator.catalog()
    want = {w.lower() for w in words or ()}
    strangers = sorted(want - {c[0].lower() for c in cat})
    if strangers:
        raise ValueError(f"not in the catalog (creator.py list): {', '.join(strangers)}")
    cat = [c for c in cat if not want or c[0].lower() in want]
    unpainted = [c[0] for c in cat if scene.avatar_source(c[0]) is None]
    if want and unpainted:
        raise ValueError(f"no avatar picture to read yet: {', '.join(unpainted)}")
    cat = [c for c in cat if c[0] not in unpainted]
    if not cat:
        raise ValueError("no avatar picture to shuffle")
    if os.path.exists(os.path.join(out, "result.json")):
        raise ValueError(f"{out} already holds a read (result.json): prep a new folder, so a read stays with the "
                         f"pictures it was made on")
    order = sorted(cat, key=lambda c: hashlib.md5(f"{salt}/{c[0]}".encode("utf-8")).hexdigest())
    for sub, old in (("full", r"p\d+\.jpg"), ("thumb", r"sheet\d+\.png")):
        os.makedirs(os.path.join(out, sub), exist_ok=True)
        for name in os.listdir(os.path.join(out, sub)):
            if re.fullmatch(old, name):
                os.remove(os.path.join(out, sub, name))
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 16)
    except OSError:
        font = ImageFont.load_default()
    key, chips = {}, []
    for i, c in enumerate(order, 1):
        pid = f"p{i:02d}"
        picture, source, box = scene.avatar_source(c[0])
        im = Image.open(source).convert("RGB")
        avatar = im.crop(box) if picture == "scene" else creator.avatar_cut(im)
        shown = os.path.join(out, "full", f"{pid}.jpg")
        (avatar if picture == "scene" else im).resize((640, 640), Image.LANCZOS).save(shown, quality=90)
        key[pid] = dict(word=c[0], person=c[1], face=c[2][0], crown=c[2][1], faction=c[3], callsign=c[4],
                        picture=picture, source_md5=_md5(source), shown_md5=_md5(shown))
        chips.append((pid, avatar.resize((64, 64), Image.LANCZOS).resize((256, 256), Image.NEAREST)))
    for s in range((len(chips) + 24) // 25):
        sheet = Image.new("RGB", (5 * 276 + 20, 5 * 300 + 20), (34, 38, 69))
        d = ImageDraw.Draw(sheet)
        for j, (pid, chip) in enumerate(chips[s * 25:s * 25 + 25]):
            x, y = 20 + (j % 5) * 276, 20 + (j // 5) * 300
            d.text((x, y), pid, fill=(255, 255, 255), font=font)
            sheet.paste(chip, (x, y + 24))
        sheet.save(os.path.join(out, "thumb", f"sheet{s + 1}.png"))
    with open(key_path(out), "w", encoding="utf-8") as fh:
        json.dump(key, fh, indent=1)
    return key


def key_path(out):
    """The key's path: beside the readers' folder, never in it ("." is the folder it names, not a file in it)."""
    return os.path.abspath(out) + ".key.json"


def picture_notes(key, out):
    """What changed since the read was prepared, as sentences: a picture in the folder that is not the one the
    key was written for, and the pilots whose avatar picture was repainted since, or is cut from a different
    picture now (a key from before 2026-11-11 records neither; one from before the scenes names only a bust)."""
    import scene
    notes = []
    swapped = [pid for pid, k in key.items() if k.get("shown_md5") and os.path.exists(os.path.join(out, "full", f"{pid}.jpg"))
               and _md5(os.path.join(out, "full", f"{pid}.jpg")) != k["shown_md5"]]
    if swapped:
        notes.append(f"{len(swapped)} pictures in the folder are not the ones this key was written for: "
                     f"{', '.join(swapped)}")
    repainted = []
    for k in key.values():
        was, md5 = k.get("picture", "bust"), k.get("source_md5") or k.get("bust_md5")
        now = scene.avatar_source(k["word"])
        if md5 and now and (now[0] != was or _md5(now[1]) != md5):
            repainted.append(k["word"])
    if repainted:
        notes.append(f"{len(repainted)} avatars were repainted since this read, which is of the earlier pictures: "
                     f"{', '.join(repainted)}")
    return notes


def _pid(text):
    """A portrait id as prep writes it: "P7", " p07 " and "p07.jpg" are p07."""
    m = re.fullmatch(r"\s*p0*(\d+)(?:\.jpe?g)?\s*", str(text).lower())
    return f"p{int(m.group(1)):02d}" if m else str(text).strip()


def _by_id(entries, who, problems):
    out = {}
    if not isinstance(entries, list):
        problems.append(f"{who} returned {type(entries).__name__}, not a list of portraits: nothing of it is graded")
        return out
    for r in entries:
        if not isinstance(r, dict):
            problems.append(f"{who} returned an entry that is no object ({str(r)[:30]!r}): it is left out")
            continue
        pid = _pid(r.get("id", ""))
        if pid in out:
            problems.append(f"{who} returned {pid} twice: the first entry is graded")
            continue
        out[pid] = r
    return out


def _strings(value, what, problems):
    """A reader's list of strings, whatever it sent: nothing is an empty list and one string a list of one; an
    entry that is no string is said and left out."""
    if value is None:
        return []
    if isinstance(value, str):
        problems.append(f"{what} is one string, not a list: it is read as a list of one")
        return [value]
    if not isinstance(value, list):
        problems.append(f"{what} is {type(value).__name__}, not a list: it is read as empty")
        return []
    bad = [x for x in value if not isinstance(x, str)]
    if bad:
        problems.append(f"{what} holds {len(bad)} entries that are no strings: they are left out")
    return [x for x in value if isinstance(x, str)]


_HAIR = re.compile(r"(?:(?<![a-z-])|(?<=human-))hair(?:styles?|dos?|cuts?|line)?(?![a-z-])")
_NO_HAIR = re.compile(r"\b(?:no|not|without|lacks?|lacking)\s+(?:any\s+|visible\s+|real\s+|actual\s+|true\s+)?"
                      r"(?:human[ -]|head\s+)?hair(?![a-z-])"
                      r"|hair(?:styles?|dos?|cuts?|line)?\s*[:=-]?\s*\(?\s*(?:none|no|absent|n/a)\b|\bnone\s*$")


def _human_hair(features):
    """Whether a reader listed human hair among the human features it saw. The free-text "hair" answer is not the
    signal: readers also use it to say "none; shaggy fur" or "reads as fur rather than styled hair"."""
    for f in features:
        f = f.lower().strip().rstrip(".")
        if _HAIR.search(f) and not _NO_HAIR.search(f):
            return True
    return False


CONFIDENCE = ("sure", "likely", "guess")
HANDS = ("human", "animal", "unclear", "none")


def grade(key, result):
    """(totals, rows, problems). rows: one dict per portrait with what each reader said and how it grades.
    problems: what could not be graded as asked (a portrait a reader left out, an id that is not in the key, a
    value the workflow's schema does not allow): a total never hides one. A portrait with no full-size read is
    left out of the totals; an icon reader that left a portrait out named nothing in it; a confidence that is
    none of sure, likely and guess counts as a guess, and a hand kind that is none of the four as unclear."""
    if not isinstance(result, dict):
        raise ValueError(f"the result is {type(result).__name__}, not what the workflow returns: "
                         f'{{"full": [...], "thumb": [[...], [...]]}}')
    strangers = sorted({k[x] for k in key.values() for x in ("face", "crown") if k.get(x) not in NAMES}, key=str)
    if strangers:
        raise ValueError(f"the key names animals blind.NAMES has no line for: {', '.join(map(str, strangers))}")
    problems = []
    full = _by_id(result.get("full") or [], "the full-size readers", problems)
    readers = result.get("thumb") or []
    if not isinstance(readers, list):
        problems.append(f"the icon readers are {type(readers).__name__}, not a list of readers: none is graded")
        readers = []
    if not readers:
        problems.append("no icon reader in the result: the icon rows are left out")
    chips = [_by_id(reader, f"icon reader {i}", problems) for i, reader in enumerate(readers, 1)]
    for who, got in [("the full-size readers", full)] + [(f"icon reader {i}", c) for i, c in enumerate(chips, 1)]:
        extra = sorted(set(got) - set(key))
        missing = sorted(set(key) - set(got))
        if extra:
            problems.append(f"{who} returned ids that are not in the key: {', '.join(extra)}")
        if missing:
            problems.append(f"{who} left out {len(missing)} of {len(key)}: {', '.join(missing)}")
    rows = []
    for pid, k in key.items():
        r = full.get(pid)
        if not r:
            continue
        said, firm = [], []
        animals = r.get("animals")
        if not isinstance(animals, list):
            problems.append(f"{pid}: the animals are {type(animals).__name__}, not a list: read as none")
            animals = []
        for a in animals:
            if not isinstance(a, dict):
                problems.append(f"{pid}: an animal is {str(a)[:30]!r}, not a name with a confidence: left out")
                continue
            name = str(a.get("name") or "")
            conf = str(a.get("confidence") or "").strip().lower()
            if conf not in CONFIDENCE:
                problems.append(f"{pid}: the confidence of {name!r} is {a.get('confidence')!r}: counted as a guess")
                conf = "guess"
            said.append(name)
            if conf != "guess":
                firm.append(name)
        hand = str(r.get("hand_kind") or "").strip().lower()
        if hand not in HANDS:
            problems.append(f"{pid}: the hand kind is {r.get('hand_kind')!r}: counted as unclear")
            hand = "unclear"
        chip_said = [_strings(c[pid].get("animals"), f"{pid}, icon reader {i}: the animals", problems) if pid in c else []
                     for i, c in enumerate(chips, 1)]
        any_, firm_ = read_levels(k["face"], k["crown"], said), read_levels(k["face"], k["crown"], firm)
        if firm_[0] > any_[0] or firm_[1] > any_[1]:
            problems.append(f"{pid}: one name is a hedge between this pilot's two animals, so an animal grades higher "
                            f"with the guesses left out ({firm_}) than with them ({any_}): check the row")
        rows.append(dict(
            id=pid, word=k["word"], face=k["face"], crown=k["crown"], said=said, any=any_, firm=firm_,
            chip=[read_levels(k["face"], k["crown"], names) for names in chip_said], chip_said=chip_said,
            hand=hand, hair=str(r.get("hair") or "none").strip().lower().rstrip("."),
            human_hair=_human_hair(_strings(r.get("human_features"), f"{pid}: the human features", problems)),
            reads_as=str(r.get("head_feature_reads_as") or ""), myth=str(r.get("mythical_or_generic") or ""),
            defects=str(r.get("defects") or ""), background=str(r.get("background") or "")))
    n = len(rows)
    t = {"portraits": n}
    for label, field in (("any confidence", "any"), ("sure or likely", "firm")):
        t[f"full size, both animals, species ({label})"] = sum(min(v[field]) == SPECIES for v in rows)
        t[f"full size, both animals, clade ({label})"] = sum(min(v[field]) >= CLADE for v in rows)
        t[f"full size, face animal, species ({label})"] = sum(v[field][0] == SPECIES for v in rows)
        t[f"full size, second animal, species ({label})"] = sum(v[field][1] == SPECIES for v in rows)
        t[f"full size, second animal, clade ({label})"] = sum(v[field][1] >= CLADE for v in rows)
    if chips:
        t["icon readers"] = len(chips)
        for label, want in (("species", SPECIES), ("clade", CLADE)):
            t[f"icon size, both animals, {label} (every reader)"] = sum(
                all(min(g) >= want for g in v["chip"]) for v in rows)
            t[f"icon size, both animals, {label} (any reader)"] = sum(
                any(min(g) >= want for g in v["chip"]) for v in rows)
        t["icon size, second animal, clade (any reader)"] = sum(any(g[1] >= CLADE for g in v["chip"]) for v in rows)
    for kind in HANDS:
        t[f"hand in frame: {kind}"] = sum(v["hand"] == kind for v in rows)
    t["human hair"] = sum(v["human_hair"] for v in rows)
    return t, rows, problems


_NOTHING = re.compile(r"(?:,?\s*otherwise\s+)?\bnone(?: obvious| on the face| visible| noted)?\b[;,.]?"
                      r"|\bno hands? (?:shown|visible|in (?:the )?frame)\b[;,.]?", re.I)


def _more(text):
    """A reader's free-text answer less its "none" and "no hands shown": what is left is worth a line."""
    return _NOTHING.sub("", str(text or "")).strip(" ;,.")


def print_rows(rows):
    order = sorted(rows, key=lambda v: (min(v["any"]), sum(v["any"]), v["word"]))
    for v in order:
        mark = {2: "species", 1: "clade", 0: "MISSED"}
        print(f"{v['word']:10} {v['face']:13} {mark[v['any'][0]]:8} {v['crown']:13} {mark[v['any'][1]]:8} "
              f"icon {v['chip']}  said {v['said']} | icon {v['chip_said']}")
        also = ([f"hand: {v['hand']}"] if v["hand"] not in ("none", "") else []) + \
            (["human hair"] if v["human_hair"] else []) + \
            [f"{label}: {_more(v[k])}" for k, label in (("myth", "mythical"), ("defects", "defects")) if _more(v[k])]
        if also:   # what else the reader reported: the hand, the hair, a mythical reading, painting defects
            print(" " * 11 + " | ".join(also))


def self_test():
    errors = []
    for text, want in (("none; no hands shown", ""), ("None.", ""), ("none on the face; no hands shown.", ""),
                       ("none obvious; a second claw at the edge", "a second claw at the edge"),
                       ("a sea-serpent feel, otherwise none", "a sea-serpent feel"),
                       ("six fingers on the glove", "six fingers on the glove"), (None, "")):
        if _more(text) != want:
            errors.append(f"--rows would print {_more(text)!r} for {text!r}, not {want!r}")
    for k in sorted(set(bust.FACE) | set(bust.CROWN)):
        if k not in NAMES:
            errors.append(f"NAMES has no line for {k} (bust.py can paint it, so a read must be gradable)")
    plurals = ("", "s", "es")
    for k, (species, clade) in NAMES.items():
        if k not in bust.FACE and k not in bust.CROWN:
            errors.append(f"NAMES names {k}, which bust.py cannot paint")
        if not species:
            errors.append(f"{k} has no species-level name")
        if set(species) & set(clade):
            errors.append(f"{k}: {sorted(set(species) & set(clade))} is on both of its lists")
        for n in species + clade:
            try:
                re.compile(n)
            except re.error as e:
                errors.append(f"{k}: bad pattern {n!r}: {e}")
                continue
            if "|" in re.sub(r"\([^)]*\)", "", n) or n != n.lower() or "-" in n:
                errors.append(f"{k}: {n!r} must be one lower-case name with a space for a hyphen (the plural is "
                              f"added at its end)")
            for w in CLASS_WORDS:   # a class is no kind, on either list, in any number
                if any(_rx(n).fullmatch(w + p) for p in plurals):
                    errors.append(f"{k}: {n!r} matches {w!r}, a class word, and a class is no kind (CLASS_WORDS)")
        if level(k, k.lower()) != SPECIES:
            errors.append(f"{k}: its own name does not grade as itself")
    for group, n in ((FISH, "FISH"), (BIRDS, "BIRDS")):
        for k in sorted(group - set(NAMES)):
            errors.append(f"{n} names {k}, which NAMES does not")
    cases = [
        (("Lynx", "Jackal"), ["lynx or caracal"], (SPECIES, 0)),
        (("Lynx", "Jackal"), ["lynx", "fox (tail)"], (SPECIES, CLADE)),
        (("Orca", "Eagle"), ["orca", "some bird (hawk or eagle feathers)"], (SPECIES, SPECIES)),
        (("Gecko", "Mantis"), ["leopard gecko", "praying mantis"], (SPECIES, SPECIES)),
        (("Flamingo", "Mantis shrimp"), ["flamingo", "praying mantis"], (SPECIES, 0)),
        (("Flamingo", "Mantis shrimp"), ["flamingo", "lobster"], (SPECIES, CLADE)),
        (("Hare", "Deer"), ["jackalope"], (0, 0)),
        (("Zebra", "Lionfish"), ["seahorse"], (0, 0)),
        (("Hedgehog", "Tortoise"), ["hedgehog", "tortoise or turtle"], (SPECIES, SPECIES)),
        (("Cheetah", "Hyena"), ["cheetah", "leopard"], (SPECIES, 0)),
        (("Mouse", "Owl"), ["peafowl", "mouse"], (SPECIES, 0)),
        (("Coyote", "Turkey"), ["wolf", "turkey"], (CLADE, SPECIES)),
        (("Egret", "Koi"), ["catfish", "caterpillar"], (0, 0)),
        # the two levels, as the docstring gives them
        (("Tortoise", "Raven"), ["turtle", "crow"], (SPECIES, SPECIES)),
        (("Crocodile", "Hare"), ["alligator", "rabbit"], (SPECIES, SPECIES)),
        (("Black hare", "Badger"), ["rabbit", "skunk"], (SPECIES, CLADE)),
        (("Egret", "Heron"), ["heron"], (SPECIES, 0)),
        (("Bison", "Jackal"), ["buffalo", "fox"], (SPECIES, CLADE)),
        (("Tiger", "Egret"), ["cheetah", "stork"], (CLADE, CLADE)),
        (("Hedgehog", "Newt"), ["porcupine", "lizard"], (CLADE, CLADE)),
        (("Lynx", "Fennec"), ["cat", "dog"], (CLADE, CLADE)),
        (("Capybara", "Lemur"), ["rodent", "monkey"], (CLADE, CLADE)),
        (("Yak", "Ocelot"), ["bull", "leopard"], (CLADE, CLADE)),
        (("Porcupine", "Zebra"), ["rodent", "equine"], (CLADE, CLADE)),
        # where the family word counts, a named relative counts too; a look-alike of the whole animal is its kind
        (("Capybara", "Lemur"), ["rat", "baboon"], (CLADE, CLADE)),
        (("Otter", "Narwhal"), ["badger", "beluga"], (CLADE, CLADE)),
        (("Peacock", "Flamingo"), ["pheasant", "stork"], (CLADE, CLADE)),
        (("Hyena", "Pangolin"), ["wild dog", "armadillo"], (CLADE, CLADE)),
        (("Scorpion", "Raven"), ["lobster", "jackdaw"], (CLADE, CLADE)),
        (("Egret", "Newt"), ["heron", "salamander"], (SPECIES, SPECIES)),
        (("Orca", "Narwhal"), ["killer whale", "sperm whale"], (SPECIES, CLADE)),
        (("Narwhal", "Owl"), ["whale shark", "owlet"], (0, SPECIES)),
        (("Tiger", "Fox"), ["tigress", "vixen"], (SPECIES, SPECIES)),
        (("Pangolin", "Hyena"), ["scaly anteater", "hyaena"], (SPECIES, SPECIES)),
        (("Platypus", "Owl"), ["beaver", "duck"], (0, 0)),
        (("Egret", "Koi"), ["stork", "heron"], (SPECIES, 0)),
        # a class is no kind
        (("Koi", "Eel"), ["koi fish"], (SPECIES, 0)),
        (("Heron", "Lynx"), ["some bird", "lynx"], (0, SPECIES)),
        (("Gecko", "Mantis"), ["reptile", "insect"], (0, 0)),
        (("Gecko", "Mantis"), ["lizard", "grasshopper"], (CLADE, CLADE)),
        # one name, one animal; a hedge is one animal; a tie goes to the face animal in either order
        (("Bison", "Yak"), ["bison"], (SPECIES, 0)),
        (("Jackal", "Wolf"), ["jackal or wolf"], (SPECIES, 0)),
        (("Hawk", "Eagle"), ["hawk or eagle"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx or owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx/owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx, possibly owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx - possibly owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx maybe owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx? owl?"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx vs owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx – possibly owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx perhaps owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx probably owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx versus owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["either lynx or owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx could be owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx might be owl"], (SPECIES, 0)),
        (("Lynx", "Owl"), ["lynx may be owl"], (SPECIES, 0)),
        (("Armadillo", "Axolotl"), ["armadillo or axolotl", "some bird"], (SPECIES, 0)),
        (("Armadillo", "Axolotl"), ["some bird", "armadillo or axolotl"], (SPECIES, 0)),
        (("Wolf", "Jackal"), ["a wolf-like dog"], (CLADE, 0)),
        (("Wolf", "Owl"), ["a wolf-like dog", "owl"], (CLADE, SPECIES)),
        (("Fox", "Owl"), ["fox-like but not a fox"], (CLADE, 0)),
        (("Fox", "Fennec"), ["fennec fox"], (0, SPECIES)),
        (("Fox", "Owl"), ["fennec fox", "red fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["fennec fox"], (CLADE, 0)),
        (("Lynx", "Caracal"), ["cat", "cat"], (CLADE, 0)),
        (("Fox", "Jackal"), ["fox", "Fox"], (SPECIES, 0)),
        (("Lynx", "Caracal"), ["cat and lynx"], (SPECIES, 0)),
        # one name that names both
        (("Basilisk", "Hawk"), ["a hawk-headed lizard"], (CLADE, SPECIES)),
        (("Lynx", "Owl"), ["lynx and owl"], (SPECIES, SPECIES)),
        (("Lynx", "Owl"), ["lynx with owl feathers"], (SPECIES, SPECIES)),
        (("Lynx", "Owl"), ["lynx, with owl feathers"], (SPECIES, SPECIES)),
        (("Lynx", "Owl"), ["lynx (owl feathers)"], (SPECIES, SPECIES)),
        (("Lynx", "Owl"), ["lynx - owl feathers"], (SPECIES, SPECIES)),
        (("Lynx", "Owl"), ["lynx, owl"], (SPECIES, SPECIES)),
        (("Lynx", "Caracal"), ["lynx and caracal"], (SPECIES, SPECIES)),
        (("Bison", "Yak"), ["half bison half yak"], (SPECIES, SPECIES)),
        (("Hedgehog", "Porcupine"), ["hedgehog with porcupine quills"], (SPECIES, SPECIES)),
        # a denied name
        (("Fox", "Owl"), ["dog (not a fox)", "owl"], (CLADE, SPECIES)),
        (("Fox", "Owl"), ["more wolf than fox"], (CLADE, 0)),
        (("Fox", "Owl"), ["not a red fox"], (0, 0)),
        (("Fox", "Owl"), ["isn't a fox"], (0, 0)),
        (("Fox", "Owl"), ["isn’t a fox"], (0, 0)),
        (("Fox", "Owl"), ["does not look like a fox"], (0, 0)),
        (("Fox", "Owl"), ["cannot be a fox"], (0, 0)),
        (("Fox", "Owl"), ["no sign of a fox"], (0, 0)),
        (("Fox", "Owl"), ["never a fox", "an owl without doubt"], (0, SPECIES)),
        (("Fox", "Owl"), ["unlike a fox, an owl"], (0, SPECIES)),
        (("Fox", "Owl"), ["neither fox nor owl"], (0, 0)),
        (("Fox", "Owl"), ["not a fox or an owl"], (0, 0)),
        (("Fox", "Wolf"), ["dog (not a fox or wolf)"], (CLADE, 0)),
        (("Fox", "Owl"), ["not a wolf but a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not just a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not unlike a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not sure - fox?"], (SPECIES, 0)),
        (("Fox", "Owl"), ["can't tell if fox or wolf"], (SPECIES, 0)),
        (("Fox", "Owl"), ["cannot tell if fox or wolf"], (SPECIES, 0)),
        (("Fox", "Owl"), ["can not tell if fox or wolf"], (SPECIES, 0)),
        (("Fox", "Owl"), ["couldn't say if fox or wolf"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not sure if it is a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["an owl without a fox's muzzle"], (0, SPECIES)),
        (("Fox", "Owl"), ["an owl instead of a fox"], (0, SPECIES)),
        (("Fox", "Owl"), ["anything but a fox"], (0, 0)),
        (("Fox", "Owl"), ["no fox, nor owl"], (0, 0)),
        (("Fox", "Owl"), ["without doubt a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["no doubt a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not only a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not certain it is a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not clear if fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["could not decide between fox and wolf"], (SPECIES, 0)),
        # a denial ends with its clause
        (("Fox", "Owl"), ["not a wolf; a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not a wolf: a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not a wolf. a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not a wolf! a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not a wolf - a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not a wolf (a fox)"], (SPECIES, 0)),
        (("Fox", "Owl"), ["(not a wolf) fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not a wolf? a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not a wolf – a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not a wolf though a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not a wolf although a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not a wolf however a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["not a wolf yet a fox"], (SPECIES, 0)),
        (("Fox", "Owl"), ["fox? no, a dog"], (CLADE, 0)),
        (("Fox", "Owl"), ["owl, fox? no"], (0, SPECIES)),
        (("Fox", "Owl"), ["fox or wolf? no idea"], (SPECIES, 0)),
        # a plural, a hyphen, two spaces
        (("Fox", "Tiger"), ["foxes", "tigers"], (SPECIES, SPECIES)),
        (("Octopus", "Lynx"), ["octopuses", "lynxes"], (SPECIES, SPECIES)),
        (("Butterfly", "Ostrich"), ["monarchs", "ostriches"], (SPECIES, SPECIES)),
        (("Lynx", "Jackal"), ["LYNX", "Red Fox"], (SPECIES, CLADE)),
        (("Wolf", "Mouse"), ["wolves", "mice"], (SPECIES, SPECIES)),
        (("Platypus", "Butterfly"), ["platypus", "butterflies"], (SPECIES, SPECIES)),
        (("Octopus", "Platypus"), ["octopi", "platypi"], (SPECIES, SPECIES)),
        (("Yak", "Mantis"), ["oxen", "mantids"], (CLADE, SPECIES)),
        (("Eagle", "Lynx"), ["birds of prey", "lynxes"], (CLADE, SPECIES)),
        (("Deer", "Mouse"), ["early stages of an owl", "first-rates"], (0, 0)),
        (("Mantis", "Newt"), ["mantis-shrimp"], (0, 0)),
        (("Mantis", "Newt"), ["mantis  shrimp"], (0, 0)),
        (("Flamingo", "Mantis shrimp"), ["mantis-shrimp"], (0, SPECIES)),
        (("Red panda", "Seahorse"), ["red  panda", "sea-horse"], (SPECIES, SPECIES)),
        (("Red panda", "Puffin"), ["red-panda", "sea bird"], (SPECIES, CLADE)),
        (("Orangutan", "Owl"), ["orang-utan"], (SPECIES, 0)),
        # a compound: the last animal word is the animal
        (("Turkey", "Owl"), ["turkey vulture"], (0, 0)),
        (("Eagle", "Owl"), ["eagle owl"], (0, SPECIES)),
        (("Tiger", "Newt"), ["tiger salamander"], (0, SPECIES)),
        (("Gecko", "Cheetah"), ["leopard gecko"], (SPECIES, 0)),
        (("Walrus", "Bison"), ["walrus", "water buffalo"], (SPECIES, CLADE)),
        (("Walrus", "Bison"), ["cape buffalo"], (0, CLADE)),
        (("Zebra", "Lionfish"), ["zebra lionfish"], (0, SPECIES)),
        (("Deer", "Beetle"), ["stag beetle"], (0, SPECIES)),
        (("Mouse", "Owl"), ["mouse lemur"], (0, 0)),
        (("Hummingbird", "Lobster"), ["hummingbird hawk-moth"], (0, 0)),
        (("Orangutan", "Sloth"), ["sloth bear"], (0, 0)),
        (("Fox", "Firefly"), ["fox terrier"], (0, 0)),
        (("Tree frog", "Scorpion"), ["scorpion fish"], (0, 0)),
        (("Puffin", "Pelican"), ["pelican eel"], (0, 0)),
        (("Zebra", "Hawk"), ["zebra finch", "hawk moth"], (0, 0)),
        (("Wolf", "Crane"), ["wolf spider", "crane fly"], (0, 0)),
        (("Deer", "Kangaroo"), ["deer mouse", "kangaroo rat"], (0, 0)),
        (("Yak", "Owl"), ["bull frog", "owl butterfly"], (0, 0)),
        (("Lynx", "Parrot"), ["cat fish", "parrot fish"], (0, 0)),
        (("Lynx", "Heron"), ["cat bird", "heron bird"], (0, SPECIES)),
        (("Tiger", "Peacock"), ["tiger lily", "peacock spider"], (0, 0)),
        (("Fennec", "Koi"), ["fennec fox", "koi carp"], (SPECIES, SPECIES)),
        (("Wolf", "Koi"), ["husky dog", "koi fish"], (CLADE, SPECIES)),
        (("Ram", "Eel"), ["bighorn sheep", "moray eel"], (SPECIES, SPECIES)),
        # a compound that is not its last word either, and a listed whole name
        (("Zebra", "Lionfish"), ["sea horse"], (0, 0)),
        (("Seahorse", "Koi"), ["sea horse"], (SPECIES, 0)),
        (("Horned lizard", "Tree frog"), ["horned toad"], (SPECIES, 0)),
        (("Tree frog", "Owl"), ["horned toad", "a toad"], (CLADE, 0)),
        (("Tree frog", "Owl"), ["horned toad"], (0, 0)),
        (("Deer", "Hare"), ["rabbit (buck teeth)"], (0, SPECIES)),
        (("Fox", "Owl"), ["flying fox"], (0, 0)),
        (("Wolf", "Capybara"), ["prairie dog", "guinea pig"], (0, CLADE)),
        (("Crocodile", "Tortoise"), ["alligator snapping turtle"], (0, SPECIES)),
        (("Parrot", "Puffin"), ["sea parrot"], (0, SPECIES)),
        (("Zebra", "Yak"), ["pit bull"], (0, 0)),
        (("Red panda", "Jellyfish"), ["racoon", "portuguese man-of-war"], (CLADE, SPECIES)),
        (("Wolf", "Parrot"), ["german shepherd", "cockatiel"], (CLADE, SPECIES)),
        (("Tiger", "Walrus"), ["sea lion"], (0, CLADE)),
        # a name used as a colour or a look
        (("Raven", "Peacock"), ["some bird (raven-black feathers)", "some bird (peacock-blue plumage)"], (0, 0)),
        (("Deer", "Eagle"), ["dog (fawn coat)", "an eagle-eyed owl"], (0, 0)),
    ]
    for (face, crown), said, want in cases:
        got = read_levels(face, crown, said)
        if got != want:
            errors.append(f"{face}/{crown} read as {said}: graded {got}, want {want}")
        if read_levels(face, crown, said[::-1]) != got:
            errors.append(f"{face}/{crown} read as {said}: the order of the names changes the grade")
    for look in ("black", "blue", "red", "grey", "gray", "white", "brown", "green", "pink", "orange", "yellow",
                 "coloured", "colored", "eyed", "coat"):
        if level("Raven", f"some bird (raven {look} feathers)") or level("Raven", f"raven-{look}"):
            errors.append(f"a name used as a look counts as the animal: 'raven {look}'")
    fish = ("Clownfish", "Eel", "Koi", "Lionfish", "Seahorse")
    birds = ("Booby", "Crane", "Eagle", "Egret", "Flamingo", "Hawk", "Heron", "Hummingbird", "Ibis", "Ostrich", "Owl",
             "Parrot", "Peacock", "Pelican", "Puffin", "Raven", "Shoebill", "Toucan", "Turkey")
    for group, word in ((fish, "fish"), (birds, "bird")):
        for k in group:     # "koi fish" is the koi, "heron bird" the heron
            if level(k, f"{k.lower()} {word}") != SPECIES:
                errors.append(f"'{k.lower()} {word}' does not grade as the {k.lower()}")
        for k in sorted(set(NAMES) - set(group)):   # "parrot fish" is no parrot, "cat bird" no cat
            if level(k, f"{k.lower()} {word}"):
                errors.append(f"'{k.lower()} {word}' grades as the {k.lower()}, which is no {word}")
    for w in ("bird", "fish", "insect", "bug", "reptile", "amphibian", "mammal"):
        if w not in CLASS_WORDS:
            errors.append(f"CLASS_WORDS lacks {w!r}: a class is no kind")
    for feature, want in (("human hair: red bun", True), ("a human hairline under the crest", True),
                          ("not animal fur but real human hair", True), ("human-hair braid", True),
                          ("human hand with hairy back", False), ("hairy knuckles on the paw-hand", False),
                          ("human hair: none", False), ("hair: none (feathers instead)", False),
                          ("human hair on the scalp: none", False),
                          ("eyebrows (hair-thin)", False), ("no human hair", False), ("hairless scalp", False),
                          ("reads as fur, not hair", False), ("clothes", False)):
        if _human_hair([feature]) != want:
            errors.append(f"human hair: {feature!r} counts as {not want}")
    key = {"p01": dict(word="X", face="Lynx", crown="Jackal"), "p02": dict(word="Y", face="Zebra", crown="Yak")}
    result = {"full": [dict(id="p01", animals=[dict(name="lynx", confidence="sure"), dict(name="fox", confidence="guess")],
                            hand_kind="none", hair="none", human_features=["clothes", "human hair: red bun"]),
                       dict(id="P2", animals=[dict(name="zebra", confidence="sure"), dict(name="yak", confidence="likely")],
                            hand_kind="human", hair="long dark mane, reads as fur rather than styled hair",
                            human_features=["clothes", "upright posture", "no human hair"])],
              "thumb": [[dict(id="p01", animals=["cat"]), dict(id="p02", animals=["zebra", "yak"])],
                        [dict(id="p01", animals=["cat", "fox"])]]}
    t, rows, problems = grade(key, result)
    for name, want in (("portraits", 2),
                       ("full size, both animals, clade (any confidence)", 2),
                       ("full size, both animals, clade (sure or likely)", 1),
                       ("full size, both animals, species (any confidence)", 1),
                       ("icon readers", 2),
                       ("icon size, both animals, clade (any reader)", 2),
                       ("icon size, both animals, species (any reader)", 1),
                       # the second icon reader left p02 out: it named nothing there, so p02 is not "every reader"
                       ("icon size, both animals, species (every reader)", 0),
                       ("icon size, both animals, clade (every reader)", 0),
                       ("hand in frame: human", 1), ("hand in frame: none", 1),
                       # hair is what a reader listed as a human feature: the mane "rather than hair" is not hair
                       ("human hair", 1)):
        if t.get(name) != want:
            errors.append(f"grade: {name} = {t.get(name)}, want {want}")
    if [v["human_hair"] for v in rows] != [True, False]:
        errors.append(f"grade: human hair is p01's listed feature, not p02's free-text answer: {rows}")
    if not any("icon reader 2 left out 1 of 2: p02" in p for p in problems):
        errors.append(f"grade: a portrait an icon reader left out is not reported: {problems}")
    t, rows, problems = grade(key, {"full": result["full"][:1] + [dict(result["full"][0], id="p09")], "thumb": []})
    if t["portraits"] != 1 or not any("not in the key: p09" in p for p in problems) \
            or not any("left out 1 of 2: p02" in p for p in problems) or not any("no icon reader" in p for p in problems):
        errors.append(f"grade: a missing full-size read, an unknown id or no icon reader is not reported: {problems}")
    # the first of two entries for one portrait is graded, and said
    twice = result["full"] + [dict(result["full"][0], animals=[dict(name="owl", confidence="sure")])]
    t, rows, problems = grade(key, {"full": twice, "thumb": result["thumb"]})
    if rows[0]["said"] != ["lynx", "fox"] or not any("returned p01 twice" in p for p in problems):
        errors.append(f"grade: of two entries for p01 the first is graded and the second reported: {rows[0]['said']}")
    # what the workflow's schema does not allow is graded on the safe side, and said
    odd = {"full": [dict(id="p01", animals=[dict(name="lynx", confidence="Sure "), dict(name="fox", confidence="certain"),
                                           dict(name=None, confidence=None), "jackal"],
                         hand_kind="Human", human_features="human hair: red bun", defects=None),
                    dict(id="p02", animals=None, hand_kind=None, human_features=[None, "clothes"]), "p03"],
           "thumb": [[dict(id="p01", animals="cat"), dict(id="p02", animals=[dict(name="zebra")])], None]}
    t, rows, problems = grade(key, odd)
    for name, want in (("portraits", 2), ("full size, both animals, clade (any confidence)", 1),
                       ("full size, both animals, clade (sure or likely)", 0),
                       ("full size, face animal, species (sure or likely)", 1),
                       ("hand in frame: human", 1), ("hand in frame: unclear", 1), ("human hair", 1)):
        if t.get(name) != want:
            errors.append(f"grade, off-schema values: {name} = {t.get(name)}, want {want}")
    for must in ("the confidence of 'fox' is 'certain': counted as a guess", "p01: an animal is 'jackal'",
                 "p02: the animals are NoneType", "p02: the hand kind is None: counted as unclear",
                 "the human features is one string", "an entry that is no object", "icon reader 2 returned NoneType",
                 "icon reader 1: the animals is one string", "entries that are no strings"):
        if not any(must in p for p in problems):
            errors.append(f"grade, off-schema values: nothing said {must!r}: {problems}")
    # every row of a portrait comes from one reading; where that makes an animal grade higher without the
    # guesses, grade says so
    hedge = {"full": [dict(id="p01", animals=[dict(name="mustelid", confidence="guess"),
                                              dict(name="newt or badger", confidence="sure")], hand_kind="none")],
             "thumb": [[dict(id="p01", animals=[])]]}
    t, rows, problems = grade({"p01": dict(word="X", face="Axolotl", crown="Badger")}, hedge)
    if (rows[0]["any"], rows[0]["firm"]) != ((CLADE, CLADE), (0, SPECIES)) \
            or not any("grades higher with the guesses left out" in p for p in problems):
        errors.append(f"grade: a hedge between the pilot's two animals is not said: {rows[0]['any']}, "
                      f"{rows[0]['firm']}, {problems}")
    print_rows_ok = True
    try:
        import contextlib
        import io
        with contextlib.redirect_stdout(io.StringIO()):
            print_rows(rows)
    except Exception as e:   # noqa: BLE001  (--rows must print whatever a reader sent)
        print_rows_ok = False
        errors.append(f"--rows fails on off-schema values: {e!r}")
    for bad, says in (([], "not what the workflow returns"), ({"full": [], "thumb": []}, None)):
        try:
            grade(key, bad)
            if says:
                errors.append(f"grade took {bad!r}")
        except ValueError as e:
            if not says or says not in str(e):
                errors.append(f"grade({bad!r}): {e}")
    try:
        grade({"p01": dict(word="X", face="Dragon", crown="Lynx")}, result)
        errors.append("grade took a key naming an animal NAMES has no line for")
    except ValueError as e:
        if "Dragon" not in str(e):
            errors.append(f"grade does not name the animal it cannot grade: {e}")
    here = os.getcwd()
    if key_path(".") != here + ".key.json" or os.path.dirname(key_path("x/")) != here:
        errors.append(f"key_path: the key of '.' must sit beside the folder, not in it: {key_path('.')}")
    # prep: words in any case, a stranger refused, a used folder refused, an earlier shuffle's pictures removed,
    # and a repainted bust said at grading
    import tempfile
    with tempfile.TemporaryDirectory() as scratch:
        import scene
        words = [c[0] for c in creator.catalog() if scene.avatar_source(c[0])][:3]
        if len(words) == 3:
            d = os.path.join(scratch, "read")
            k3 = prep(d, [w.swapcase() for w in words])
            if sorted(v["word"] for v in k3.values()) != sorted(words):
                errors.append(f"prep: words in another case are not found: {sorted(v['word'] for v in k3.values())}")
            k1 = prep(d, words[:1])
            left = sorted(os.listdir(os.path.join(d, "full")))
            if left != ["p01.jpg"] or list(k1) != ["p01"]:
                errors.append(f"prep: pictures of an earlier shuffle stay in the folder: {left}")
            if picture_notes(k1, d):
                errors.append(f"a fresh shuffle has nothing to note: {picture_notes(k1, d)}")
            if not any("not the ones this key was written for: p01" in n for n in picture_notes(dict(k1, p01=dict(k1["p01"], shown_md5="0")), d)) \
                    or not any(f"repainted since this read, which is of the earlier pictures: {words[0]}" in n
                               for n in picture_notes(dict(k1, p01=dict(k1["p01"], source_md5="0")), d)) \
                    or not any(f"repainted since this read, which is of the earlier pictures: {words[0]}" in n
                               for n in picture_notes(dict(k1, p01=dict(k1["p01"], picture="x")), d)):
                errors.append("a swapped picture, a repainted avatar or a changed source is not noted at grading")
            scened = [w for w in (c[0] for c in creator.catalog()) if (scene.avatar_source(w) or ("",))[0] == "scene"][:1]
            if scened:   # a pilot whose scene is painted is read from its close-up, the square the avatar is cut from
                ks = prep(os.path.join(scratch, "scened"), scened)
                box = scene.close_of(scened[0])
                from PIL import Image as _Image
                want_ = _Image.open(scene.scene_path(scened[0])).convert("RGB").crop(box).resize((640, 640), _Image.LANCZOS)
                got_ = _Image.open(os.path.join(scratch, "scened", "full", "p01.jpg")).convert("RGB")
                import numpy as _np
                if ks["p01"].get("picture") != "scene" or \
                        _np.abs(_np.asarray(want_).astype(int) - _np.asarray(got_).astype(int)).mean() > 4:
                    errors.append("a pilot with a painted scene is not read from the scene's close-up")
            for call, says in ((lambda: prep(d, ["nobody"]), "not in the catalog"),):
                try:
                    call()
                    errors.append("prep took a word that is not in the catalog")
                except ValueError as e:
                    if says not in str(e):
                        errors.append(f"prep: {e}")
            painted, closed = creator.bust_path, scene.close_of
            try:    # a named pilot with neither picture is said, not skipped
                creator.bust_path = lambda w: os.path.join(scratch, "nowhere", f"{w}.jpg")
                scene.close_of = lambda w: None
                prep(os.path.join(scratch, "read2"), words[:1])
                errors.append("prep shuffled a pilot that has no avatar picture")
            except ValueError as e:
                if "no avatar picture to read yet" not in str(e):
                    errors.append(f"prep: {e}")
            finally:
                creator.bust_path, scene.close_of = painted, closed
            with open(os.path.join(d, "result.json"), "w", encoding="utf-8") as fh:
                fh.write("{}")
            try:
                prep(d, words[:1])
                errors.append("prep shuffled into a folder that already holds a read")
            except ValueError as e:
                if "already holds a read" not in str(e):
                    errors.append(f"prep: {e}")
    # the command line refuses a folder in the repository, and one whose key would land in it
    import subprocess
    for inside in (os.path.join(creator.ROOT, "Docs", "blind_read_x"), creator.ROOT):
        for command in ("prep", "grade"):
            run = subprocess.run([sys.executable, os.path.abspath(__file__), command, inside],
                                 capture_output=True, text=True)
            if run.returncode != 2 or "not in the repository" not in run.stderr or os.path.exists(key_path(inside)):
                errors.append(f"blind.py {command} {inside}: exit {run.returncode}, not refused as a repository folder")
    print("blind.py self-test:", "FAIL" if errors else f"ok ({len(NAMES)} animals, {len(cases)} reads)")
    for e in errors:
        print("  -", e)
    return 1 if errors or not print_rows_ok else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("command", nargs="?", choices=["prep", "grade"])
    ap.add_argument("dir", nargs="?")
    ap.add_argument("words", nargs="*")
    ap.add_argument("--rows", action="store_true", help="grade: one line per portrait, worst first")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    if not args.command or not args.dir:
        ap.error("prep <dir> [WORD...] or grade <dir> [--rows]")
    # the folder as it really is (a link resolved): the key is written beside THAT, and neither may be in the repo
    where, repo = os.path.realpath(args.dir), os.path.realpath(creator.ROOT)
    for path in (where, key_path(where)):
        if os.path.commonpath([path, repo]) == repo:
            ap.error("the blind read's folder and its key belong in a scratch folder, not in the repository")
    if args.command == "prep":
        try:
            key = prep(where, args.words)
        except ValueError as e:
            ap.error(str(e))
        from_scene = sum(k.get("picture") == "scene" for k in key.values())
        print(f"{len(key)} avatars shuffled into {where} (key: {key_path(where)}): {from_scene} read from a scene's "
              f"close-up, {len(key) - from_scene} from a bust")
        return 0
    loaded = []
    for path, what in ((key_path(where), "the key (blind.py prep writes it)"),
                       (os.path.join(where, "result.json"), "the read (save what the workflow returns as result.json)")):
        try:
            with open(path, encoding="utf-8") as fh:
                loaded.append(json.load(fh))
        except FileNotFoundError:
            ap.error(f"{path} is missing: {what}")
        except json.JSONDecodeError as e:
            ap.error(f"{path} is not JSON: {e}")
    key, result = loaded
    try:
        totals, rows, problems = grade(key, result)
    except ValueError as e:
        ap.error(str(e))
    if args.rows:
        print_rows(rows)
    for k, v in totals.items():
        print(f"{k:58} {v}")
    for p in problems:
        print("WARNING:", p)
    for n in picture_notes(key, where):
        print("NOTE:", n)
    return 0


if __name__ == "__main__":
    sys.exit(main())
