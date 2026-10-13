#!/usr/bin/env python3
"""Bind the Rhino, Serpent and Scarab four-icon ability rows to their OWN abilities.

What shipped (re-measured from the prefabs, not the docs):

  Rhino    RhinoVesselHUDView serialized no `abilityIcons` at all - 0/4 bound, so all four
           lockup cards drew LOCKED, including the two abilities the map names (Mass: Trail
           Slabs, Time: Ramp Spool).
  Serpent  the view is an ADDED component on Serpent.prefab's SerpentHUDVariant instance and
           bound one entry (Time, the fuel-pellet icon). The Sniper Shot (Charge, RT) and the
           Scope (Space, LT) are shipped abilities and drew LOCKED - the Charge card's cooldown
           veil swept a bare plate (SERPENT_SNIPER_SCOPE.md round 4).
  Scarab   4/4 bound, but every icon Image carried a SPARROW sprite (missiles / swap weapon /
           bullet / boost, HUD UI/New_Sparrow), matching none of its abilities.

What this writes:

  Rhino    RhinoHUDVariant.prefab: a MassButton and a TimeButton host at the fleet-standard
           bands (VesselAbilityRowWirer's numbers), each holding one 60x60 icon Image, and a
           four-entry `abilityIcons` list on the view.
  Serpent  SerpentHUDVariant.prefab: a ChargeButton and a SpaceButton host, added under the
           base HUD root beside the shipped Boost Button (the Time host), on the same frame and
           the lockup's 128-unit pitch. Serpent.prefab: two stripped references to those icon
           Images and a four-entry `abilityIcons` list. The binding has to live in the VESSEL
           prefab because that is where the view component lives - moving the view into the
           variant is a different change.
  Scarab   ScarabHUDVariant.prefab: the four icon Images' m_Sprite swapped from the Sparrow's
           art to the Scarab's own placeholders. Bindings, gauges and tints are untouched.

OPEN DESIGN SLOTS are bound as an entry with `icon: {fileID: 0}` - the Rhino's Charge and
Space (the Serpent's Mass was one until the Seed Wall, which serpent_add_slots adds). That is the contract for a slot with no ability behind it: the
lockup draws a LOCKED card (AbilityLockupView.ResolveLockedHost), and keeping the entry makes
the list four long so ValidateAbilityIconRow checks the ORDER instead of stopping at the count.
No icon object is created for them, so nothing about an undesigned ability is invented
(/vessel skill section 3). When the design lands, add the sprite to
author_hull_icon_placeholders.py and the slot here.

Sprites come from Tools/Build/author_hull_icon_placeholders.py (run it first). Layout is not
a decision made here: the lockup re-derives position, pitch, cell size and icon scale from
Resources/AbilityLockupStyle at build time; icons are authored at its iconBoxSize (60) so the
derived scale is exactly 1.

Idempotent (a second run verifies rather than rewrites), deterministic fileIDs, --check.

    python3 Tools/Build/author_hull_ability_rows.py           # author
    python3 Tools/Build/author_hull_ability_rows.py --check   # fail on drift
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is not the project root: {ROOT}"
CHECK = "--check" in sys.argv

HUD_DIR = "Assets/_Prefabs/UI Elements/VesselHUD"
ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons"
IMAGE_SCRIPT_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"
ICON_BOX = 60          # Resources/AbilityLockupStyle.iconBoxSize
CARD_PITCH = 128       # Resources/AbilityLockupStyle.cardPitch
# Unity writes an empty scalar as "key: " WITH the trailing space; spelled out so no
# whitespace-trimming editor can silently change what this emits.
SP = " "

CHARGE, MASS, SPACE, TIME = 1, 2, 3, 4
ORDER = (CHARGE, MASS, SPACE, TIME)
NAMES = {CHARGE: "Charge", MASS: "Mass", SPACE: "Space", TIME: "Time"}

# VesselAbilityRowWirer.BandYMin/Max and BandsX - THE fleet standard, in AbilityDisplayOrder.
BAND_Y = (0.027730448, 0.1665395)
BANDS_X = {CHARGE: (0.68481258, 0.76293758), MASS: (0.75652886, 0.83465386),
           SPACE: (0.82824515, 0.90637015), TIME: (0.89996143, 0.97808643)}


def sprite_guid(hull: str, png: str) -> str:
    path = os.path.join(ROOT, ICON_DIR, hull, png + ".meta")
    assert os.path.exists(path), (
        f"missing {path} - run Tools/Build/author_hull_icon_placeholders.py first")
    m = re.search(r"^guid: ([0-9a-f]{32})$", open(path, encoding="utf-8").read(), re.M)
    assert m, f"no guid in {path}"
    return m.group(1)


# -- YAML block builders -------------------------------------------------------

def game_object(fid, name, components):
    comps = "".join(f"\n  - component: {{fileID: {c}}}" for c in components)
    return f"""--- !u!1 &{fid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:{comps}
  m_Layer: 5
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""


def rect(fid, go, parent, children, anchor_min, anchor_max, pos, size, pivot):
    kids = "".join(f"\n  - {{fileID: {c}}}" for c in children) or " []"
    return f"""--- !u!224 &{fid}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:{kids}
  m_Father: {{fileID: {parent}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: {anchor_min[0]}, y: {anchor_min[1]}}}
  m_AnchorMax: {{x: {anchor_max[0]}, y: {anchor_max[1]}}}
  m_AnchoredPosition: {{x: {pos[0]}, y: {pos[1]}}}
  m_SizeDelta: {{x: {size[0]}, y: {size[1]}}}
  m_Pivot: {{x: {pivot[0]}, y: {pivot[1]}}}
"""


def canvas_renderer(fid, go):
    return f"""--- !u!222 &{fid}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_CullTransparentMesh: 1
"""


def image(fid, go, guid):
    return f"""--- !u!114 &{fid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_SCRIPT_GUID}, type: 3}}
  m_Name:{SP}
  m_EditorClassIdentifier:{SP}
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 21300000, guid: {guid}, type: 3}}
  m_Type: 0
  m_PreserveAspect: 1
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""


def slot_ids(base, element):
    b = base + element * 10
    return dict(host_go=b, host_rt=b + 1, icon_go=b + 2, icon_rt=b + 3, icon_cr=b + 4, icon_img=b + 5)


def host_blocks(ids, element, parent_rt, host_frame, guid):
    """One host (the card's re-homed parent) holding one icon Image."""
    amin, amax, pos, size, pivot = host_frame
    name = NAMES[element]
    return [
        game_object(ids["host_go"], f"{name}Button", [ids["host_rt"]]),
        rect(ids["host_rt"], ids["host_go"], parent_rt, [ids["icon_rt"]], amin, amax, pos, size, pivot),
        game_object(ids["icon_go"], f"{name}Icon", [ids["icon_rt"], ids["icon_cr"], ids["icon_img"]]),
        rect(ids["icon_rt"], ids["icon_go"], ids["host_rt"], [], (0.5, 0.5), (0.5, 0.5),
             (0, 0), (ICON_BOX, ICON_BOX), (0.5, 0.5)),
        canvas_renderer(ids["icon_cr"], ids["icon_go"]),
        image(ids["icon_img"], ids["icon_go"], guid),
    ]


def bindings(icon_ids):
    """Four entries in AbilityDisplayOrder; an open slot is bound with no icon (LOCKED card)."""
    out = []
    for element in ORDER:
        out.append(f"""  - element: {element}
    icon: {{fileID: {icon_ids.get(element, 0)}}}
    upgradedSprite: {{fileID: 0}}
    gauge: {{fileID: 0}}""")
    return "  abilityIcons:\n" + "\n".join(out) + "\n"


def component_block(src, fid):
    """(start, end) of the YAML document `--- !u!... &fid`."""
    m = re.search(rf"^--- !u!\d+ &{fid}( stripped)?\n", src, re.M)
    assert m, f"no document &{fid}"
    n = re.search(r"^--- !u!", src[m.end():], re.M)
    return m.start(), (m.end() + n.start()) if n else len(src)


def replace_once(src, old, new, what):
    assert src.count(old) == 1, f"{what}: expected exactly one anchor, found {src.count(old)}"
    return src.replace(old, new, 1)


# -- Rhino ---------------------------------------------------------------------

RHINO_PREFAB = f"{HUD_DIR}/RhinoHUDVariant.prefab"
RHINO_ROOT_RT = "1335802071243411841"
RHINO_VIEW = "5161366343835098938"
RHINO_BASE = 7720000000000003000
RHINO_ICONS = {MASS: "Rhino_TrailSlabs-PLACEHOLDER.png", TIME: "Rhino_RampSpool-PLACEHOLDER.png"}


def rhino_frame(element):
    x0, x1 = BANDS_X[element]
    return ((x0, BAND_Y[0]), (x1, BAND_Y[1]), (0, 0), (0, 0), (0.5, 0.5))


def rhino(src):
    blocks, icon_ids, hosts = [], {}, []
    for element, png in RHINO_ICONS.items():
        ids = slot_ids(RHINO_BASE, element)
        blocks += host_blocks(ids, element, RHINO_ROOT_RT, rhino_frame(element),
                              sprite_guid("Rhino", png))
        icon_ids[element] = ids["icon_img"]
        hosts.append(ids["host_rt"])

    out = src
    # The root's children: append the two hosts after the shipped three.
    s, e = component_block(out, RHINO_ROOT_RT)
    root = out[s:e]
    new_root = replace_once(root, "  - {fileID: 5864610081205471824}\n  m_Father: {fileID: 0}\n",
                            "  - {fileID: 5864610081205471824}\n"
                            + "".join(f"  - {{fileID: {h}}}\n" for h in hosts)
                            + "  m_Father: {fileID: 0}\n", "Rhino root children")
    out = out[:s] + new_root + out[e:]

    # The view: abilityIcons goes straight after highlights (VesselHUDView's field order).
    s, e = component_block(out, RHINO_VIEW)
    view = out[s:e]
    assert "abilityIcons" not in view, "Rhino view already serializes abilityIcons"
    new_view = replace_once(view, "  highlights:\n  - input: 0\n    image: {fileID: 8279697361072404982}\n",
                            "  highlights:\n  - input: 0\n    image: {fileID: 8279697361072404982}\n"
                            + bindings(icon_ids), "Rhino view highlights")
    out = out[:s] + new_view + out[e:]
    return out.rstrip("\n") + "\n" + "".join(blocks), icon_ids


# -- Serpent -------------------------------------------------------------------

SERPENT_VARIANT = f"{HUD_DIR}/SerpentHUDVariant.prefab"
SERPENT_VESSEL = "Assets/_Prefabs/Spacevessels/Serpent.prefab"
SERPENT_VARIANT_GUID = "af7b78e6bb3a8394f88f24bb0e31667a"
SERPENT_BASE_HUD_GUID = "bc09c07dcb0724842a327c887609f156"
SERPENT_BASE_ROOT_RT = "6557045720525393826"     # VesselHUDPrefab's root RectTransform
SERPENT_STRIPPED_ROOT = "3412957971693408596"    # the variant's stripped handle on it
SERPENT_VARIANT_INSTANCE = "8476352183690256118"  # the variant's PrefabInstance of the base
SERPENT_HUD_INSTANCE = 4033090122440962496       # Serpent.prefab's PrefabInstance of the variant
SERPENT_VIEW = "5470374086122723936"              # SerpentVesselHUDView, added on Serpent.prefab
SERPENT_TIME_ICON = "5867477772541908519"         # the shipped Time binding (stripped TimeIcon)
SERPENT_BASE = 7710000000000002000
SERPENT_ICONS = {CHARGE: "Serpent_SniperShot-PLACEHOLDER.png", MASS: "Serpent_SeedWall-PLACEHOLDER.png",
                 SPACE: "Serpent_Scope-PLACEHOLDER.png"}
# Elements the FIRST authoring pass wrote. The Mass slot (Seed Wall, SERPENT_SEED_WALL.md) was
# designed later and is added by serpent_add_slots onto a row that already carries these.
SERPENT_FIRST_PASS = (CHARGE, SPACE)
# The Time host (Boost Button) is anchored bottom-right, pivot top-right, at (-28.7, 365), 150
# square. New hosts share that frame and step left by the lockup's pitch per slot, so the row
# reads charge -> mass -> space -> time before the lockup re-homes it.
SERPENT_TIME_X, SERPENT_Y, SERPENT_HOST = -28.7, 365.0, 150


def serpent_frame(element):
    x = round(SERPENT_TIME_X - CARD_PITCH * (ORDER.index(TIME) - ORDER.index(element)), 4)
    return ((1, 0), (1, 0), (x, SERPENT_Y), (SERPENT_HOST, SERPENT_HOST), (1, 1))


def serpent_variant(src, elements=None):
    blocks, icon_ids, added = [], {}, []
    for element, png in SERPENT_ICONS.items():
        if elements is not None and element not in elements:
            continue
        ids = slot_ids(SERPENT_BASE, element)
        blocks += host_blocks(ids, element, SERPENT_STRIPPED_ROOT, serpent_frame(element),
                              sprite_guid("Serpent", png))
        icon_ids[element] = ids["icon_img"]
        added.append(f"""    - targetCorrespondingSourceObject: {{fileID: {SERPENT_BASE_ROOT_RT}, guid: {SERPENT_BASE_HUD_GUID},
        type: 3}}
      insertIndex: -1
      addedObject: {{fileID: {ids["host_rt"]}}}
""")

    s, e = component_block(src, SERPENT_VARIANT_INSTANCE)
    inst = src[s:e]
    i = inst.index("    m_AddedGameObjects:\n")
    j = inst.index("    m_AddedComponents:", i)
    inst = inst[:j] + "".join(added) + inst[j:]
    out = src[:s] + inst + src[e:]
    return out.rstrip("\n") + "\n" + "".join(blocks), icon_ids


def stripped_image(variant_fid):
    local = int(variant_fid) ^ SERPENT_HUD_INSTANCE
    return local, f"""--- !u!114 &{local} stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {{fileID: {variant_fid}, guid: {SERPENT_VARIANT_GUID},
    type: 3}}
  m_PrefabInstance: {{fileID: {SERPENT_HUD_INSTANCE}}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_SCRIPT_GUID}, type: 3}}
  m_Name:{SP}
  m_EditorClassIdentifier:{SP}
"""


def serpent_add_slots(var_src, ves_src, elements):
    """Add slots designed after the first pass (today: Mass) to an already-authored row: their
    hosts in the variant, their stripped Images in the vessel, and the four-entry binding list
    rewritten to name them. The first pass's documents are left byte-identical."""
    var_out, new_ids = serpent_variant(var_src, elements)

    before = {TIME: SERPENT_TIME_ICON}
    for el in SERPENT_FIRST_PASS:
        before[el] = int(slot_ids(SERPENT_BASE, el)["icon_img"]) ^ SERPENT_HUD_INSTANCE
    after = dict(before)
    blocks = []
    for el, fid in new_ids.items():
        local, block = stripped_image(fid)
        assert f"&{local}" not in ves_src, f"Serpent.prefab already has &{local}"
        after[el] = local
        blocks.append(block)

    s, e = component_block(ves_src, SERPENT_VIEW)
    view = replace_once(ves_src[s:e], bindings(before), bindings(after), "Serpent view abilityIcons")
    ves_out = ves_src[:s] + view + ves_src[e:]
    return var_out, ves_out.rstrip("\n") + "\n" + "".join(blocks)


def serpent_vessel(src, variant_icon_ids):
    blocks, local_ids = [], {TIME: SERPENT_TIME_ICON}
    for element, fid in variant_icon_ids.items():
        local, block = stripped_image(fid)
        assert f"&{local}" not in src, f"Serpent.prefab already has &{local}"
        local_ids[element] = local
        blocks.append(block)

    s, e = component_block(src, SERPENT_VIEW)
    view = src[s:e]
    shipped = ("  abilityIcons:\n  - element: 4\n"
               f"    icon: {{fileID: {SERPENT_TIME_ICON}}}\n"
               "    upgradedSprite: {fileID: 0}\n    gauge: {fileID: 0}\n")
    view = replace_once(view, shipped, bindings(local_ids), "Serpent view abilityIcons")
    out = src[:s] + view + src[e:]
    return out.rstrip("\n") + "\n" + "".join(blocks), local_ids


# -- Scarab --------------------------------------------------------------------

SCARAB_PREFAB = f"{HUD_DIR}/ScarabHUDVariant.prefab"
# icon Image fileID -> (the Sparrow sprite it shipped with, the Scarab placeholder)
SCARAB_SWAPS = {
    "3855639277037614910": ("7aededbf59d8deb4fb340521261c6efa", "Scarab_CavitationBlast-PLACEHOLDER.png"),  # Charge (was missiles icon)
    "6611959372928416812": ("4a5259289f0bdff46a3c22300b3933d7", "Scarab_Switch-PLACEHOLDER.png"),           # Mass (was swap weapon icon)
    "4990196459091587280": ("6da9cfe8d216bd84695e3b4c71bfa533", "Scarab_BallForge-PLACEHOLDER.png"),        # Space (was bullet icon)
    "3258612490553136050": ("f0197e0a2ce38654ea3d882fff64967a", "Scarab_Throttle-PLACEHOLDER.png"),         # Time (was boost icon)
}


def scarab(src):
    out = src
    for fid, (old, png) in SCARAB_SWAPS.items():
        s, e = component_block(out, fid)
        blk = out[s:e]
        new = sprite_guid("Scarab", png)
        if f"guid: {new}," in blk:
            continue
        blk = replace_once(blk, f"  m_Sprite: {{fileID: 21300000, guid: {old}, type: 3}}\n",
                           f"  m_Sprite: {{fileID: 21300000, guid: {new}, type: 3}}\n",
                           f"Scarab icon &{fid} sprite")
        out = out[:s] + blk + out[e:]
    return out


# -- Driver --------------------------------------------------------------------

def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8", newline="") as f:
        return f.read()


def write(rel, text):
    with open(os.path.join(ROOT, rel), "w", encoding="utf-8", newline="") as f:
        f.write(text)


def verify(rel, text, needles, what):
    missing = [n.splitlines()[0] for n in needles if n not in text]
    if missing:
        print(f"DRIFT: {what} in {rel} is partly authored - missing:\n    " + "\n    ".join(missing))
        return False
    return True


def main() -> int:
    ok, wrote = True, []

    # Rhino
    have = read(RHINO_PREFAB)
    if "abilityIcons" not in have[slice(*component_block(have, RHINO_VIEW))]:
        want, _ = rhino(have)
        if CHECK:
            print(f"DRIFT: {RHINO_PREFAB} (row not authored)"); ok = False
        else:
            write(RHINO_PREFAB, want); wrote.append(RHINO_PREFAB)
    else:
        ids = {el: slot_ids(RHINO_BASE, el)["icon_img"] for el in RHINO_ICONS}
        needles = [bindings(ids)]
        for el, png in RHINO_ICONS.items():
            needles += host_blocks(slot_ids(RHINO_BASE, el), el, RHINO_ROOT_RT, rhino_frame(el),
                                   sprite_guid("Rhino", png))
        ok &= verify(RHINO_PREFAB, have, needles, "the Rhino row")

    # Serpent - the variant and the vessel move together.
    var_have, ves_have = read(SERPENT_VARIANT), read(SERPENT_VESSEL)
    var_ids = {el: slot_ids(SERPENT_BASE, el)["icon_img"] for el in SERPENT_ICONS}
    var_done = f"&{var_ids[CHARGE]}\n" in var_have
    ves_done = f"&{var_ids[CHARGE] ^ SERPENT_HUD_INSTANCE} stripped" in ves_have
    late = [el for el in SERPENT_ICONS if el not in SERPENT_FIRST_PASS
            and f"&{var_ids[el]}\n" not in var_have]
    if var_done and ves_done and late:
        if CHECK:
            print(f"DRIFT: {SERPENT_VARIANT} + {SERPENT_VESSEL} (slots {late} not authored)"); ok = False
        else:
            var_want, ves_want = serpent_add_slots(var_have, ves_have, late)
            write(SERPENT_VARIANT, var_want); write(SERPENT_VESSEL, ves_want)
            wrote += [SERPENT_VARIANT, SERPENT_VESSEL]
            var_have, ves_have = var_want, ves_want
    if not var_done and not ves_done:
        var_want, var_ids = serpent_variant(var_have)
        ves_want, _ = serpent_vessel(ves_have, var_ids)
        if CHECK:
            print(f"DRIFT: {SERPENT_VARIANT} + {SERPENT_VESSEL} (row not authored)"); ok = False
        else:
            write(SERPENT_VARIANT, var_want); write(SERPENT_VESSEL, ves_want)
            wrote += [SERPENT_VARIANT, SERPENT_VESSEL]
    elif var_done != ves_done:
        print("DRIFT: the Serpent row is half-authored (variant and vessel prefab disagree)"); ok = False
    else:
        needles = []
        for el, png in SERPENT_ICONS.items():
            needles += host_blocks(slot_ids(SERPENT_BASE, el), el, SERPENT_STRIPPED_ROOT,
                                   serpent_frame(el), sprite_guid("Serpent", png))
        ok &= verify(SERPENT_VARIANT, var_have, needles, "the Serpent hosts")
        local = {TIME: SERPENT_TIME_ICON}
        ves_needles = []
        for el, fid in var_ids.items():
            lid, blk = stripped_image(fid)
            local[el] = lid
            ves_needles.append(blk)
        ves_needles.append(bindings(local))
        ok &= verify(SERPENT_VESSEL, ves_have, ves_needles, "the Serpent bindings")

    # Scarab
    have = read(SCARAB_PREFAB)
    want = have
    for fid, (old, png) in SCARAB_SWAPS.items():
        blk = have[slice(*component_block(have, fid))]
        new = sprite_guid("Scarab", png)
        if f"guid: {new}," in blk:
            continue
        if f"guid: {old}," not in blk:
            print(f"DRIFT: Scarab icon &{fid} carries neither the Sparrow sprite nor its placeholder")
            ok = False
            continue
        want = None
    if want is None:
        if CHECK:
            print(f"DRIFT: {SCARAB_PREFAB} (Sparrow sprites still bound)"); ok = False
        else:
            write(SCARAB_PREFAB, scarab(have)); wrote.append(SCARAB_PREFAB)

    if CHECK:
        print("check clean" if ok else "check FAILED")
        return 0 if ok else 1
    print("\n".join(f"wrote {w}" for w in wrote) if wrote else "already authored - nothing to write")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
