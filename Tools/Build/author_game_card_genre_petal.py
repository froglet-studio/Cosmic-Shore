#!/usr/bin/env python3
"""Author the arcade card's GENRE PETAL child onto every live GameCard.

WHY THIS IS A TOOL AND NOT A PREFAB EDIT
----------------------------------------
`Assets/_Prefabs/UI Elements/GameCard.prefab` is NOT what runs. Menu_Main carries 36
scene-local GameCard objects that are not instances of it (and the two `Arcade Screen`
prefabs that also hold cards are referenced by nothing at all), so editing the prefab
asset alone ships a feature that renders on no card - the GAMECANVAS.md §9 trap, one
prefab over. This writes the child onto every live card AND the canonical prefab, so
the two cannot drift.

WHAT IT WRITES
--------------
One `GenrePetal` child per card - RectTransform + CanvasRenderer + Image - anchored in
the card's top-right corner, clear of GameTitle (which ends at x 0.818) and of the
bottom row (AvatarSpace / VesselIcon / FavoriteIcon, all at y 0.02-0.298). The Image
ships with NO sprite and `m_Enabled: 0`, because `GameCard.UpdateGenrePetal` resolves
both the art and whether there is any at runtime, and an enabled Image with no sprite
draws a white quad. `m_RaycastTarget: 0` - it is decoration and must never eat the
card's own click.

fileIDs are DERIVED from the card's own GameObject id, so a re-run is a no-op rather
than a second copy, and are collision-checked against the file before use.

Usage:
  python3 Tools/Build/author_game_card_genre_petal.py            # write
  python3 Tools/Build/author_game_card_genre_petal.py --check    # fail if any card lacks it
"""

import hashlib
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
assert (ROOT / "Assets").is_dir(), f"ROOT is wrong: {ROOT}"

GAMECARD_SCRIPT_GUID = "dbaebc1ed836d1847b41976e206448f5"
IMAGE_SCRIPT_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"

# Top-right corner of the 275x203 card: ~35x35 px, clear of the title and the bottom row.
ANCHOR_MIN = (0.838, 0.72)
ANCHOR_MAX = (0.965, 0.892)

TARGETS = [
    "Assets/_Prefabs/UI Elements/GameCard.prefab",
    "Assets/_Scenes/Menu_Main.unity",
]


def derive_ids(go_id: str, taken: set) -> tuple:
    """Four stable, unused fileIDs for one card's petal, derived from the card itself."""
    seed = int(hashlib.sha256(f"GenrePetal:{go_id}".encode()).hexdigest()[:15], 16)
    out, n = [], 0
    while len(out) < 4:
        candidate = str((seed + n * 7919) % 9_000_000_000_000_000 + 100_000_000)
        n += 1
        if candidate not in taken:
            taken.add(candidate)
            out.append(candidate)
    return tuple(out)


def petal_blocks(go_id_new, rt_id, cr_id, img_id, father_rt) -> str:
    amin = f"{{x: {ANCHOR_MIN[0]}, y: {ANCHOR_MIN[1]}}}"
    amax = f"{{x: {ANCHOR_MAX[0]}, y: {ANCHOR_MAX[1]}}}"
    return f"""--- !u!1 &{go_id_new}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {rt_id}}}
  - component: {{fileID: {cr_id}}}
  - component: {{fileID: {img_id}}}
  m_Layer: 5
  m_Name: GenrePetal
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{rt_id}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id_new}}}
  m_LocalRotation: {{x: -0, y: -0, z: -0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {father_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {amin}
  m_AnchorMax: {amax}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{cr_id}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id_new}}}
  m_CullTransparentMesh: 1
--- !u!114 &{img_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id_new}}}
  m_Enabled: 0
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_SCRIPT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
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


def split_docs(text):
    """[(header_or_None, body)] preserving the file's preamble as docs[0]."""
    parts = text.split("--- ")
    return parts


def process(path: Path, check: bool):
    text = path.read_text(encoding="utf-8")
    taken = set(re.findall(r"^--- !u!\d+ &(\d+)", text, re.M))

    parts = split_docs(text)
    # index: GameObject id -> its RectTransform id
    rt_of_go = {}
    for part in parts[1:]:
        m = re.match(r"!u!224 &(\d+)\nRectTransform:", part)
        if m:
            g = re.search(r"m_GameObject: \{fileID: (\d+)\}", part)
            if g:
                rt_of_go[g.group(1)] = m.group(1)

    cards, already = [], 0
    for i, part in enumerate(parts):
        if not part.startswith("!u!114 &"):
            continue
        if f"guid: {GAMECARD_SCRIPT_GUID}" not in part:
            continue
        if re.search(r"^  GenrePetal: ", part, re.M):
            already += 1
            continue
        g = re.search(r"m_GameObject: \{fileID: (\d+)\}", part)
        if not g:
            continue
        cards.append((i, g.group(1)))

    if check:
        return already, [go for _, go in cards]

    if not cards:
        return already, []

    new_blocks = []
    for idx, go_id in cards:
        father_rt = rt_of_go.get(go_id)
        if father_rt is None:
            raise SystemExit(f"{path}: GameCard GameObject {go_id} has no RectTransform")

        new_go, new_rt, new_cr, new_img = derive_ids(go_id, taken)

        # 1) wire the serialized field, right after VesselIcon so the two identity marks read together
        body = parts[idx]
        if re.search(r"^  VesselIcon: .*$", body, re.M):
            body = re.sub(r"^(  VesselIcon: .*)$",
                          r"\1\n  GenrePetal: {fileID: " + new_img + "}",
                          body, count=1, flags=re.M)
        else:
            body = body.rstrip("\n") + f"\n  GenrePetal: {{fileID: {new_img}}}\n"
        parts[idx] = body

        # 2) list the child on the card's own RectTransform
        for j, part in enumerate(parts):
            if not part.startswith(f"!u!224 &{father_rt}\n"):
                continue
            if "  m_Children: []\n" in part:
                parts[j] = part.replace("  m_Children: []\n",
                                        f"  m_Children:\n  - {{fileID: {new_rt}}}\n", 1)
            else:
                parts[j] = re.sub(r"^(  m_Father: )", f"  - {{fileID: {new_rt}}}\n" + r"\1",
                                  part, count=1, flags=re.M)
            break

        new_blocks.append(petal_blocks(new_go, new_rt, new_cr, new_img, father_rt))

    out = "--- ".join(parts)
    if not out.endswith("\n"):
        out += "\n"

    # Insert before the scene-roots document so it stays last, else append.
    roots = re.search(r"^--- !u!1660057539 &", out, re.M)
    blob = "".join(new_blocks)
    out = out[:roots.start()] + blob + out[roots.start():] if roots else out + blob

    path.write_text(out, encoding="utf-8")
    return already, [go for _, go in cards]


def main():
    check = "--check" in sys.argv
    problems, total_written, total_present = [], 0, 0

    for rel in TARGETS:
        path = ROOT / rel
        if not path.exists():
            problems.append(f"missing target: {rel}")
            continue
        present, missing = process(path, check)
        total_present += present
        if check:
            if missing:
                problems.append(f"{rel}: {len(missing)} GameCard(s) with no GenrePetal wired "
                                f"(GameObject ids {', '.join(missing[:6])}"
                                f"{'...' if len(missing) > 6 else ''})")
            print(f"  {rel}: {present} card(s) wired, {len(missing)} missing")
        else:
            total_written += len(missing)
            print(f"  {rel}: {present} already wired, {len(missing)} written")

    if check:
        if problems:
            print("\ngenre-petal check: FAIL")
            for p in problems:
                print("  -", p)
            return 1
        print(f"\ngenre-petal check: OK ({total_present} card(s) wired)")
        return 0

    print(f"\nwrote {total_written} GenrePetal object(s); {total_present} already present")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
