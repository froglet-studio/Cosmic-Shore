#!/usr/bin/env python3
"""
Per-vessel prefab survey: which renderers a vessel prefab carries, which MODEL
each renderer's mesh actually resolves to, and whether every nested prefab
instance is REACHABLE from its parent.

Companion to `measure_vessel_models.py`. Together they are the Phase 0 gate of
`Docs/VESSEL_CONSTRUCTION_FOLLOWUP.md`.

Three things it checks that nothing else does:

1. **Guid ownership** (`VESSEL_CONSTRUCTION.md` §2). A mesh reference names a guid;
   exactly one `.meta` OWNS that guid - the file whose own top-level `guid:` line
   carries it. `grep -rl | head -1` sorts by filename and returns a plausible
   REFERRER, because an FBX's `.meta` can remap materials into another FBX.
2. **Nested-instance reachability** (§3). A prefab instance's parenting lives in
   `m_TransformParent` in its own modification block ALWAYS, plus an entry in the
   parent Transform's `m_Children` IFF that parent is a plain (non-stripped)
   Transform. Generalising "no entry needed" from the Squirrel's jets - whose
   parent is stripped and structurally cannot carry one - shipped eight
   unreachable Rhino jets.
3. **Coincident duplicate renderers** (§3.4). Two SkinnedMeshRenderers drawing the
   same hull from two files is a duplicate draw the morph audit counts twice.
"""

import argparse
import os
import re
import subprocess
import sys
from collections import defaultdict

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VESSEL_PREFABS = os.path.join(REPO, "Assets", "_Prefabs", "Spacevessels")

_GUID_OWNER_CACHE = {}


def guid_owner(guid):
    """The single file whose own `.meta` DECLARES this guid, or None."""
    if guid in _GUID_OWNER_CACHE:
        return _GUID_OWNER_CACHE[guid]
    out = subprocess.run(
        ["grep", "-rl", "--include=*.meta", "-e", "^guid: " + guid,
         os.path.join(REPO, "Assets")],
        capture_output=True, text=True,
    ).stdout.strip()
    hits = [h[:-5] for h in out.split("\n") if h]
    owner = os.path.relpath(hits[0], REPO) if len(hits) == 1 else None
    _GUID_OWNER_CACHE[guid] = owner
    return owner


# --------------------------------------------------------- crude YAML document split

DOC_RE = re.compile(r"^--- !u!(\d+) &(\d+)(?: (stripped))?\s*$")


def parse_prefab(path):
    """
    Split a Unity YAML asset into documents: {fileID: {"class": int, "stripped": bool,
    "type": str, "body": str}}.
    """
    docs = {}
    cur = None
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()
    i = 0
    order = []
    while i < len(lines):
        m = DOC_RE.match(lines[i].rstrip("\n"))
        if m:
            cls, fid, stripped = int(m.group(1)), m.group(2), bool(m.group(3))
            # the type name is the next line's key, e.g. "Transform:"
            tname = lines[i + 1].split(":")[0].strip() if i + 1 < len(lines) else "?"
            cur = {"class": cls, "stripped": stripped, "type": tname, "body": []}
            docs[fid] = cur
            order.append(fid)
            i += 2
            continue
        if cur is not None:
            cur["body"].append(lines[i])
        i += 1
    for d in docs.values():
        d["body"] = "".join(d["body"])
    return docs, order


def field(body, key):
    m = re.search(r"^\s*%s:\s*(.*)$" % re.escape(key), body, re.M)
    return m.group(1).strip() if m else None


def ref_guid(value):
    m = re.search(r"guid:\s*([0-9a-f]{32})", value or "")
    return m.group(1) if m else None


def ref_fileid(value):
    m = re.search(r"fileID:\s*(-?\d+)", value or "")
    return m.group(1) if m else None


def go_name(docs, go_fid):
    d = docs.get(go_fid)
    if not d:
        return "?"
    return field(d["body"], "m_Name") or "?"


def owner_go_of(docs, comp_fid):
    d = docs.get(comp_fid)
    if not d:
        return None
    return ref_fileid(field(d["body"], "m_GameObject"))


def survey_prefab(path):
    docs, order = parse_prefab(path)
    name = os.path.basename(path)[:-7]

    mesh_filters, skinned, prefab_instances, transforms = [], [], [], {}

    for fid, d in docs.items():
        if d["type"] == "Transform" or d["class"] == 4:
            transforms[fid] = d
        elif d["type"] == "MeshFilter":
            g = ref_guid(field(d["body"], "m_Mesh"))
            go = owner_go_of(docs, fid)
            mesh_filters.append({"fid": fid, "go": go_name(docs, go), "guid": g,
                                 "model": guid_owner(g) if g else None})
        elif d["type"] == "SkinnedMeshRenderer":
            g = ref_guid(field(d["body"], "m_Mesh"))
            go = owner_go_of(docs, fid)
            gob = docs.get(go)
            active = field(gob["body"], "m_IsActive") if gob else None
            skinned.append({"fid": fid, "go": go_name(docs, go), "guid": g,
                            "model": guid_owner(g) if g else None,
                            "enabled": field(d["body"], "m_Enabled"),
                            "go_active": active})
        elif d["type"] == "PrefabInstance":
            src = ref_guid(field(d["body"], "m_SourcePrefab"))
            parent = ref_fileid(field(d["body"], "m_TransformParent"))
            nm = None
            m = re.search(r"propertyPath:\s*m_Name\s*\n\s*value:\s*(.*)", d["body"])
            if m:
                nm = m.group(1).strip()
            prefab_instances.append({"fid": fid, "src": src,
                                     "src_file": guid_owner(src) if src else None,
                                     "parent": parent, "name": nm})

    # ---- nested-instance reachability
    unreachable = []
    for pi in prefab_instances:
        p = pi["parent"]
        if not p:
            unreachable.append((pi, "no m_TransformParent"))
            continue
        pd = docs.get(p)
        if pd is None:
            # parent lives in ANOTHER prefab document (a stripped ref we don't hold)
            continue
        if pd["stripped"]:
            continue                       # structurally cannot carry m_Children
        kids = re.findall(r"fileID:\s*(-?\d+)", field(pd["body"], "m_Children") or "")
        blob = pd["body"]
        m = re.search(r"m_Children:\s*\n((?:\s*-\s*\{fileID:\s*-?\d+\}\s*\n)*)", blob)
        kid_ids = re.findall(r"fileID:\s*(-?\d+)", m.group(1)) if m else []
        # A nested instance appears in m_Children as its STRIPPED transform's fileID,
        # which is a doc in this file whose m_PrefabInstance points at pi.
        stripped_for_pi = [
            fid for fid, d in docs.items()
            if d["stripped"] and ref_fileid(field(d["body"], "m_PrefabInstance")) == pi["fid"]
        ]
        if not any(s in kid_ids for s in stripped_for_pi):
            unreachable.append((pi, "absent from plain parent '%s' m_Children"
                                % (field(pd["body"], "m_GameObject") or p)))

    # ---- coincident duplicate skinned renderers
    by_go_name = defaultdict(list)
    for s in skinned:
        by_go_name[s["go"]].append(s)
    dupes = {k: v for k, v in by_go_name.items() if len(v) > 1}

    return {
        "vessel": name,
        "mesh_filters": mesh_filters,
        "skinned": skinned,
        "instances": prefab_instances,
        "unreachable": unreachable,
        "duplicate_skinned": dupes,
    }



# Unity's built-in resources. A reference to a built-in mesh (the skimmer sphere, the
# crackle overlay quad) is not a model wiring and must never be reported as one.
BUILTIN_GUID = "0000000000000000e000000000000000"

MODEL_EXTS = (".fbx", ".obj", ".blend", ".dae")


def models_wired(s):
    """Which MODEL files this vessel's hull actually draws from.

    A hull reaches its model two ways, and reading only the first is what made a
    rig-swapped vessel report UNRESOLVED:

    * **Direct** - a MeshFilter/SkinnedMeshRenderer in the prefab holds `m_Mesh`
      pointing into the model file. This is the part-per-mesh family, and the
      skinned family while its renderer lives in the prefab itself.
    * **Nested instance** - the model is instantiated as a nested prefab, so the
      renderer document here is `stripped` and carries no `m_Mesh` at all; the
      wiring is the instance's `m_SourcePrefab`. This is what a rig swap produces
      (`VESSEL_CONSTRUCTION.md` s3), and the Dolphin is the first vessel to use it.

    Built-in-resource references are excluded outright - they name Unity, not a model.
    """
    out = set()
    for x in s["mesh_filters"] + s["skinned"]:
        g = x["guid"]
        if not g or g == BUILTIN_GUID:
            continue
        out.add(os.path.basename(x["model"]) if x["model"]
                else "UNRESOLVED(%s)" % g)
    for pi in s["instances"]:
        f = pi.get("src_file")
        if f and f.lower().endswith(MODEL_EXTS):
            out.add(os.path.basename(f))
    return out

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only")
    args = ap.parse_args()

    files = sorted(
        os.path.join(VESSEL_PREFABS, f)
        for f in os.listdir(VESSEL_PREFABS) if f.endswith(".prefab")
    )
    if args.only:
        files = [f for f in files if args.only.lower() in os.path.basename(f).lower()]

    print("%-10s %4s %4s  %s" % ("vessel", "MF", "SMR", "models wired"))
    print("-" * 100)
    rows = []
    for f in files:
        s = survey_prefab(f)
        rows.append(s)
        models = sorted(models_wired(s))
        print("%-10s %4d %4d  %s" % (s["vessel"], len(s["mesh_filters"]),
                                     len(s["skinned"]), ", ".join(models) or "-"))

    print()
    print("=== nested prefab-instance reachability (VESSEL_CONSTRUCTION.md §3) ===")
    any_bad = False
    for s in rows:
        if s["unreachable"]:
            any_bad = True
            print("  %s: %d UNREACHABLE" % (s["vessel"], len(s["unreachable"])))
            for pi, why in s["unreachable"]:
                print("     %-24s src=%s  (%s)" % (pi["name"] or pi["fid"],
                      os.path.basename(pi["src_file"]) if pi["src_file"] else pi["src"], why))
    if not any_bad:
        print("  none - every plain-Transform-parented instance is listed in m_Children")

    print()
    print("=== coincident duplicate skinned renderers (§3.4) ===")
    found = False
    for s in rows:
        for goname, group in s["duplicate_skinned"].items():
            found = True
            print("  %s: %d SkinnedMeshRenderers on GameObjects named '%s'"
                  % (s["vessel"], len(group), goname))
            for g in group:
                print("     enabled=%s go_active=%s  <- %s"
                      % (g["enabled"], g["go_active"],
                         os.path.basename(g["model"]) if g["model"] else g["guid"]))
    if not found:
        print("  none")
    return 0


if __name__ == "__main__":
    sys.exit(main())
