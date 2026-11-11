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
    python3 Tools/Build/pilot_art/creator.py capture <word|file> <url>   # download a painting into busts/
    python3 Tools/Build/pilot_art/creator.py sheet [out.png] [words...]  # a look sheet: bust + avatar sizes
    python3 Tools/Build/pilot_art/creator.py --self-test
The painter (Figma generate_image, gpt-image-2.5-sunburst, 1024 square) and the round protocol:
.claude/skills/pilot-creator/SKILL.md.
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
    ("Longshift", "Black hare", "Badger", "Explorer"),
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
LEGACY_NAMES = {   # the spare paintings never had a name; these are rolled from PilotGenerator's tables' syllables
    "Spare01": "Korvane Ashwhistle", "Spare02": "Lunari Hollowreach", "Spare03": "Bellomo Stripewright",
    "Spare04": "Zeria Emberwick", "Spare05": "Halven Frostgate", "Spare06": "Tamsin Dusklight",
    "Spare07": "Fennaro Mossbell", "Spare08": "Oki Sandrunner",
}


def spec_of(name):
    import importlib.util
    path = os.path.join(HERE, "specs", f"{name}.py")
    s = importlib.util.spec_from_file_location(f"spec_{name}", path)
    m = importlib.util.module_from_spec(s)
    s.loader.exec_module(m)
    return m.PORTRAIT


def catalog():
    """[(word, person, (face, crown), faction, callsign or None, bust overrides)] in avatar id order."""
    out = []
    for word, spec in CALLSIGN_PILOTS.items():
        p = spec_of(spec)
        assert p["callsign"] == word, f"{spec}.py is {p['callsign']}, not {word}"
        out.append((word, p["person"], tuple(p["heritage"]), p["faction"], word, p.get("bust", {})))
    for word, a, b, faction in LEGACY:
        out.append((word, LEGACY_NAMES.get(word, word), (a, b), faction, None, {}))
    return out


def brief_of(word):
    for w, person, animals, faction, callsign, overrides in catalog():
        if w.lower() == word.lower():
            return bust.compose(animals, faction, person, callsign, **overrides)
    raise SystemExit(f"{word} is not in the catalog (creator.py list)")


def bust_path(word):
    return os.path.join(ROOT, BUSTS, f"{word}.jpg")


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


def roll(callsign, seed):
    """The person PilotGenerator.Roll(callsign, seed) mints: name, heritage, faction type and founding faction."""
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
                faction_name=fname, callsign=callsign.upper(), seed=f"{seed:016x}")


# ---------------------------------------------------------------------------------------------------------------
# Capture and the look sheet.
# ---------------------------------------------------------------------------------------------------------------
def capture(word, url):
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


def avatar_cut(im):
    """The avatar square from a bust: the whole frame less a thin margin (the bust is already framed for it)."""
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
    rows = [r for r in catalog() if os.path.exists(bust_path(r[0])) and (not words or r[0] in words)]
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


def self_test():
    errors = []
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
    for c in catalog():
        try:
            bust.compose(c[2], c[3], c[1], c[4], **c[5])
        except KeyError as e:
            errors.append(f"{c[0]}: {e}")
    words = [c[0] for c in catalog()]
    if len(words) != 50 or len(set(words)) != 50:
        errors.append(f"the catalog has {len(words)} pilots ({len(set(words))} distinct), the avatar list 50")
    import author_pilot_avatar_assets as av  # noqa
    legacy = [n for n, *_ in av.PAINTED]
    if legacy != [w for w, *_ in LEGACY]:
        errors.append("LEGACY is not in the avatar list's PAINTED order")
    print("creator.py self-test:", "FAIL" if errors else f"ok (50 pilots, roll TALON#3039 = {r1['person']}, "
          f"{'/'.join(r1['heritage'])}, {r1['faction_name'] or r1['faction']})")
    for e in errors:
        print("  -", e)
    return 1 if errors else 0


def main():
    sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", nargs="?", choices=["roll", "brief", "list", "capture", "sheet"])
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
        print(bust.compose(r["heritage"], r["faction"], r["person"], r["callsign"]))
    elif a.cmd == "brief":
        print(brief_of(a.args[0]))
    elif a.cmd == "list":
        for i, (w, person, (x, y), f, c, _) in enumerate(catalog(), 1):
            print(f"{i:2} {w:10} {person:24} {x}/{y:14} {f:15} {'bust' if os.path.exists(bust_path(w)) else '-'}")
    elif a.cmd == "capture":
        capture(a.args[0], a.args[1])
    elif a.cmd == "sheet":
        sheet(a.args[0] if a.args else os.path.join(ROOT, BUSTS, "sheet.png"), a.args[1:] or None)
    else:
        ap.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())
