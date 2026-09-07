#!/usr/bin/env python3
"""
Switch OFF the hand-placed SkyboxModel in the GAMEPLAY scenes.

`BigMembraneVariant` ("SkyboxModel") is `MembraneBase` scaled to 1600 at the world origin,
drawn with `SkyboxModelGraphMaterial` - `RenderType: Opaque`, render queue 2000, `_Cull: 2`
on an inward-facing sphere. So it is an OPAQUE, DEPTH-WRITING shell around the playfield,
and everything past ~1600 units from the origin is occluded by it. Reported as "the end
appears black and dark, like it's not rendering or culling".

It is redundant as a backdrop: every scene already sets the same `m_SkyboxMaterial`
(b9e5d3c7...), which renders behind everything at infinite depth. The model adds nothing a
skybox does not, and adds a finite occluder a skybox does not.

Its presence correlates exactly with the report: it is in the 5 OLDEST multiplayer scenes
and in none of the 10 newer ones. Scurry (= the Crystal Capture card, `DisplayName: Scurry`)
is one of the 5 and is reported broken; Skim Race has none and is reported fine.

The two TOOL scenes are left alone - there the SkyboxModel is the intended and only geometry
(`CLAUDE.md`: "the only geometry in the tool scenes").

SWITCHED OFF, NOT DELETED, so re-activating one GameObject restores it if a scene turns out
to want it.

    python3 Tools/Build/disable_scene_skybox_model.py [--check]
"""
import re, sys, pathlib

SKYBOX_GUID = "8b8886c5e424ace48980294a663d65a3"   # BigMembraneVariant
SCENES = [
    "Assets/_Scenes/Multiplayer Scenes/ArcadeGameMultiplayer2v2CoOpVsAI.unity",
    "Assets/_Scenes/Multiplayer Scenes/MinigameScurryMultiplayer_Gameplay.unity",
    "Assets/_Scenes/Multiplayer Scenes/MinigameDuelForCellMultiplayer_Gameplay.unity",
    "Assets/_Scenes/Multiplayer Scenes/MinigameFreestyleMultiplayer_Gameplay.unity",
    "Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity",
]
# deliberately NOT touched: _Scenes/Tools/Recording Studio, MattsRecording Studio


def patch(path):
    p = pathlib.Path(path)
    txt = p.read_text()
    m = re.search(r'--- !u!1001 &(-?\d+)\nPrefabInstance:(.*?)(?=\n--- !u!|\Z)', txt, re.S)
    inst = None
    for m in re.finditer(r'--- !u!1001 &(-?\d+)\nPrefabInstance:(.*?)(?=\n--- !u!|\Z)', txt, re.S):
        if f"guid: {SKYBOX_GUID}" in m.group(2):
            assert inst is None, f"{p.name}: two SkyboxModel instances"
            inst = m
    if inst is None:
        return "no SkyboxModel"

    body = inst.group(2)
    # the GameObject the instance already renames is the prefab ROOT - the one to deactivate
    nm = re.search(r'- target: \{fileID: (-?\d+), guid: ' + SKYBOX_GUID +
                   r',\s*\n\s*type: 3\}\n\s+propertyPath: m_Name\n', body)
    assert nm, f"{p.name}: no m_Name override to anchor the root GameObject"
    root = nm.group(1)

    if re.search(r'- target: \{fileID: ' + root + r', guid: ' + SKYBOX_GUID +
                 r',\s*\n\s*type: 3\}\n\s+propertyPath: m_IsActive\n', body):
        return "already off"

    add = (f"    - target: {{fileID: {root}, guid: {SKYBOX_GUID},\n"
           f"        type: 3}}\n"
           f"      propertyPath: m_IsActive\n"
           f"      value: 0\n"
           f"      objectReference: {{fileID: 0}}\n")
    at = body.index("      propertyPath: m_Name\n")
    line_start = body.rindex("    - target:", 0, at)
    new_body = body[:line_start] + add + body[line_start:]

    rebuilt = txt[:inst.start(2)] + new_body + txt[inst.end(2):]
    # validate before writing: one instance, one m_IsActive on the root, nothing else moved
    assert rebuilt.count(f"guid: {SKYBOX_GUID}") == txt.count(f"guid: {SKYBOX_GUID}") + 1
    assert len(rebuilt) > len(txt)
    assert rebuilt.count("--- !u!") == txt.count("--- !u!"), "document count changed"
    p.write_text(rebuilt)
    return f"switched off (root {root})"


def main():
    check = "--check" in sys.argv
    bad = []
    for s in SCENES:
        txt = pathlib.Path(s).read_text()
        inst = [m for m in re.finditer(r'--- !u!1001 &-?\d+\nPrefabInstance:(.*?)(?=\n--- !u!|\Z)', txt, re.S)
                if f"guid: {SKYBOX_GUID}" in m.group(1)]
        off = inst and re.search(r'propertyPath: m_IsActive\n\s+value: 0', inst[0].group(1))
        if check:
            if inst and not off: bad.append(pathlib.Path(s).name)
        else:
            print(f"  {pathlib.Path(s).name:52s} {patch(s)}")
    if check:
        print("SkyboxModel still ACTIVE in: " + ", ".join(bad) if bad
              else "SkyboxModel is off in every gameplay scene")
        sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
