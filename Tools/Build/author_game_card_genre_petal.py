#!/usr/bin/env python3
"""Author the arcade card's GENRE PETAL children onto every live GameCard.

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
TWO petal children per card - `GenrePetal` and `GenrePetalSecondary`, each a
RectTransform + CanvasRenderer + Image - in the card's top-right corner, clear of
GameTitle (which ends at x 0.818) and of the bottom row (AvatarSpace / VesselIcon /
FavoriteIcon, all at y 0.02-0.298).

The second sits UNDER the first rather than beside it, and that is the load-bearing
part of the layout: nearly every card has ONE genre, so a horizontal pair would either
push the primary off its place on every card or leave a gap where the second would be.
Stacked, a single-genre card draws exactly where it always did and a two-genre card
grows downward into space nothing else occupies (x 0.838-0.965 is empty from y 0.30 up
to the first petal).

Both Images ship with NO sprite and `m_Enabled: 0`, because `GameCard.UpdateGenrePetal`
resolves the art and whether there is any at runtime, and an enabled Image with no
sprite draws a white quad. `m_RaycastTarget: 0` - they are decoration and must never eat
the card's own click.

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

# Top-right corner of the 275x203 card: ~35x35 px each, clear of the title and the
# bottom row. The second is stacked directly under the first with a 0.018 gap.
# (field name, anchorMin, anchorMax)
SLOTS = [
    ("GenrePetal", (0.838, 0.72), (0.965, 0.892)),
    ("GenrePetalSecondary", (0.838, 0.53), (0.965, 0.702)),
]

TARGETS = [
    "Assets/_Prefabs/UI Elements/GameCard.prefab",
    "Assets/_Scenes/Menu_Main.unity",
]


def derive_ids(field: str, go_id: str, taken: set) -> tuple:
    """Four stable, unused fileIDs for one card's petal, derived from the card itself.

    Seeded on the FIELD name as well as the card, so the primary petal's ids are
    byte-identical to the ones already shipped and only the second slot is new.
    """
    seed = int(hashlib.sha256(f"{field}:{go_id}".encode()).hexdigest()[:15], 16)
    out, n = [], 0
    while len(out) < 4:
        candidate = str((seed + n * 7919) % 9_000_000_000_000_000 + 100_000_000)
        n += 1
        if candidate not in taken:
            taken.add(candidate)
            out.append(candidate)
    return tuple(out)


def petal_blocks(name, anchor_min, anchor_max, go_id_new, rt_id, cr_id, img_id, father_rt) -> str:
    amin = f"{{x: {anchor_min[0]}, y: {anchor_min[1]}}}"
    amax = f"{{x: {anchor_max[0]}, y: {anchor_max[1]}}}"
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
  m_Name: {name}
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

    cards, already, missing = [], 0, []
    for i, part in enumerate(parts):
        if not part.startswith("!u!114 &"):
            continue
        if f"guid: {GAMECARD_SCRIPT_GUID}" not in part:
            continue
        g = re.search(r"m_GameObject: \{fileID: (\d+)\}", part)
        if not g:
            continue
        wanted = [s for s in SLOTS if not re.search(rf"^  {s[0]}: ", part, re.M)]
        already += len(SLOTS) - len(wanted)
        if wanted:
            cards.append((i, g.group(1), wanted))
            missing.extend(f"{g.group(1)}/{s[0]}" for s in wanted)

    if check or not cards:
        return already, missing

    new_blocks = []
    for idx, go_id, wanted in cards:
        father_rt = rt_of_go.get(go_id)
        if father_rt is None:
            raise SystemExit(f"{path}: GameCard GameObject {go_id} has no RectTransform")

        for order, (field, anchor_min, anchor_max) in enumerate(wanted):
            new_go, new_rt, new_cr, new_img = derive_ids(field, go_id, taken)

            # 1) wire the serialized field. The first petal goes straight after VesselIcon so
            #    the two identity marks read together; each later one follows its predecessor.
            body = parts[idx]
            anchors = [s[0] for s in SLOTS[:SLOTS.index((field, anchor_min, anchor_max))]][::-1]
            anchors.append("VesselIcon")
            for after in anchors:
                if re.search(rf"^  {after}: .*$", body, re.M):
                    body = re.sub(rf"^(  {after}: .*)$",
                                  r"\1\n  " + field + ": {fileID: " + new_img + "}",
                                  body, count=1, flags=re.M)
                    break
            else:
                body = body.rstrip("\n") + f"\n  {field}: {{fileID: {new_img}}}\n"
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

            new_blocks.append(petal_blocks(field, anchor_min, anchor_max,
                                           new_go, new_rt, new_cr, new_img, father_rt))

    out = "--- ".join(parts)
    if not out.endswith("\n"):
        out += "\n"

    # Insert before the scene-roots document so it stays last, else append.
    roots = re.search(r"^--- !u!1660057539 &", out, re.M)
    blob = "".join(new_blocks)
    out = out[:roots.start()] + blob + out[roots.start():] if roots else out + blob

    path.write_text(out, encoding="utf-8")
    return already, missing


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
                problems.append(f"{rel}: {len(missing)} petal slot(s) unwired "
                                f"({', '.join(missing[:6])}"
                                f"{'...' if len(missing) > 6 else ''})")
            print(f"  {rel}: {present} slot(s) wired, {len(missing)} missing")
        else:
            total_written += len(missing)
            print(f"  {rel}: {present} already wired, {len(missing)} written")

    if check:
        if problems:
            print("\ngenre-petal check: FAIL")
            for p in problems:
                print("  -", p)
            return 1
        print(f"\ngenre-petal check: OK ({total_present} petal slot(s) wired across "
              f"{total_present // len(SLOTS)} card(s))")
        return 0

    print(f"\nwrote {total_written} petal object(s); {total_present} already present")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
