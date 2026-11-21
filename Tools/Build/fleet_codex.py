#!/usr/bin/env python3
"""
The Fleet Codex: every vessel's abilities, element scaling, level-5 upgrades, controls, flight numbers, game
modes, docs and recent changes, read from the SHIPPED assets and code, written as the data the Fleet Codex
artifact draws (Docs/FleetCodex/README.md).

    python3 Tools/Build/fleet_codex.py [--out DIR] [--no-images] [--files-json] [--branch-label L] [--pending BRANCH ...]

The /fleet-codex skill (.claude/skills/fleet-codex/SKILL.md) publishes and refreshes the artifact from it.

A READER: it writes only into --out (default Docs/FleetCodex/build/, git-ignored) and never touches an asset.
Nothing here is typed by hand, so the page cannot drift from the game: abilities, inputs, prose, L5 labels and
their gates, and the scaling numbers come from element_ability_table.py (the maps, the prefabs and the code they
reach); names, descriptions and crews from the class assets (SO_Class_*); flight numbers from the prefabs
(regatta_balance.read_vessel); cards from the arcade cards and their roster lists (vessel_mode_chart.cards);
the one-line note and the toybox roster from VesselClassType.cs / ToyVesselRoster.cs; docs by file name; changes
from git log on the vessel's own files.

Output: fleet.json (schema cosmic-shore.fleet-codex/1) and img/<Key>.webp (the hangar preview, 480 wide).
The page keys everything by the VesselClassType member, so a class asset's display name ("Brittle Star") and its
map's name ("BrittleStar") can never split one vessel into two rows.
"""
import argparse
import functools
import glob
import hashlib
import json
import os
import re
import subprocess
import sys
from datetime import datetime, timezone

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
import vessel_mode_chart as vmc  # noqa: E402
# The two readers the analytics console already keeps from the game: each mode's genre element (ModeGenre.cs) and the
# colour-set themes (ColorSetCatalog: Nocturne, Photic, Dart Frog). The codex reads the same ones, never its own copy.
sys.path.insert(0, os.path.join(REPO, "Tools", "Analytics", "server"))
sys.path.insert(0, os.path.join(REPO, "Tools", "Analytics", "console"))
import mode_elements  # noqa: E402
import make_themes  # noqa: E402

DEFAULT_OUT = os.path.join(REPO, "Docs", "FleetCodex", "build")
GITHUB = "https://github.com/froglet-studio/Cosmic-Shore/blob/bleeding-edge/"
ELEMENTS = ["Charge", "Mass", "Space", "Time"]   # the HUD's order (LOCKED four-icon row)
# The codex's themes: the game's own colour sets (Docs/UI_STYLE_GUIDE.md section 14; Shombith 2026-11-08: "three themes:
# Nocturne, Photic and Dart Frog"). The film-named themes (Silver Screen ... HyperSea) moved to these (Frogma, Style Guide).
THEME_SETS = ("nocturne", "photic", "dartfrog")
CHANGES_PER_VESSEL = 8


def read(rel):
    return vmc.read(rel)


def git(*args):
    r = subprocess.run(["git", "-C", REPO, *args], capture_output=True, text=True)
    return r.stdout.strip()


def contains_bleeding_edge():
    """None when the tree read carries every commit of origin/bleeding-edge, else why not. The codex states what the
    game ships, so it is built on bleeding-edge (a worktree of it, with any shipped-but-unmerged branch merged on
    top: /fleet-codex 1, /ship 5.6) - never on a feature branch alone, which silently drops whatever landed on
    bleeding-edge since the branch was cut (2026-11-03: the live page had been published from a feature branch)."""
    if subprocess.run(["git", "-C", REPO, "rev-parse", "--verify", "-q", "origin/bleeding-edge"],
                      capture_output=True).returncode != 0:
        return "origin/bleeding-edge is not fetched here (git fetch origin bleeding-edge)"
    if subprocess.run(["git", "-C", REPO, "merge-base", "--is-ancestor", "origin/bleeding-edge", "HEAD"],
                      capture_output=True).returncode != 0:
        return ("HEAD does not contain origin/bleeding-edge: build in a worktree of bleeding-edge, merging any shipped "
                "branch on top (/fleet-codex 1)")
    return None


def class_enum():
    """VesselClassType: value -> (member, the comment on its line)."""
    out = {}
    for line in read("Assets/_Scripts/Data/Enums/VesselClassType.cs").splitlines():
        m = re.match(r"\s+(\w+)\s*=\s*(-?\d+),?\s*(?://\s*(.*))?$", line)
        if m and int(m.group(2)) > 0:
            out[int(m.group(2))] = (m.group(1), (m.group(3) or "").strip())
    return out


def toy_roster():
    m = re.search(r"Default\s*=\s*\{(.*?)\};", read("Assets/_Scripts/Controller/Toys/ToyVesselRoster.cs"), re.S)
    return set(re.findall(r"VesselClassType\.(\w+)", m.group(1))) if m else set()


def yaml_scalar(text, key):
    """A top-level (2-space) scalar of a Unity asset, folded lines joined, quotes undone."""
    m = re.search(rf"^  {key}:[ ]?(.*(?:\n    .*)*)", text, re.M)
    if not m:
        return ""
    raw = " ".join(part.strip() for part in m.group(1).split("\n")).strip()
    if len(raw) >= 2 and raw[0] == raw[-1] and raw[0] in "'\"":
        raw = raw[1:-1].replace("''", "'") if raw[0] == "'" else _yaml_unescape(raw[1:-1])
    return raw


_YAML_ESC = {"n": "\n", "t": "\t", '"': '"', "\\": "\\", "/": "/", "0": "\0"}


def _yaml_unescape(body):
    """A double-quoted YAML scalar's escapes: Unity writes typographic characters as \\uXXXX ("You\\u2019re")."""
    def one(m):
        if m.group(1):
            return chr(int(m.group(1), 16))
        return _YAML_ESC.get(m.group(2), m.group(0))
    return re.sub(r"\\(?:u([0-9a-fA-F]{4})|(.))", one, body)


def guid_paths(roots, ext):
    out = {}
    for root in roots:
        for meta in glob.glob(os.path.join(REPO, root, "**", f"*{ext}.meta"), recursive=True):
            with open(meta, encoding="utf-8", errors="replace") as fh:
                m = re.search(r"^guid: (\w+)", fh.read(400), re.M)
            if m:
                out[m.group(1)] = meta[:-5]
    return out


def classes():
    """VesselClassType value -> its class asset's facts."""
    out = {}
    for path in sorted(glob.glob(os.path.join(REPO, "Assets", "_SO_Assets", "Classes", "SO_Class_*.asset"))):
        text = open(path, encoding="utf-8").read()
        m = re.search(r"^  Class: (\d+)", text, re.M)
        if not m:
            continue
        crew = re.search(r"^  CrewNames:\n((?:  - .*\n)+)", text, re.M)
        preview = re.search(r"^  PreviewImage: \{.*guid: (\w+)", text, re.M)
        out[int(m.group(1))] = {
            "asset": os.path.relpath(path, REPO),
            "name": yaml_scalar(text, "Name"),
            "description": yaml_scalar(text, "Description"),
            "crew": [c.strip()[2:].strip() for c in crew.group(1).splitlines()] if crew else [],
            "previewGuid": preview.group(1) if preview else None,
            "locked": yaml_scalar(text, "isLocked") == "1",
        }
    return out


def ability_rows():
    r = subprocess.run([sys.executable, os.path.join(HERE, "element_ability_table.py"), "--json", "--verbose"],
                       capture_output=True, text=True, cwd=REPO)
    if r.returncode != 0:
        sys.exit(f"element_ability_table.py failed:\n{r.stderr}")
    out = {}
    for row in json.loads(r.stdout):
        out.setdefault(row["vessel"], {})[row["element"]] = row
    return out


def unquote(s):
    s = (s or "").strip()
    if len(s) >= 2 and s[0] == s[-1] == "'":
        s = s[1:-1].replace("''", "'")
    return s


def element_entry(row):
    gates = [g for g in row.get("upgrade_gates", []) if g.get("kind") == "upgrade"]
    live_gates = [g for g in gates if not g.get("disabled_by")]
    scaling = []
    for s in row.get("scaling", []):
        scaling.append({
            "field": s.get("field"), "min": s.get("min"), "max": s.get("max"),
            "on": bool(s.get("enabled", True)) and not s.get("disabled_by"),
            "offBecause": s.get("disabled_by") if (s.get("disabled_by") or not s.get("enabled", True)) else None,
            "where": s.get("site"), "asset": s.get("asset") or None, "script": s.get("script") or None,
        })
    label = unquote(row.get("upgrade"))
    return {
        "ability": unquote(row.get("ability")),
        "open": "(open design slot)" in (row.get("ability") or "") or not row.get("ability"),
        "input": row.get("input") or "", "pad": row.get("pad") or "", "key": row.get("key") or "",
        "description": unquote(row.get("description")),
        "upgrade": {
            "label": label,
            "description": unquote(row.get("upgrade_description")),
            "status": ("wired" if label and live_gates else "off" if label and gates
                       else "label-only" if label else "gate-only" if live_gates else "open"),
            "gates": [{"site": g.get("site"), "script": g.get("script") or None,
                       "offBecause": g.get("disabled_by")} for g in gates],
            "unlockLevel": row.get("unlock_level"), "relockBelow": row.get("relock_below"),
        },
        "scaling": scaling,
        "notes": [n for n in row.get("notes", []) if n],
    }


def flight(key):
    s = vmc.stats(key)
    if not s:
        return None
    return {"cruise": round(s["ts"] + s["ms"], 1), "throttle": s["ts"], "minimum": s["ms"],
            "pitch": s["pitch"], "yaw": s["yaw"], "turnPerSpeed": s.get("rts"), "boost": s.get("boost"),
            "source": f"Assets/_Prefabs/Spacevessels/{key}.prefab"}


def card_facts():
    """The arcade cards with their description and intensity range, keyed by display name of each hull."""
    extra = {}
    for path in glob.glob(os.path.join(REPO, "Assets", "_SO_Assets", "Games", "ArcadeGame*.asset")):
        text = open(path, encoding="utf-8").read()
        extra[yaml_scalar(text, "DisplayName")] = {
            "description": yaml_scalar(text, "Description"),
            "intensity": [int(yaml_scalar(text, "MinIntensity") or 1), int(yaml_scalar(text, "MaxIntensity") or 4)],
            "golf": yaml_scalar(text, "GolfScoring") == "1",
            "asset": os.path.relpath(path, REPO),
        }
    # What kind of game each mode is, as the game answers it (ModeGenre.TryElementsFor: the mode's own row, else its
    # scoring metric's element): Joust a Charge game, Scurry a Mass game, Skim Race a Time game. A mode the game has no
    # answer for carries no element and the page lists it under "other games".
    genre = mode_elements.build()["modes"]
    out = {}
    for c in vmc.cards():
        facts = extra.get(c["name"], {})
        elements = [e for e in genre.get(c["mode"]) or [] if e in ELEMENTS]
        for hull in c["hulls"]:
            out.setdefault(hull, []).append({
                "name": c["name"], "mode": c["mode"], "rosters": c["lists"], "hullsOnCard": len(c["hulls"]),
                "elements": elements, **facts})
    return out


def write_themes(out_dir):
    """themes.css and theme.js for the page, from the analytics console's own theme builder (make_themes.build), limited
    to the three colour sets players pick in the game. Returns a hash of both, so a palette change republishes."""
    sets = [x for x in make_themes.catalog() if x["id"] in THEME_SETS]
    missing = sorted(set(THEME_SETS) - {x["id"] for x in sets})
    if missing:
        sys.exit(f"fleet_codex: ColorSetCatalog has no colour set {', '.join(missing)}")
    css, js, _ = make_themes.build(sets=sets)
    css = css.replace("Tools/Analytics/console/make_themes.py", "Tools/Analytics/console/make_themes.py via Tools/Build/fleet_codex.py")
    js = (js.replace("'fa-theme'", "'fleetCodexColourSet'").replace("window.FA_THEMES", "window.FC_THEMES")
            .replace("Tools/Analytics/console/make_themes.py", "Tools/Analytics/console/make_themes.py via Tools/Build/fleet_codex.py"))
    os.makedirs(out_dir, exist_ok=True)
    for name, body in (("themes.css", css), ("theme.js", js)):
        with open(os.path.join(out_dir, name), "w", encoding="utf-8") as fh:
            fh.write(body)
    return hashlib.sha256((css + js).encode()).hexdigest()[:16]


def norm(s):
    return re.sub(r"[^a-z]", "", s.lower())


def docs_for(key, name, note):
    want = {norm(key), norm(name)}
    found = []
    pools = ["Assets/_Scripts/Controller/Vessel/**/*.md", "Docs/VesselModels/*.md", "Docs/Studios/*.md",
             "Assets/_Scripts/Controller/Arcade/*.md", ".claude/skills/duo-vessel/references/designs/*.md"]
    for pool in pools:
        for p in sorted(glob.glob(os.path.join(REPO, pool), recursive=True)):
            stem = norm(os.path.basename(p)[:-3])
            if any(stem.startswith(w) for w in want if w):
                found.append(os.path.relpath(p, REPO))
    for ref in re.findall(r"([\w/]+\.md)", note):
        hits = glob.glob(os.path.join(REPO, "**", os.path.basename(ref)), recursive=True)
        for h in hits:
            rel = os.path.relpath(h, REPO)
            if rel.endswith(ref) and rel not in found:
                found.insert(0, rel)
    seen, out = set(), []
    for rel in found:
        if rel not in seen and "/Library/" not in rel:
            seen.add(rel)
            out.append({"path": rel, "url": GITHUB + rel})
    return out[:10]


def vessel_paths(key):
    paths = [f"Assets/_Prefabs/Spacevessels/{key}.prefab", f"Assets/Resources/ElementalAbilityMaps/{key}.asset",
             f"Assets/_SO_Assets/VesselActions/{key}"]
    for p in glob.glob(os.path.join(REPO, "Assets", "_Scripts", "**", f"*{key}*.cs"), recursive=True):
        paths.append(os.path.relpath(p, REPO))
    for p in glob.glob(os.path.join(REPO, "Assets", "_Scripts", "Controller", "Vessel", "**", "*.md"), recursive=True):
        if norm(os.path.basename(p)[:-3]).startswith(norm(key)):
            paths.append(os.path.relpath(p, REPO))
    return [p for p in paths if os.path.exists(os.path.join(REPO, p))]


def changes(key, class_asset=None, cards=()):
    """What changed lately, in plain words, never a commit message (Shombith 2026-11-08: "dont show commit messages",
    then "show recent changes by when somebody updates a game or a vessel"). Each entry is a date with the PARTS of the
    vessel whose files changed that day (its abilities, the vessel, its model, its icons...) and the games of it that
    changed (a card asset or its scene), read from git's file lists, so the words come from what was touched."""
    watch = [(f"Assets/Resources/ElementalAbilityMaps/{key}.asset", "abilities and level-5 upgrades"),
             (f"Assets/_Prefabs/Spacevessels/{key}.prefab", "the vessel"),
             (f"Assets/_SO_Assets/VesselActions/{key}", "how its abilities fire"),
             (f"Assets/_Graphics/Icons/AbilityIcons/{key}", "its ability icons")]
    if class_asset:
        watch.append((class_asset, "its description and card"))
    for p in glob.glob(os.path.join(REPO, "Assets", "_Scripts", "**", f"*{key}*.cs"), recursive=True):
        watch.append((os.path.relpath(p, REPO), "how it plays"))
    for p in glob.glob(os.path.join(REPO, "Assets", "_Models", "Vessel Models", "**", f"*{key}*"), recursive=True):
        if not p.endswith(".meta"):
            watch.append((os.path.relpath(p, REPO), "its model"))
    games = []
    for c in cards:
        if not c.get("asset"):
            continue
        games.append((c["asset"], c["name"]))
        scene = yaml_scalar(open(os.path.join(REPO, c["asset"]), encoding="utf-8").read(), "SceneName")
        for sp in glob.glob(os.path.join(REPO, "Assets", "_Scenes", "**", f"{scene}.unity"), recursive=True) if scene else []:
            games.append((os.path.relpath(sp, REPO), c["name"]))
    watch = [(w, l) for w, l in watch if os.path.exists(os.path.join(REPO, w))]
    games = [(w, n) for w, n in games if os.path.exists(os.path.join(REPO, w))]
    if not watch and not games:
        return []
    # a shallow clone's oldest commits look like they add every file: they are the history's edge, not an update
    shallow_file = os.path.join(REPO, git("rev-parse", "--git-dir"), "shallow")
    edge = set(open(shallow_file).read().split()) if os.path.exists(shallow_file) else set()
    log = git("log", "-n40", "--no-merges", "--date=short", "--name-only", "--format=\x1e%H %ad", "--",
              *[w for w, _ in watch], *[w for w, _ in games])
    days = {}
    for block in log.split("\x1e")[1:]:
        lines = [x for x in block.splitlines() if x.strip()]
        if not lines:
            continue
        sha, _, date = lines[0].partition(" ")
        if sha in edge:
            continue
        day = days.setdefault(date, {"parts": [], "games": []})
        for f in lines[1:]:
            for w, label in watch:
                if (f == w or f.startswith(w.rstrip("/") + "/")) and label not in day["parts"]:
                    day["parts"].append(label)
            for w, name in games:
                if f == w and name not in day["games"]:
                    day["games"].append(name)
    out = [{"date": d, "parts": v["parts"], "games": v["games"]} for d, v in sorted(days.items(), reverse=True)
           if v["parts"] or v["games"]]
    return out[:CHANGES_PER_VESSEL]


def write_image(guid, previews, key, out_dir):
    src = previews.get(guid) if guid else None
    if not src:
        return None
    try:
        from PIL import Image
    except ImportError:
        return None
    os.makedirs(os.path.join(out_dir, "img"), exist_ok=True)
    rel = f"img/{key}.webp"
    im = Image.open(src).convert("RGBA")
    w = 480
    im = im.resize((w, max(1, round(im.height * w / im.width))), Image.LANCZOS)
    im.save(os.path.join(out_dir, rel), "WEBP", quality=80, method=6)
    return rel


# ---- Controls: one row per action, a column per device (Controller, PC, Mobile) ----------------------------------

GLYPH_TEXT = {"LiftLeftThumb": "Lift left thumb", "LiftRightThumb": "Lift right thumb",
              "LiftEitherThumb": "Lift either thumb", "LiftBothThumbs": "Lift both thumbs",
              "ThumbsApart": "Thumbs apart"}
# What a thumb lift raises on a thumb-lift hull (TouchInputStrategy): left thumb = RT's pair, right = LT's, both = Idle.
LIFT_FOR_INPUT = {1: "Lift left thumb", 11: "Lift left thumb", 2: "Lift right thumb", 12: "Lift right thumb",
                  4: "Lift both thumbs"}
ONE_STICK_BASE = "SingleStickVesselTransformer"


def script_classes():
    """class name -> (base class, .cs path), and script guid -> class name, for the vessel scripts."""
    bases, by_guid = {}, {}
    for cs in glob.glob(os.path.join(REPO, "Assets", "_Scripts", "**", "*.cs"), recursive=True):
        try:
            text = open(cs, encoding="utf-8", errors="replace").read()
        except OSError:
            continue
        for name, base in re.findall(r"\bclass\s+(\w+)\s*(?:<[^>]*>)?\s*:\s*([\w.]+)", text):
            bases.setdefault(name, base.split(".")[-1])
        meta = cs + ".meta"
        if os.path.exists(meta):
            m = re.search(r"^guid: (\w+)", open(meta).read(400), re.M)
            if m:
                by_guid[m.group(1)] = os.path.basename(cs)[:-3]
    return bases, by_guid


def is_one_stick(key, bases, by_guid):
    path = os.path.join(REPO, f"Assets/_Prefabs/Spacevessels/{key}.prefab")
    if not os.path.exists(path):
        return None
    for g in set(re.findall(r"m_Script: \{fileID: 11500000, guid: (\w+)", open(path, encoding="utf-8").read())):
        name, seen = by_guid.get(g), set()
        if not name or "Transformer" not in name:
            continue
        while name and name not in seen:
            if name == ONE_STICK_BASE:
                return True
            seen.add(name)
            name = bases.get(name)
    return False


def prefab_bindings(key, asset_names):
    """InputEvent -> the action assets bound to it, for the shared list and the touch overrides."""
    path = os.path.join(REPO, f"Assets/_Prefabs/Spacevessels/{key}.prefab")
    out = {"shared": {}, "touch": {}}
    if not os.path.exists(path):
        return out
    text = open(path, encoding="utf-8").read()
    for field, slot in (("_inputEventShipActions", "shared"), ("_touchActionOverrides", "touch")):
        m = re.search(r"\n  %s:(.*?)(?=\n  [A-Za-z_])" % field, text, re.S)
        if not m:
            continue
        for entry in re.split(r"\n  - ", m.group(1)):
            ie = re.search(r"InputEvent: (\d+)", entry)
            names = [asset_names.get(g) for g in re.findall(r"guid: ([0-9a-f]{32})", entry)]
            names = [n for n in names if n]
            if ie and names:
                out[slot].setdefault(int(ie.group(1)), []).extend(names)
    return out


def action_label(asset, key):
    stem = os.path.basename(asset)[:-6] if asset.endswith(".asset") else asset
    for prefix in (key, "Vessel", "Ship"):
        if stem.startswith(prefix) and len(stem) > len(prefix):
            stem = stem[len(prefix):]
    stem = re.sub(r"(Action|ActionSO|SO)$", "", stem) or stem
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", stem).strip()


def touch_profiles():
    try:
        import author_touch_profiles as atp
    except Exception:   # the touch table is optional: say so on the page rather than guess
        return None, {}
    by_value = {v: k for k, v in atp.VESSEL.items()}
    rows = {by_value[p["vessel"]]: p for p in atp.PROFILES if p["vessel"] in by_value}
    return atp, rows


def controls(key, elements, one_stick, bindings, atp, profile):
    import element_ability_table as eat
    names = eat.INPUT_EVENTS
    scheme = ("TotemButtons" if profile and profile["scheme"] == atp.SCHEME["TotemButtons"] else "ThumbLifts") if atp else None

    def touch_for(element, inp, label):
        if not atp:
            return ""
        p = profile or {}
        if inp is None:   # no button on any device: only a glyph the phone draws for a gesture says anything
            for el, glyph in p.get("glyphs", []):
                if el == element:
                    return GLYPH_TEXT.get(glyph, glyph)
            return "no button"
        if scheme == "TotemButtons":
            if element and element in p.get("displayOnly", []):
                return "Throttle slider" if p.get("slider") else "Shown on its card, no press"
            for b in p.get("extras", []):
                if b["input"] == inp:
                    icon = re.sub(r"^TouchIcon_", "", b["icon"])
                    extra = " (drag aims)" if b.get("aims") else " (toggle)" if b.get("behaviour") == atp.TOGGLE else ""
                    return f"{icon} button{extra}"
            if element:
                return f"{label} card" + (" (latches)" if element in p.get("toggles", []) else "")
            return ""
        for el, glyph in p.get("glyphs", []):
            if el == element:
                return GLYPH_TEXT.get(glyph, glyph)
        if inp in LIFT_FOR_INPUT:
            return LIFT_FOR_INPUT[inp]
        if element and inp in (6, 7, 8, 14):
            return f"{label} card"
        return ""

    rows = [{
        "action": "Steer", "detail": "one-stick flight" if one_stick else "two-stick flight",
        "pad": "Left stick" if one_stick else "Both sticks",
        "pc": "W A S D" if one_stick else "W A S D + P ; L '",
        "touch": "Either thumb" if one_stick else "Two thumbs",
    }]
    used = set()
    for el in ELEMENTS:
        e = elements.get(el)
        if not e or e["open"]:
            continue
        inp = next((i for i, n in names.items() if n[0] == e["input"]), None)
        if inp is not None and not names[inp][1]:
            inp = None   # an input no device has a button for (0 = passive or a flight gesture)
        if inp is not None:
            used.add(inp)
        rows.append({"action": e["ability"], "detail": el, "element": el, "pad": e["pad"] or "no button",
                     "pc": e["key"] or "no button", "touch": touch_for(el, inp, e["ability"])})
    for inp in sorted(bindings["shared"]):
        if inp in used or inp not in names or not names[inp][1]:
            continue
        label = ", ".join(dict.fromkeys(action_label(a, key) for a in bindings["shared"][inp]))
        rows.append({"action": label, "detail": names[inp][0].replace("Action", ""), "pad": names[inp][1],
                     "pc": names[inp][2], "touch": touch_for(None, inp, label) or ""})
        used.add(inp)
    notes = []
    p = profile or {}
    if atp:
        notes.append("Phone: buttons round the right thumb; lifting a thumb fires nothing." if scheme == "TotemButtons"
                     else "Phone: abilities on thumb lifts (left thumb = RT, right thumb = LT, both = idle)."
                     + ("" if profile else " No row of its own: the default thumb-lift profile."))
    for flag, text in (("tiltSteer", "Tilt the phone to steer with no thumb down."),
                       ("tiltRoll", "The phone rolls the hull one to one while the aiming thumb is down."),
                       ("idleTriggers", "Both thumbs off pulls both triggers."),
                       ("idleFull", "Both thumbs off flies at full throttle."),
                       ("slider", "A set-and-hold throttle slider.")):
        if p.get(flag):
            notes.append(text)
    if bindings["touch"]:
        notes.append("Touch overrides on the prefab: " + ", ".join(
            f"{names.get(i, (str(i),))[0].replace('Action', '')} = " + ", ".join(action_label(a, key) for a in acts)
            for i, acts in sorted(bindings["touch"].items())) + ".")
    notes.append("Only a controller has analog triggers: a key, a mouse button or a phone button is a full pull.")
    return {"oneStick": one_stick, "touchScheme": scheme, "rows": rows, "notes": notes}


# ---- Art: the game's own icons (Rev 48 B, Shombith 2026-11-02: "use the correct iconography for everything") --------

ELEMENT_NUM = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}
UI_ART = {
    # the element marks: the element's FLOWER drawn the way the HUD draws it (KitElementFlower / ElementalBarsView.
    # ConfigurePetal): its petal sprite five times, each stretched over the flower and turned PetalSpacing (72) degrees
    # clockwise from the last, tinted by the level's tick colour. Shombith 2026-11-08: "Use upgrade flowers and make them
    # animated when l5 is clicked"; then "weird orientation" on the ElementIcons PNGs, an older drawing the game does not
    # use for this, so the page builds the flower from the petal like the game. Copied uncropped (UNCROPPED): the petal
    # sits in the sprite's upper half and the turn is about the sprite's centre.
    **{f"petal/{el}": f"Assets/Resources/ElementPetals/{el.lower()}_petal.png" for el in ("Charge", "Mass", "Space", "Time")},
    "omni": "Assets/_Graphics/ElementIcons/OmniCrystal_Active.png",
    # the HUD's level-5 look: the bloom AbilityLockupView puts behind an upgraded card (AbilityLockupStyle.bloomSprite)
    "bloom": "Assets/_Graphics/Design Assests/HUD UI/AbilityLockup/LockupBloom.png",
}
UNCROPPED = {k for k in UI_ART if k.startswith("petal/")}
PAD_ART = {   # a controls row's pad label -> the controller art (Xbox face buttons, the LT / RT trigger art)
    "A": "Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/A button.png",
    "B": "Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/B button.png",
    "X": "Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/x button.png",
    "Y": "Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/Y button.png",
    "LT": "Assets/_Graphics/UI/ControlIcons/Trigger_LT.png",
    "RT": "Assets/_Graphics/UI/ControlIcons/Trigger_RT.png",
    "RB": "Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/R1.png",
    "Left stick": "Assets/_Graphics/UI/ControlIcons/Thumbstick_Base.png",
    "Right stick": "Assets/_Graphics/UI/ControlIcons/Thumbstick_Look.png",
    "Both sticks": "Assets/_Graphics/UI/ControlIcons/Thumbsticks_Outward.png",
}
TOUCH_DIR = "Assets/_Graphics/Design Assests/HUD UI/Touch"
IMAGE_SCRIPT = "fe87c0e1cc204ed48ad3b37840f39efc"
GENERIC_CRYSTAL_EFFECTS = re.compile(r"Haptics|Morph|Absorb|Retire|Fusion|Sound|Audio", re.I)


class Art:
    """Copies (and scales) the game's PNGs into <out>/img/, once each, and remembers the published path."""

    def __init__(self, out_dir, enabled=True):
        self.out, self.enabled, self.done = out_dir, enabled, {}

    def png(self, src, rel, box=128, crop=True):
        if not self.enabled or not src or not os.path.exists(os.path.join(REPO, src)):
            return None
        rel = "img/" + rel
        if rel not in self.done:
            from PIL import Image
            im = Image.open(os.path.join(REPO, src)).convert("RGBA")
            bb = im.getbbox() if crop else None
            if bb:
                im = im.crop(bb)
            im.thumbnail((box, box), Image.LANCZOS)
            self.save(im, rel)
        return rel

    def save(self, im, rel):
        path = os.path.join(self.out, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        im.save(path, optimize=True)
        self.done[rel] = path
        return rel


_yaml_cache = {}


def unity_docs(path):
    if path not in _yaml_cache:
        text = open(path, encoding="utf-8", errors="replace").read()
        parts = re.split(r"^--- !u!(\d+) &(-?\d+)( stripped)?.*$", text, flags=re.M)
        docs = {parts[i + 1]: (parts[i], bool(parts[i + 2]), parts[i + 3]) for i in range(1, len(parts), 4)}
        _yaml_cache[path] = (text, docs)
    return _yaml_cache[path]


def prefab_mods(body):
    out = []
    for blk in re.split(r"\n    - target: ", body)[1:]:
        tf = re.match(r"\{fileID: (-?\d+), guid: (\w+)", blk)
        pp = re.search(r"propertyPath: (\S+)", blk)
        orf = re.search(r"objectReference: \{fileID: (-?\d+)(?:, guid: (\w+))?", blk)
        if tf and pp:
            out.append((tf.group(1), tf.group(2), pp.group(1), orf.group(2) if orf else None))
    return out


def image_sprite(prefabs, path, guid, fid, outer):
    """The sprite guid an Image shows, through the vessel's overrides and the prefab-variant chain."""
    for tf, tg, pp, og in outer:
        if tf == fid and tg == guid and pp == "m_Sprite":
            return og
    _, docs = unity_docs(path)
    doc = docs.get(fid)
    if not doc:
        return None
    cls, stripped, body = doc
    if not stripped:
        s = re.search(r"m_Sprite: \{fileID: (-?\d+)(?:, guid: (\w+))?", body)
        return s.group(2) if s and s.group(1) != "0" else None
    src = re.search(r"m_CorrespondingSourceObject: \{fileID: (-?\d+), guid: (\w+)", body)
    inst = re.search(r"m_PrefabInstance: \{fileID: (-?\d+)", body)
    if not src or src.group(2) not in prefabs:
        return None
    mods = prefab_mods(docs.get(inst.group(1), ("", False, ""))[2]) if inst else []
    return image_sprite(prefabs, prefabs[src.group(2)], src.group(2), src.group(1), outer + mods)


def image_parts(path, fid):
    """(Image fileID, sprite guid) of an Image's visible child images (the cards the HUD builds from parts), in draw
    order."""
    _, docs = unity_docs(path)
    doc = docs.get(fid)
    go = re.search(r"m_GameObject: \{fileID: (-?\d+)", doc[2]) if doc else None
    if not go:
        return []
    by_go = {}
    for k, (c, s, b) in docs.items():
        g = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", b)
        if g:
            by_go.setdefault(g.group(1), []).append(k)
    out = []

    def walk(gid, depth):
        for k in by_go.get(gid, []):
            c, s, b = docs[k]
            if c == "114" and IMAGE_SCRIPT in b:
                sp = re.search(r"m_Sprite: \{fileID: (-?\d+)(?:, guid: (\w+))?", b)
                if sp and sp.group(2):
                    out.append((k, sp.group(2)))
            if c in ("224", "4") and depth < 2 and "m_Children:" in b:
                for ch in re.findall(r"- \{fileID: (-?\d+)\}", b.split("m_Children:")[1].split("m_Father")[0]):
                    cg = re.search(r"m_GameObject: \{fileID: (-?\d+)", docs.get(ch, ("", False, ""))[2])
                    if cg:
                        act = re.search(r"m_IsActive: (\d)", docs.get(cg.group(1), ("", False, ""))[2])
                        if not act or act.group(1) == "1":
                            walk(cg.group(1), depth + 1)
    walk(go.group(1), 0)
    return out


def hud_of(key, prefabs):
    path = os.path.join(REPO, f"Assets/_Prefabs/Spacevessels/{key}.prefab")
    if not os.path.exists(path):
        return None
    _, docs = unity_docs(path)
    for fid, (cls, stripped, body) in docs.items():
        g = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: (\w+)", body) if cls == "1001" else None
        sp = prefabs.get(g.group(1)) if g else None
        if sp and "abilityIcons:" in open(sp, encoding="utf-8", errors="replace").read():
            return sp, g.group(1), prefab_mods(body)
    # the Manta and the Grizzly carry their HUD view inside the vessel prefab itself
    if any(c == "114" and "abilityIcons:" in b for c, s, b in docs.values()):
        meta = open(path + ".meta", encoding="utf-8").read()
        return path, re.search(r"guid: (\w+)", meta).group(1), []
    return None


L5_SHIELDED = (104, 164, 255, 255)   # the HUD's shielded signal (states.SHIELDED["jade"]): an L5 that shields reads as one


ICON_LAYERS = {}   # published icon path -> its tint layers (every icon drawn with the HUD's real colours)
DOMAINS = ("Jade", "Ruby", "Gold")   # the playable domains (Blue is the game's "no team", Docs/PALETTE.md 2.8)
def _live_color_set():
    """The palette the game runs on: the SO_ColorSet ThemeManagerDataContainer.ColorSet names, found by its guid, so a
    switch of the default colour set (Photic -> Nocturne, 2026-11-03) moves the codex with the game, not after it."""
    container = open(os.path.join(REPO, "Assets", "_SO_Assets", "ThemeManagerDataContainer.asset"), encoding="utf-8").read()
    guid = re.search(r"^  ColorSet: \{fileID: \d+, guid: (\w+)", container, re.M).group(1)
    palettes = os.path.join(REPO, "Assets", "_SO_Assets", "Color Palettes")
    for name in sorted(os.listdir(palettes)):
        if name.endswith(".asset.meta") and re.search(rf"^guid: {guid}$", open(os.path.join(palettes, name), encoding="utf-8").read(), re.M):
            return os.path.join(palettes, name[:-len(".meta")])
    raise SystemExit(f"fleet_codex: ThemeManagerDataContainer names ColorSet guid {guid}, and no palette in {palettes} has it")


COLOR_SET = _live_color_set()
BARS_CONFIG = os.path.join(REPO, "Assets", "Resources", "ElementalBarsConfig.asset")


def _hex(rgb):
    return "#" + "".join("%02x" % max(0, min(255, round(c * 255))) for c in rgb[:3])


def _linear_to_gamma(c):
    """Unity's Mathf.LinearToGammaSpace (what Color.gamma does per channel)."""
    if c <= 0.0:
        return 0.0
    if c <= 0.0031308:
        return 12.92 * c
    return 1.055 * (c ** (1 / 2.4)) - 0.055


@functools.lru_cache(maxsize=1)
def palette():
    """The colours the game tints a HUD with, read from the shipped palette and computed the way SO_ColorSet does:
    signal = GetDomainSignalColor (TrailHighlightColor with its brightest channel driven to 1), shielded =
    GetShieldedSignalColor (ShieldedOutsideBlockColor.gamma), omni = ElementalBarsConfigSO.LimeColor (the lime a
    free-for-all crystal wears)."""
    text = open(COLOR_SET, encoding="utf-8").read().replace("\n      ", " ")

    def col(block, field):
        m = re.search(rf"{field}: \{{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+), a: ([-\d.e]+)\}}", block)
        return [float(x) for x in m.groups()]
    out = {}
    for d in DOMAINS:
        block = re.search(rf"^  {d}Colors:\n((?:    .*\n)+)", text, re.M).group(1)
        ui = col(block, "TrailHighlightColor")
        peak = max(ui[:3]) or 1.0
        sh = col(block, "ShieldedOutsideBlockColor")
        out[d] = {"signal": _hex([c / peak for c in ui[:3]]), "shielded": _hex([_linear_to_gamma(c) for c in sh[:3]])}
    bars = open(BARS_CONFIG, encoding="utf-8").read()
    lime = re.search(r"limeColor: \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+)", bars)
    tick = {k: _hex(list(_rgba(bars, k + "Color")[:3])) for k in ("grey", "white")}   # a petal's tick colours (0 and 1)
    return {"domains": out, "default": DOMAINS[0], "omni": _hex([float(x) for x in lime.groups()]), "petal": tick,
            "sources": [os.path.relpath(COLOR_SET, REPO), os.path.relpath(BARS_CONFIG, REPO)]}


def _rgba(text, field):
    m = re.search(rf"\b{field}: \{{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+), a: ([-\d.e]+)\}}", text)
    return tuple(float(x) for x in m.groups()) if m else None


@functools.lru_cache(maxsize=1)
def game_colours():
    """The fixed (domain-independent) colours a HUD paints, read from the shipped assets: danger =
    SO_ColorSet.GetDangerSignalColor (EnvironmentColors.Danger, brightest channel to 1); fire, blue, lime, grey and the
    petal white = ElementalBarsConfigSO; white = Color.white."""
    cs = open(COLOR_SET, encoding="utf-8").read().replace("\n      ", " ")
    bars = open(BARS_CONFIG, encoding="utf-8").read()
    d = _rgba(cs, "Danger")
    peak = max(d[:3]) or 1.0
    out = {"danger": (d[0] / peak, d[1] / peak, d[2] / peak), "white": (1.0, 1.0, 1.0)}
    for tok, field in (("fire", "fireColor"), ("blue", "blueColor"), ("lime", "limeColor"), ("grey", "greyColor"),
                       ("petalwhite", "whiteColor")):
        out[tok] = _rgba(bars, field)[:3]
    return out


# How a HUD view paints an Image, read from `<image>.color = <expr>` in its script. Order matters: the first match wins.
COLOUR_RULES = [
    (r"Color\.clear|new Color\(\s*1f?\s*,\s*1f?\s*,\s*1f?\s*,\s*0f?\s*\)", "hidden"),
    (r"[Dd]anger", "danger"), (r"[Ss]hielded", "shielded"), (r"BlueColor", "blue"),
    (r"LimeColor|CtaSignal|\bCTA", "lime"), (r"FireColor", "fire"), (r"GreyColor", "grey"),
    (r"WhiteColor", "petalwhite"), (r"[Dd]omain|_teamColor|TeamColor", "domain"), (r"Color\.white", "white"),
]
# methods whose writes are a moment, not the card at rest (a hit flash, a punch, a toll)
MOMENT_METHODS = re.compile(r"Flash|Hit|Punch|Pulse|Blink|Bite|Shake|Toll|Collect", re.I)
_METHOD = re.compile(r"^ {8}(?:\[[^\]]*\]\s*)?(?:(?:public|private|protected|internal|static|override|virtual|sealed|"
                     r"async|new)\s+)*[\w<>\[\],.?]+\s+(\w+)\s*\([^;{]*$", re.M)


_RETRACE = re.compile(r"\bColorTrace\.Retrace\(\s*(?:ColorTrace\.\w+|\"\w+\")\s*,\s*")


def unwrap_retrace(expr):
    """`ColorTrace.Retrace(role, c)` -> `c`, wherever it sits in the expression. Retrace is the worn colour set's
    repaint of c at runtime (Docs/VesselModels/COLOR_TRACE.md); the codex reads the authored colour, so it reads c."""
    while True:
        m = _RETRACE.search(expr)
        if not m:
            return expr
        depth, i = 1, m.end()
        while i < len(expr) and depth:
            depth += {"(": 1, ")": -1}.get(expr[i], 0)
            i += 1
        if depth:
            return expr
        expr = expr[:m.start()] + expr[m.end():i - 1].strip() + expr[i:]


def resolve_colour(expr, method, view_body, aliases=None, depth=0):
    """A colour expression -> a token (domain, shielded, danger, blue, lime, fire, grey, petalwhite, white, hidden),
    an (r, g, b, a) the prefab authors, or None when the script does not say."""
    expr = unwrap_retrace(" ".join(expr.split()))
    if "?" in expr and ":" in expr:
        cond, rest = expr.split("?", 1)
        a, b = rest.rsplit(":", 1)
        # `tint.a > 0f ? tint : white` paints the tint once it lands; any other condition is a state, so the card at
        # rest is the `else`
        # a null check (`barsConfig ? barsConfig.BlueColor : white`) paints the true branch too
        landed = re.fullmatch(r"\s*\(?\s*[\w.]+\.a\s*>\s*0f?\s*\)?\s*", cond)
        null_check = re.fullmatch(r"\s*!?\s*\(?\s*[\w.]+\s*(?:!=\s*null)?\s*\)?\s*", cond) and "!" not in cond
        expr = (a if landed or null_check else b).strip()
    ident = re.fullmatch(r"\w+", expr)
    if ident:
        lit = _rgba(view_body, ident.group(0))
        if lit:
            return lit
    for pat, tok in COLOUR_RULES:
        if re.search(pat, expr):
            return tok
    if ident and aliases and depth < 2:   # a field the script fills elsewhere (_driftHullColor = domain)
        for e2, m2 in aliases.get(ident.group(0), []):
            tok = resolve_colour(e2, m2, view_body, aliases, depth + 1)
            if tok and tok != "hidden":
                return tok
    if ident:   # a parameter: the method says what it carries (SetPlayerDomainColor(color))
        for pat, tok in COLOUR_RULES[1:]:
            if re.search(pat, method):
                return tok
    return None


def hud_colours(hud_path, scripts_by_guid):
    """{Image fileID: colour} for a vessel HUD: the field each view Image is wired to, and what the view's script
    paints that field at rest (the first write outside a moment-method that resolves). An Image no script paints keeps
    the m_Color its prefab authors."""
    _, docs = unity_docs(hud_path)
    out = {}
    for fid, (c, stripped, body) in docs.items():
        if c != "114" or "abilityIcons:" not in body:
            continue
        sg = re.search(r"m_Script: \{fileID: \d+, guid: (\w+)", body)
        cs_path = scripts_by_guid.get(sg.group(1)) if sg else None
        if not cs_path:
            continue
        text = open(cs_path, encoding="utf-8", errors="replace").read()
        methods = [(m.start(), m.group(1)) for m in _METHOD.finditer(text)]

        def method_at(pos):
            return max(((p, n) for p, n in methods if p < pos), default=(0, ""))[1]
        aliases = {}
        for m in re.finditer(r"(?<![.\w])(_\w+)\s*=(?!=)\s*([^;]+);", text):
            if not MOMENT_METHODS.search(method_at(m.start())):
                aliases.setdefault(m.group(1), []).append((m.group(2), method_at(m.start())))
        # a Color property is a name for its expression too (`Color HighLightColor => ColorTrace.Retrace(..., c)`)
        for m in re.finditer(r"\bColor\s+(\w+)\s*=>\s*([^;]+);", text):
            aliases.setdefault(m.group(1), []).append((m.group(2), ""))
        painted = {}
        for m in re.finditer(r"\b(\w+)\.color\s*=\s*([^;]+);", text):
            name = method_at(m.start())
            if MOMENT_METHODS.search(name):
                continue
            tok = resolve_colour(m.group(2), name, body, aliases)
            if tok is None:
                continue
            if tok != "hidden" or m.group(1) not in painted:
                if painted.get(m.group(1)) in (None, "hidden"):
                    painted[m.group(1)] = tok
        for field, ref in re.findall(r"^  (\w+): \{fileID: (-?\d+)\}$", body, re.M):
            if field in painted:
                out[ref] = painted[field]
    return out


def omni_tint(key):
    """What the vessel's HUD controller pushes onto the omni card (SetOmniAbilityTint): the Squirrel's is the shielded
    colour; a hull that pushes nothing keeps the icon white."""
    for path in glob.glob(os.path.join(REPO, "Assets/_Scripts/**", f"{key}HUDController.cs"), recursive=True):
        m = re.search(r"SetOmniAbilityTint\(\s*(\w+)\s*\)", open(path, encoding="utf-8", errors="replace").read())
        if m:
            return resolve_colour(m.group(1), "", "") or "white"
    return "white"


def colour_layers(parts, fixed):
    """Draw an icon from (image, colour) parts the way a Unity Image does (sprite x colour), as the page's tint layers:
    d = parts painted the pilot's domain, s = the domain's shielded colour (white masks), f = everything else in its
    real colour. A later part hides earlier ones in every layer. Returns (preview in Jade, layers)."""
    from PIL import Image, ImageChops
    size = (128, 128)
    layers = {k: Image.new("RGBA", size, (0, 0, 0, 0)) for k in "dsf"}
    jade = palette()["domains"]["Jade"]
    preview = Image.new("RGBA", size, (0, 0, 0, 0))
    for im, colour in parts:
        if colour == "hidden":
            continue
        im = im.convert("RGBA")
        r, g, b, a = im.split()
        if isinstance(colour, tuple) and len(colour) == 4:
            if colour[3] <= 0.01:
                continue
            a = a.point(lambda v, k=colour[3]: int(v * k))
            colour = colour[:3]
        if colour in ("domain", "shielded"):
            lum = ImageChops.lighter(ImageChops.lighter(r, g), b)
            part, k = Image.merge("RGBA", (r.point(lambda _: 255), g.point(lambda _: 255), b.point(lambda _: 255),
                                           ImageChops.multiply(a, lum))), "d" if colour == "domain" else "s"
            hexv = jade["signal" if colour == "domain" else "shielded"]
            tint = tuple(int(hexv[i:i + 2], 16) / 255 for i in (1, 3, 5))
        else:
            tint = fixed.get(colour, (1.0, 1.0, 1.0)) if isinstance(colour, str) else colour
            part, k = None, "f"
        coloured = Image.merge("RGBA", (r.point(lambda v, t=tint[0]: int(v * t)), g.point(lambda v, t=tint[1]: int(v * t)),
                                        b.point(lambda v, t=tint[2]: int(v * t)), a))
        part = part or coloured
        cover = part.split()[3]
        for other in "dsf":
            if other != k:
                lr, lg, lb, la = layers[other].split()
                layers[other] = Image.merge("RGBA", (lr, lg, lb, ImageChops.multiply(la, ImageChops.invert(cover))))
        layers[k].alpha_composite(part)
        preview.alpha_composite(coloured)
    return preview, {k: im for k, im in layers.items() if im.getbbox()}


def _fit(path, box=128):
    """A sprite cropped to its pixels and centred on a box x box canvas (a single-sprite card)."""
    from PIL import Image
    im = Image.open(path).convert("RGBA")
    bb = im.getbbox()
    if bb:
        im = im.crop(bb)
    im.thumbnail((box, box), Image.LANCZOS)
    canvas = Image.new("RGBA", (box, box), (0, 0, 0, 0))
    canvas.alpha_composite(im, ((box - im.width) // 2, (box - im.height) // 2))
    return canvas


def kit_layers(entries, draws, states, silver_shielded=(190, 190, 190, 255)):
    """A kit card state as a silver composite plus its tint layers: d = the parts the HUD paints in the pilot's domain,
    s = the parts it paints in the domain's shielded signal (both white masks), f = the fixed-colour parts. A part laid
    over earlier ones hides them in every layer, as it does on the card."""
    from PIL import Image, ImageChops
    size = (128, 128)
    layers = {k: Image.new("RGBA", size, (0, 0, 0, 0)) for k in "dsf"}
    silver = Image.new("RGBA", size, (0, 0, 0, 0))
    for entry in entries:
        name, colour = entry[0], entry[1]
        alpha = entry[2] if len(entry) > 2 else 1.0
        if colour == "domain":
            k, rgba, sv = "d", (255, 255, 255, 255), (236, 236, 236, 255)
        elif colour == "shielded":
            k, rgba, sv = "s", (255, 255, 255, 255), silver_shielded
        else:
            k = "f"
            if isinstance(colour, tuple):
                rgba = colour
            else:   # the kit names the colour; the value is the game's (game_colours), not the sheet's sample
                rgb = game_colours().get(colour)
                rgba = tuple(int(round(c * 255)) for c in rgb) + (255,) if rgb else states.FIXED[colour]
            sv = rgba
        part = states._colour(draws[name](), rgba, alpha)
        cover = part.split()[3]
        for other in "dsf":
            if other != k:
                r, g, b, a = layers[other].split()
                layers[other] = Image.merge("RGBA", (r, g, b, ImageChops.multiply(a, ImageChops.invert(cover))))
        layers[k].alpha_composite(part)
        silver.alpha_composite(states._colour(draws[name](), sv, alpha))
    return silver, {k: im for k, im in layers.items() if im.getbbox()}


def split_layers(im):
    """A HUD sprite's tint layers: its white and grey pixels are what the HUD's Image colour tints (the pilot's
    domain), its coloured pixels keep their colour."""
    from PIL import Image
    im = im.convert("RGBA")
    px = im.load()
    d = Image.new("RGBA", im.size, (0, 0, 0, 0))
    f = Image.new("RGBA", im.size, (0, 0, 0, 0))
    dp, fp = d.load(), f.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if not a:
                continue
            hi, lo = max(r, g, b), min(r, g, b)
            if hi and (hi - lo) / hi >= 0.3:
                fp[x, y] = (r, g, b, a)
            else:
                dp[x, y] = (255, 255, 255, a * hi // 255)
    return {k: v for k, v in (("d", d), ("f", f)) if v.getbbox()}


def save_layers(art, rel, layers):
    """Writes img/ab/<name>.<d|s|f>.png beside the icon; returns {layer: published path}."""
    stem = rel[:-4]
    return {k: art.save(im, f"{stem}.{k}.png") for k, im in layers.items()}


def lockup_cards(key):
    """The icon kit's cards for a hull, each as (silver composite, tint layers): the resting card (spec LOCKUP) per slot,
    and the level-5 state per element as '<Element>@L5'. Reads the kit; never writes to it."""
    spec_path = os.path.join(HERE, "icon_kit", "specs", key.lower() + ".py")
    if not os.path.exists(spec_path):
        return {}
    try:
        import importlib
        sys.path.insert(0, os.path.join(HERE, "icon_kit"))
        import states
        spec = importlib.import_module("specs." + key.lower())
        out = {}
        for el in ELEMENTS + ["Omni"]:
            sib = getattr(spec, "LOCKUP", {}).get(el)
            if sib:
                out[el] = kit_layers(sib["state"], sib["parts"], states)
        # no LOCKUP slot: the element's own card in the state the HUD rests it in (its lockup_state, else the first)
        for c in getattr(spec, "CARDS", []):
            el = c.get("element")
            if el in ELEMENTS and el not in out and not c.get("group") and c.get("states"):
                st = c["states"][min(c.get("lockup_state", 0), len(c["states"]) - 1)]
                out[el] = kit_layers(st[1], c["parts"], states)
        # The level-5 card: the state the kit draws for "<Element> 5" on that element's own card (the picked card, not
        # an options group) - what the HUD swaps the card's parts to when the upgrade comes on (the Tiger Moth's gilded
        # keys, the Sparrow's shielded rounds). Shielded parts keep the HUD's shielded signal so the change reads.
        for c in getattr(spec, "CARDS", []):
            el = c.get("element")
            if c.get("group") or el not in ELEMENTS or f"{el}@L5" in out:
                continue
            for st in c.get("states", []):
                if re.search(rf"(?<!\d){el} 5(?!\d)", st[0]):
                    out[f"{el}@L5"] = kit_layers(st[1], c["parts"], states, silver_shielded=L5_SHIELDED)
                    break
        return out
    except Exception as e:   # the kit is optional: the HUD's own sprites still answer
        print(f"  icon kit could not render {key}: {e}", file=sys.stderr)
        return {}


def own_icon(key, png_path, hud_path):
    """A HUD sprite counts for this hull only when the HUD is the hull's own or the art sits in the hull's own icon folder:
    a hull that borrows another's HUD (the Wrasse on the Sparrow's) would otherwise show that hull's abilities."""
    own_hud = os.path.basename(hud_path).lower().startswith(key.lower())
    own_art = f"/AbilityIcons/{key}/".lower() in png_path.replace(os.sep, "/").lower()
    return own_hud or own_art


def named_icon(key, label, pngs_by_dir):
    """The hull's own icon whose name is the ability's name (Manta_Kabloom.png for Kabloom)."""
    want = norm(label)
    for path in pngs_by_dir.get(key, []):
        stem = norm(os.path.basename(path)[:-4].split("_", 1)[-1])
        if want and stem == want:
            return path
    return None


_SCRIPTS = {}


def scripts_by_guid():
    """script guid -> .cs path, for the UI scripts (the vessel HUD views)."""
    if not _SCRIPTS:
        for cs in glob.glob(os.path.join(REPO, "Assets", "_Scripts", "**", "*.cs"), recursive=True):
            meta = cs + ".meta"
            if os.path.exists(meta):
                g = re.search(r"guid: (\w+)", open(meta, encoding="utf-8", errors="replace").read())
                if g:
                    _SCRIPTS[g.group(1)] = cs
    return _SCRIPTS


def ability_art(key, art, prefabs, pngs, labels=None):
    """element -> published icon path, plus 'Omni'. Kit card, else the HUD's sprite (own HUD or own art), else its
    visible parts, else the hull's own icon named after the ability."""
    out, layers = {}, {}
    labels = labels or {}
    for el, (im, lay) in lockup_cards(key).items():
        name = el.replace("@L5", "_L5")
        out[el] = art.save(im, f"img/ab/{key}_{name}.png") if art.enabled else None
        if out[el]:
            layers[out[el]] = save_layers(art, out[el], lay)
    ICON_LAYERS.update(layers)
    hud = hud_of(key, prefabs)
    if hud and art.enabled:
        from PIL import Image
        path, guid, outer = hud
        text, docs = unity_docs(path)
        colours = hud_colours(path, scripts_by_guid())
        fixed = game_colours()

        def colour_of(fid, root=False):
            """What the HUD paints this Image at rest: its view script's write, else the prefab's m_Color. The ability
            icon's own Image is often authored at alpha 0 because the lockup card draws its sprite (the Jacks), so its
            alpha is not a hide; a child part at alpha 0 is a state that is off at rest."""
            if fid in colours:
                return colours[fid]
            doc = docs.get(fid)
            c = _rgba(doc[2], "m_Color") if doc else None
            return c[:3] + (1.0,) if c and root else c

        def publish(el, parts, name):
            im, lay = colour_layers(parts, fixed)
            if im.getbbox():
                out[el] = art.save(im, f"img/ab/{key}_{name}.png")
                ICON_LAYERS[out[el]] = save_layers(art, out[el], lay)
        m = re.search(r"\n  abilityIcons:\n((?:  - .*\n(?:    .*\n)*)*)", text)
        for el_num, fid in re.findall(r"  - element: (\d+)\n    icon: \{fileID: (-?\d+)\}", m.group(1) if m else ""):
            el = ELEMENTS[int(el_num) - 1] if 1 <= int(el_num) <= 4 else None
            if not el or el in out:
                continue
            g = image_sprite(prefabs, path, guid, fid, outer)
            if g and g in pngs:
                if own_icon(key, pngs[g], path):
                    publish(el, [(_fit(pngs[g]), colour_of(fid, root=True) or "white")], el)
                continue
            parts = [(f, pngs[x]) for f, x in image_parts(path, fid) if x in pngs and own_icon(key, pngs[x], path)]
            stems = [os.path.basename(x)[:-4] for _, x in parts]
            # a part whose name extends another's is that part's other state (TrailsFat of Trails): the card at rest has one
            parts = [(f, x) for (f, x), st in zip(parts, stems) if not any(st != o and st.startswith(o) for o in stems)]
            if parts:
                publish(el, [(Image.open(x).convert("RGBA").resize((128, 128), Image.LANCZOS), colour_of(f) or "white")
                             for f, x in parts], el)
        if "Omni" not in out:
            om = re.search(r"omniAbilitySprite: \{fileID: \d+, guid: (\w+)", text)
            ov = [og for tf, tg, pp, og in outer if pp == "omniAbilitySprite"]
            g = ov[0] if ov else (om.group(1) if om else None)
            if g and g in pngs and own_icon(key, pngs[g], path):
                publish("Omni", [(_fit(pngs[g]), omni_tint(key))], "Omni")
    by_dir = {}
    for path in pngs.values():
        m = re.search(r"/AbilityIcons/([^/]+)/(?:[^/]+/)*[^/]+\.png$", path.replace(os.sep, "/"))
        if m:
            by_dir.setdefault(m.group(1), []).append(path)
    for el, label in labels.items():
        if el not in out:
            hit = named_icon(key, label, by_dir)
            if hit and art.enabled:
                im, lay = colour_layers([(_fit(hit), "white")], game_colours())
                out[el] = art.save(im, f"img/ab/{key}_{el}.png")
                ICON_LAYERS[out[el]] = save_layers(art, out[el], lay)
    return out


def omni_entry(key, asset_names):
    """What this hull does with an OMNI crystal: the map's own words, else its crystal effects named."""
    label = ""
    map_path = os.path.join(REPO, f"Assets/Resources/ElementalAbilityMaps/{key}.asset")
    if os.path.exists(map_path):
        m = re.search(r"^  omniAbilityLabel: ?(.*)$", open(map_path, encoding="utf-8").read(), re.M)
        label = unquote(m.group(1)) if m else ""
    effects = []
    container = os.path.join(REPO, f"Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/{key}ImpactorDataContainer.asset")
    if os.path.exists(container):
        m = re.search(r"^  vesselCrystalEffects:\n((?:  - .*\n)*)", open(container, encoding="utf-8").read(), re.M)
        for g in re.findall(r"guid: (\w+)", m.group(1) if m else ""):
            name = os.path.basename(asset_names.get(g, ""))[:-6]
            if name and not GENERIC_CRYSTAL_EFFECTS.search(name):
                effects.append(action_label(re.sub(r"(ByCrystal|Crystal)?Effect$", "", name), key))
    return {"label": label, "effects": effects, "fromMap": bool(label)}


def crew_of(c, note):
    m = re.search(r"(\d+)-seat", note)
    seats = int(m.group(1)) if m else (len(c.get("crew", [])) or 1)
    return {"names": c.get("crew", []), "seats": seats}


def control_art(rows, elements, icons, art):
    """Each controls row's pad art and phone art, as published paths."""
    for r in rows:
        r["padArt"] = art.png(PAD_ART.get(r["pad"]), "ui/pad_" + re.sub(r"\W", "", r["pad"]) + ".png", 64) if r["pad"] in PAD_ART else None
        touch, tart = r.get("touch") or "", None
        g = re.match(r"(Lift (left|right|either|both) thumbs?|Thumbs apart)", touch)
        if g:
            glyph = {"Lift left thumb": "LiftLeftThumb", "Lift right thumb": "LiftRightThumb", "Lift either thumb": "LiftEitherThumb",
                     "Lift both thumbs": "LiftBothThumbs", "Thumbs apart": "ThumbsApart"}.get(g.group(0))
            tart = art.png(f"{TOUCH_DIR}/TouchGlyph_{glyph}.png", f"ui/touch_{glyph}.png", 96) if glyph else None
        elif touch.endswith("card") or " card " in touch:
            tart = icons.get(r.get("element") or "")
        else:
            b = re.match(r"(\w+) button", touch)
            if b:
                tart = art.png(f"{TOUCH_DIR}/TouchIcon_{b.group(1)}.png", f"ui/touch_{b.group(1)}.png", 96)
        r["touchArt"] = tart
    return rows


SEATS = os.path.join(REPO, "Docs", "FleetCodex", "seats.json")


def seats_of(key, crew, elements):
    """The crew vessel's seats from Docs/FleetCodex/seats.json, each ability linked to the map's card when its name is the
    map's AbilityLabel for that element. A seat the class asset does not name, or an element the map lacks, stops the
    build: the table is the one hand-written source here, so it must fail the day it stops matching the game."""
    if not os.path.exists(SEATS):
        return None
    table = json.load(open(SEATS, encoding="utf-8")).get(key)
    if not table:
        return None
    out = []
    for seat in table["seats"]:
        if crew and seat["name"] not in crew:
            sys.exit(f"seats.json: {key} seat '{seat['name']}' is not in the class asset's CrewNames {crew}")
        abilities = []
        for a in seat["abilities"]:
            e = elements.get(a["element"])
            if e is None:
                sys.exit(f"seats.json: {key} seat '{seat['name']}' names {a['element']}, which {key}'s ability map lacks")
            linked = norm(a["name"]) == norm(e["ability"])
            abilities.append({"element": a["element"], "name": a["name"], "text": a.get("text", ""), "linked": linked})
        out.append({"name": seat["name"], "seats": seat.get("seats", 1), "role": seat.get("role", ""),
                    "abilities": abilities})
    return {"source": table.get("source"), "url": GITHUB + table["source"] if table.get("source") else None, "list": out}


ABILITIES = os.path.join(REPO, "Docs", "FleetCodex", "abilities.json")


def ability_words(key, elements, omni):
    """What each ability DOES, from Docs/FleetCodex/abilities.json. The ability map records only what an element scales,
    so this is the second hand-written table (each entry cites the doc or code it was read from). An entry whose ability
    name no longer matches the map's AbilityLabel is dropped with a warning instead of describing a different ability:
    re-read its source and update the table when that warning prints."""
    if not os.path.exists(ABILITIES):
        return
    table = json.load(open(ABILITIES, encoding="utf-8")).get(key, {})
    for el, words in table.items():
        if el.startswith("_"):
            continue
        if el == "Omni":
            if omni is not None:
                omni["words"] = {k: words.get(k, "") for k in ("ability", "does", "trigger", "source")}
            continue
        e = elements.get(el)
        if e is None or norm(words.get("ability", "")) != norm(e["ability"]):
            print(f"abilities.json: {key} {el} describes '{words.get('ability')}', but the map's ability is "
                  f"'{e['ability'] if e else None}': entry dropped, update the table", file=sys.stderr)
            continue
        e["words"] = {k: words.get(k, "") for k in ("does", "trigger", "scales", "source")}
        if words.get("tint"):   # the HUD paints this icon something other than the pilot's domain (the Dolphin's seed)
            e["words"]["tint"] = words["tint"]


TIERS = os.path.join(REPO, "Docs", "FleetCodex", "tiers.json")
CERT_CHECKS = ["Abilities", "Level 5", "Time game", "Mass or Space game", "Charge game"]
CERT_STAGES = ["Arena Ready", "Fleet Ready", "Launch Ready", "In the hangar"]


def element_games(cards):
    """The vessel's ELEMENT games: each arcade card (on the ArcadeGames roster, or on no roster) under its first element,
    the same rule the page uses for 'Its element games'."""
    out = {e: [] for e in ELEMENTS}
    for c in cards:
        els = c.get("elements") or []
        if els and ("ArcadeGames" in (c.get("rosters") or []) or not c.get("rosters")):
            out[els[0]].append(c["name"])
    return out


def certificate(v):
    """The Flight Certificate of a SOLO vessel (Rev 84 A, picked by Shombith 2026-11-10, section 155): five checks, 20 %
    each - every element's ability in the map; the level-5 upgrades, 5 % per wired one; a Time game; a Mass or a Space
    game (either); a Charge game. Launch Ready = the first three with all four upgrades wired (the minimum viable pass),
    Fleet Ready = and the fourth, Arena Ready = all five; below Launch Ready a vessel is In the hangar. Crews and vessels
    without a map get none (None)."""
    if not v["status"]["hasMap"] or (v["crew"] or {}).get("seats", 1) > 1:
        return None
    els, games = v["elements"], element_games(v["cards"])
    abil = all(e in els and not els[e]["open"] for e in ELEMENTS)
    wired = sum(1 for e in ELEMENTS if e in els and els[e]["upgrade"]["status"] == "wired")
    has = {"time": bool(games["Time"]), "ms": bool(games["Mass"] or games["Space"]), "charge": bool(games["Charge"])}
    done = [abil, wired == 4, has["time"], has["ms"], has["charge"]]
    launch = done[0] and done[1] and done[2]
    stage = ("Arena Ready" if launch and done[3] and done[4] else "Fleet Ready" if launch and done[3]
             else "Launch Ready" if launch else "In the hangar")
    detail = ["All four" if abil else f"{sum(1 for e in ELEMENTS if e in els and not els[e]['open'])} of 4",
              f"{wired} of 4 wired", ", ".join(games["Time"]) or "None yet",
              ", ".join(games["Mass"] + games["Space"]) or "None yet", ", ".join(games["Charge"]) or "None yet"]
    return {"pct": 20 * abil + 5 * wired + 20 * sum(has.values()), "stage": stage, "wired": wired,
            "checks": [{"name": n, "done": d, "detail": t} for n, d, t in zip(CERT_CHECKS, done, detail)],
            "next": next((n for n, d in zip(CERT_CHECKS, done) if not d), None)}


def tiers_of(key, cert, games):
    """Each element's tier (S, A, B, C) from Docs/FleetCodex/tiers.json, the one place a person rates a vessel: a letter,
    null for not rated yet, or '-' where the vessel has no game of that element (no letter can be given there; one that is
    is dropped with a warning)."""
    if cert is None:
        return None
    table = json.load(open(TIERS, encoding="utf-8")).get(key, {}) if os.path.exists(TIERS) else {}
    out = {}
    for el in ELEMENTS:
        letter = table.get(el)
        if not games[el]:
            if letter:
                print(f"tiers.json: {key} {el} is rated {letter}, but {key} has no {el} game: dropped", file=sys.stderr)
            out[el] = "-"
        elif letter in ("S", "A", "B", "C"):
            out[el] = letter
        else:
            if letter:
                print(f"tiers.json: {key} {el} '{letter}' is not S, A, B or C: dropped", file=sys.stderr)
            out[el] = None
    return out


def hud_l5_fields():
    """{field: element} for every Sprite a vessel HUD view serializes under a "(<ELEMENT> 5)" header - the art the HUD
    script swaps in itself when that upgrade comes on (the Serpent's five-row pellet slots, "Fifth pellet (TIME 5)")."""
    out = {}
    for path in glob.glob(os.path.join(REPO, "Assets/_Scripts/UI/View/*.cs")):
        el = None
        for line in open(path, encoding="utf-8", errors="replace"):
            h = re.search(r'\[Header\("[^"]*\((Charge|Mass|Space|Time) 5\)"\)\]', line, re.I)
            if h:
                el = h.group(1).capitalize()
                continue
            if "[Header(" in line:
                el = None
            f = re.search(r"\bSprite\s+(\w+)\s*[;=]", line)
            if el and f:
                out[f.group(1)] = el
    return out


def upgrade_art(key, elements, art, pngs_by_dir, png_index=None, l5_fields=None):
    """A level-5 upgrade's own art: the HUD script's level-5 sprite on the vessel prefab, else a file in the hull's
    icon folder named after the upgrade (the Scarab's Snap Dash)."""
    prefab = os.path.join(REPO, f"Assets/_Prefabs/Spacevessels/{key}.prefab")
    text = open(prefab, encoding="utf-8", errors="replace").read() if l5_fields and os.path.exists(prefab) else ""
    for field, el in (l5_fields or {}).items():
        e = elements.get(el)
        m = re.search(rf"^  {field}: \{{fileID: \d+, guid: (\w+)", text, re.M)
        if e and m and not e["upgrade"].get("icon") and png_index and m.group(1) in png_index and art.enabled:
            # the sprite is the card's unlit layout (the HUD lights its pips over it): drawn lit, in the codex's silver,
            # so it reads like the resting icon beside it
            from PIL import Image
            im = Image.open(png_index[m.group(1)]).convert("RGBA")
            im.thumbnail((128, 128), Image.LANCZOS)
            lit = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
            a = im.split()[3]
            lit.paste(Image.merge("RGBA", (a.point(lambda _: 236),) * 3 + (a,)), ((128 - im.width) // 2, (128 - im.height) // 2))
            e["upgrade"]["icon"] = art.save(lit, f"img/ab/{key}_{el}_L5.png")
    for el, e in elements.items():
        if e["upgrade"].get("icon"):
            continue   # the icon kit's level-5 card or the HUD's own level-5 sprite already answered
        label = e["upgrade"]["label"]
        hit = named_icon(key, label, pngs_by_dir) if label else None
        e["upgrade"]["icon"] = art.png(os.path.relpath(hit, REPO), f"ab/{key}_{el}_L5.png") if hit else None


def palette_bytes(path):
    """A colour set's bytes without its TracedColors list. The colour trace (Docs/VesselModels/COLOR_TRACE.md) rewrites
    that list on every set whenever any colour in the game is traced, and no render reads it, so it must not make every
    vessel render again."""
    text = open(path, encoding="utf-8").read()
    return re.sub(r"^  TracedColors:.*\n(?:  - .*\n|    .*\n)*", "", text, flags=re.M).encode()


def domain_images(vessels, preview_by_value, out_dir, render=False):
    """{domain: img/dom/<model>_<Domain>_<hash>.webp} per vessel: the hangar preview rendered in that domain's livery
    by Tools/Build/vessel_model/render_cards.py (its camera, look and the palette's domain colours), for the model the
    class asset's PreviewImage was rendered from (the Orca and Wrasse ship the Sparrow's, the Meerkat the Stoat's). The
    hash covers the FBX, the colour scheme and the renderer, so a changed model renders again. Without --render-domains
    only cached renders are used; a vessel with none keeps its shipped preview for every domain."""
    vm = os.path.join(HERE, "vessel_model")
    sys.path.insert(0, vm)
    try:
        import render_cards as RC
    except Exception as e:
        print(f"  domain renders skipped: {e}", file=sys.stderr)
        return
    by_guid = {}
    for name in RC.VESSELS:
        hang = RC.outputs(name).get("hangar")
        if hang and os.path.exists(hang + ".meta"):
            g = re.search(r"guid: (\w+)", open(hang + ".meta").read())
            if g:
                by_guid[g.group(1)] = name
    tool = hashlib.sha256()
    for f in ("render_cards.py", "vessel_colors.py"):
        tool.update(open(os.path.join(vm, f), "rb").read())
    tool.update(palette_bytes(COLOR_SET))
    dom_dir = os.path.join(out_dir, "img", "dom")
    os.makedirs(dom_dir, exist_ok=True)
    keep = set()
    for value, guid in preview_by_value.items():
        v = next((x for x in vessels if x["id"] == value), None)
        name = by_guid.get(guid)
        if not v or not name:
            continue
        h = tool.copy()
        h.update(open(os.path.join(REPO, RC.VESSELS[name]["fbx"]), "rb").read())
        h = h.hexdigest()[:10]
        found = {}
        for d in DOMAINS:
            rel = f"img/dom/{name}_{d}_{h}.webp"
            path = os.path.join(out_dir, rel)
            if not os.path.exists(path) and render:
                try:
                    render_domain(RC, name, d, path)
                except Exception as e:
                    print(f"  could not render {name} in {d}: {e}", file=sys.stderr)
            if os.path.exists(path):
                found[d] = rel
                keep.add(os.path.basename(path))
        v["domainImages"] = found or None
    for f in os.listdir(dom_dir):   # a stale render (its model changed) is never republished
        if f not in keep and f.endswith(".webp"):
            os.remove(os.path.join(dom_dir, f))


def render_domain(RC, name, domain, path):
    """One hangar preview in a domain's livery, composed exactly as render_cards.compose composes the shipped one."""
    import tempfile
    from PIL import Image, ImageFilter
    sys.path.insert(0, os.path.join(HERE, "vessel_model"))
    import vessel_colors as VC
    cfg = RC.VESSELS[name]
    hw, hh = cfg.get("hangar_size", (960, 540))
    with tempfile.TemporaryDirectory() as tmp:
        files = RC.render_view(cfg, RC.colors_for(VC.scheme(), domain), tmp, {"hangar": (hw, hh, 0.62)},
                               outline=cfg.get("outline", 0.006), samples=12)
        hang = Image.open(files["hangar"]).convert("RGBA").resize((hw, hh), Image.LANCZOS)
    bg = RC._hangar_background(hw, hh, int(RC._guid(name)[:6], 16))
    glow = Image.new("RGBA", (hw, hh), RC.HANGAR_GLOW + (0,))
    glow.putalpha(hang.split()[3].filter(ImageFilter.GaussianBlur(18 * hw / 960)).point(lambda v: int(v * 0.45)))
    bg.alpha_composite(glow)
    bg.alpha_composite(hang)
    bg.convert("RGB").save(path, "WEBP", quality=80, method=6)


def card_art(card_asset, art, pngs, name):
    if not card_asset:
        return None
    m = re.search(r"^  CardBackground: \{fileID: \d+, guid: (\w+)", open(os.path.join(REPO, card_asset), encoding="utf-8").read(), re.M)
    src = pngs.get(m.group(1)) if m else None
    if not src or not art.enabled:
        return None
    from PIL import Image
    rel = "img/cards/" + re.sub(r"\W", "", name) + ".jpg"
    if rel not in art.done:
        im = Image.open(src).convert("RGB")
        im.thumbnail((640, 640), Image.LANCZOS)
        path = os.path.join(art.out, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        im.save(path, quality=78, optimize=True)
        art.done[rel] = path
    return rel


def build(out_dir, images=True, render_domains=False, branch_label=None, pending=()):
    import shutil
    if images and os.path.isdir(os.path.join(out_dir, "img")):
        for entry in os.listdir(os.path.join(out_dir, "img")):   # a removed icon must not be republished;
            if entry != "dom":                                   # img/dom is the domain renders' cache, keyed by hash
                path = os.path.join(out_dir, "img", entry)
                shutil.rmtree(path) if os.path.isdir(path) else os.remove(path)
    enum, roster, cls, rows, cards = class_enum(), toy_roster(), classes(), ability_rows(), card_facts()
    previews = guid_paths(["Assets/_Graphics"], ".png") if images else {}
    asset_names = guid_paths(["Assets/_SO_Assets", "Assets/Resources"], ".asset")
    prefab_index = guid_paths(["Assets"], ".prefab")
    png_index = guid_paths(["Assets"], ".png")
    art = Art(out_dir, enabled=images)
    pngs_by_dir = {}
    for path in png_index.values():
        m = re.search(r"/AbilityIcons/([^/]+)/(?:[^/]+/)*[^/]+\.png$", path.replace(os.sep, "/"))
        if m:
            pngs_by_dir.setdefault(m.group(1), []).append(path)
    ui = {k: art.png(v, "ui/" + k.replace("/", "_") + ".png", 128 if k in UNCROPPED else 96, crop=k not in UNCROPPED)
          for k, v in UI_ART.items()}
    l5_fields = hud_l5_fields()
    bases, by_guid = script_classes()
    atp, touch_rows = touch_profiles()
    vessels = []
    for value in sorted(set(cls) | {v for v, (k, _) in enum.items() if k in rows}):
        key, note = enum.get(value, (str(value), ""))
        c = cls.get(value, {})
        name = c.get("name") or key
        maps = rows.get(key, {})
        elements = {e: element_entry(maps[e]) for e in ELEMENTS if e in maps}
        one_stick = is_one_stick(key, bases, by_guid)
        icons = ability_art(key, art, prefab_index, png_index,
                            {el: e["ability"] for el, e in elements.items() if not e["open"]}) if maps else {}
        for el, e in elements.items():
            e["icon"] = icons.get(el)
            e["shape"] = ui.get("petal/" + el)
            e["upgrade"]["icon"] = icons.get(el + "@L5")
        omni = dict(omni_entry(key, asset_names), icon=icons.get("Omni"), mark=ui.get("omni")) if maps else None
        ability_words(key, elements, omni)
        retired = "RETIRED" in note.upper()
        vessels.append({
            "key": key, "id": value, "name": name,
            "description": c.get("description", ""), "note": note,
            "status": {
                "hasMap": bool(maps), "hasPrefab": os.path.exists(os.path.join(REPO, f"Assets/_Prefabs/Spacevessels/{key}.prefab")),
                "inToybox": key in roster, "retired": retired, "locked": c.get("locked", False),
                "abilities": sum(1 for e in elements.values() if not e["open"]),
                "scaling": sum(1 for e in elements.values() if any(s["on"] for s in e["scaling"])),
                "upgradesWired": sum(1 for e in elements.values() if e["upgrade"]["status"] == "wired"),
            },
            "elements": elements,
            "flight": flight(key) if maps else None,
            "controls": controls(key, elements, bool(one_stick), prefab_bindings(key, asset_names), atp,
                                 touch_rows.get(key)) if maps else None,
            "omni": omni,
            "crew": crew_of(c, note),
            "seats": seats_of(key, c.get("crew", []), elements) if maps else None,
            "cards": sorted(cards.get(name, []) + (cards.get(key, []) if key != name else []), key=lambda x: x["name"]),
            "docs": docs_for(key, name, note),
            "changes": changes(key, c.get("asset"), cards.get(name, []) + (cards.get(key, []) if key != name else [])),
            "image": write_image(c.get("previewGuid"), previews, key, out_dir) if images else None,
            "classAsset": c.get("asset"),
        })
    for v in vessels:
        if v["controls"]:
            icons = {el: e.get("icon") for el, e in v["elements"].items()}
            control_art(v["controls"]["rows"], v["elements"], icons, art)
            # the omni crystal's control: whatever the omni ability fires on, when the hull binds one (the Ring on LT)
        if v["elements"]:
            upgrade_art(v["key"], v["elements"], art, pngs_by_dir, png_index, l5_fields)
        for card in v["cards"]:
            card["art"] = card_art(card.get("asset"), art, png_index, card["name"])
        v["certificate"] = certificate(v)
        v["tiers"] = tiers_of(v["key"], v["certificate"], element_games(v["cards"]))

    def icon_layers(rel):
        if not rel or not art.enabled:
            return None
        if rel in ICON_LAYERS:
            return ICON_LAYERS[rel] or None
        from PIL import Image
        return save_layers(art, rel, split_layers(Image.open(os.path.join(out_dir, rel)))) or None
    def tinted(rel):
        """An icon the HUD paints whole in one colour the scripts compute at runtime (abilities.json `tint`): one mask."""
        from PIL import Image
        return save_layers(art, rel, {"d": split_layers(Image.open(os.path.join(out_dir, rel))).get("d") or
                                           Image.open(os.path.join(out_dir, rel)).convert("RGBA")}) if rel and art.enabled else None
    for v in vessels:
        for e in v["elements"].values():
            tint = (e.get("words") or {}).get("tint")
            if tint and e.get("icon"):
                whole = tinted(e["icon"])
                e["layers"] = whole
                e["upgrade"]["layers"] = icon_layers(e["upgrade"].get("icon")) if e["upgrade"].get("icon") else None
                continue
            e["layers"] = icon_layers(e.get("icon"))
            e["upgrade"]["layers"] = icon_layers(e["upgrade"].get("icon"))
        if v["omni"]:
            v["omni"]["layers"] = icon_layers(v["omni"].get("icon"))
    if images:
        domain_images(vessels, {value: c.get("previewGuid") for value, c in cls.items()}, out_dir, render_domains)
    data = {
        "schema": "cosmic-shore.fleet-codex/2",
        "generated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%MZ"),
        # The tree read: the branch name, or a label for a merged tree ("bleeding-edge + claude/x"). `pending` lists
        # the SHIPPED branches folded in that bleeding-edge does not carry yet (/ship 5.6, /fleet-codex 1): the daily
        # refresh keeps merging each one in until it lands, so a shipped branch's facts are not reverted overnight.
        "branch": branch_label or git("rev-parse", "--abbrev-ref", "HEAD"),
        "pending": sorted(set(pending)),
        "commit": git("rev-parse", "--short", "HEAD"),
        "commitDate": git("log", "-1", "--date=short", "--format=%ad"),
        "github": GITHUB,
        "elements": ELEMENTS,
        "ui": ui,
        "palette": palette(),
        "themes": write_themes(out_dir),
        "vessels": vessels,
    }
    # The content hash ignores the stamp, so a run that found nothing new says so.
    body = json.dumps({k: v for k, v in data.items() if k not in ("generated", "branch", "pending", "commit", "commitDate")},
                      sort_keys=True)
    data["contentHash"] = hashlib.sha256(body.encode()).hexdigest()[:16]
    os.makedirs(out_dir, exist_ok=True)
    with open(os.path.join(out_dir, "fleet.json"), "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=1, ensure_ascii=False)
    return data


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--out", default=DEFAULT_OUT, help="where fleet.json and img/ go (default Docs/FleetCodex/build)")
    ap.add_argument("--no-images", action="store_true", help="skip the hangar previews")
    ap.add_argument("--render-domains", action="store_true",
                    help="render each hull in Jade, Ruby and Gold (render_cards.py's look; needs bpy, about 3 s a render, "
                         "cached in <out>/img/dom by model hash)")
    ap.add_argument("--files-json", action="store_true",
                    help="after building, print the Artifact publish `files` list (paths relative to --out, the root)")
    ap.add_argument("--branch-label", help="the stamp's name for the tree read (default: the checked-out branch)")
    ap.add_argument("--pending", action="append", default=[], metavar="BRANCH",
                    help="a shipped branch folded into this tree that bleeding-edge does not carry yet (repeatable); "
                         "recorded in the stamp so the daily refresh keeps it until it merges")
    ap.add_argument("--allow-off-bleeding-edge", action="store_true",
                    help="build a tree that does not contain origin/bleeding-edge (a local look only: never publish it)")
    a = ap.parse_args()
    why = contains_bleeding_edge()
    if why and not a.allow_off_bleeding_edge:
        sys.exit(f"fleet_codex: refusing to build - {why}")
    data = build(os.path.abspath(a.out), images=not a.no_images, render_domains=a.render_domains,
                 branch_label=a.branch_label, pending=a.pending)
    v = data["vessels"]
    print(f"fleet.json: {len(v)} vessels, {sum(x['status']['abilities'] for x in v)} abilities, "
          f"{sum(x['status']['upgradesWired'] for x in v)} L5 upgrades wired, "
          f"{sum(len(x['cards']) for x in v)} card seats; "
          + ", ".join(f"{sum(1 for x in v if (x.get('certificate') or {}).get('stage') == st)} {st}" for st in CERT_STAGES)
          + f"; content {data['contentHash']} at {data['commit']}")
    print(f"written to {os.path.abspath(a.out)}")
    if a.files_json:
        out = os.path.abspath(a.out)
        files = sorted(os.path.relpath(os.path.join(d, n), out).replace(os.sep, "/")
                       for d, _, ns in os.walk(out) for n in ns)
        print("FILES " + json.dumps([{"path": f} for f in files]))


if __name__ == "__main__":
    main()
