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
        Image per entry (name, icon, the view field that is pointed at it; None for a part the view never tints,
        e.g. a white ring; an optional 4th item (ax, ay, w, h) point-anchors the child instead of stretching it,
        for a part the view ROTATES about its own centre) - the Dolphin's jaw-pair arrangement, for a card whose
        parts are tinted, swapped or turned separately. `fields={...}` sets other view fields: ("icon", name) for
        a sprite, or a raw YAML value (`"[]"` empties a serialized LIST, its items included). A child whose icon is
        None is a bare GAUGE Image (no sprite, Filled / Vertical / origin Bottom, empty): the lockup adopts it as the
        card's plate fill when `binding=dict(element=4, gauge="<child name>")` names it on the view's abilityIcons
        entry (the Dolphin's Time card: the boost bank is the plate, not a ring); `gauge=None` UNBINDS the entry's
        gauge (the Sparrow's roll pip, 2026-10-26). A field whose value is None is DELETED from the view block (a
        field the C# no longer has). Idempotent: an existing child of that name is left alone.
    dict(file="Assets/.../ScarabHUDVariant.prefab", core=dict(view="<view fileID>", donor="<button GameObject fileID>",
         name="Drift", ability=1, input=2, icon="Scarab_Drift", omni="Scarab_BallForge"))
        a NON-ELEMENTAL card (VesselHUDView.coreAbilities, CoreAbility `ability`, control `input`): the donor ability
        button (its RectTransform, plate Image and Button) and its `Icon` child are cloned beside it as `<name>Button`
        / `<name>Icon` (deterministic fileIDs; the donor's other children are not copied), the icon wears `icon`, and
        a coreAbilities entry binds it - the Squirrel's DriftButton shape. `omni` (optional) also sets the view's
        omniAbilitySprite, the omni crystal card's art. Idempotent.
    dict(file="Assets/_Prefabs/Spacevessels/Serpent.prefab", proxy=dict(view="<view fileID>", instance=<the vessel's
         PrefabInstance of the HUD variant>, source_file="<the variant>", source_guid="<the variant's guid>",
         after="pipEmptyColor", refs=[("seedWallBricksIcon", "<container fileID>", "SeedWallBricks"), ...],
         fields={"omniAbilitySprite": ("icon", "Serpent_WallShield")}))
        for a view that lives on the VESSEL prefab and reaches the HUD variant's Images through stripped proxies (the
        Serpent, author_hull_ability_rows.py): each ref names a child a `pair` step made in the variant; its proxy
        (fileID = the child's Image fileID XOR `instance`, Unity's rule) is added to the vessel once and the view
        field pointed at it. `fields` as in `pair`. Idempotent.
    dict(file="Assets/.../SparrowHUDVariant.prefab", deactivate="<Image fileID>")
        switches that Image's GameObject off (m_IsActive: 0) - a readout nothing binds any more, left in place

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


def _set_field(blk, key, value):
    """Replace `key: <value>` in a YAML block. A flow mapping value may WRAP onto a second line (Unity breaks long
    `{fileID: ..., guid: ..., type: 3}` values after the guid), so a `{...}` value is matched across lines."""
    pat = rf"^(\s*{re.escape(key)}: )(\{{[^}}]*\}}|[^\n]*)"
    if not re.search(pat, blk, re.M):
        return None
    return re.sub(pat, lambda m: m.group(1) + value, blk, count=1, flags=re.M)


def _set_in_block(text, fid, key, value):
    a, b = _block(text, fid)
    new = _set_field(text[a:b], key, value)
    if new is None:
        raise SystemExit(f"{key} not found in block {fid}")
    return text[:a] + new + text[b:]


def _child_blocks(go_name, parent_rt, layer, guid, fid_go, fid_rt, fid_cr, fid_img, rect=None):
    """rect: None = stretched to the container; (ax, ay, w, h) = point-anchored at the container's centre, offset
    (ax, ay) in the container's units, size (w, h) - a part the view ROTATES about its own centre (the drift hull)."""
    if guid is None:   # a bare gauge: Filled / Vertical / Bottom, empty; the lockup gives it its plain sprite
        sprite, itype, aspect, fmethod, famount = "{fileID: 0}", 3, 0, 1, 0
    else:
        sprite, itype, aspect, fmethod, famount = f"{{fileID: 21300000, guid: {guid}, type: 3}}", 0, 1, 4, 1
    if rect is None:
        anchors = "  m_AnchorMin: {x: 0, y: 0}\n  m_AnchorMax: {x: 1, y: 1}\n  m_AnchoredPosition: {x: 0, y: 0}\n  m_SizeDelta: {x: 0, y: 0}"
    else:
        ax, ay, w, h = rect
        anchors = f"  m_AnchorMin: {{x: 0.5, y: 0.5}}\n  m_AnchorMax: {{x: 0.5, y: 0.5}}\n  m_AnchoredPosition: {{x: {ax}, y: {ay}}}\n  m_SizeDelta: {{x: {w}, y: {h}}}"
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
{anchors}
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
  m_Sprite: {sprite}
  m_Type: {itype}
  m_PreserveAspect: {aspect}
  m_FillCenter: 1
  m_FillMethod: {fmethod}
  m_FillAmount: {famount}
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
    for child in pair["children"]:
        name, icon, field = child[:3]
        rect = child[3] if len(child) > 3 else None
        seed = f"{step['file']}/{container}/{name}"
        fid_go, fid_rt, fid_cr, fid_img = (_fid(seed + k) for k in ("go", "rt", "cr", "img"))
        guid = guid_of(icon_meta[icon]) if icon else None
        if f"&{fid_img}\n" not in text:
            text = text.rstrip("\n") + "\n" + _child_blocks(name, rt, layer, guid, fid_go, fid_rt, fid_cr, fid_img, rect)
            ra, rb = _block(text, rt)
            rblk = text[ra:rb]
            if "m_Children: []" in rblk:
                rblk = rblk.replace("m_Children: []", f"m_Children:\n  - {{fileID: {fid_rt}}}", 1)
            else:
                rblk = re.sub(r"(m_Children:\n(?:  - \{fileID: \d+\}\n)*)", lambda m: m.group(1) + f"  - {{fileID: {fid_rt}}}\n", rblk, count=1)
            text = text[:ra] + rblk + text[rb:]
        elif guid:
            text = _set_in_block(text, fid_img, "m_Sprite", f"{{fileID: 21300000, guid: {guid}, type: 3}}")
        if pair.get("view") and field:
            va, vb = _block(text, pair["view"])
            vblk = text[va:vb]
            replaced = _set_field(vblk, field, f"{{fileID: {fid_img}}}")
            if replaced is not None:
                vblk = replaced
            else:
                anchor = pair.get("after", "impactIcon")
                vblk = re.sub(rf"^(\s*{anchor}: .*\n)", lambda m: m.group(1) + f"  {field}: {{fileID: {fid_img}}}\n", vblk, count=1, flags=re.M)
            text = text[:va] + vblk + text[vb:]
    # The card's plate GAUGE: the abilityIcons entry for `element` points its `gauge` at the named child.
    if pair.get("binding"):
        bnd = pair["binding"]
        seed = f"{step['file']}/{container}/{bnd.get('gauge') or ''}"
        fid_img = _fid(seed + "img")
        va, vb = _block(text, pair["view"])
        vblk = text[va:vb]
        pat = rf"^(  - element: {bnd['element']}\n    icon: [^\n]*\n    upgradedSprite: [^\n]*\n)(    gauge: [^\n]*\n)?"
        if not re.search(pat, vblk, re.M):
            raise SystemExit(f"{step['file']}: no abilityIcons entry for element {bnd['element']} on {pair['view']}")
        gauge_ref = f"{{fileID: {fid_img}}}" if bnd.get("gauge") else "{fileID: 0}"
        vblk = re.sub(pat, lambda m: m.group(1) + f"    gauge: {gauge_ref}\n", vblk, count=1, flags=re.M)
        text = text[:va] + vblk + text[vb:]
    # Other serialized fields on the view the pair needs: a sprite (("icon", name)), a raw YAML value, or "[]" to
    # empty a serialized list (its items go with it).
    for field, value in pair.get("fields", {}).items():
        v = _sprite_ref(guid_of(icon_meta[value[1]])) if isinstance(value, tuple) else value
        va, vb = _block(text, pair["view"])
        vblk = text[va:vb]
        if v is None:
            vblk = re.sub(rf"^\s*{re.escape(field)}: [^\n]*\n", "", vblk, count=1, flags=re.M)
            text = text[:va] + vblk + text[vb:]
            continue
        if v == "[]":
            vblk = re.sub(rf"^(\s*{re.escape(field)}:)[^\n]*\n(?:\s+- [^\n]*\n)*", lambda m: m.group(1) + " []\n", vblk, count=1, flags=re.M)
            text = text[:va] + vblk + text[vb:]
            continue
        replaced = _set_field(vblk, field, v)
        if replaced is None:
            anchor = pair.get("after", "impactIcon")
            replaced = re.sub(rf"^(\s*{anchor}: .*\n)", lambda m: m.group(1) + f"  {field}: {v}\n", vblk, count=1, flags=re.M)
        text = text[:va] + replaced + text[vb:]
    return text


def _rt_of_go(text, go):
    ga, gb = _block(text, go)
    for c in re.findall(r"component: \{fileID: (\d+)\}", text[ga:gb]):
        if text[_block(text, c)[0]:_block(text, c)[0] + 40].startswith("--- !u!224"):
            return c
    raise SystemExit(f"no RectTransform on {go}")


def _apply_core(text, step, icon_meta):
    core = step["core"]
    seed = f"{step['file']}/core/{core['name']}"
    donor_go = core["donor"]
    donor_rt = _rt_of_go(text, donor_go)
    ra, rb = _block(text, donor_rt)
    parent_rt = re.search(r"m_Father: \{fileID: (\d+)\}", text[ra:rb]).group(1)
    kids = re.findall(r"- \{fileID: (\d+)\}", text[ra:rb].split("m_Children:")[1].split("m_Father")[0])
    icon_rt = next(k for k in kids
                   if re.search(r"m_Name: Icon\n", text[slice(*_block(text, re.search(r"m_GameObject: \{fileID: (\d+)\}", text[slice(*_block(text, k))]).group(1)))]))
    icon_go = re.search(r"m_GameObject: \{fileID: (\d+)\}", text[slice(*_block(text, icon_rt))]).group(1)
    ids = {}
    for go in (donor_go, icon_go):
        ids[go] = None
        ga, gb = _block(text, go)
        for c in re.findall(r"component: \{fileID: (\d+)\}", text[ga:gb]):
            ids[c] = None
    for k in ids:
        ids[k] = str(_fid(seed + k))
    new_button_rt, new_icon_rt = ids[donor_rt], ids[icon_rt]
    new_icon_img = next(ids[c] for c in re.findall(r"component: \{fileID: (\d+)\}", text[slice(*_block(text, icon_go))])
                        if "m_Sprite:" in text[slice(*_block(text, c))])
    guid = guid_of(icon_meta[core["icon"]])
    if f"&{new_icon_img}\n" not in text:
        blocks = []
        for old in ids:
            blk = text[slice(*_block(text, old))].rstrip("\n") + "\n"
            for o, n in ids.items():
                blk = re.sub(rf"(&|fileID: ){o}\b", lambda m: m.group(1) + n, blk)
            if old == donor_go:
                blk = re.sub(r"m_Name: [^\n]*", f"m_Name: {core['name']}Button", blk, count=1)
            elif old == icon_go:
                blk = re.sub(r"m_Name: [^\n]*", f"m_Name: {core['name']}Icon", blk, count=1)
            elif old == donor_rt:
                blk = re.sub(r"m_Children:\n(?:  - \{fileID: \d+\}\n)*", f"m_Children:\n  - {{fileID: {new_icon_rt}}}\n", blk, count=1)
            blocks.append(blk)
        text = text.rstrip("\n") + "\n" + "".join(blocks)
        pa, pb = _block(text, parent_rt)
        pblk = re.sub(r"(m_Children:\n(?:  - \{fileID: \d+\}\n)*)", lambda m: m.group(1) + f"  - {{fileID: {new_button_rt}}}\n",
                      text[pa:pb], count=1)
        text = text[:pa] + pblk + text[pb:]
    text = _set_in_block(text, new_icon_img, "m_Sprite", _sprite_ref(guid))
    text = _set_in_block(text, new_icon_img, "m_Color", "{r: 1, g: 1, b: 1, a: 1}")
    # the coreAbilities entry
    va, vb = _block(text, core["view"])
    vblk = text[va:vb]
    entry = (f"  - ability: {core['ability']}\n    icon: {{fileID: {new_icon_img}}}\n    gauge: {{fileID: 0}}\n"
             f"    emblem: {{fileID: 0}}\n    input: {core['input']}\n")
    if re.search(rf"^  - ability: {core['ability']}\n", vblk, re.M):
        pass
    elif re.search(r"^  coreAbilities: \[\]\n", vblk, re.M):
        vblk = re.sub(r"^  coreAbilities: \[\]\n", "  coreAbilities:\n" + entry, vblk, count=1, flags=re.M)
    elif re.search(r"^  coreAbilities:\n", vblk, re.M):
        vblk = re.sub(r"^  coreAbilities:\n", "  coreAbilities:\n" + entry, vblk, count=1, flags=re.M)
    else:
        vblk = re.sub(r"^(  upgradeHighlightScale: )", "  coreAbilities:\n" + entry + r"\1", vblk, count=1, flags=re.M)
    # optionally the OMNI card's lower-plate art (VesselHUDView.omniAbilitySprite) in the same pass
    if core.get("omni"):
        ref = _sprite_ref(guid_of(icon_meta[core["omni"]]))
        replaced = _set_field(vblk, "omniAbilitySprite", ref)
        vblk = replaced if replaced is not None else re.sub(
            r"^(  upgradeHighlightScale: )", f"  omniAbilitySprite: {ref}\n" + r"\1", vblk, count=1, flags=re.M)
    return text[:va] + vblk + text[vb:]


def _apply_proxy(text, step, icon_meta):
    px = step["proxy"]
    for field, container, name in px["refs"]:
        src_img = _fid(f"{px['source_file']}/{container}/{name}img")
        local = int(src_img) ^ int(px["instance"])
        if f"&{local} stripped\n" not in text:
            text = text.rstrip("\n") + "\n" + f"""--- !u!114 &{local} stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {{fileID: {src_img}, guid: {px['source_guid']},
    type: 3}}
  m_PrefabInstance: {{fileID: {px['instance']}}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
"""
        va, vb = _block(text, px["view"])
        vblk = text[va:vb]
        replaced = _set_field(vblk, field, f"{{fileID: {local}}}")
        if replaced is None:
            replaced = re.sub(rf"^(\s*{px['after']}: .*\n)", lambda m: m.group(1) + f"  {field}: {{fileID: {local}}}\n",
                              vblk, count=1, flags=re.M)
        text = text[:va] + replaced + text[vb:]
    for field, value in px.get("fields", {}).items():
        v = _sprite_ref(guid_of(icon_meta[value[1]])) if isinstance(value, tuple) else value
        va, vb = _block(text, px["view"])
        vblk = text[va:vb]
        replaced = _set_field(vblk, field, v)
        if replaced is None:
            replaced = re.sub(rf"^(\s*{px['after']}: .*\n)", lambda m: m.group(1) + f"  {field}: {v}\n", vblk, count=1,
                              flags=re.M)
        text = text[:va] + replaced + text[vb:]
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
        elif "core" in step:
            new = _apply_core(text, step, icon_meta)
        elif "proxy" in step:
            new = _apply_proxy(text, step, icon_meta)
        elif "deactivate" in step:
            a, b = _block(text, step["deactivate"])
            go = re.search(r"m_GameObject: \{fileID: (\d+)\}", text[a:b]).group(1)
            new = _set_in_block(text, go, "m_IsActive", "0")
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
