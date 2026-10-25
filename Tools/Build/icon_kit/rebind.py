"""Point a HUD prefab (or any asset) at the icons the kit wrote - by GUID, in YAML, idempotently.

A spec may carry `REBIND = [...]` with two kinds of step:

    dict(file="Assets/.../SquirrelHUDVariant.prefab", old="28ece4...", icon="Squirrel_CrystalJoust", expect=1)
        every `guid: <old>` in that file becomes the icon's guid (asserting the count the first time;
        0 occurrences afterwards is the "already done" state and passes)
    dict(file="Assets/.../SparrowHUDVariant.prefab", list="missileIcons", icons=["Sparrow_Rockets_0", ...])
        the serialized sprite list under `<list>:` is rewritten to those icons, in order
    dict(file="Assets/.../SquirrelHUDVariant.prefab", pair=dict(container="4425814395415128621",
         view="<view MonoBehaviour fileID>", children=[("JoustSelf", "Squirrel_JoustSelf", "joustSelfIcon"), ...]))
        the icon Image `container` becomes a transparent holder (no sprite, alpha 0) and gets one stretched child
        Image per entry (name, icon, the view field that is pointed at it) - the Dolphin's jaw-pair arrangement,
        for a card whose parts are tinted separately. Idempotent: an existing child of that name is left alone.

The guid of an icon is read from its .meta (the kit writes the meta before it rebinds). A rebind never touches a
file it was not named for: the same placeholder sprite can be referenced from another prefab on purpose (the
Squirrel's skull is also the Jousts metric icon in ModeControlsLibrary.asset), and each such site is its own step.
"""
import os
import re


def guid_of(meta_path):
    m = re.search(r"^guid: (\w+)$", open(meta_path, encoding="utf-8").read(), re.M)
    if not m:
        raise SystemExit(f"no guid in {meta_path}")
    return m.group(1)


def _sprite_ref(guid):
    return f"{{fileID: 21300000, guid: {guid}, type: 3}}"


def _fid(seed):
    """A deterministic positive int64 fileID from a seed string (Unity wants a non-zero signed 64-bit id)."""
    import hashlib
    return int(hashlib.md5(seed.encode()).hexdigest()[:15], 16) + 1


def _block(text, fid):
    i = text.find(f"&{fid}\n")
    if i < 0:
        raise SystemExit(f"fileID {fid} not in file")
    start = text.rfind("--- !u!", 0, i)
    end = text.find("\n--- !u!", i + 1)
    return start, (end + 1 if end > 0 else len(text))


def _set_in_block(text, fid, key, value):
    a, b = _block(text, fid)
    blk = text[a:b]
    if not re.search(rf"^\s*{re.escape(key)}: ", blk, re.M):
        raise SystemExit(f"{key} not found in block {fid}")
    new = re.sub(rf"^(\s*{re.escape(key)}: ).*$", lambda m: m.group(1) + value, blk, count=1, flags=re.M)
    return text[:a] + new + text[b:]


def _child_blocks(go_name, parent_rt, layer, guid, fid_go, fid_rt, fid_cr, fid_img):
    return f"""--- !u!1 &{fid_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {fid_rt}}}
  - component: {{fileID: {fid_cr}}}
  - component: {{fileID: {fid_img}}}
  m_Layer: {layer}
  m_Name: {go_name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{fid_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {parent_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{fid_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{fid_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {fid_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
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


def _apply_pair(text, step, icon_meta):
    pair = step["pair"]
    container = pair["container"]
    a, b = _block(text, container)
    go = re.search(r"m_GameObject: \{fileID: (\d+)\}", text[a:b]).group(1)
    ga, gb = _block(text, go)
    gblk = text[ga:gb]
    layer = re.search(r"m_Layer: (\d+)", gblk).group(1)
    comps = re.findall(r"component: \{fileID: (\d+)\}", gblk)
    rt = next(c for c in comps if text[_block(text, c)[0]:_block(text, c)[0] + 40].startswith("--- !u!224"))
    # the container: no sprite, alpha 0 - it kerns and punches, its children draw
    text = _set_in_block(text, container, "m_Sprite", "{fileID: 0}")
    text = _set_in_block(text, container, "m_Color", "{r: 1, g: 1, b: 1, a: 0}")
    for name, icon, field in pair["children"]:
        seed = f"{step['file']}/{container}/{name}"
        fid_go, fid_rt, fid_cr, fid_img = (_fid(seed + k) for k in ("go", "rt", "cr", "img"))
        guid = guid_of(icon_meta[icon])
        if f"&{fid_img}\n" not in text:
            text = text.rstrip("\n") + "\n" + _child_blocks(name, rt, layer, guid, fid_go, fid_rt, fid_cr, fid_img)
            ra, rb = _block(text, rt)
            rblk = text[ra:rb]
            if "m_Children: []" in rblk:
                rblk = rblk.replace("m_Children: []", f"m_Children:\n  - {{fileID: {fid_rt}}}", 1)
            else:
                rblk = re.sub(r"(m_Children:\n(?:  - \{fileID: \d+\}\n)*)", lambda m: m.group(1) + f"  - {{fileID: {fid_rt}}}\n", rblk, count=1)
            text = text[:ra] + rblk + text[rb:]
        else:
            text = _set_in_block(text, fid_img, "m_Sprite", f"{{fileID: 21300000, guid: {guid}, type: 3}}")
        if pair.get("view"):
            va, vb = _block(text, pair["view"])
            vblk = text[va:vb]
            if re.search(rf"^\s*{field}: ", vblk, re.M):
                vblk = re.sub(rf"^(\s*{field}: ).*$", lambda m: m.group(1) + f"{{fileID: {fid_img}}}", vblk, count=1, flags=re.M)
            else:
                anchor = pair.get("after", "impactIcon")
                vblk = re.sub(rf"^(\s*{anchor}: .*\n)", lambda m: m.group(1) + f"  {field}: {{fileID: {fid_img}}}\n", vblk, count=1, flags=re.M)
            text = text[:va] + vblk + text[vb:]
    return text


def apply(root, steps, icon_meta, check=False):
    """icon_meta: name -> .meta path. Returns the list of files that changed (or would, under check)."""
    changed = []
    for step in steps:
        path = os.path.join(root, step["file"])
        text = open(path, encoding="utf-8", newline="").read()
        new = text
        if "old" in step:
            guid = guid_of(icon_meta[step["icon"]])
            n = text.count(f"guid: {step['old']}")
            if n == 0 and text.count(f"guid: {guid}") == 0:
                raise SystemExit(f"{step['file']}: neither {step['old']} nor {step['icon']}'s guid is referenced - the binding moved")
            if n and step.get("expect") and n != step["expect"]:
                raise SystemExit(f"{step['file']}: {n} references to {step['old']}, expected {step['expect']}")
            new = text.replace(f"guid: {step['old']}", f"guid: {guid}")
        elif "pair" in step:
            new = _apply_pair(text, step, icon_meta)
        elif "list" in step:
            key = step["list"]
            m = re.search(rf"^(\s*){re.escape(key)}:\n((?:\s*- \{{fileID: \d+, guid: \w+, type: 3\}}\n)+)", text, re.M)
            if not m:
                raise SystemExit(f"{step['file']}: no sprite list `{key}:`")
            indent = m.group(1)
            body = "".join(f"{indent}- {_sprite_ref(guid_of(icon_meta[n]))}\n" for n in step["icons"])
            new = text[:m.start(2)] + body + text[m.end(2):]
        if new != text:
            changed.append(step["file"])
            if not check:
                open(path, "w", encoding="utf-8", newline="").write(new)
    return changed
