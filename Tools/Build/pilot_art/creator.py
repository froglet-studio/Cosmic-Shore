#!/usr/bin/env python3
"""The PILOT CREATOR: from a pilot record (or a seed the game would roll) to a signature bust and its avatar.

Garrett, 2026-11-10: "I want to produce a character creator skill that will allow us to give players unique pilots."
A pilot is a unique person (Docs/Pilots/INVENTORY.md section 3): a name, human + two animals, a faction, a callsign.
The game mints one with PilotGenerator.Roll(callsign, seed); this file rolls the SAME person from the same seed (the
C# tables are read from the source and SeededRandom is ported bit for bit, held by --self-test), composes the bust
brief (bust.py), records each painting under Docs/Pilots/art/busts/, and lists the 50 pilots the avatar catalog shows.

    python3 Tools/Build/pilot_art/creator.py roll TALON 0x5eed...       # the person the game mints for that seed
    python3 Tools/Build/pilot_art/creator.py roll TALON                  # a fresh seed: a new unique pilot
    python3 Tools/Build/pilot_art/creator.py brief <word>                # the bust brief of a catalog pilot
    python3 Tools/Build/pilot_art/creator.py list                        # the catalog: which have a bust
    python3 Tools/Build/pilot_art/creator.py cast                        # CAST lines for catalog pilots without one
    python3 Tools/Build/pilot_art/creator.py capture <word> <url>        # download a painting to busts/<WORD>.jpg
                                                                         # and record the brief it was painted from
    python3 Tools/Build/pilot_art/creator.py stale                       # busts the tables would now paint differently
    python3 Tools/Build/pilot_art/creator.py sheet [out.png] [words...]  # a look sheet: bust + avatar sizes
    python3 Tools/Build/pilot_art/creator.py tiles <scratch dir> [words...]  # each bust whole and in four enlarged
                                                                         # quarters, for the emblem sweep's readers
    python3 Tools/Build/pilot_art/creator.py --self-test
The painter (Figma generate_image, gpt-image-2.5-sunburst, 1024 square) and the round protocol:
.claude/skills/pilot-creator/SKILL.md. Whether a stranger names both animals: blind.py.
"""
import argparse
import os
import re
import secrets
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import brief  # noqa: E402
import bust   # noqa: E402

ROOT = brief.ROOT
BUSTS = "Docs/Pilots/art/busts"
GENERATOR_CS = "Assets/_Scripts/Data/Inventory/PilotGenerator.cs"
CALLSIGNS_CS = "Assets/_Scripts/Data/Inventory/Callsigns.cs"
MASK = (1 << 64) - 1

# ---------------------------------------------------------------------------------------------------------------
# The avatar catalog (Tools/Build/author_pilot_avatar_assets.py: ids 1-18 the callsign pilots, 19-50 the painted
# legacy pilots). The callsign pilots are the people the pilot-art kit painted (specs/<name>.py); the legacy pilots
# were painted before pilots had two animals, so each was given one (cast 2026-11-11: their own animal stays the
# face, the second is the feature their full-body painting already showed where it had one, a tail, wings or a shell,
# moved to the head). word: the avatar's word; spec: the full-body spec, when there is one.
# A second animal that was given can be given again: Longshift's was the badger until Garrett read him
# (2026-11-11) as a skunk, "Longshift should have a bushy skunk tail that wraps around his body and into hit lap like
# a blanket ... otherwise he is just a rabbit man with skunk coloring". A recast pilot's bust is stale (`stale`), and
# their pictures start from the words frozen by `scene.py recast <WORD>`, not from the bust's old brief.
# ---------------------------------------------------------------------------------------------------------------
CALLSIGN_PILOTS = {   # word -> spec name (specs/<spec>.py holds the person, the heritage and the faction)
    "ENSIGN": "veliane", "MANGER": "tamako", "ANVIL": "brikon", "VOLT": "karenth", "ZODIAC": "ivelia",
    "SUNDIAL": "delmira", "OPAL": "quivera", "RAMPART": "obelo", "CITADEL": "nedira", "HORNET": "zeko",
    "IVY": "mathalo", "TALON": "wyvendo", "DOE": "fenlia", "WINGMAN": "jorin", "BANDIT": "rosh",
    "NEEDLE": "ilia", "GALLEON": "haradek", "FLUME": "ekora",
}
LEGACY = [   # (word, face animal, crown animal, faction type)
    ("Astrofoil", "Snail", "Jellyfish", "Explorer"),
    ("Bushy", "Horned lizard", "Hedgehog", "Gatherer"),
    ("Comet", "Clownfish", "Lobster", "Sport"),
    ("Damsel", "Hummingbird", "Lobster", "Activist"),
    ("Dozer", "Tree frog", "Scorpion", "Contractor"),
    ("Echo", "Mouse", "Owl", "Researcher"),
    ("Fiest", "Cheetah", "Hyena", "BountyHunter"),
    ("Flux", "Oryx", "Orca", "Sport"),
    ("Jugger", "Orca", "Eagle", "Contractor"),
    ("Krush", "Coyote", "Turkey", "Thief"),
    ("Longshift", "Black hare", "Skunk", "Explorer"),
    ("Maestro", "Platypus", "Butterfly", "Priest"),
    ("Mason", "Ostrich", "Porcupine", "Contractor"),
    ("Reaper", "Gecko", "Mantis", "Thief"),
    ("Riptide", "Zebra", "Lionfish", "Pirate"),
    ("Scales", "Egret", "Koi", "Diplomat"),
    ("Shrapnel", "Toucan", "Newt", "Activist"),
    ("Siphon", "Caracal", "Red panda", "Thief"),
    ("Slingshot", "Flamingo", "Mantis shrimp", "Sport"),
    ("Strider", "Shoebill", "Crocodile", "Hunter"),
    ("Tenacious", "Tiger", "Tortoise", "FirstResponder"),
    ("Tender", "Parrot", "Narwhal", "Preservationist"),
    ("Tomahawk", "Basilisk", "Hawk", "Hunter"),
    ("Trace", "Booby", "Jellyfish", "Researcher"),
    ("Spare01", "Mandrill", "Peacock", "Priest"),
    ("Spare02", "Opossum", "Lynx", "Hunter"),
    ("Spare03", "Zebra", "Yak", "Merchant"),
    ("Spare04", "Fox", "Firefly", "Thief"),
    ("Spare05", "Hare", "Ram", "Aristocrat"),
    ("Spare06", "Hare", "Tiger", "BountyHunter"),
    ("Spare07", "Hare", "Deer", "Preservationist"),
    ("Spare08", "Kangaroo", "Armadillo", "FirstResponder"),
]
LEGACY_NAMES = {   # the spare paintings never had a name; these were given by hand, in the generator's style
    "Spare01": "Korvane Ashwhistle", "Spare02": "Lunari Hollowreach", "Spare03": "Bellomo Stripewright",
    "Spare04": "Zeria Emberwick", "Spare05": "Halven Frostgate", "Spare06": "Tamsin Dusklight",
    "Spare07": "Fennaro Mossbell", "Spare08": "Oki Sandrunner",
}

# ---------------------------------------------------------------------------------------------------------------
# CAST: what each catalog pilot is painted with (round 2, 2026-11-11): the place it stands in, which of its faction
# type's three outfits it wears, its pose and expression, and the human tells. Dealt by propose_cast() so the fifty
# differ (fifty pilots, fifty places), with the places of the callsign pilots chosen by hand for the person, then
# FROZEN here: the busts on disk were painted from exactly these looks. Change a line on purpose and repaint that
# pilot; a new pilot gets its line from `creator.py cast`.
# Round 3 (2026-11-11, "please never make crab hands"): Comet, Damsel, Dozer, Slingshot and Reaper lost a claw, a
# pincer, clubs and forelegs. Comet now holds a shard in a lobster-shell hand with fingers, Dozer and Reaper show
# their face animal's true hand, Damsel and Slingshot show none.
# ---------------------------------------------------------------------------------------------------------------
CAST = {   # word: the look it is painted with (bust.compose"s keywords); dealt by propose_cast, then frozen
    "ENSIGN": dict(backdrop="water-garden", gear=0, pose=0, expression="shy and gentle", hair="short spiky white human hair", limb=False, hand="none"),
    "MANGER": dict(backdrop="cell-membrane", gear=1, pose=5, expression="mischievous, holding back a laugh", hair="none", limb=True, hand="hooked over the strap across the chest"),
    "ANVIL": dict(backdrop="ark-hangar", gear=0, pose=6, expression="stern and watchful", hair="none", limb=False, hand="none"),
    "VOLT": dict(backdrop="aurum-quay", gear=1, pose=8, expression="fierce and determined", hair="a sleek ink-blue human bob", limb=False, hand="none"),
    "ZODIAC": dict(backdrop="open-sea-dusk", gear=2, pose=9, expression="bright, open and curious", hair="none", limb=False, hand="none"),
    "SUNDIAL": dict(backdrop="court-windows", gear=1, pose=3, expression="calm and faintly amused", hair="none", limb=False, hand="none"),
    "OPAL": dict(backdrop="glassmere", gear=0, pose=1, expression="proud and unbowed", hair="none", limb=False, hand="none"),
    "RAMPART": dict(backdrop="storm-front", gear=1, pose=4, expression="serene, almost sleepy", hair="none", limb=False, hand="none"),
    "CITADEL": dict(backdrop="crystal-shallows", gear=2, pose=2, expression="warm and kind", hair="none", limb=False, hand="none"),
    "HORNET": dict(backdrop="reed-blind", gear=0, pose=7, expression="solemn and far-seeing", hair="none", limb=True, hand="hooked over the lapel"),
    "IVY": dict(backdrop="nine-wells", gear=0, pose=0, expression="cool and assessing", hair="none", limb=True, hand="none"),
    "TALON": dict(backdrop="dawn-mist", gear=2, pose=2, expression="delighted, on the edge of laughing", hair="none", limb=False, hand="none"),
    "DOE": dict(backdrop="hunters-lodge", gear=2, pose=3, expression="weary but steady", hair="a neat high knot of copper-red human hair", limb=False, hand="none"),
    "WINGMAN": dict(backdrop="gullbone-landing", gear=0, pose=8, expression="wry and unimpressed", hair="none", limb=False, hand="none"),
    "BANDIT": dict(backdrop="storm-deck", gear=1, pose=7, expression="calm and faintly amused", hair="none", limb=True, hand="holding a small glowing crystal shard up beside the face"),
    "NEEDLE": dict(backdrop="spindrift", gear=1, pose=1, expression="stern and watchful", hair="none", limb=False, hand="none"),
    "GALLEON": dict(backdrop="night-harbour", gear=2, pose=4, expression="shy and gentle", hair="none", limb=False, hand="holding a small glowing crystal shard up beside the face"),
    "FLUME": dict(backdrop="wreck-ring", gear=0, pose=9, expression="proud and unbowed", hair="none", limb=True, hand="curled under the chin in thought"),
    "Astrofoil": dict(backdrop="chart-room", gear=0, pose=6, expression="mischievous, holding back a laugh", hair="a thick dark human plait hanging down the back", limb=False, hand="none"),
    "Bushy": dict(backdrop="harvest-lines", gear=0, pose=5, expression="warm and kind", hair="none", limb=False, hand="none"),
    "Comet": dict(backdrop="grandstand-night", gear=0, pose=4, expression="fierce and determined", hair="none", limb=True, hand="holding a small glowing crystal shard up beside the face"),
    "Damsel": dict(backdrop="night-march", gear=0, pose=9, expression="delighted, on the edge of laughing", hair="none", limb=False, hand="none"),
    "Dozer": dict(backdrop="shipyard", gear=0, pose=6, expression="serene, almost sleepy", hair="none", limb=True, hand="holding the welding goggles at the chest"),
    "Echo": dict(backdrop="tidal-archive", gear=0, pose=5, expression="cool and assessing", hair="none", limb=False, hand="none"),
    "Fiest": dict(backdrop="warrant-wall", gear=0, pose=3, expression="weary but steady", hair="none", limb=False, hand="none"),
    "Flux": dict(backdrop="race-gates", gear=1, pose=7, expression="bright, open and curious", hair="none", limb=False, hand="none"),
    "Jugger": dict(backdrop="dry-dock", gear=1, pose=2, expression="solemn and far-seeing", hair="none", limb=False, hand="none"),
    "Krush": dict(backdrop="magpie-hollow", gear=0, pose=8, expression="wry and unimpressed", hair="none", limb=False, hand="giving a small wave"),
    "Longshift": dict(backdrop="uncharted-edge", gear=1, pose=1, expression="shy and gentle", hair="none", limb=True, hand="giving a small wave"),
    "Maestro": dict(backdrop="ninefold-shell", gear=0, pose=0, expression="cool and assessing", hair="long honey-brown human waves swept to one side", limb=False, hand="none"),
    "Mason": dict(backdrop="lattice-pens", gear=2, pose=7, expression="mischievous, holding back a laugh", hair="none", limb=False, hand="none"),
    "Reaper": dict(backdrop="kelp-alley", gear=1, pose=8, expression="serene, almost sleepy", hair="none", limb=True, hand="holding one thin lockpick up beside the face"),
    "Riptide": dict(backdrop="nucleus-sun", gear=1, pose=5, expression="stern and watchful", hair="none", limb=False, hand="resting flat over the heart"),
    "Scales": dict(backdrop="harbour-terrace", gear=0, pose=3, expression="proud and unbowed", hair="a short swept-back undercut of dark human hair", limb=False, hand="none"),
    "Shrapnel": dict(backdrop="rally-wall", gear=1, pose=9, expression="delighted, on the edge of laughing", hair="a long silver-white human braid falling over one shoulder", limb=False, hand="none"),
    "Siphon": dict(backdrop="lantern-reach", gear=2, pose=6, expression="warm and kind", hair="a wild mop of auburn human curls", limb=False, hand="none"),
    "Slingshot": dict(backdrop="ark-greenhouse", gear=2, pose=4, expression="fierce and determined", hair="none", limb=False, hand="none"),
    "Strider": dict(backdrop="tollmoor-market", gear=1, pose=1, expression="weary but steady", hair="none", limb=False, hand="none"),
    "Tenacious": dict(backdrop="rescue-bay", gear=0, pose=0, expression="calm and faintly amused", hair="long straight black human hair falling behind the shoulders", limb=False, hand="none"),
    "Tender": dict(backdrop="old-growth-reef", gear=0, pose=2, expression="wry and unimpressed", hair="none", limb=False, hand="none"),
    "Tomahawk": dict(backdrop="cinder-shoal", gear=2, pose=0, expression="bright, open and curious", hair="none", limb=False, hand="none"),
    "Trace": dict(backdrop="lumen-lab", gear=1, pose=5, expression="solemn and far-seeing", hair="loose copper-red human curls spilling out at the nape", limb=False, hand="none"),
    "Spare01": dict(backdrop="deepreed-shrine", gear=1, pose=8, expression="delighted, on the edge of laughing", hair="none", limb=False, hand="none"),
    "Spare02": dict(backdrop="ark-bridge", gear=0, pose=2, expression="proud and unbowed", hair="none", limb=False, hand="none"),
    "Spare03": dict(backdrop="counting-house", gear=2, pose=1, expression="stern and watchful", hair="none", limb=False, hand="none"),
    "Spare04": dict(backdrop="derelict-ark", gear=0, pose=3, expression="calm and faintly amused", hair="none", limb=False, hand="none"),
    "Spare05": dict(backdrop="treaty-hall", gear=2, pose=7, expression="solemn and far-seeing", hair="none", limb=False, hand="none"),
    "Spare06": dict(backdrop="rooftop-stakeout", gear=1, pose=9, expression="mischievous, holding back a laugh", hair="none", limb=False, hand="none"),
    "Spare07": dict(backdrop="bloom-garden", gear=1, pose=6, expression="weary but steady", hair="none", limb=False, hand="none"),
    "Spare08": dict(backdrop="storm-rescue", gear=1, pose=4, expression="shy and gentle", hair="none", limb=True, hand="resting flat over the heart"),
}


def spec_of(name):
    import importlib.util
    path = os.path.join(HERE, "specs", f"{name}.py")
    s = importlib.util.spec_from_file_location(f"spec_{name}", path)
    m = importlib.util.module_from_spec(s)
    s.loader.exec_module(m)
    return m.PORTRAIT


def catalog():
    """[(word, person, (face, crown), faction, callsign or None, the look it is painted with)] in avatar id order."""
    out = []
    for word, spec in CALLSIGN_PILOTS.items():
        p = spec_of(spec)
        assert p["callsign"] == word, f"{spec}.py is {p['callsign']}, not {word}"
        out.append((word, p["person"], tuple(p["heritage"]), p["faction"], word, dict(CAST.get(word, {}))))
    for word, a, b, faction in LEGACY:
        out.append((word, LEGACY_NAMES.get(word, word), (a, b), faction, None, dict(CAST.get(word, {}))))
    return out


def propose_cast(wish=None):
    """A look for every catalog pilot, spread so the set varies: no backdrop twice while another is free (a faction
    type's home grounds go to its own pilots first), the three outfits of a faction type dealt in turn, poses,
    expressions and hair styles dealt evenly, and the human tells as bust.roll_tells rolls them. wish: the parts of a
    pilot's look that are chosen by hand ({word: {backdrop: ..., gear: ...}}); the rest is dealt around them.
    The result is frozen in CAST: a pilot is repainted only when its look is changed on purpose."""
    import collections
    import hashlib
    wish = wish or {}
    used, values, poses, moods, outfits, hairs = (collections.Counter() for _ in range(6))
    for look in wish.values():
        if "backdrop" in look:
            used[look["backdrop"]] += 1
            values[bust.BACKDROPS[look["backdrop"]][0]] += 1
        if look.get("hair", "none") != "none":   # a style a wished pilot wears is taken, like its place
            hairs[look["hair"]] += 1
        if look.get("pose") is not None:         # and so are its pose and its mood, wherever it sits in the catalog
            poses[look["pose"]] += 1
        if look.get("expression"):
            moods[look["expression"]] += 1
    out = {}

    def h(*parts):
        return int(hashlib.md5("/".join(str(x) for x in parts).encode("utf-8")).hexdigest(), 16)

    for word, person, (a, b), faction, callsign, _ in catalog():
        w = wish.get(word, {})
        backdrop = w.get("backdrop")
        if not backdrop:
            home, anyone = bust.backdrops_for(faction)
            pool = [k for k in home if not used[k]] or anyone
            backdrop = min(pool, key=lambda k: (used[k], k not in bust.UNEARTHLY, values[bust.BACKDROPS[k][0]],
                                                h(person, k)))   # a place that could not be on Earth first
            used[backdrop] += 1
            values[bust.BACKDROPS[backdrop][0]] += 1
        gear = w.get("gear", outfits[faction] % 3)
        outfits[faction] += 1
        pose = w.get("pose")
        if pose is None:
            pose = min(range(len(bust.POSES)), key=lambda i: (poses[i], h(person, "pose", i)))
            poses[pose] += 1
        mood = w.get("expression")
        if not mood:
            mood = min(bust.EXPRESSIONS, key=lambda m: (moods[m], h(person, m)))
            moods[mood] += 1
        look = dict(backdrop=backdrop, gear=gear, pose=pose, expression=mood, **bust.roll_tells(a, b, person))
        if not w:    # a NEW pilot may show more of the human side; a pilot with a wished look keeps exactly that look
            more = bust.roll_more(a, b, person)
            if more["touch"] != "none":
                look["touch"] = more["touch"]
            if not more["glove"]:
                look["glove"] = False
        if look["hair"] != "none" and "hair" not in w:   # deal the hair styles too
            cap = bust.crown_of(a, b)[1]
            look["hair"] = min(bust.HAIRS_LOW if cap else bust.HAIRS, key=lambda x: (hairs[x], h(person, x)))
            hairs[look["hair"]] += 1
        for k in ("hair", "limb", "hand") + LOOK_MORE:
            if k in w:
                look[k] = w[k]
        if look["hair"] != "none" and not look["limb"]:
            look["hand"] = "none"            # the rule: hair is enough
        bust.compose((a, b), faction, person, callsign, **look)   # a look that breaks a rule raises here
        out[word] = look
    return out


LOOK_KEYS = ("backdrop", "gear", "pose", "expression", "hair", "limb", "hand")   # every frozen look carries these
LOOK_MORE = ("extra", "iris", "touch", "glove")                                   # and may carry these


def cast_errors(cast=None):
    """What is wrong with CAST (or the cast given), as a list of sentences: every catalog pilot has a line and no
    line is left over, no place is used twice, each place is one that pilot's faction type stands in
    (bust.backdrops_for), a look carries every key of LOOK_KEYS and no unknown one, the outfit, pose, mood and hair
    style are on their lists (under a crown that covers the scalp the hair is a HAIRS_LOW style), text written by
    hand (what the hand does, extra, iris) names nothing of the game's world and summons no emblem
    (bust.world_name_in, bust.emblem_word), and the look composes (bust.compose raises for a broken human tell)."""
    cast = CAST if cast is None else cast
    errors = []
    rows = catalog()
    words = [r[0] for r in rows]
    for w in sorted(set(cast) ^ set(words)):
        errors.append(f"{w}: " + ("in CAST but not in the catalog" if w in cast else "no line in CAST"))
    places = [look.get("backdrop") for look in cast.values()]
    for k in sorted({k for k in places if places.count(k) > 1}, key=str):
        errors.append(f"{k}: {places.count(k)} pilots stand there "
                      f"({', '.join(w for w, look in cast.items() if look.get('backdrop') == k)})")
    for word, person, animals, faction, callsign, _ in rows:
        look = cast.get(word)
        if look is None:
            continue    # said above: no line in CAST
        missing = [k for k in LOOK_KEYS if k not in look]
        unknown = [k for k in look if k not in LOOK_KEYS + LOOK_MORE]
        if missing:     # a look with a key left out would be painted from a roll that nothing froze
            errors.append(f"{word}: the look lacks {', '.join(missing)} (a frozen look carries {', '.join(LOOK_KEYS)})")
        if unknown:
            errors.append(f"{word}: unknown key {', '.join(repr(k) for k in unknown)} "
                          f"(a look has {', '.join(LOOK_KEYS + LOOK_MORE)})")
        if missing or unknown:
            continue
        for k in ("hair", "hand") + LOOK_MORE:
            text = look.get(k)
            if not isinstance(text, str) or text == "none":
                continue
            if bust.world_name_in(text):
                errors.append(f"{word}: its {k} names {bust.world_name_in(text)!r}: the brief goes to an outside "
                              f"painter and names nothing of the game or its world")
            if bust.emblem_word(text):
                errors.append(f"{word}: its {k} says {bust.emblem_word(text)!r}, which is painted as an emblem or as "
                              f"lettering")
        if look.get("backdrop") not in bust.BACKDROPS:
            errors.append(f"{word}: no BACKDROPS entry {look.get('backdrop')!r}")
            continue
        home, anyone = bust.backdrops_for(faction)
        if look["backdrop"] not in home and look["backdrop"] not in anyone:
            errors.append(f"{word}: {look['backdrop']} is not a place a {faction} stands in (bust.backdrops_for)")
        if type(look["gear"]) is not int or look["gear"] not in range(len(bust.GEAR[faction])):
            errors.append(f"{word}: gear {look['gear']!r} is not one of {faction}'s {len(bust.GEAR[faction])} outfits")
        if type(look["pose"]) is not int or look["pose"] not in range(len(bust.POSES)):
            errors.append(f"{word}: pose {look['pose']!r} is not an index in bust.POSES")
        if look["expression"] not in bust.EXPRESSIONS:
            errors.append(f"{word}: {look['expression']!r} is not in bust.EXPRESSIONS")
        if look.get("touch", "none") not in ["none"] + bust.TOUCHES + bust.TOUCHES_FUR:
            errors.append(f"{word}: {look['touch']!r} is not a human touch on bust.TOUCHES or bust.TOUCHES_FUR")
        if type(look.get("glove", True)) is not bool:
            errors.append(f"{word}: glove {look['glove']!r} is not True or False")
        covered = bust.crown_of(*animals)[1]    # a frozen look wears a listed hair style, of the list its crown allows
        if look["hair"] != "none" and look["hair"] not in (bust.HAIRS_LOW if covered else bust.HAIRS + bust.HAIRS_LOW):
            errors.append(f"{word}: {look['hair']!r} is not a hair style " + (
                f"for a crown that covers the scalp, where hair shows only below it (bust.HAIRS_LOW)" if covered
                else "on bust.HAIRS or bust.HAIRS_LOW"))
        try:
            bust.compose(animals, faction, person, callsign, **look)
        except (KeyError, ValueError, TypeError) as e:
            errors.append(f"{word}: {e.args[0] if e.args else e}")
    return errors


def brief_of(word):
    for w, person, animals, faction, callsign, look in catalog():
        if w.lower() == word.lower():
            return bust.compose(animals, faction, person, callsign, **look)
    raise SystemExit(f"{word} is not in the catalog (creator.py list)")


def bust_path(word):
    return os.path.join(ROOT, BUSTS, f"{word}.jpg")


def painted_brief_path(word):
    """The brief a bust was painted from, kept beside it: the tables move on, the painting does not."""
    return os.path.join(ROOT, BUSTS, "briefs", f"{word}.txt")


def stale():
    """[(word, why)] for the catalog busts that the tables would now paint differently, or that have no record."""
    out = []
    for word, *_ in catalog():
        if not os.path.exists(bust_path(word)):
            out.append((word, "no bust"))
        elif not os.path.exists(painted_brief_path(word)):
            out.append((word, "no brief on record"))
        elif open(painted_brief_path(word), encoding="utf-8").read().strip() != brief_of(word).strip():
            out.append((word, "painted from an older brief"))
    return out


# ---------------------------------------------------------------------------------------------------------------
# The game's roll, ported (PilotGenerator.Roll, SeededRandom): the same seed paints the same person.
# ---------------------------------------------------------------------------------------------------------------
class SeededRandom:
    def __init__(self, seed):
        self.state = seed & MASK

    @staticmethod
    def hash(text):
        h = 0xCBF29CE484222325
        for ch in text:
            h ^= ord(ch)
            h = (h * 0x100000001B3) & MASK
        return h

    @classmethod
    def for_(cls, seed, purpose):
        return cls(seed ^ cls.hash(purpose))

    def next_ulong(self):
        self.state = (self.state + 0x9E3779B97F4A7C15) & MASK
        z = self.state
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & MASK
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & MASK
        return z ^ (z >> 31)

    def next(self, count):
        return self.next_ulong() % count

    def pick(self, seq):
        return seq[self.next(len(seq))]


def cs_array(text, name):
    m = re.search(r"static readonly string\[\] %s\s*=\s*\{(.*?)\};" % name, text, re.S)
    return re.findall(r'"((?:[^"\\]|\\.)*)"', m.group(1))


def faction_tables():
    text = open(os.path.join(ROOT, brief.FACTION_CATALOG_CS), encoding="utf-8").read()
    rec = re.search(r"Recruitable\s*=\s*\{(.*?)\};", text, re.S).group(1)
    recruitable = re.findall(r"FactionType\.(\w+)", rec)
    founding = {t: re.findall(r'"([^"]+)"', body)
                for t, body in re.findall(r"\[FactionType\.(\w+)\]\s*=\s*new\[\]\s*\{(.*?)\}", text, re.S)}
    return recruitable, founding


def cs_table(text, name, enum):
    """{member: line} of the generator's `Dictionary<enum, string> name` (TypeLines, SightLines)."""
    m = re.search(r"\b%s\s*=\s*new\(\)\s*\{(.*?)\n\s*\};" % name, text, re.S)
    return dict(re.findall(r'\[%s\.(\w+)\]\s*=\s*"((?:[^"\\]|\\.)*)"' % enum, m.group(1)))


def faction_in_sentence(name):
    """PilotGenerator.FactionInSentence: a faction's name mid-sentence."""
    if not name:
        return "a crew with no name"
    if name.startswith("The "):
        return "the " + name[4:]
    return name if name.startswith("House ") else "the " + name


STORY_PARTS = ("home", "upbringing", "heritage_line", "joining", "quirk", "want", "backstory")


def story(text, seed, given, family, heritage, ftype, fname, callsign):
    """The story PilotGenerator.Roll gives a seed, from its one "pilot.story" stream in the generator's own order:
    the homeworld (in Roll), then Backstory's five picks (Upbringings, HeritageLines, Joinings, Quirks, Wants), and
    the paragraph Backstory builds from them. This is what a scene's `source` quotes (scene.py): the upbringing is
    the pilot's history, the quirk a hobby, the want what they want most. It names the world, so it is for whoever
    writes the record and never goes into a brief."""
    s = SeededRandom.for_(seed, "pilot.story")
    home = s.pick(cs_array(text, "Homeworlds"))
    upbringing = s.pick(cs_array(text, "Upbringings"))
    one, two = (h.lower() for h in heritage)        # Species.Name(...).ToLowerInvariant()
    heritage_line = s.pick(cs_array(text, "HeritageLines")).replace("{one}", one).replace("{two}", two)
    joining = (s.pick(cs_array(text, "Joinings")).replace("{faction}", faction_in_sentence(fname))
               .replace("{they}", "they"))
    quirk = s.pick(cs_array(text, "Quirks"))
    want = s.pick(cs_array(text, "Wants"))
    type_line = cs_table(text, "TypeLines", "FactionType").get(ftype, "")
    sight = {k.upper(): v for k, v in cs_table(text, "SightLines", "PilotVision").items()}.get(callsign.upper(), "")
    back = (f"{given} {family} {upbringing} on {home}. Part human, part {one}, part {two}, {given} {heritage_line}. "
            f"{given} {joining}. "
            + (f"Its members are {type_line}. " if type_line else "")
            + (sight.replace("{name}", given) + ". " if sight else "")
            + f"{quirk} {want}")
    back = "".join(c for c in back if " " <= c <= "~")     # Printable
    return dict(home=home, upbringing=upbringing, heritage_line=heritage_line, joining=joining, quirk=quirk,
                want=want, backstory=back)


def scene_sources(r):
    """The four lines a scene record's `source` may start from, for a rolled pilot (scene.SOURCES)."""
    hobby = re.sub(r"^They ", "", r["quirk"]).rstrip(".")
    want = re.sub(r"^What they want most is ", "", r["want"]).rstrip(".")
    return (f"profession: {brief.faction_words(r['faction'])}",
            f"history: \"{r['upbringing']}\" (the game's Upbringings)",
            f"hobby: \"{hobby}\" (the game's Quirks; put it in the third person)",
            f"want: \"{want}\" (the game's Wants)")


def roll(callsign, seed):
    """The person PilotGenerator.Roll(callsign, seed) mints: name, heritage, faction type and founding faction, and
    the story (homeworld, upbringing, heritage line, joining, quirk, want, and the backstory built from them)."""
    text = open(os.path.join(ROOT, GENERATOR_CS), encoding="utf-8").read()
    seed = seed or 1
    names = SeededRandom.for_(seed, "pilot.name")
    given = names.pick(cs_array(text, "GivenStarts")) + names.pick(cs_array(text, "GivenMiddles")) + \
        names.pick(cs_array(text, "GivenEnds"))
    given = given[:1].upper() + given[1:].lower()
    family = names.pick(cs_array(text, "FamilyFronts")) + names.pick(cs_array(text, "FamilyBacks"))
    pool = [a for a in brief.enum_members(brief.ANIMAL_SPECIES_CS, "AnimalSpecies") if a != "None"]
    her = SeededRandom.for_(seed, "pilot.species")
    first = her.next(len(pool))
    second = her.next(len(pool) - 1)
    if second >= first:
        second += 1
    recruitable, founding = faction_tables()
    alleg = SeededRandom.for_(seed, "pilot.faction")
    ftype = alleg.pick(recruitable)
    fnames = founding.get(ftype, [])
    fname = fnames[alleg.next(len(fnames))] if fnames else ""
    return dict(person=f"{given} {family}", heritage=(pool[first], pool[second]), faction=ftype,
                faction_name=fname, callsign=callsign.upper(), seed=f"{seed:016x}",
                **story(text, seed, given, family, (pool[first], pool[second]), ftype, fname, callsign))


# ---------------------------------------------------------------------------------------------------------------
# Capture and the look sheet.
# ---------------------------------------------------------------------------------------------------------------
def capture(word, url):
    known = [w for w, *_ in catalog() if w.lower() == word.lower()]
    if not known:   # a painting is filed under a pilot: a typo would leave a stray picture with no brief
        raise SystemExit(f"{word} is not in the catalog (creator.py list)")
    word = known[0]   # the catalog's own spelling: one TALON.jpg
    out = bust_path(word)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    raw = out + ".download.png"
    subprocess.run(["curl", "-sS", "-f", "-L", "-o", raw, url], check=True)
    from PIL import Image
    im = Image.open(raw).convert("RGB")
    os.remove(raw)
    assert im.size[0] == im.size[1] and im.size[0] >= 1024, f"{url} is {im.size}, expected a 1024 square"
    im.save(out, quality=93, subsampling=0, optimize=True)   # a 1024 painting: ~0.3 MB as JPEG, 1.6 MB as PNG
    print(f"captured {out} {im.size}")
    os.makedirs(os.path.dirname(painted_brief_path(word)), exist_ok=True)   # the brief it was painted from, beside it
    with open(painted_brief_path(word), "w", encoding="utf-8") as f:
        f.write(brief_of(word) + "\n")


def avatar_cut(im):
    """The avatar square from a bust, which is already framed for it: the top-centre square, 4% off each side and
    8% off the bottom, the top kept (what grows from the head is up there)."""
    w, h = im.size
    m = int(w * 0.04)
    return im.crop((m, 0, w - m, h - 2 * m))


def octagon(im, size, plate=(34, 38, 69)):
    from PIL import Image, ImageDraw
    im = im.resize((size, size), Image.LANCZOS)
    mask = Image.new("L", (size, size), 0)
    c = size * 0.29
    ImageDraw.Draw(mask).polygon([(c, 0), (size - c, 0), (size, c), (size, size - c), (size - c, size), (c, size),
                                  (0, size - c), (0, c)], fill=255)
    out = Image.new("RGB", (size, size), plate)
    out.paste(im, (0, 0), mask)
    return out


def sheet(out, words=None):
    from PIL import Image, ImageDraw, ImageFont
    want = {w.lower() for w in words or ()}
    strangers = sorted(want - {r[0].lower() for r in catalog()})
    if strangers:
        raise SystemExit(f"not in the catalog (creator.py list): {', '.join(strangers)}")
    rows = [r for r in catalog() if os.path.exists(bust_path(r[0])) and (not want or r[0].lower() in want)]
    if not rows:
        raise SystemExit("none of these pilots has a bust yet: nothing to put on a sheet")
    try:
        f1 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 15)
        f2 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 12)
    except OSError:
        f1 = f2 = ImageFont.load_default()
    cols, cw, ch = 4, 470, 330
    img = Image.new("RGB", (cols * cw + 20, ((len(rows) + cols - 1) // cols) * ch + 20), (14, 16, 32))
    d = ImageDraw.Draw(img)
    for i, (word, person, (a, b), faction, callsign, _) in enumerate(rows):
        x, y = 20 + (i % cols) * cw, 20 + (i // cols) * ch
        src = Image.open(bust_path(word)).convert("RGB")
        img.paste(src.resize((256, 256), Image.LANCZOS), (x, y + 44))
        d.text((x, y), f"{person}" + (f"  ({callsign})" if callsign else f"  [{word}]"), fill=(255, 255, 255), font=f1)
        d.text((x, y + 20), f"Human / {a} / {b} - {brief.faction_words(faction)} ({brief.DOMAIN_OF[faction]})",
               fill=(170, 180, 220), font=f2)
        av = avatar_cut(src)
        yy = y + 44
        for s in (128, 64, 44):
            img.paste(octagon(av, s), (x + 270, yy))
            yy += s + 10
    img.save(out)
    print(f"sheet {out}: {len(rows)} pilots")


TILE_BOXES = ((0, 0, 576, 576), (448, 0, 1024, 576), (0, 448, 576, 1024), (448, 448, 1024, 1024))


def tiles(out, words=None):
    """Each bust as five pictures for the emblem sweep's readers (.claude/skills/pilot-creator/references/
    emblem_sweep.js): <WORD>_0.jpg the whole frame at 640 px, and <WORD>_1.jpg to _4.jpg its four overlapping
    quarters enlarged to 1024 px, so that a pin on a lapel or a sign on a far banner is large enough to read.
    out is a scratch folder, never one in the repository. Returns the words it wrote."""
    from PIL import Image
    out, repo = os.path.realpath(out), os.path.realpath(ROOT)
    if os.path.commonpath([out, repo]) == repo:
        raise SystemExit(f"{out} is in the repository: the tiles belong in a scratch folder")
    want = {w.lower() for w in words or ()}
    strangers = sorted(want - {r[0].lower() for r in catalog()})
    if strangers:
        raise SystemExit(f"not in the catalog (creator.py list): {', '.join(strangers)}")
    rows = [r[0] for r in catalog() if os.path.exists(bust_path(r[0])) and (not want or r[0].lower() in want)]
    if not rows:
        raise SystemExit("none of these pilots has a bust yet: nothing to cut")
    os.makedirs(out, exist_ok=True)
    for word in rows:
        im = Image.open(bust_path(word)).convert("RGB").resize((1024, 1024), Image.LANCZOS)
        im.resize((640, 640), Image.LANCZOS).save(os.path.join(out, f"{word}_0.jpg"), quality=90)
        for n, box in enumerate(TILE_BOXES, 1):
            im.crop(box).resize((1024, 1024), Image.LANCZOS).save(os.path.join(out, f"{word}_{n}.jpg"), quality=92)
    print(f"tiles {out}: {len(rows)} busts, five pictures each")
    return rows


def self_test():
    errors = []
    # the sweep's four tiles cover the whole frame, and neighbours overlap so a sign on a seam is whole in one
    covered = [[False] * 1024 for _ in range(1024)]
    for x0, y0, x1, y1 in TILE_BOXES:
        for y in range(y0, y1):
            covered[y][x0:x1] = [True] * (x1 - x0)
    if not all(all(row) for row in covered) or TILE_BOXES[0][2] - TILE_BOXES[1][0] < 96:
        errors.append("TILE_BOXES leaves part of the frame out, or its quarters overlap by less than 96 px")
    # SeededRandom: FNV-1a 64 of "" is the offset basis, and splitmix64 from state 0 is a known value.
    if SeededRandom.hash("") != 0xCBF29CE484222325:
        errors.append("FNV basis")
    if SeededRandom.hash("a") != 0xAF63DC4C8601EC8C:
        errors.append("FNV-1a of 'a'")
    if SeededRandom(0).next_ulong() != 0xE220A8397B1DCDAF:
        errors.append("splitmix64 first value")
    r1, r2 = roll("TALON", 12345), roll("TALON", 12345)
    if r1 != r2:
        errors.append("roll is not deterministic")
    if r1["heritage"][0] == r1["heritage"][1]:
        errors.append("roll gave the same animal twice")
    # The story: every table the generator picks from is read, every part is rolled, the paragraph ends on the
    # want with no token left in it, and another seed tells another story.
    gen = open(os.path.join(ROOT, GENERATOR_CS), encoding="utf-8").read()
    for table in ("Homeworlds", "Upbringings", "HeritageLines", "Joinings", "Quirks", "Wants"):
        if len(cs_array(gen, table)) < 6:
            errors.append(f"PilotGenerator.{table} was not read (fewer than 6 lines)")
    for table, enum in (("TypeLines", "FactionType"), ("SightLines", "PilotVision")):
        if len(cs_table(gen, table, enum)) < 6:
            errors.append(f"PilotGenerator.{table} was not read (fewer than 6 lines)")
    missing = [k for k in STORY_PARTS if not r1.get(k)]
    if missing:
        errors.append(f"roll gives no {', '.join(missing)}")
    elif "{" in r1["backstory"] or not r1["backstory"].endswith(r1["want"]) or r1["home"] not in r1["backstory"]:
        errors.append("the backstory keeps a token, or does not hold the homeworld and end on the want")
    elif roll("TALON", 12346)["backstory"] == r1["backstory"]:
        errors.append("two seeds told the same story")
    elif "TALON" not in r1["backstory"] or len(scene_sources(r1)) != 4:
        errors.append("the backstory has no sight line for the callsign, or the scene sources are not four")
    if faction_in_sentence("The Black Tide") != "the Black Tide" or faction_in_sentence("House Verdance") != \
            "House Verdance" or faction_in_sentence("Red Reef Rovers") != "the Red Reef Rovers" or \
            faction_in_sentence("") != "a crew with no name":
        errors.append("faction_in_sentence departs from PilotGenerator.FactionInSentence")
    errors += cast_errors()
    words = [c[0] for c in catalog()]
    if len(set(words)) != len(words):
        errors.append(f"the catalog names a pilot twice: {sorted({w for w in words if words.count(w) > 1})}")
    # the checks must fire: each of these looks breaks one rule of the cast, and the check must say which
    faction = next(r[3] for r in catalog() if r[0] == "VOLT")
    domain = brief.DOMAIN_OF[faction]
    avoids = next(k for k, v in bust.BACKDROPS.items() if domain in v[1] and not v[2])
    someone_elses = next(k for k, v in bust.BACKDROPS.items() if domain not in v[1] and v[2] and faction not in v[2])
    for word, change, says in (("VOLT", dict(backdrop=CAST["ENSIGN"]["backdrop"]), "2 pilots stand there"),
                               ("VOLT", dict(backdrop=avoids), f"VOLT: {avoids} is not a place"),
                               ("VOLT", dict(backdrop=someone_elses), f"VOLT: {someone_elses} is not a place"),
                               ("VOLT", dict(backdrop="nowhere"), "no BACKDROPS entry"),
                               ("ENSIGN", dict(gear=9), "gear 9"),
                               ("ENSIGN", dict(pose=len(bust.POSES)), "is not an index in bust.POSES"),
                               ("ENSIGN", dict(expression="smug"), "is not in bust.EXPRESSIONS"),
                               ("IVY", dict(limb=False), "always in the frame"),
                               ("ENSIGN", dict(touch="wears a top hat"), "ENSIGN: 'wears a top hat' is not a human touch"),
                               ("ENSIGN", dict(glove="no"), "ENSIGN: glove 'no' is not True or False"),
                               ("ANVIL", dict(hair=bust.HAIRS[0]), "a mane of its own"),
                               ("Krush", dict(hair=bust.HAIRS[0]), "the hair is enough"),
                               ("TALON", dict(hairr="none"), "TALON: unknown key 'hairr'"),
                               ("TALON", dict(hair=bust.HAIRS[9]), "TALON: 'short spiky white human hair' is not a hair "
                                                                    "style for a crown that covers the scalp"),
                               ("ENSIGN", dict(hair="a mohawk"), "ENSIGN: 'a mohawk' is not a hair style on bust.HAIRS"),
                               ("Krush", dict(hand=""), "the hand is '': give its text, or 'none'"),
                               ("ENSIGN", dict(gear=True), "ENSIGN: gear True is not one of"),
                               ("TALON", {}, "TALON: the look lacks backdrop, gear, pose, expression, hair, limb, hand"),
                               ("MANGER", dict(hand=f"holding a pennant of the {bust.world_names()[0]}"), "MANGER: its hand names"),
                               ("MANGER", dict(hand="raised in a fist"), "MANGER: its hand says 'fist'"),
                               ("Krush", dict(extra="A red armband on one sleeve"), "Krush: its extra says 'armband'"),
                               ("Spare08", None, "Spare08: no line in CAST")):
        cast = {w: dict(look) for w, look in CAST.items()}
        if change is None:
            cast["Spare8"] = cast.pop(word)
        elif not change:
            cast[word] = {}
        else:
            cast[word].update(change)
        if not any(says in e for e in cast_errors(cast)):
            errors.append(f"the cast check did not say {says!r} for {word} with {change}")
    # a painting is filed under a catalog pilot and a sheet knows its words (ROOT is a scratch folder meanwhile)
    import collections
    import tempfile
    real_root = ROOT
    with tempfile.TemporaryDirectory() as scratch:
        globals()["ROOT"] = scratch
        try:
            for what, call in (("capture nobody", lambda: capture("nobody", "file:///dev/null")),
                               ("capture ../../escape", lambda: capture("../../escape", "file:///dev/null")),
                               ("sheet nobody", lambda: sheet(os.path.join(scratch, "sheet.png"), ["TALON", "nobody"])),
                               ("tiles nobody", lambda: tiles(os.path.join(real_root, "..", "tiles"), ["nobody"]))):
                try:
                    call()
                    errors.append(f"{what}: a word that is not in the catalog was taken")
                except SystemExit as e:
                    if "not in the catalog" not in str(e):
                        errors.append(f"{what}: refused without saying the word is not in the catalog: {e}")
            try:    # ROOT is the scratch folder here, so this is "a folder in the repository"
                tiles(os.path.join(scratch, "tiles"), ["TALON"])
                errors.append("tiles wrote into a folder in the repository")
            except SystemExit as e:
                if "in the repository" not in str(e):
                    errors.append(f"tiles refused a folder in the repository without saying so: {e}")
            if os.listdir(scratch):
                errors.append(f"a refused capture, sheet or tiles left files behind: {os.listdir(scratch)}")
        finally:
            globals()["ROOT"] = real_root
    # a new pilot is dealt around every frozen look, wherever it sits in the catalog (the avatar list's order)
    poses = collections.Counter(look["pose"] for look in CAST.values())
    moods = collections.Counter(look["expression"] for look in CAST.values())
    for at in (0, len(LEGACY)):
        LEGACY.insert(at, ("Zed", "Owl", "Lynx", "Merchant"))
        try:
            dealt = propose_cast(CAST)
        finally:
            del LEGACY[at]
        look = dealt["Zed"]
        if any(dealt[w] != CAST[w] for w in CAST):
            errors.append("dealing a new pilot changed a frozen look")
        if poses[look["pose"]] != min(poses[i] for i in range(len(bust.POSES))) \
                or moods[look["expression"]] != min(moods[m] for m in bust.EXPRESSIONS):
            errors.append(f"a new pilot at place {at} of LEGACY was not dealt a least-used pose and mood: {look}")
    import author_pilot_avatar_assets as av  # noqa
    # the catalog IS the avatar list, pilot for pilot and in its order: an avatar id is a saved profile's face
    if [w for _, w, _ in av.CALLSIGNS] != list(CALLSIGN_PILOTS):
        errors.append("CALLSIGN_PILOTS is not the callsign list in its order (author_callsign_assets.CALLSIGNS)")
    legacy = [n for n, *_ in av.PAINTED]
    if legacy != [w for w, *_ in LEGACY]:
        errors.append("LEGACY is not the avatar list's PAINTED, row for row: a new pilot is appended to both")
    print("creator.py self-test:", "FAIL" if errors else f"ok ({len(words)} pilots, roll TALON#3039 = "
          f"{r1['person']}, {'/'.join(r1['heritage'])}, {r1['faction_name'] or r1['faction']})")
    for e in errors:
        print("  -", e)
    return 1 if errors else 0


def main():
    sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", nargs="?", choices=["roll", "brief", "list", "cast", "capture", "stale", "sheet", "tiles"])
    ap.add_argument("args", nargs="*")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    if a.cmd == "roll":
        callsign = a.args[0]
        seed = int(a.args[1], 16) if len(a.args) > 1 else int.from_bytes(secrets.token_bytes(8), "little") or 1
        r = roll(callsign, seed)
        print(f"{r['person']}  {r['callsign']}  Human / {r['heritage'][0]} / {r['heritage'][1]}  "
              f"{brief.faction_words(r['faction'])} of {r['faction_name']}  seed {r['seed']}\n")
        print(f"Their story, as the game tells it (for the scene's writer; never sent to a painter):\n"
              f"{r['backstory']}\n")
        print("A scene's source starts from one of:\n  " + "\n  ".join(scene_sources(r)) + "\n")
        more = bust.roll_more(*r["heritage"], r["person"])     # a new pilot may show more of the human side
        print(bust.compose(r["heritage"], r["faction"], r["person"], r["callsign"], **more))
    elif a.cmd == "brief":
        print(brief_of(a.args[0]))
    elif a.cmd == "list":
        for i, (w, person, (x, y), f, c, look) in enumerate(catalog(), 1):
            print(f"{i:2} {w:10} {person:22} {x + '/' + y:24} {f:15} {look.get('backdrop', '-'):18} "
                  f"{'bust' if os.path.exists(bust_path(w)) else '-'}")
    elif a.cmd == "cast":
        dealt = propose_cast(CAST)
        for word, look in dealt.items():
            if word not in CAST or a.args:
                print(f"    {word!r}: dict(" + ", ".join(f"{k}={v!r}" for k, v in look.items()) + "),")
                taken = [w for w, other in dealt.items() if w != word and other["backdrop"] == look["backdrop"]]
                if taken:   # no two catalog pilots share a place, and --self-test holds it
                    print(f"    # {word}: every place this pilot may stand in is taken ({look['backdrop']} is "
                          f"{taken[0]}'s). Add a place to bust.BACKDROPS and run cast again.")
    elif a.cmd == "capture":
        capture(a.args[0], a.args[1])
    elif a.cmd == "stale":
        rows = stale()
        for word, why in rows:
            print(f"{word:10} {why}")
        print(f"{len(rows)} of {len(catalog())} busts differ from what the tables would paint today")
    elif a.cmd == "sheet":
        sheet(a.args[0] if a.args else os.path.join(ROOT, BUSTS, "sheet.png"), a.args[1:] or None)
    elif a.cmd == "tiles":
        if not a.args:
            ap.error("tiles <scratch dir> [WORD...]")
        tiles(a.args[0], a.args[1:] or None)
    else:
        ap.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())
