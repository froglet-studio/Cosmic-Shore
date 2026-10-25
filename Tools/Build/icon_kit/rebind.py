"""Point a HUD prefab (or any asset) at the icons the kit wrote - by GUID, in YAML, idempotently.

A spec may carry `REBIND = [...]` with two kinds of step:

    dict(file="Assets/.../SquirrelHUDVariant.prefab", old="28ece4...", icon="Squirrel_CrystalJoust", expect=1)
        every `guid: <old>` in that file becomes the icon's guid (asserting the count the first time;
        0 occurrences afterwards is the "already done" state and passes)
    dict(file="Assets/.../SparrowHUDVariant.prefab", list="missileIcons", icons=["Sparrow_Rockets_0", ...])
        the serialized sprite list under `<list>:` is rewritten to those icons, in order

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
