#!/usr/bin/env python3
"""
Author the player AVATAR choices from the whole pilot catalog: one avatar per pilot portrait, cut from it.
Garrett, 2026-11-08: "update the avatar choices to reflect all the pilots"; 2026-11-10: "add our whole catalog of
pilots as avatars". The catalog is the 18 callsign pilots, then the 32 painted pilots (Docs/Pilots/PORTRAITS.md §2,
the old 40-pilot roster's art). This script OWNS:

  Assets/_Graphics/Profile/Pilots/Avatar_<NAME>.png        256x256 RGBA: the close-up of the pilot's scene (a pilot
                                                           without a scene: their signature bust, cut square;
                                                           without a bust: head and shoulders from its portrait
                                                           on a Deep Sea glow)
  Assets/_Graphics/Profile/Pilots/Avatar_<NAME>.png.meta   the old avatar set's importer, deterministic guid
                                                           md5("CosmicShore/PilotAvatar/<NAME>")
  Assets/_SO_Assets/SO_DefaultProfileIcons.asset               the SO_ProfileIconList every avatar picker and
                                                               avatar face reads (PlayerDataService.GetAvatarSprite)

THE SOURCE is the CLOSE-UP OF THE PILOT'S SCENE when the scene is painted (Docs/Pilots/art/scenes/<NAME>.jpg, the
realistic widescreen picture of Tools/Build/pilot_art/scene.py; Garrett 2026-11-11: "the scene close ups are
categorically better than the previous avatar pictures and should be used in their place"): the square that the
pilot's scene record names (`close`: the head, with the second animal's nearest piece when the scene carries one
near it, so both clades show; never smaller than SIZE), resized to SIZE. A pilot whose scene is not
painted yet keeps their SIGNATURE BUST (Docs/Pilots/art/busts/<NAME>.jpg, Garrett 2026-11-10: "pilot images that
really show both clades in the avatar bust"; Tools/Build/pilot_art/creator.py, the /pilot-creator skill): a 1024
square already framed for an avatar, so the avatar is its top-centre square, 4% off each side and 8% off the bottom
(creator.avatar_cut), resized to SIZE. A pilot with neither falls back to the old cut from its portrait
(wired.json's 220x280 sprite for a callsign, the painted PNG otherwise): the square starts just above the top of the
silhouette and centres on the head (the median column of the opaque pixels in its upper half).

THE IDS are the callsign values (CALLSIGNS.md §2: ENSIGN = 1 ... FLUME = 18), the same 1..18 the old avatar set
used, so a profile saved with an old avatar id lands on a pilot instead of falling back to the first face. The
painted pilots follow as 19..50 in PAINTED's order. An id is a saved profile's avatar: never renumber one, only
append (a new painted pilot takes the next id at the END of PAINTED).

Usage:
    python3 Tools/Build/author_pilot_avatar_assets.py             # write
    python3 Tools/Build/author_pilot_avatar_assets.py --check     # diff disk; exit 1 on drift
    python3 Tools/Build/author_pilot_avatar_assets.py --self-test # negative controls
    python3 Tools/Build/author_pilot_avatar_assets.py --sheet out.png   # every avatar on one sheet, writes nothing
"""
import hashlib
import json
import os
import re
import sys

try:                                     # numpy and Pillow are not in every collaborator's Python: the ship gate
    import numpy as np                   # then SKIPs, like pilot_art/author.py
    from PIL import Image
    MISSING = None
except ImportError as _e:                # pragma: no cover - environment dependent
    np = Image = None
    MISSING = str(_e)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))
from author_callsign_assets import CALLSIGNS  # noqa: E402  (value, word, meaning): one row per callsign

WIRED = "Tools/Build/pilot_art/wired.json"
OUT_DIR = "Assets/_Graphics/Profile/Pilots"
META_DONOR = "Assets/_Graphics/Profile/ProfileIcon01.png.meta"     # the old avatar set's importer settings
ICON_LIST = "Assets/_SO_Assets/SO_DefaultProfileIcons.asset"
ICON_LIST_SCRIPT_META = "Assets/_Scripts/ScriptableObjects/SO_ProfileIconList.cs.meta"

SIZE = 256              # the avatars carry their detail (both clades) at 256; the picker, chips and HUD faces
                        # all scale it down (the old portrait cuts were 128)
BUST_DIR = "Docs/Pilots/art/busts"
SCENE_DIR = "Docs/Pilots/art/scenes"
CUT = 88                # the square cut from the 220x280 portrait: head and shoulders
TOP_PAD = 6             # rows of air above the silhouette's top
HEAD_SHARE = 0.55       # the upper share of the cut whose opaque columns place the head
ALPHA_SOLID = 40        # an alpha above this is the pilot, not the matte's fringe
GLOW_INNER = (0x43, 0x4C, 0x89)   # Deep Sea surface-raised (Docs/UI/froglet-tokens.json)
GLOW_OUTER = (0x00, 0x04, 0x1F)   # Deep Sea ground

TOLERANCE_LEVELS = 3    # --check: Lanczos and float maths may move a few values by a level between builds
TOLERANCE_SHARE = 0.01


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8") as f:
        return f.read()


def write_text(rel, text):
    full = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def guid_of(rel_meta):
    return re.search(r"^guid: ([0-9a-f]{32})\s*$", read(rel_meta), re.M).group(1)


def avatar_guid(word):
    return hashlib.md5(f"CosmicShore/PilotAvatar/{word}".encode("utf-8")).hexdigest()


def png_path(word):
    return f"{OUT_DIR}/Avatar_{word}.png"


# The painted pilots (Docs/Pilots/PORTRAITS.md §2), in id order from 19. APPEND ONLY: an id is saved in profiles.
# (name, PNG stem[, cut[, centre column]]): the spares are drawn bigger, so they take a wider cut; Dozer crouches
# with its head at the far left, so its cut is wide and pinned there (both judged on the sheet, 2026-11-10).
PAINTED_DIR = "Assets/_Graphics/Pilots"
PAINTED = [
    ("Astrofoil", "Portrait_Astrofoil_Placeholder"), ("Bushy", "Portrait_Bushy_Placeholder"),
    ("Comet", "Portrait_Comet_Placeholder"), ("Damsel", "Portrait_Damsel_Placeholder"),
    ("Dozer", "Portrait_Dozer_Placeholder", 150, 75), ("Echo", "Portrait_Echo_Placeholder"),
    ("Fiest", "Portrait_Fiest_Placeholder"), ("Flux", "Portrait_Flux_Placeholder"),
    ("Jugger", "Portrait_Jugger_Placeholder"), ("Krush", "Portrait_Krush_Placeholder"),
    ("Longshift", "Portrait_Longshift_Placeholder"), ("Maestro", "Portrait_Maestro_Placeholder"),
    ("Mason", "Portrait_Mason_Placeholder"), ("Reaper", "Portrait_Reaper_Placeholder"),
    ("Riptide", "Portrait_Riptide_Placeholder"), ("Scales", "Portrait_Scales_Placeholder"),
    ("Shrapnel", "Portrait_Shrapnel_Placeholder"), ("Siphon", "Portrait_Siphon_Placeholder"),
    ("Slingshot", "Portrait_Slingshot_Placeholder"), ("Strider", "Portrait_Strider_Placeholder"),
    ("Tenacious", "Portrait_Tenacious_Placeholder"), ("Tender", "Portrait_Tender_Placeholder"),
    ("Tomahawk", "Portrait_Tomahawk_Placeholder"), ("Trace", "Portrait_Trace_Placeholder"),
    ("Spare01", "PilotPortrait_01", 120), ("Spare02", "PilotPortrait_Placeholder01", 120),
    ("Spare03", "PilotPortrait_Placeholder02", 120), ("Spare04", "PilotPortrait_Placeholder03", 120),
    ("Spare05", "PilotPortrait_Placeholder04", 120), ("Spare06", "PilotPortrait_Placeholder05", 120),
    ("Spare07", "PilotPortrait_Placeholder06", 120), ("Spare08", "PilotPortrait_Placeholder07", 120),
]


def bust_of(word):
    rel = f"{BUST_DIR}/{word}.jpg"
    return rel if os.path.exists(os.path.join(ROOT, rel)) else None


def _scene_kit():
    """Tools/Build/pilot_art/scene.py, imported the way the kit imports its own modules (by bare name)."""
    kit = os.path.join(ROOT, "Tools", "Build", "pilot_art")
    if kit not in sys.path:
        sys.path.insert(0, kit)
    import scene
    return scene


def painted_of(word, scenes):
    """A pilot's painted source: (scene jpg, close box) once their scene is painted and its close-up set, else
    (bust jpg,), else None."""
    if word in scenes:
        return tuple(scenes[word])
    return (bust_of(word),) if bust_of(word) else None


def roster():
    """[(id, name, source)]: the callsign pilots (id = callsign value), then the painted pilots. source is
    (scene jpg, close box) when the pilot's scene is painted, (bust jpg,) when they only have a signature bust, else
    (portrait png[, cut[, centre column]])."""
    wired = json.loads(read(WIRED)).get("portraits", {})
    scenes = _scene_kit().avatar_sources()
    out = []
    for value, word, _ in CALLSIGNS:
        if painted_of(word, scenes):
            out.append((value, word, painted_of(word, scenes)))
            continue
        entry = wired.get(f"pilot.callsign.{word.lower()}")
        assert entry, f"{word} has no wired portrait in {WIRED} (run Tools/Build/pilot_art/author.py for it)"
        assert os.path.exists(os.path.join(ROOT, entry["png"])), f"{entry['png']} is missing"
        out.append((value, word, (entry["png"],)))
    for i, (name, stem, *frame) in enumerate(PAINTED):
        if painted_of(name, scenes):
            out.append((len(CALLSIGNS) + 1 + i, name, painted_of(name, scenes)))
            continue
        png = f"{PAINTED_DIR}/{stem}.png"
        assert os.path.exists(os.path.join(ROOT, png)), f"{png} is missing"
        out.append((len(CALLSIGNS) + 1 + i, name, (png, *frame)))
    return out


def cut_box(alpha, cut=CUT, centre=None):
    """(left, top) of the cut x cut square on a portrait's alpha channel (H x W uint8). centre pins the column."""
    h, w = alpha.shape
    solid = alpha > ALPHA_SOLID
    rows = np.where(solid.sum(1) > 2)[0]
    top = max(0, int(rows[0]) - TOP_PAD) if len(rows) else 0
    top = min(top, h - cut)
    cols = np.where(solid[top:top + int(cut * HEAD_SHARE)])[1]
    cx = centre if centre is not None else (int(np.median(cols)) if len(cols) else w // 2)
    left = max(0, min(w - cut, cx - cut // 2))
    return left, top


def glow():
    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float64)
    r = np.clip(np.sqrt((xx - SIZE / 2) ** 2 + (yy - SIZE * 0.43) ** 2) / (SIZE * 0.707), 0, 1)[..., None]
    rgb = np.array(GLOW_INNER) * (1 - r) + np.array(GLOW_OUTER) * r
    return Image.fromarray(np.dstack([rgb.round().astype(np.uint8), np.full((SIZE, SIZE), 255, np.uint8)]), "RGBA")


def render(source):
    """source: (scene jpg, close box), (bust jpg,) or (portrait png[, cut[, centre column]]) as roster() gives it."""
    if source[0].startswith(SCENE_DIR + "/"):
        im = Image.open(os.path.join(ROOT, source[0])).convert("RGB").crop(tuple(source[1]))
        return im.resize((SIZE, SIZE), Image.LANCZOS).convert("RGBA")
    if source[0].startswith(BUST_DIR + "/"):
        im = _scene_kit().creator.avatar_cut(Image.open(os.path.join(ROOT, source[0])).convert("RGB"))
        return im.resize((SIZE, SIZE), Image.LANCZOS).convert("RGBA")
    png, cut, centre = (tuple(source) + (CUT, None))[:3]
    im = Image.open(os.path.join(ROOT, png)).convert("RGBA")
    left, top = cut_box(np.asarray(im)[:, :, 3], cut, centre)
    face = im.crop((left, top, left + cut, top + cut)).resize((SIZE, SIZE), Image.LANCZOS)
    out = glow()
    out.alpha_composite(face)
    return out


def meta_text(word):
    return re.sub(r"^guid: [0-9a-f]{32}$", f"guid: {avatar_guid(word)}", read(META_DONOR), count=1, flags=re.M)


def icon_list_text(rows):
    script = guid_of(ICON_LIST_SCRIPT_META)
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}",
        "  m_Name: SO_DefaultProfileIcons",
        "  m_EditorClassIdentifier: ",
        "  profileIcons:",
    ]
    for value, word, _ in rows:
        lines += [
            f"  - Name: {word}",
            f"    Id: {value}",
            f"    IconSprite: {{fileID: 21300000, guid: {avatar_guid(word)}, type: 3}}",
        ]
    return "\n".join(lines) + "\n"


def write_all():
    rows = roster()
    for _, word, png in rows:
        rel = png_path(word)
        full = os.path.join(ROOT, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        render(png).save(full, optimize=True)
        write_text(rel + ".meta", meta_text(word))
    folder_meta = OUT_DIR + ".meta"
    if not os.path.exists(os.path.join(ROOT, folder_meta)):
        guid = hashlib.md5(b"CosmicShore/PilotAvatar/folder").hexdigest()
        write_text(folder_meta, "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n"
                                "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)
    write_text(ICON_LIST, icon_list_text(rows))
    print(f"wrote {len(rows)} pilot avatars under {OUT_DIR} and {ICON_LIST}")


def check():
    problems = []
    rows = roster()
    for _, word, png in rows:
        rel = png_path(word)
        full = os.path.join(ROOT, rel)
        if not os.path.exists(full):
            problems.append(f"{rel} is missing")
            continue
        disk = np.asarray(Image.open(full).convert("RGBA")).astype(np.int16)
        mine = np.asarray(render(png)).astype(np.int16)
        if disk.shape != mine.shape:
            problems.append(f"{rel} differs from the cut of {png[0]} (size)")
        else:
            diff = np.abs(disk - mine)
            if diff.max() > TOLERANCE_LEVELS or (diff > 0).mean() > TOLERANCE_SHARE:
                problems.append(f"{rel} differs from the cut of {png[0]} (max {int(diff.max())} levels)")
        if not os.path.exists(full + ".meta") or read(rel + ".meta") != meta_text(word):
            problems.append(f"{rel}.meta drifted")
    if not os.path.exists(os.path.join(ROOT, OUT_DIR + ".meta")):
        problems.append(f"{OUT_DIR}.meta is missing")
    if read(ICON_LIST) != icon_list_text(rows):
        problems.append(f"{ICON_LIST} drifted from the roster")
    scene = _scene_kit()
    for _, word, source in rows:        # a scene's close-up is square, inside the picture and never enlarged
        if source[0].startswith(SCENE_DIR + "/"):
            problems += scene.close_errors(word)
    problems += scene.manifest_errors()  # scenes.json is what a screen reads to place a scene: it rides this gate
    return problems


def self_test():
    failures = []
    rows = roster()
    if [v for v, _, _ in rows] != list(range(1, len(CALLSIGNS) + len(PAINTED) + 1)):
        failures.append("avatar ids are not 1..N (callsign values, then the painted pilots)")
    if len({n for _, n, _ in rows}) != len(rows):
        failures.append("two avatars share a name")
    if len({avatar_guid(w) for _, w, _ in rows}) != len(rows):
        failures.append("two avatars share a guid")
    text = icon_list_text(rows)
    if text.count("  - Name: ") != len(CALLSIGNS) + len(PAINTED):
        failures.append("the icon list does not hold one avatar per pilot in the catalog")
    # The cut finds the head: a blob high on the right pulls the square right and up.
    a = np.zeros((280, 220), np.uint8)
    a[60:90, 150:180] = 255
    a[90:280, 120:210] = 255
    left, top = cut_box(a)
    if top != 60 - TOP_PAD or not (left <= 150 and left + CUT >= 180):
        failures.append(f"cut_box missed a synthetic head (left={left}, top={top})")
    # An empty sprite cuts from the top centre instead of crashing.
    left, top = cut_box(np.zeros((280, 220), np.uint8))
    if (left, top) != ((220 - CUT) // 2, 0):
        failures.append(f"cut_box on an empty sprite gave ({left}, {top})")
    # A scene's close-up is cut from its box: a picture that is red inside the box and blue outside gives a red avatar.
    import tempfile
    with tempfile.TemporaryDirectory(dir=os.path.join(ROOT, "Docs", "Pilots", "art")) as tmp:
        probe = Image.new("RGB", (1536, 864), (0, 0, 255))
        probe.paste((255, 0, 0), (400, 100, 700, 400))
        probe.save(os.path.join(tmp, "probe.png"))
        rel = os.path.relpath(os.path.join(tmp, "probe.png"), ROOT).replace(os.sep, "/")
        global SCENE_DIR
        kept, SCENE_DIR = SCENE_DIR, os.path.dirname(rel)
        try:
            got = np.asarray(render((rel, (400, 100, 700, 400))))
        finally:
            SCENE_DIR = kept
        if got.shape != (SIZE, SIZE, 4) or tuple(got[SIZE // 2, SIZE // 2]) != (255, 0, 0, 255) \
                or tuple(got[2, 2]) != (255, 0, 0, 255):
            failures.append("a scene's avatar is not the cut of its close box")
    # Drift is caught: a corrupted list text is not the authored one.
    if icon_list_text(rows).replace("Id: 1\n", "Id: 99\n", 1) == text:
        failures.append("an edited icon list would not read as drift")
    for f in failures:
        print("FAIL", f)
    print("self-test:", "ok" if not failures else f"{len(failures)} failure(s)")
    return 1 if failures else 0


def sheet(out):
    rows = roster()
    s = Image.new("RGBA", (6 * (SIZE + 8), -(-len(rows) // 6) * (SIZE + 8)), (20, 20, 30, 255))
    for i, (_, _, png) in enumerate(rows):
        s.paste(render(png), ((i % 6) * (SIZE + 8) + 4, (i // 6) * (SIZE + 8) + 4))
    s.save(out)
    print("sheet:", out)


def main(argv):
    if MISSING is not None:
        print(f"SKIP: numpy/Pillow not available ({MISSING})")
        return 0
    if "--self-test" in argv:
        return self_test()
    if "--sheet" in argv:
        sheet(argv[argv.index("--sheet") + 1])
        return 0
    if "--check" in argv:
        problems = check()
        for p in problems:
            print("DRIFT", p)
        print("check:", "ok" if not problems else f"{len(problems)} problem(s)")
        return 1 if problems else 0
    write_all()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
