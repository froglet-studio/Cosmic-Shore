#!/usr/bin/env python3
"""
The Fleet Codex: every vessel's abilities, element scaling, level-5 upgrades, controls, flight numbers, game
modes, docs and recent changes, read from the SHIPPED assets and code, written as the data the Fleet Codex
artifact draws (Docs/FleetCodex/README.md).

    python3 Tools/Build/fleet_codex.py [--out DIR] [--no-images] [--files-json]

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

DEFAULT_OUT = os.path.join(REPO, "Docs", "FleetCodex", "build")
GITHUB = "https://github.com/froglet-studio/Cosmic-Shore/blob/bleeding-edge/"
ELEMENTS = ["Charge", "Mass", "Space", "Time"]   # the HUD's order (LOCKED four-icon row)
CHANGES_PER_VESSEL = 8


def read(rel):
    return vmc.read(rel)


def git(*args):
    r = subprocess.run(["git", "-C", REPO, *args], capture_output=True, text=True)
    return r.stdout.strip()


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
        raw = raw[1:-1].replace("''", "'") if raw[0] == "'" else raw[1:-1]
    return raw


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
    out = {}
    for c in vmc.cards():
        facts = extra.get(c["name"], {})
        for hull in c["hulls"]:
            out.setdefault(hull, []).append({
                "name": c["name"], "mode": c["mode"], "rosters": c["lists"], "hullsOnCard": len(c["hulls"]),
                **facts})
    return out


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


def changes(key):
    paths = vessel_paths(key)
    if not paths:
        return []
    log = git("log", f"-n{CHANGES_PER_VESSEL}", "--no-merges", "--date=short", "--format=%h%x1f%ad%x1f%s", "--", *paths)
    out = []
    for line in log.splitlines():
        sha, date, subject = (line.split("\x1f") + ["", "", ""])[:3]
        out.append({"sha": sha, "date": date, "subject": subject})
    return out


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
    inv_input = {v: k for k, v in (atp.INPUT.items() if atp else [])}
    inv_el = {v: k for k, v in (atp.ELEMENT.items() if atp else [])}

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
    # the element marks: the Codex's shapes, never the HUD petal ("check codex for that")
    "shape/Charge": "Assets/_Graphics/ElementShapes/charge_pentagon_label.png",
    "shape/Mass": "Assets/_Graphics/ElementShapes/mass_triangle_label.png",
    "shape/Space": "Assets/_Graphics/ElementShapes/space_kite_label.png",
    "shape/Time": "Assets/_Graphics/ElementShapes/time_diamond_label.png",
    "omni": "Assets/_Graphics/ElementIcons/OmniCrystal_Active.png",
    # the HUD's level-5 look: the bloom AbilityLockupView puts behind an upgraded card (AbilityLockupStyle.bloomSprite)
    "bloom": "Assets/_Graphics/Design Assests/HUD UI/AbilityLockup/LockupBloom.png",
}
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

    def png(self, src, rel, box=128):
        if not self.enabled or not src or not os.path.exists(os.path.join(REPO, src)):
            return None
        rel = "img/" + rel
        if rel not in self.done:
            from PIL import Image
            im = Image.open(os.path.join(REPO, src)).convert("RGBA")
            bb = im.getbbox()
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
    """The sprites of an Image's visible child images (the cards the HUD builds from parts), in draw order."""
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
                    out.append(sp.group(2))
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
    return None


L5_SHIELDED = (104, 164, 255, 255)   # the HUD's shielded signal (states.SHIELDED["jade"]): an L5 that shields reads as one


def lockup_cards(key):
    """The icon kit's resting cards (spec LOCKUP), rendered in silver: the HUD's card as it sits, parts and all."""
    spec_path = os.path.join(HERE, "icon_kit", "specs", key.lower() + ".py")
    if not os.path.exists(spec_path):
        return {}
    try:
        import importlib
        sys.path.insert(0, os.path.join(HERE, "icon_kit"))
        import states
        spec = importlib.import_module("specs." + key.lower())
        silver = {k: (236, 236, 236, 255) for k in states.domain_colours()}
        shielded = getattr(states, "SHIELDED", None)
        if isinstance(shielded, dict):
            for k in list(shielded):
                shielded[k] = (190, 190, 190, 255)
        out = {}
        for el in ELEMENTS + ["Omni"]:
            sib = getattr(spec, "LOCKUP", {}).get(el)
            if sib:
                out[el] = states.compose(sib["state"], sib["parts"], next(iter(silver)), silver)
        # The level-5 card: the state the kit draws for "<Element> 5" on that element's own card (the picked card, not
        # an options group) - what the HUD swaps the card's parts to when the upgrade comes on (the Tiger Moth's gilded
        # keys, the Sparrow's shielded rounds). Shielded parts keep the HUD's shielded signal so the change reads.
        signal = L5_SHIELDED
        for c in getattr(spec, "CARDS", []):
            el = c.get("element")
            if c.get("group") or el not in ELEMENTS or f"{el}@L5" in out:
                continue
            for st in c.get("states", []):
                if re.search(rf"(?<!\d){el} 5(?!\d)", st[0]):
                    states.SHIELDED.update({k: signal for k in states.SHIELDED})
                    out[f"{el}@L5"] = states.compose(st[1], c["parts"], next(iter(silver)), silver)
                    states.SHIELDED.update({k: (190, 190, 190, 255) for k in states.SHIELDED})
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


def ability_art(key, art, prefabs, pngs, labels=None):
    """element -> published icon path, plus 'Omni'. Kit card, else the HUD's sprite (own HUD or own art), else its
    visible parts, else the hull's own icon named after the ability."""
    out = {}
    labels = labels or {}
    for el, im in lockup_cards(key).items():
        name = el.replace("@L5", "_L5")
        out[el] = art.save(im, f"img/ab/{key}_{name}.png") if art.enabled else None
    hud = hud_of(key, prefabs)
    if hud:
        path, guid, outer = hud
        text, _ = unity_docs(path)
        m = re.search(r"\n  abilityIcons:\n((?:  - .*\n(?:    .*\n)*)*)", text)
        for el_num, fid in re.findall(r"  - element: (\d+)\n    icon: \{fileID: (-?\d+)\}", m.group(1) if m else ""):
            el = ELEMENTS[int(el_num) - 1] if 1 <= int(el_num) <= 4 else None
            if not el or el in out:
                continue
            g = image_sprite(prefabs, path, guid, fid, outer)
            if g and g in pngs:
                if own_icon(key, pngs[g], path):
                    out[el] = art.png(os.path.relpath(pngs[g], REPO), f"ab/{key}_{el}.png")
                continue
            parts = [pngs[x] for x in image_parts(path, fid) if x in pngs and own_icon(key, pngs[x], path)]
            stems = [os.path.basename(x)[:-4] for x in parts]
            # a part whose name extends another's is that part's other state (TrailsFat of Trails): the card at rest has one
            parts = [x for x, s in zip(parts, stems) if not any(s != o and s.startswith(o) for o in stems)]
            if parts and art.enabled:
                from PIL import Image
                canvas = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
                for x in parts:
                    im = Image.open(x).convert("RGBA").resize((128, 128), Image.LANCZOS)
                    canvas.alpha_composite(im)
                out[el] = art.save(canvas, f"img/ab/{key}_{el}.png")
        if "Omni" not in out:
            om = re.search(r"omniAbilitySprite: \{fileID: \d+, guid: (\w+)", text)
            ov = [og for tf, tg, pp, og in outer if pp == "omniAbilitySprite"]
            g = ov[0] if ov else (om.group(1) if om else None)
            if g and g in pngs and own_icon(key, pngs[g], path):
                out["Omni"] = art.png(os.path.relpath(pngs[g], REPO), f"ab/{key}_Omni.png")
    by_dir = {}
    for path in pngs.values():
        m = re.search(r"/AbilityIcons/([^/]+)/(?:[^/]+/)*[^/]+\.png$", path.replace(os.sep, "/"))
        if m:
            by_dir.setdefault(m.group(1), []).append(path)
    for el, label in labels.items():
        if el not in out:
            hit = named_icon(key, label, by_dir)
            if hit:
                out[el] = art.png(os.path.relpath(hit, REPO), f"ab/{key}_{el}.png")
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


def build(out_dir, images=True):
    import shutil
    if images and os.path.isdir(os.path.join(out_dir, "img")):
        shutil.rmtree(os.path.join(out_dir, "img"))   # a removed icon must not be republished
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
    ui = {k: art.png(v, "ui/" + k.replace("/", "_") + ".png", 96) for k, v in UI_ART.items()}
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
            e["shape"] = ui.get("shape/" + el)
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
            "changes": changes(key),
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
    data = {
        "schema": "cosmic-shore.fleet-codex/2",
        "generated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%MZ"),
        "branch": git("rev-parse", "--abbrev-ref", "HEAD"),
        "commit": git("rev-parse", "--short", "HEAD"),
        "commitDate": git("log", "-1", "--date=short", "--format=%ad"),
        "github": GITHUB,
        "elements": ELEMENTS,
        "ui": ui,
        "vessels": vessels,
    }
    # The content hash ignores the stamp, so a run that found nothing new says so.
    body = json.dumps({k: v for k, v in data.items() if k not in ("generated", "branch", "commit", "commitDate")},
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
    ap.add_argument("--files-json", action="store_true",
                    help="after building, print the Artifact publish `files` list (paths relative to --out, the root)")
    a = ap.parse_args()
    data = build(os.path.abspath(a.out), images=not a.no_images)
    v = data["vessels"]
    print(f"fleet.json: {len(v)} vessels, {sum(x['status']['abilities'] for x in v)} abilities, "
          f"{sum(x['status']['upgradesWired'] for x in v)} L5 upgrades wired, "
          f"{sum(len(x['cards']) for x in v)} card seats; content {data['contentHash']} at {data['commit']}")
    print(f"written to {os.path.abspath(a.out)}")
    if a.files_json:
        out = os.path.abspath(a.out)
        files = sorted(os.path.relpath(os.path.join(d, n), out).replace(os.sep, "/")
                       for d, _, ns in os.walk(out) for n in ns)
        print("FILES " + json.dumps([{"path": f} for f in files]))


if __name__ == "__main__":
    main()
