#!/usr/bin/env python3
"""
Re-point every reference to the two unlicensed vendor packs onto the first-party replacements,
and retire the one vendor PREFAB instance the project carries.

    Assets/PrimitivePlus/              -> Assets/_Models/Primitives/      (author_primitive_meshes.py)
    Assets/Shift - Complete Sci-Fi UI/ -> Assets/_Graphics/UI/Frames/     (author_ui_frame_sprites.py)

REFERENCES ARE RE-POINTED, NEVER SQUATTED. The replacements carry their own guids; keeping the
vendor's guid on first-party content would leave the asset PATH saying `Shift - Complete Sci-Fi
UI/...`, so the next licence audit re-reports it and a re-import restores vendor art over ours.
Only the guid changes - the sub-asset fileIDs are the conventional ones and are identical on
both sides (a Mesh in a `.asset` is 4300000, a single-mode texture's generated Sprite is
21300000), which is what makes this a guid swap rather than a re-wire.

TWO THINGS ARE REMOVED RATHER THAN REPLACED, and both are measured, not assumed:

  * `PrimitivePlusMaterial` (on `oldWallFlora`, `AOEConicExplosion`, `AOEConicSkyBurst`) is an
    EDITOR-AUTHORING helper. It declares no serialized fields, nothing in the three prefabs
    references its fileID, no UnityEvent lists it as a target, and its only two public methods
    (`SetNewMaterial`, `SetSharedMaterial`) are called from exactly one place - the vendor's own
    `PrimitivePlusMaterialEditor` inspector. At runtime it caches a MeshRenderer into a private
    field nobody reads. A first-party rewrite would be a no-op compiled into `Assembly-CSharp`
    and carried on three prefabs, so the component is deleted instead.

  * The `Switch.prefab` instance in `_Prefabs/UI Elements/ModalWindows.prefab` ships
    `m_IsActive: 0`, its host prefab is referenced by NOTHING (no scene, no prefab, not in a
    `Resources/` folder, and the `ModalWindows` the code names is `ScreenSwitcher.ModalWindows`,
    an enum), and the project's live settings panel does not use a switch at all - every one of
    its seven on/off rows is a `GameSettingsPanelController.OnOffControl` (an ON button + an OFF
    button). So there is no toggle to rebuild: the instance is removed. Its prefab also carries
    two `m_Script` guids owned by no `.meta` under `Assets/`, which is why editing it in place
    was never an option.

    python3 Tools/Build/repoint_vendored_ui_assets.py           # re-point + retire
    python3 Tools/Build/repoint_vendored_ui_assets.py --check   # fail if any vendor guid remains
"""

import argparse
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(REPO, "Assets")

# vendor guid -> (first-party guid, label)
REMAP = {
    # PrimitivePlus meshes
    "869889764e8a84e2fbbfff0a8d09c3e8": ("e11078cfbb5db29e25aa25b65f5dfa6b", "Cone -> PrimitiveCone"),
    "2341dea8fec8c4334bee2b71dee3da21": ("981cce11266cdeef498dedf1fdbaea4d", "Sphere -> PrimitiveSphere"),
    "6fb0bfb513572460f942db4a2de09855": ("c2685322aa3c31ffc3b1dc4fe00667f2", "Cube -> PrimitiveCube"),
    "fc089023da14b4107a1959883e74b3fb": ("b34d33502ce8caa5059e344c33f6791f", "CylinderTube -> PrimitiveCylinderTube"),
    # Shift frames
    "235a0c40b1e7b994ab7ea60769a04d32": ("1cee1d42a1d323669b9104472c778b75", "Cut Frame Big - 6px (200ppu) -> frame_cut_outline_200"),
    "df57fed132e9eba448cfb8d87788318c": ("3534b67f827601a31392b30ef77dd5f8", "Cut Frame Filled Big (200ppu) -> frame_cut_filled_200"),
    "fe5c69cb52e569a40b68fc63f7fb5fbb": ("0e81743001c259ce8606abcf8024428c", "Cut Frame Filled Big (300ppu) -> frame_cut_filled_300"),
}

PRIMITIVE_PLUS_MATERIAL = "89d18bf1872354d8a84e751f5c2cccda"
SHIFT_SWITCH_PREFAB = "cb8096e5a0bc9ce42adfe2fbe1a3b43a"

VENDOR_ROOTS = ("Assets/PrimitivePlus/", "Assets/Shift - Complete Sci-Fi UI/")

TEXT_EXT = (".prefab", ".unity", ".asset", ".mat", ".shadergraph", ".controller",
            ".anim", ".shadervariants", ".playable", ".overrideController", ".meta")


def scan():
    for dp, dn, fn in os.walk(ASSETS):
        rel = os.path.relpath(dp, REPO).replace(os.sep, "/") + "/"
        if any(rel.startswith(v) for v in VENDOR_ROOTS):
            continue                       # inside the packs: they are about to be deleted whole
        for f in fn:
            if f.endswith(TEXT_EXT):
                yield os.path.join(dp, f)


# --------------------------------------------------------------------------------------------
# Component removal: a MonoBehaviour block plus its entry in the owning GameObject's component
# list. Both halves, or Unity reports a missing script on load.
# --------------------------------------------------------------------------------------------

def strip_component(text, script_guid):
    removed = 0
    while True:
        hit = None
        for m in re.finditer(r"--- !u!114 &(-?\d+)\nMonoBehaviour:\n(.*?)(?=\n--- !u!|\Z)",
                             text, re.S):
            if f"guid: {script_guid}" in m.group(2):
                hit = m
                break
        if hit is None:
            return text, removed
        fid = hit.group(1)
        text = text[:hit.start()] + text[hit.end():].lstrip("\n")
        # drop the component entry from whichever GameObject listed it
        text = re.sub(r"\n  - component: \{fileID: " + re.escape(fid) + r"\}(?=\n)", "", text)
        removed += 1


def strip_prefab_instance(text, prefab_guid):
    """Remove a nested PrefabInstance and every trace of it: the block itself, its stripped
    Transform/GameObject shims, the parent Transform's m_Children entry, and any m_Component or
    ordered-index reference to the ids it defined."""
    removed = 0
    while True:
        hit = None
        for m in re.finditer(r"--- !u!1001 &(\d+)\nPrefabInstance:\n(.*?)(?=\n--- !u!|\Z)",
                             text, re.S):
            if f"guid: {prefab_guid}" in m.group(2):
                hit = m
                break
        if hit is None:
            return text, removed
        inst_id = hit.group(1)
        text = text[:hit.start()] + text[hit.end():].lstrip("\n")

        # Stripped shims declare `m_PrefabInstance: {fileID: <inst_id>}`; take them all.
        dead = {inst_id}
        while True:
            grew = False
            for m in re.finditer(r"--- !u!\d+ &(-?\d+) stripped\n\w+:\n(.*?)(?=\n--- !u!|\Z)",
                                 text, re.S):
                if (f"m_PrefabInstance: {{fileID: {inst_id}}}" in m.group(2)
                        and m.group(1) not in dead):
                    dead.add(m.group(1))
                    text = text[:m.start()] + text[m.end():].lstrip("\n")
                    grew = True
                    break
            if not grew:
                break

        for fid in dead:
            text = re.sub(r"\n  - \{fileID: " + re.escape(fid) + r"\}(?=\n)", "", text)
            text = re.sub(r"\n  - component: \{fileID: " + re.escape(fid) + r"\}(?=\n)", "", text)
            text = re.sub(r"\n  - target: \{fileID: " + re.escape(fid) + r"\}(?=\n)", "", text)
        removed += 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    files = sorted(scan())
    edits = []
    leftovers = []

    for path in files:
        try:
            text = open(path, newline="").read()
        except (UnicodeDecodeError, OSError):
            continue
        original = text
        notes = []

        for old, (new, label) in REMAP.items():
            n = text.count(old)
            if n:
                text = text.replace(old, new)
                notes.append(f"{n}x {label}")

        if PRIMITIVE_PLUS_MATERIAL in text:
            text, n = strip_component(text, PRIMITIVE_PLUS_MATERIAL)
            if n:
                notes.append(f"{n}x removed PrimitivePlusMaterial component")

        if SHIFT_SWITCH_PREFAB in text:
            text, n = strip_prefab_instance(text, SHIFT_SWITCH_PREFAB)
            if n:
                notes.append(f"{n}x removed Shift Switch prefab instance")

        # --check reports on the file AS IT IS ON DISK, never on the in-memory rewrite
        for g in list(REMAP) + [PRIMITIVE_PLUS_MATERIAL, SHIFT_SWITCH_PREFAB]:
            if g in original:
                leftovers.append((os.path.relpath(path, REPO), g))

        if text != original or notes:
            edits.append((path, notes, text))

    if args.check:
        if leftovers:
            for p, g in sorted(leftovers):
                print(f"LEFTOVER {p}  still references {g}")
            print(f"FAIL: {len(leftovers)} vendor reference(s) remain outside the packs")
            return 1
        print("OK: no file outside the vendor packs references either pack")
        return 0

    if not edits:
        print("Nothing to do - already re-pointed.")
        return 0

    for path, notes, text in edits:
        with open(path, "w", newline="") as f:
            f.write(text)
        print(f"{os.path.relpath(path, REPO)}\n    {'; '.join(notes)}")
    print(f"\n{len(edits)} file(s) re-pointed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
