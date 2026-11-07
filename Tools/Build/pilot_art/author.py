#!/usr/bin/env python3
"""Author a pilot portrait through the kit: spec -> matte -> finish -> verify -> sheet -> write -> wire.

    python3 Tools/Build/pilot_art/author.py wyvendo --preview   # matte, finish, gate, sheet; writes nothing under Assets/
    python3 Tools/Build/pilot_art/author.py wyvendo             # ... and write the PNG + .meta (guid kept), wired.json,
                                                                #     and the pilot asset through author_pilot_assets.py
    python3 Tools/Build/pilot_art/author.py wyvendo --check     # drift gate: re-finish from the source, compare to disk
    python3 Tools/Build/pilot_art/author.py --all [--check | --preview]   # every spec under specs/
    python3 Tools/Build/pilot_art/author.py <a> <b> ... --preview --sheet round2 --dump <dir>
                                                                # several specs on one named sheet, the finished
                                                                # sprites also saved into <dir> for a blind test
    python3 Tools/Build/pilot_art/author.py --self-test

A portrait is a spec, specs/<name>.py, whose PORTRAIT names the pilot (callsign, heritage, faction: the brief),
the SOURCE (the captured painting under Docs/Pilots/art/sources/, over the key colour the brief named, or with its
own alpha: key="alpha"), the gates it is excused from (exempt=(("width", "the wings"),)) and the PilotSO it is
wired to (pilot="pilot.callsign.talon", or None). The kit owns:

  * Assets/_Graphics/Pilots/Portrait_<Name>.png        220x280 RGBA, the finished sprite (references/CONTRACT.md)
  * Assets/_Graphics/Pilots/Portrait_<Name>.png.meta   the set's own importer settings with a deterministic guid,
                                                        md5("CosmicShore/PilotArt/<Name>"); written once, never moved
  * Tools/Build/pilot_art/wired.json                   which pilot id shows which portrait (author_pilot_assets.py
                                                        reads it and writes the sprite reference into the pilot asset)
  * Docs/Pilots/art/sheets/<name>.png                  the sheet the owner judges

The source is CAPTURED, not generated here (an image model through the Figma connector, an artist's board): the
painter is not deterministic, the finish is. --check re-runs the matte and the finish from the stored source and
compares pixels with the PNG on disk, so a hand-edited sprite, a stale one, or a changed source reads as drift.
"""
import hashlib
import io
import json
import os
import re
import subprocess
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import brief  # noqa: E402
import finish  # noqa: E402
import matte  # noqa: E402
import measure  # noqa: E402
import sheet as sheet_mod  # noqa: E402
import verify  # noqa: E402

ROOT = measure.ROOT
OUT_DIR = "Assets/_Graphics/Pilots"
SHEETS = sheet_mod.SHEETS
WIRED = "Tools/Build/pilot_art/wired.json"
META_DONOR = "Assets/_Graphics/Pilots/Portrait_Mason_Placeholder.png.meta"   # the set's importer (serializedVersion 13)
PILOT_GENERATOR = "Tools/Build/author_pilot_assets.py"
ROSTER_DIR = "Assets/_SO_Assets/Pilots/Roster"


def guid_for(name):
    return hashlib.md5(f"CosmicShore/PilotArt/{name}".encode("utf-8")).hexdigest()


def spec_names():
    d = os.path.join(HERE, "specs")
    return sorted(f[:-3] for f in os.listdir(d) if f.endswith(".py") and not f.startswith("_"))


def png_path(spec):
    return f"{OUT_DIR}/Portrait_{spec['name']}.png"


def meta_text(spec):
    donor = open(os.path.join(ROOT, META_DONOR), encoding="utf-8").read()
    return re.sub(r"^guid: [0-9a-f]{32}$", f"guid: {guid_for(spec['name'])}", donor, count=1, flags=re.M)


def render(spec):
    """The source through the matte and the finish: (sprite PIL RGBA 220x280, info)."""
    src = os.path.join(ROOT, spec["source"])
    if not os.path.exists(src):
        raise FileNotFoundError(f"{spec['name']}: source {spec['source']} is missing (capture the painting first)")
    key = spec.get("key") or brief.brief_of(spec)["key"]
    rgba, info = matte.matte_file(src, key)
    if spec.get("crop"):
        h, w = rgba.shape[:2]
        l, t, r, b = spec["crop"]
        rgba = rgba[int(t * h):int(b * h), int(l * w):int(r * w)]
    sprite = finish.finish(rgba)
    info.update(key_used=key)
    return sprite, info


def head_png(rel):
    """The PNG at HEAD (the icon kit's lesson 8: BEFORE comes from HEAD, not disk), or None."""
    try:
        data = subprocess.run(["git", "show", f"HEAD:{rel}"], cwd=ROOT, capture_output=True, check=True).stdout
        return Image.open(io.BytesIO(data)).convert("RGBA")
    except (subprocess.CalledProcessError, OSError):
        return None


def current_portrait_png(pilot_id):
    """The PNG the pilot asset references at HEAD, if any (the BEFORE of a wired callsign)."""
    if not pilot_id:
        return None
    word = pilot_id.split(".")[-1].capitalize()
    try:
        text = subprocess.run(["git", "show", f"HEAD:{ROSTER_DIR}/Pilot_{word}.asset"], cwd=ROOT,
                              capture_output=True, check=True, text=True).stdout
    except subprocess.CalledProcessError:
        return None
    m = re.search(r"portrait: \{fileID: \d+, guid: ([0-9a-f]{32}), type: 3\}", text)
    if not m:
        return None
    hit = subprocess.run(["grep", "-rl", "--include=*.png.meta", f"^guid: {m.group(1)}$", os.path.join(ROOT, "Assets")],
                         capture_output=True, text=True).stdout.split("\n")
    hit = [h for h in hit if h]
    return head_png(os.path.relpath(hit[0][:-5], ROOT)) if hit else None


def candidate(spec, sprite, rows, m):
    b = brief.brief_of(spec)
    before = head_png(spec["before"]) if spec.get("before") else current_portrait_png(spec.get("pilot"))
    sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))
    from author_callsign_assets import CALLSIGNS  # noqa: E402
    line = next((meaning for _, word, meaning in CALLSIGNS if word == spec["callsign"].upper()), "")
    return dict(name=spec["name"], sprite=sprite, before=before, person=spec.get("person", spec["name"]),
                callsign=spec["callsign"].upper(), line=line, rows=rows, metrics=m, nearest=sheet_mod.nearest(m),
                heritage=dict(b["lines"])["heritage"], faction=dict(b["lines"])["faction"], state="")


def load_wired():
    path = os.path.join(ROOT, WIRED)
    if not os.path.exists(path):
        return dict(note="which pilot id shows which portrait; written by Tools/Build/pilot_art/author.py, read by "
                         "author_pilot_assets.py", portraits={})
    return json.load(open(path, encoding="utf-8"))


def save_wired(data):
    with open(os.path.join(ROOT, WIRED), "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=1, sort_keys=True)
        f.write("\n")


def write(spec, sprite):
    rel = png_path(spec)
    full = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    sprite.save(full)
    meta = full + ".meta"
    if not os.path.exists(meta):
        with open(meta, "w", encoding="utf-8", newline="\n") as f:
            f.write(meta_text(spec))
    data = load_wired()
    if spec.get("pilot"):
        data["portraits"][spec["pilot"]] = dict(png=rel, guid=guid_for(spec["name"]), spec=spec["name"])
    save_wired(data)
    return rel


def wire():
    """Run the pilot generator so each wired callsign's asset references its portrait."""
    r = subprocess.run([sys.executable, os.path.join(ROOT, PILOT_GENERATOR)], cwd=ROOT, capture_output=True, text=True)
    print(r.stdout.strip().split("\n")[-1] if r.stdout.strip() else "", r.stderr.strip())
    return r.returncode


def check(spec, sprite):
    """Drift: the sprite on disk equals the re-finished source pixel for pixel; the meta carries the kit's guid;
    the wiring record names this portrait when the spec names a pilot."""
    problems = []
    rel = png_path(spec)
    full = os.path.join(ROOT, rel)
    if not os.path.exists(full):
        problems.append(f"{rel} is missing")
    else:
        disk = np.asarray(Image.open(full).convert("RGBA"))
        if disk.shape != np.asarray(sprite).shape or not np.array_equal(disk, np.asarray(sprite)):
            problems.append(f"{rel} differs from the finish of {spec['source']}")
    meta = full + ".meta"
    if not os.path.exists(meta):
        problems.append(f"{rel}.meta is missing")
    elif f"guid: {guid_for(spec['name'])}" not in open(meta, encoding="utf-8").read():
        problems.append(f"{rel}.meta does not carry the kit's guid")
    if spec.get("pilot"):
        entry = load_wired()["portraits"].get(spec["pilot"])
        if not entry or entry.get("png") != rel:
            problems.append(f"{WIRED} does not wire {spec['pilot']} to {rel}")
    return problems


def run(names, mode, sheet_name=None, dump=None):
    """names: the specs; mode: preview | write | check; sheet_name: the sheet's stem under Docs/Pilots/art/sheets/
    (default: the one name, or `round`); dump: a folder the finished sprites are also saved into, so a round can be
    looked at or blind-tested (SKILL.md 3.3) before anything under Assets/ changes."""
    failures = 0
    cands = []
    if dump:
        os.makedirs(dump, exist_ok=True)
    for name in names:
        spec = brief.load_spec(name)
        try:
            sprite, info = render(spec)
        except FileNotFoundError as e:
            print("MISSING:", e)
            failures += 1
            continue
        if dump:
            sprite.save(os.path.join(dump, f"Portrait_{spec['name']}.png"))
        ok, rows, m = verify.verify(np.asarray(sprite, dtype=np.float32) / 255.0, spec.get("exempt", ()))
        fails = [f"{n}{' (exempt: ' + r + ')' if r else ''}" for n, p, r in rows if not p]
        print(f"{name:<12} key {info['key_used']} spread {info['spread']:.3f}  gates {'PASS' if ok else 'FAIL'}"
              + (f"  [{', '.join(fails)}]" if fails else ""))
        if mode == "check":
            problems = check(spec, sprite)
            for p in problems:
                print("DRIFT:", p)
            failures += len(problems) + (0 if ok else 1)
            continue
        cands.append(candidate(spec, sprite, rows, m))
        if not ok:
            failures += 1
            print(f"  not written: {name} fails a gate (exempt it in the spec with a reason, or fix the source)")
            continue
        if mode == "write":
            print("  wrote", write(spec, sprite))
    if cands:
        out = os.path.join(ROOT, SHEETS, (sheet_name or (names[0] if len(names) == 1 else "round")) + ".png")
        sheet_mod.sheet(cands, out, title=f"Pilot portraits: {', '.join(c['person'] for c in cands)}")
        print("sheet", os.path.relpath(out, ROOT))
    if mode == "write" and any(brief.load_spec(n).get("pilot") for n in names):
        failures += wire()
    return 1 if failures else 0


def self_test():
    failures = []
    if guid_for("Wyvendo") != guid_for("Wyvendo") or guid_for("Wyvendo") == guid_for("Rosh"):
        failures.append("guid_for is not deterministic or not distinct")
    if len(guid_for("Wyvendo")) != 32:
        failures.append("guid_for is not a 32-hex guid")
    mt = meta_text(dict(name="Wyvendo"))
    if f"guid: {guid_for('Wyvendo')}" not in mt or "spriteMode: 1" not in mt or "alphaIsTransparency: 1" not in mt:
        failures.append("meta_text does not carry the set's importer with the kit's guid")
    try:
        render(dict(name="Nobody", source="Docs/Pilots/art/sources/nobody.png", callsign="TALON",
                    heritage=("Lynx", "Jackal"), faction="Hunter"))
        failures.append("a missing source was not refused")
    except FileNotFoundError:
        pass
    # check() reports a tampered sprite, a missing meta and a missing wire, on a scratch spec.
    import tempfile
    spec = dict(name="SelfTest", pilot="pilot.callsign.talon", callsign="TALON", heritage=("Lynx", "Jackal"),
                faction="Hunter", source="x")
    sprite = Image.new("RGBA", measure.SIZE, (10, 20, 30, 255))
    global OUT_DIR, WIRED
    keep = (OUT_DIR, WIRED)
    with tempfile.TemporaryDirectory(dir=ROOT) as tmp:
        OUT_DIR = os.path.relpath(tmp, ROOT)
        WIRED = os.path.relpath(os.path.join(tmp, "wired.json"), ROOT)
        try:
            if "is missing" not in " ".join(check(spec, sprite)):
                failures.append("check() did not report a missing sprite")
            write(spec, sprite)
            if check(spec, sprite):
                failures.append(f"check() reports drift right after write(): {check(spec, sprite)}")
            tampered = sprite.copy()
            tampered.putpixel((0, 0), (99, 99, 99, 255))
            if not any("differs" in p for p in check(spec, tampered)):
                failures.append("check() did not report a tampered sprite")
            data = load_wired()
            if data["portraits"]["pilot.callsign.talon"]["guid"] != guid_for("SelfTest"):
                failures.append("write() did not record the wire")
        finally:
            OUT_DIR, WIRED = keep
    for f in failures:
        print("SELF-TEST FAIL:", f)
    print("self-test:", "FAILED" if failures else "OK (7 checks)")
    return 1 if failures else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    def option(flag):
        return argv[argv.index(flag) + 1] if flag in argv and argv.index(flag) + 1 < len(argv) else None

    taken = {option("--sheet"), option("--dump")}
    names = [a for a in argv if not a.startswith("--") and a not in taken]
    mode = "check" if "--check" in argv else "preview" if "--preview" in argv else "write"
    if "--all" in argv or (not names and mode == "check"):      # a bare --check is the ship gate: every spec
        names = spec_names()
    if not names:
        print(__doc__)
        return 2
    return run(names, mode, sheet_name=option("--sheet"), dump=option("--dump"))


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
