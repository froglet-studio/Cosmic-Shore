#!/usr/bin/env python3
"""
Bring the pilot SCENES into the game: the widescreen picture of each callsign's painted pilot, shown full screen on
that pilot's profile page in the Ark (Garrett, 2026-11-12: "use the full size art for each pilot's profile page in
the ark"). This script OWNS:

  Assets/Resources/Pilots/Scenes/<WORD>.jpg          a byte copy of the scene (Docs/Pilots/art/scenes/<WORD>.jpg)
  Assets/Resources/Pilots/Scenes/<WORD>.jpg.meta     its importer; the guid is deterministic,
                                                     md5("CosmicShore/PilotScene/<WORD>")
  Assets/Resources/Pilots/Scenes.meta                the folder
  Assets/_SO_Assets/Pilots/PilotSceneCatalog.asset   the PilotSceneCatalogSO the Ark's Pilots tab and the page read
  Assets/_SO_Assets/Pilots/PilotSceneCatalog.asset.meta

WHICH SCENES. One per CALLSIGN (author_callsign_assets.CALLSIGNS), because a crew holds one pilot per callsign and a
callsign's art is its first painted pilot (Docs/Pilots/PORTRAITS.md §6, decision 1). The other painted pilots are in
nobody's crew, so their scenes stay in Docs/Pilots/art/scenes/ (their close-ups are avatar choices). A callsign whose
scene is not painted is left out, and its page shows the words alone.

WHY UNDER Resources, AND NAMED BY PATH. A scene is 1536x864. Referenced from a prefab, all of them would load with
the menu; named by path (the catalog's `resource`), the page loads the one it shows and lets it go again
(PilotSceneImage). The catalog references only the close-ups, which are the avatars the profile already holds
(author_pilot_avatar_assets.py; this script reads their guids from disk and never writes them).

THE IMPORTER is the card backgrounds' (author_card_backgrounds.texture_meta: a Sprite, no mip maps, clamped), with
three differences. Standalone asks for BC7, because the page shows the picture across the whole screen and DXT1
bands its skies. Android keeps the project's default instead of the cards' ETC2 override. WebGL keeps a 32 px
stand-in: everything under Resources ships in every build, and the browser Toybox never opens the Ark (ScreenSwitcher
never builds the page there), so the browser download does not carry pictures it cannot show. Whoever opens the Ark
in a browser build lifts that override here (WEBGL_STAND_IN) and in PilotSceneAssetTests.

Usage:
    python3 Tools/Build/author_pilot_scene_assets.py             # write
    python3 Tools/Build/author_pilot_scene_assets.py --check     # compare with disk; exit 1 on drift
    python3 Tools/Build/author_pilot_scene_assets.py --self-test # negative controls
"""
import copy
import hashlib
import json
import os
import re
import shutil
import struct
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))
from author_callsign_assets import CALLSIGNS  # noqa: E402  (value, word, meaning): one row per callsign

MANIFEST = "Docs/Pilots/art/scenes/scenes.json"      # Tools/Build/pilot_art/scene.py manifest writes it
SCENE_DIR = "Assets/Resources/Pilots/Scenes"
RESOURCE_DIR = "Pilots/Scenes"                       # SCENE_DIR as Resources.Load names it
CATALOG = "Assets/_SO_Assets/Pilots/PilotSceneCatalog.asset"
CATALOG_SCRIPT_META = "Assets/_Scripts/ScriptableObjects/PilotsAndArtifacts/PilotSceneCatalogSO.cs.meta"
AVATAR = "Assets/_Graphics/Profile/Pilots/Avatar_{word}.png"     # author_pilot_avatar_assets.py owns these
PEOPLE = "Docs/Pilots/art/scenes/people.json"         # the painted pilots as people: the starter crew (authored)
CREW = "Assets/Resources/Pilots/PaintedCrew.asset"    # PaintedCrewSO.ResourcePath
CREW_SCRIPT_META = "Assets/_Scripts/ScriptableObjects/PilotsAndArtifacts/PaintedCrewSO.cs.meta"
SPECIES_CS = "Assets/_Scripts/Data/Inventory/AnimalSpecies.cs"
FACTION_TYPES_CS = "Assets/_Scripts/Data/Voyages/VoyageEnums.cs"
FACTIONS_CS = "Assets/_Scripts/Data/Inventory/FactionCatalog.cs"

WEBGL_STAND_IN = 32      # px: the longest side a browser build keeps (see the module docstring)
BC7 = 25                 # TextureImporterFormat.BC7
SIDES = {"left": 0, "centre": 1, "center": 1, "right": 2}        # PilotSceneSide
PLAIN_NAME = re.compile(r"^[A-Za-z][A-Za-z \-]*[A-Za-z]$")       # a name YAML takes without quotes


def read(rel, root=ROOT):
    with open(os.path.join(root, rel), encoding="utf-8") as f:
        return f.read()


def read_bytes(rel, root=ROOT):
    with open(os.path.join(root, rel), "rb") as f:
        return f.read()


def write_bytes(rel, data, root=ROOT):
    full = os.path.join(root, rel)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "wb") as f:
        f.write(data)


def guid_of(rel_meta):
    return re.search(r"^guid: ([0-9a-f]{32})\s*$", read(rel_meta), re.M).group(1)


def minted(key):
    return hashlib.md5(f"CosmicShore/PilotScene/{key}".encode("utf-8")).hexdigest()


def f32(x):
    """Unity's float spelling: float32 shortest round-trip, integers bare (author_ui_kit_prefabs.f32)."""
    v = struct.unpack("<f", struct.pack("<f", float(x)))[0]
    if v == int(v) and abs(v) < 1e15:
        return str(int(v))
    for p in range(6, 12):
        s = f"{v:.{p}g}"
        if struct.unpack("<f", struct.pack("<f", float(s)))[0] == v:
            return s
    return repr(v)


def jpeg_size(data):
    """(width, height) from a JPEG's frame header, or None when the bytes are not a JPEG."""
    if data[:2] != b"\xff\xd8":
        return None
    i = 2
    while i + 9 < len(data):
        if data[i] != 0xFF:
            return None
        marker = data[i + 1]
        if marker == 0xFF:                       # fill byte
            i += 1
            continue
        if 0xD0 <= marker <= 0xD9 or marker == 0x01:
            i += 2                               # no payload
            continue
        length = struct.unpack(">H", data[i + 2:i + 4])[0]
        if 0xC0 <= marker <= 0xCF and marker not in (0xC4, 0xC8, 0xCC):
            height, width = struct.unpack(">HH", data[i + 5:i + 9])
            return width, height
        i += 2 + length
    return None


def face_rect(close, size):
    """A scene's close box (left, top, right, bottom in pixels from the top left) as a Unity Rect in 0..1 from the
    bottom left: (x, y, width, height)."""
    left, top, right, bottom = close
    w, h = size
    return left / w, 1 - bottom / h, (right - left) / w, (bottom - top) / h


def rows(manifest=None):
    """[(value, word, scene)] for every callsign whose scene is painted, in callsign order."""
    scenes = (manifest if manifest is not None else json.loads(read(MANIFEST)))["scenes"]
    return [(value, word, scenes[word]) for value, word, _ in CALLSIGNS if word in scenes]


def unpainted(manifest=None):
    scenes = (manifest if manifest is not None else json.loads(read(MANIFEST)))["scenes"]
    return [word for _, word, _ in CALLSIGNS if word not in scenes]


def scene_path(word):
    return f"{SCENE_DIR}/{word}.jpg"


def texture_meta(guid):
    """The card backgrounds' Sprite importer (author_card_backgrounds.texture_meta) with BC7 on Standalone, the
    project's default on Android and a WEBGL_STAND_IN px stand-in on WebGL."""
    platforms = "".join(f"""  - serializedVersion: 3
    buildTarget: {target}
    maxTextureSize: {size}
    resizeAlgorithm: 0
    textureFormat: {fmt}
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: {over}
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
""" for target, size, fmt, over in [("DefaultTexturePlatform", 2048, -1, 0), ("Standalone", 2048, BC7, 1),
                                    ("Server", 2048, -1, 0), ("Android", 2048, -1, 0), ("iPhone", 2048, -1, 0),
                                    ("WebGL", WEBGL_STAND_IN, -1, 1)])
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  cookieLightType: 0
  platformSettings:
{platforms}  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  spritePackingTag:
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def folder_meta(guid):
    return ("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)


def asset_meta(guid):
    return ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
            "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)


def catalog_text(entries, script_guid):
    """entries: [(value, painted name, resource, close-up guid, (x, y, width, height), side)]."""
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
        f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}",
        "  m_Name: PilotSceneCatalog",
        "  m_EditorClassIdentifier: ",
        "  scenes:" if entries else "  scenes: []",
    ]
    for value, painted, resource, close_up, (x, y, w, h), side in entries:
        lines += [
            f"  - callsign: {value}",
            f"    painted: {painted}",
            f"    resource: {resource}",
            f"    closeUp: {{fileID: 21300000, guid: {close_up}, type: 3}}",
            "    face:",
            "      serializedVersion: 2",
            f"      x: {f32(x)}",
            f"      y: {f32(y)}",
            f"      width: {f32(w)}",
            f"      height: {f32(h)}",
            f"    side: {side}",
        ]
    return "\n".join(lines) + "\n"


def entry_of(value, word, scene, source):
    """One catalog row, with the faults a scene record can carry raised by name."""
    size = jpeg_size(source)
    assert size is not None, f"{scene['file']} is not a JPEG"
    assert list(size) == list(scene["size"]), f"{scene['file']} is {size}, its record says {scene['size']}"
    left, top, right, bottom = scene["close"]
    assert 0 <= left < right <= size[0] and 0 <= top < bottom <= size[1], \
        f"{word}'s close-up {scene['close']} is not inside its {size[0]}x{size[1]} scene"
    assert scene["side"] in SIDES, f"{word}'s side is '{scene['side']}'; a scene stands left, centre or right"
    # A page puts its words on the side the pilot does not stand on: a side that disagrees with the face covers it.
    middle = (left + right) / 2 / size[0]
    assert SIDES[scene["side"]] == 1 or (middle < 0.5) == (SIDES[scene["side"]] == 0), \
        f"{word}'s side is '{scene['side']}' but the close-up's middle is {middle:.2f} of the way across"
    name = scene.get("name", "")
    assert not name or PLAIN_NAME.match(name), f"{word}'s painted name '{name}' needs quoting; extend catalog_text"
    avatar_meta = AVATAR.format(word=word) + ".meta"
    assert os.path.exists(os.path.join(ROOT, avatar_meta)), \
        f"{avatar_meta} is missing (run Tools/Build/author_pilot_avatar_assets.py)"
    return (value, name, f"{RESOURCE_DIR}/{word}", guid_of(avatar_meta), face_rect(scene["close"], size),
            SIDES[scene["side"]])


def enum_values(rel, enum):
    """{member: value} of a C# enum whose members are written `Name = 3,`."""
    body = re.search(r"enum " + enum + r"\s*\{(.*?)\}", read(rel), re.S).group(1)
    return {m.group(1): int(m.group(2)) for m in re.finditer(r"^\s*([A-Za-z]+)\s*=\s*(\d+)", body, re.M)}


def founding_names():
    """{faction type: [founding names]} from FactionCatalog.cs."""
    return {m.group(1): re.findall(r'"([^"]+)"', m.group(2))
            for m in re.finditer(r"\[FactionType\.([A-Za-z]+)\] = new\[\] \{([^}]*)\}", read(FACTIONS_CS))}


def yaml_text(s):
    """A YAML double-quoted scalar: what Unity reads back as the same string, line breaks included."""
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n") + '"'


PRINTABLE = re.compile(r"^[ -~]*$")


def crew_rows(people=None, manifest=None):
    """[(callsign value, person)] for every painted pilot whose scene is in the game, with each fault named."""
    people = (people if people is not None else json.loads(read(PEOPLE)))["people"]
    species, types, factions = enum_values(SPECIES_CS, "AnimalSpecies"), enum_values(FACTION_TYPES_CS, "FactionType"), founding_names()
    out = []
    for value, word, scene in rows(manifest):
        p = people.get(word)
        assert p, f"{word}'s scene is in the game but {PEOPLE} has no person for it: its page would show a stranger"
        name = f"{p['given']} {p['family']}"
        assert name == scene.get("name"), f"{word}: {PEOPLE} names {name}, the scene shows {scene.get('name')}"
        one, two = p["species"]
        for animal in (one, two):
            assert animal in species and species[animal] > 0, f"{word}: '{animal}' is not an AnimalSpecies"
        assert one != two, f"{word}: the two animals are the same"
        kind, faction = p["faction"]
        assert kind in types and types[kind] > 0, f"{word}: '{kind}' is not a FactionType"
        assert faction in factions.get(kind, []), f"{word}: '{faction}' is not one of FactionCatalog's {kind} factions"
        story = p["story"]
        assert story and all(par.strip() == par and par for par in story), f"{word}: an empty or padded paragraph"
        for text in (p["given"], p["family"], p["home"], faction, *story):
            assert PRINTABLE.match(text), f"{word}: '{text}' has a character the UI font cannot draw"
        out.append((value, dict(given=p["given"], family=p["family"], one=species[one], two=species[two],
                                type=types[kind], faction=faction, home=p["home"], story="\n\n".join(story))))
    return out


def crew_text(people, script_guid):
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
        f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}",
        "  m_Name: PaintedCrew",
        "  m_EditorClassIdentifier: ",
        "  people:" if people else "  people: []",
    ]
    for value, p in people:
        lines += [
            f"  - callsign: {value}",
            f"    given: {yaml_text(p['given'])}",
            f"    family: {yaml_text(p['family'])}",
            f"    speciesOne: {p['one']}",
            f"    speciesTwo: {p['two']}",
            f"    factionType: {p['type']}",
            f"    factionName: {yaml_text(p['faction'])}",
            f"    homeworld: {yaml_text(p['home'])}",
            f"    story: {yaml_text(p['story'])}",
        ]
    return "\n".join(lines) + "\n"


def outputs(manifest=None):
    """{repo path: bytes} of everything this script owns."""
    out, entries = {}, []
    for value, word, scene in rows(manifest):
        source = read_bytes(scene["file"])
        entries.append(entry_of(value, word, scene, source))
        out[scene_path(word)] = source
        out[scene_path(word) + ".meta"] = texture_meta(minted(word)).encode("utf-8")
    out[SCENE_DIR + ".meta"] = folder_meta(minted("folder")).encode("utf-8")
    out[CATALOG] = catalog_text(entries, guid_of(CATALOG_SCRIPT_META)).encode("utf-8")
    out[CATALOG + ".meta"] = asset_meta(minted("catalog")).encode("utf-8")
    out[CREW] = crew_text(crew_rows(manifest=manifest), guid_of(CREW_SCRIPT_META)).encode("utf-8")
    out[CREW + ".meta"] = asset_meta(minted("crew")).encode("utf-8")
    return out


def problems(expected, root=ROOT):
    """What disk under `root` holds that `expected` does not say, as lines."""
    found = []
    for rel in sorted(expected):
        full = os.path.join(root, rel)
        if not os.path.exists(full):
            found.append(f"{rel} is missing")
        elif read_bytes(rel, root) != expected[rel]:
            found.append(f"{rel} differs from what this script writes")
    folder = os.path.join(root, SCENE_DIR)
    if os.path.isdir(folder):
        for name in sorted(os.listdir(folder)):
            if f"{SCENE_DIR}/{name}" not in expected:
                found.append(f"{SCENE_DIR}/{name} is not a scene this script owns (a stray ships in every build)")
    guids = [re.search(rb"^guid: ([0-9a-f]{32})", data, re.M).group(1)
             for rel, data in expected.items() if rel.endswith(".meta")]
    if len(set(guids)) != len(guids):
        found.append("two of this script's files share a guid")
    return found


def write_all(root=ROOT):
    expected = outputs()
    wrote = 0
    for rel, data in expected.items():
        full = os.path.join(root, rel)
        if os.path.exists(full) and read_bytes(rel, root) == data:
            continue
        write_bytes(rel, data, root)
        wrote += 1
    folder = os.path.join(root, SCENE_DIR)
    for name in sorted(os.listdir(folder)) if os.path.isdir(folder) else []:
        if f"{SCENE_DIR}/{name}" not in expected:    # a scene this script no longer owns leaves the build
            os.remove(os.path.join(folder, name))
            wrote += 1
    return wrote, expected


def self_test():
    failures = []

    def expect(label, ok):
        if not ok:
            failures.append(label)

    expected = outputs()
    words = [word for _, word, _ in rows()]
    expect("today every callsign has a painted scene", len(words) == len(CALLSIGNS) and not unpainted())
    expect("one scene, one importer a callsign, plus the folder, the catalog, the crew and their metas",
           len(expected) == 2 * len(words) + 5)

    # The maths a page's crop rests on: the close box, top-left pixels, as a Rect from the bottom left.
    x, y, w, h = face_rect([296, 0, 606, 310], [1536, 864])
    expect("face_rect measures from the bottom left",
           abs(x - 296 / 1536) < 1e-9 and abs(y - (1 - 310 / 864)) < 1e-9
           and abs(w - 310 / 1536) < 1e-9 and abs(h - 310 / 864) < 1e-9)
    expect("f32 writes Unity's floats", (f32(0.5), f32(1), f32(0.1), f32(1 / 3)) == ("0.5", "1", "0.1", "0.33333334"))
    expect("jpeg_size reads a real scene", jpeg_size(expected[scene_path(words[0])]) == (1536, 864))
    expect("jpeg_size refuses what is not a JPEG", jpeg_size(b"\x89PNG\r\n\x1a\n" + b"\0" * 32) is None)

    # A record's faults are raised by name, never written.
    value, word, scene = rows()[0]
    source = expected[scene_path(word)]
    for label, change in [
        ("a close-up outside its scene", {"close": [1400, 0, 1700, 300]}),
        ("a close-up with no area", {"close": [300, 100, 300, 400]}),
        ("a side that is not left, centre or right", {"side": "middle"}),
        ("a side the close-up is not on", {"side": "right" if scene["side"] == "left" else "left"}),
        ("a size the picture does not have", {"size": [1920, 1080]}),
        ("a name YAML would misread", {"name": "Veliane: Crestward"}),
    ]:
        try:
            entry_of(value, word, dict(scene, **change), source)
            failures.append(f"{label} was written")
        except AssertionError:
            pass
    try:
        entry_of(value, word, scene, b"not a picture")
        failures.append("bytes that are not a JPEG were written")
    except AssertionError:
        pass

    # The painted crew: every fault in a person is raised by name, never written.
    people = json.loads(read(PEOPLE))
    first = words[0]

    def person_fault(label, mutate):
        bad = copy.deepcopy(people)
        mutate(bad["people"])
        try:
            crew_rows(bad)
            failures.append(f"{label} was written")
        except AssertionError:
            pass

    person_fault("a painted pilot with no person", lambda ps: ps.pop(first))
    person_fault("a person not named as the scene names them", lambda ps: ps[first].__setitem__("given", "Someone"))
    person_fault("an animal that is not an AnimalSpecies", lambda ps: ps[first].__setitem__("species", ["Dragon", "Koi"]))
    person_fault("the same animal twice", lambda ps: ps[first].__setitem__("species", ["Koi", "Koi"]))
    person_fault("a faction type that does not exist", lambda ps: ps[first].__setitem__("faction", ["Wizard", "X"]))
    person_fault("a faction FactionCatalog does not found",
                 lambda ps: ps[first].__setitem__("faction", [ps[first]["faction"][0], "Nobody's Company"]))
    person_fault("an empty paragraph", lambda ps: ps[first]["story"].append(""))
    person_fault("a character the UI font cannot draw", lambda ps: ps[first]["story"].__setitem__(0, "Caf\u00e9 life."))
    crew = expected[CREW].decode("utf-8")
    expect("the crew holds one person a scene", crew.count("  - callsign: ") == len(words))
    expect("a story keeps its paragraph breaks", "\\n\\n" in crew)

    # A callsign with no painted scene is left out, not invented.
    manifest = json.loads(read(MANIFEST))
    del manifest["scenes"][words[-1]]
    fewer = outputs(manifest)
    expect("an unpainted callsign has no scene, no importer and no catalog row",
           scene_path(words[-1]) not in fewer and scene_path(words[-1]) + ".meta" not in fewer
           and fewer[CATALOG].count(b"  - callsign: ") == len(words) - 1 and unpainted(manifest) == [words[-1]])

    # The catalog names every scene by the path Resources.Load takes and holds the avatar's own guid.
    catalog = expected[CATALOG].decode("utf-8")
    expect("the catalog holds one row a scene", catalog.count("  - callsign: ") == len(words))
    for _, word, _ in rows():
        expect(f"{word}'s row names its scene", f"    resource: {RESOURCE_DIR}/{word}\n" in catalog)
        expect(f"{word}'s row holds its avatar", guid_of(AVATAR.format(word=word) + ".meta") in catalog)
    expect("a browser build keeps a stand-in",
           f"    buildTarget: WebGL\n    maxTextureSize: {WEBGL_STAND_IN}\n" in texture_meta(minted("x")))

    # Drift is caught: every kind of difference between disk and this script is reported.
    with tempfile.TemporaryDirectory() as tmp:
        for rel, data in expected.items():
            write_bytes(rel, data, tmp)
        expect("a fresh write is clean", problems(expected, tmp) == [])

        def control(label, rel, mutate):
            kept = read_bytes(rel, tmp) if os.path.exists(os.path.join(tmp, rel)) else None
            mutate(os.path.join(tmp, rel))
            if not any(rel in line for line in problems(expected, tmp)):
                failures.append(f"{label} was not reported")
            if kept is None:
                os.remove(os.path.join(tmp, rel))
            else:
                write_bytes(rel, kept, tmp)

        def edit(change):
            """A control that rewrites one text file under the temp root through `change`."""
            def apply(full):
                with open(full, encoding="utf-8") as f:
                    text = f.read()
                with open(full, "w", encoding="utf-8", newline="\n") as f:
                    f.write(change(text))
            return apply

        def flip(full):
            with open(full, "rb") as f:
                data = bytearray(f.read())
            data[len(data) // 2] ^= 0x01
            with open(full, "wb") as f:
                f.write(bytes(data))

        reguid = edit(lambda text: re.sub(r"guid: [0-9a-f]{32}", "guid: " + "0" * 32, text, count=1))
        reside = edit(lambda text: text.replace("    side: 0\n", "    side: 2\n", 1))
        lift = edit(lambda text: text.replace(f"maxTextureSize: {WEBGL_STAND_IN}\n", "maxTextureSize: 2048\n", 1))

        control("a scene with one bit changed", scene_path(words[0]), flip)
        control("a scene that was deleted", scene_path(words[1]), os.remove)
        control("an importer with another guid", scene_path(words[2]) + ".meta", reguid)
        control("an importer that was deleted", scene_path(words[3]) + ".meta", os.remove)
        control("an importer whose browser stand-in was lifted", scene_path(words[4]) + ".meta", lift)
        control("a catalog row moved to the other side", CATALOG, reside)
        control("a catalog whose meta was deleted", CATALOG + ".meta", os.remove)
        control("a folder without its meta", SCENE_DIR + ".meta", os.remove)
        control("a stray picture in the scenes folder", f"{SCENE_DIR}/Stray.jpg",
                lambda full: shutil.copyfile(os.path.join(tmp, scene_path(words[0])), full))
        expect("the controls put everything back", problems(expected, tmp) == [])

    for line in failures:
        print(f"FAIL {line}")
    print("self-test: ok" if not failures else f"self-test: {len(failures)} failure(s)")
    return 1 if failures else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" in argv:
        found = problems(outputs())
        for line in found:
            print(f"DRIFT {line}")
        for word in unpainted():
            print(f"note: {word} has no painted scene; its page shows the words alone")
        print("check: ok" if not found
              else f"check: {len(found)} problem(s); run Tools/Build/author_pilot_scene_assets.py")
        return 1 if found else 0
    wrote, expected = write_all()
    scenes = sum(1 for rel in expected if rel.endswith(".jpg"))
    print(f"wrote {wrote} file(s): {scenes} pilot scenes under {SCENE_DIR} and {CATALOG}" if wrote
          else f"no files written (already current): {scenes} pilot scenes under {SCENE_DIR} and {CATALOG}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
