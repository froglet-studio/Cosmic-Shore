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


def build(out_dir, images=True):
    enum, roster, cls, rows, cards = class_enum(), toy_roster(), classes(), ability_rows(), card_facts()
    previews = guid_paths(["Assets/_Graphics"], ".png") if images else {}
    asset_names = guid_paths(["Assets/_SO_Assets", "Assets/Resources"], ".asset")
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
        retired = "RETIRED" in note.upper()
        vessels.append({
            "key": key, "id": value, "name": name, "crew": c.get("crew", []),
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
            "cards": sorted(cards.get(name, []) + (cards.get(key, []) if key != name else []), key=lambda x: x["name"]),
            "docs": docs_for(key, name, note),
            "changes": changes(key),
            "image": write_image(c.get("previewGuid"), previews, key, out_dir) if images else None,
            "classAsset": c.get("asset"),
        })
    data = {
        "schema": "cosmic-shore.fleet-codex/1",
        "generated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%MZ"),
        "branch": git("rev-parse", "--abbrev-ref", "HEAD"),
        "commit": git("rev-parse", "--short", "HEAD"),
        "commitDate": git("log", "-1", "--date=short", "--format=%ad"),
        "github": GITHUB,
        "elements": ELEMENTS,
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
        files = ["fleet.json"] + sorted(f"img/{n}" for n in os.listdir(os.path.join(out, "img"))
                                        if n.endswith(".webp")) if os.path.isdir(os.path.join(out, "img")) else ["fleet.json"]
        print("FILES " + json.dumps([{"path": f} for f in files]))


if __name__ == "__main__":
    main()
