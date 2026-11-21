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
RectTransform + CanvasRenderer + Image - in the card's BOTTOM-LEFT corner at ~87x87 px
on a 275x203 card, sharing FavoriteIcon's vertical centre so the two read as one pair in
opposite lower corners (see SLOTS for what that costs at the bottom edge).

The second sits ABOVE the first rather than beside it, and that is the load-bearing
part of the layout: nearly every card has ONE genre, so a horizontal pair would either
push the primary off its place on every card or leave a gap where the second would be.
Stacked, a single-genre card always draws in the same corner and a two-genre card grows
upward. At this size two stacked petals are 91% of the card's height, so the second one
DOES cross the title band - stated rather than designed around, because exactly one
shipped card (Brood Rush) has a second genre and the Arena roster it belongs to is being
treated separately anyway.

Both Images ship with NO sprite and `m_Enabled: 0`, because `GameCard.UpdateGenrePetal`
resolves the art and whether there is any at runtime, and an enabled Image with no
sprite draws a white quad. `m_RaycastTarget: 0` - they are decoration and must never eat
the card's own click (which matters more here than it did in the corner: the petal now
lies over AvatarSpace, the party-pick chip row).

fileIDs are DERIVED from the card's own GameObject id, so a re-run is a no-op rather
than a second copy, and are collision-checked against the file before use.

A petal that is ALREADY wired has its anchors REFRESHED to the table below - a slot the
tool owns is a slot the tool keeps current, or moving the row means hand-editing 74
RectTransforms. `--check` reports drift as a failure for the same reason.

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

# Bottom-left corner of the 275x203 card, ~87x87 px each, on the 0.02 margin the bottom
# row already uses - and CENTRED ON THE SAME y AS FavoriteIcon, so the petal and the star
# read as one pair in opposite lower corners. The star's band is y 0.02-0.2978, centre
# 0.1588889; the petal is 0.43 tall, so it spans that centre +/- 0.215 and consequently
# hangs 11.37 px BELOW the card rect. That is deliberate and it is bounded: `Background`
# is a 313x208 plate offset off the card's top-left and reaches 11.48 px below the rect,
# so the petal lands 0.11 px inside the plate's own bottom edge. It does cross `Border`
# (the arcade-card frame, which IS the card rect) and draws over it, since it is the last
# child - a badge clipped to the corner rather than a thing inside the frame.
#
# The second is stacked ABOVE the first with the old 3.65 px gap scaled by the same 2.5.
# (field name, anchorMin, anchorMax)
SLOTS = [
    ("GenrePetal", (0.02, -0.0561111), (0.3375, 0.3738889)),
    ("GenrePetalSecondary", (0.02, 0.4188889), (0.3375, 0.8488889)),
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


def anchor_pair(anchor_min, anchor_max) -> tuple:
    return (f"{{x: {anchor_min[0]}, y: {anchor_min[1]}}}",
            f"{{x: {anchor_max[0]}, y: {anchor_max[1]}}}")


def petal_blocks(name, anchor_min, anchor_max, go_id_new, rt_id, cr_id, img_id, father_rt) -> str:
    amin, amax = anchor_pair(anchor_min, anchor_max)
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
    # indexes: GameObject id -> its RectTransform id; RectTransform id -> doc index;
    # component id -> the GameObject it hangs off.
    rt_of_go, doc_of_rt, go_of_component = {}, {}, {}
    for i, part in enumerate(parts[1:], start=1):
        m = re.match(r"!u!(\d+) &(\d+)\n", part)
        if not m:
            continue
        g = re.search(r"m_GameObject: \{fileID: (\d+)\}", part)
        if not g:
            continue
        go_of_component[m.group(2)] = g.group(1)
        if m.group(1) == "224":
            rt_of_go[g.group(1)] = m.group(2)
            doc_of_rt[m.group(2)] = i

    cards, already, missing, drifted = [], 0, [], []
    for i, part in enumerate(parts):
        if not part.startswith("!u!114 &"):
            continue
        if f"guid: {GAMECARD_SCRIPT_GUID}" not in part:
            continue
        g = re.search(r"m_GameObject: \{fileID: (\d+)\}", part)
        if not g:
            continue
        card_go = g.group(1)

        wanted = []
        for field, anchor_min, anchor_max in SLOTS:
            wired = re.search(rf"^  {field}: \{{fileID: (\d+)\}}$", part, re.M)
            if not wired:
                wanted.append((field, anchor_min, anchor_max))
                continue
            already += 1

            # The slot exists - keep its rect current with the table above.
            petal_go = go_of_component.get(wired.group(1))
            petal_rt = rt_of_go.get(petal_go) if petal_go else None
            doc = doc_of_rt.get(petal_rt) if petal_rt else None
            if doc is None:
                drifted.append(f"{card_go}/{field}: unresolvable RectTransform")
                continue

            amin, amax = anchor_pair(anchor_min, anchor_max)
            body = parts[doc]
            fixed = re.sub(r"^  m_AnchorMin: .*$", f"  m_AnchorMin: {amin}", body, count=1, flags=re.M)
            fixed = re.sub(r"^  m_AnchorMax: .*$", f"  m_AnchorMax: {amax}", fixed, count=1, flags=re.M)
            if fixed != body:
                drifted.append(f"{card_go}/{field}")
                if not check:
                    parts[doc] = fixed

        if wanted:
            cards.append((i, card_go, wanted))
            missing.extend(f"{card_go}/{s[0]}" for s in wanted)

    if check:
        return already, missing, drifted

    new_blocks = []
    for idx, go_id, wanted in cards:
        father_rt = rt_of_go.get(go_id)
        if father_rt is None:
            raise SystemExit(f"{path}: GameCard GameObject {go_id} has no RectTransform")

        for field, anchor_min, anchor_max in wanted:
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

    if new_blocks:
        # Insert before the scene-roots document so it stays last, else append.
        roots = re.search(r"^--- !u!1660057539 &", out, re.M)
        blob = "".join(new_blocks)
        out = out[:roots.start()] + blob + out[roots.start():] if roots else out + blob

    path.write_text(out, encoding="utf-8")
    return already, missing, drifted


def main():
    check = "--check" in sys.argv
    problems, total_written, total_present, total_moved = [], 0, 0, 0

    for rel in TARGETS:
        path = ROOT / rel
        if not path.exists():
            problems.append(f"missing target: {rel}")
            continue
        present, missing, drifted = process(path, check)
        total_present += present
        total_moved += len(drifted)
        if check:
            if missing:
                problems.append(f"{rel}: {len(missing)} petal slot(s) unwired "
                                f"({', '.join(missing[:6])}"
                                f"{'...' if len(missing) > 6 else ''})")
            if drifted:
                problems.append(f"{rel}: {len(drifted)} petal slot(s) off the authored rect "
                                f"({', '.join(drifted[:6])}"
                                f"{'...' if len(drifted) > 6 else ''})")
            print(f"  {rel}: {present} slot(s) wired, {len(missing)} missing, "
                  f"{len(drifted)} drifted")
        else:
            total_written += len(missing)
            print(f"  {rel}: {present} already wired, {len(missing)} written, "
                  f"{len(drifted)} re-anchored")

    if check:
        if problems:
            print("\ngenre-petal check: FAIL")
            for p in problems:
                print("  -", p)
            return 1
        print(f"\ngenre-petal check: OK ({total_present} petal slot(s) wired across "
              f"{total_present // len(SLOTS)} card(s))")
        return 0

    print(f"\nwrote {total_written} petal object(s); re-anchored {total_moved}; "
          f"{total_present} already present")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
