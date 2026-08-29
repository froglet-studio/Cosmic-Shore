#!/usr/bin/env python3
"""Object-level three-way merge for a Unity scene or prefab — validate, then write.

USAGE (from a conflicted merge):
    git show :1:<path> > /tmp/scenemerge/base.unity     # merge base
    git show :2:<path> > /tmp/scenemerge/ours.unity     # HEAD
    git show :3:<path> > /tmp/scenemerge/theirs.unity   # incoming
    python3 Tools/Build/merge_unity_scene.py            # writes /tmp/scenemerge/merged.unity

The PANEL/THEIR_GATE constants below encode ONE branch\'s hand resolution; a new merge either
has no genuine conflict (the assert fires and tells you the id) or needs its own resolution
written here. Technique + the assertions that make it trustworthy: /asset-surgery
§ "resolve a .unity / .prefab MERGE per OBJECT".

A .unity file is a stream of independent documents headed `--- !u!<class> &<id>`.
Two branches that mostly APPEND objects interleave catastrophically under a LINE
merge (git produced 36 hunks that split single objects in half). Merging by
fileID is the structurally sound operation.

Resolution for the ONE genuinely-conflicting object (GameObject `Arcade_Panel`,
&1588431009219518451): our branch swapped its background Graphic (RawImage ->
UnityEngine.UI.Image); bleeding-edge added an `OfflineUIGate` beside it. The two
are orthogonal, so the merged component list is OURS plus their added gate.

Documents are kept as LINE LISTS and reassembled with a single join, so the
reconstruction is byte-exact — asserted by round-tripping all three parents
before any merge logic runs.
"""
import re, sys, collections

DOC = re.compile(r'^--- !u!(\d+) &(-?\d+)(.*)$')
PANEL = '1588431009219518451'
THEIR_GATE = '6633029320521394827'


def parse(path):
    """-> (preamble_lines, {fileID: [header_line, *body_lines]}, order)"""
    text = open(path, encoding='utf-8', errors='surrogateescape').read()
    preamble, docs, order, cur = [], {}, [], None
    for ln in text.split('\n'):
        if DOC.match(ln):
            cur = DOC.match(ln).group(2)
            assert cur not in docs, f"duplicate anchor &{cur} in {path}"
            docs[cur] = [ln]
            order.append(cur)
        elif cur is None:
            preamble.append(ln)
        else:
            docs[cur].append(ln)
    return preamble, docs, order, text


def emit(preamble, docs, order):
    out = list(preamble)
    for fid in order:
        out.extend(docs[fid])
    return '\n'.join(out)


# ---- round-trip proof: the codec is exact before it is trusted ---------------
for name in ('base', 'ours', 'theirs'):
    pre, docs, order, text = parse(f'/tmp/scenemerge/{name}.unity')
    assert emit(pre, docs, order) == text, f"round-trip FAILED on {name}"
print("round-trip: base, ours, theirs all reproduce byte-exactly")

base_pre, base, base_order, _ = parse('/tmp/scenemerge/base.unity')
ours_pre, ours, ours_order, _ = parse('/tmp/scenemerge/ours.unity')
thrs_pre, thrs, thrs_order, _ = parse('/tmp/scenemerge/theirs.unity')
assert base_pre == ours_pre == thrs_pre

# The last document carries the file's trailing newline as a final '' line; the
# merged file must end on whichever document we emit last, so normalise: strip a
# trailing '' from every doc and re-add one at the very end.
trailing = []
for d in (base, ours, thrs):
    for fid, lines in d.items():
        if lines[-1] == '':
            lines.pop()
            trailing.append(fid)
assert trailing, "no document carried the trailing newline"

merged, stats = {}, collections.Counter()
for fid in set(base) | set(ours) | set(thrs):
    b, o, t = base.get(fid), ours.get(fid), thrs.get(fid)
    if o == t:
        pick = o; stats['same'] += 1
    elif o == b:
        pick = t; stats['theirs-only'] += 1
    elif t == b:
        pick = o; stats['ours-only'] += 1
    else:
        assert fid == PANEL, f"unexpected conflict on &{fid}"
        pick = list(o)
        i = pick.index('  m_Layer: 5')
        pick.insert(i, f'  - component: {{fileID: {THEIR_GATE}}}')
        stats['RESOLVED'] += 1
    if pick is not None:
        merged[fid] = pick

order = [f for f in ours_order if f in merged] + \
        [f for f in thrs_order if f in merged and f not in ours]
assert len(order) == len(merged) == len(set(order))

# ---- validate BEFORE writing -------------------------------------------------
def local_refs(docs):
    refs = set()
    for lines in docs.values():
        for m in re.finditer(r'\{fileID: (-?\d+)\}', '\n'.join(lines)):
            refs.add(m.group(1))
    refs.discard('0')
    return refs

anchors = set(merged)
missing = local_refs(merged) - anchors
parents_missing = (local_refs(base) - set(base)) | (local_refs(ours) - set(ours)) \
                  | (local_refs(thrs) - set(thrs))
new_dangling = sorted(missing - parents_missing)

print("per-object merge:", dict(stats))
print(f"documents: base {len(base)} ours {len(ours)} theirs {len(thrs)} -> merged {len(merged)}")
print(f"dangling local refs: parents {len(parents_missing)}  merged {len(missing)}")
print(f"NEWLY dangling (must be 0): {new_dangling}")
assert not new_dangling

for name, side in (("ours", ours), ("theirs", thrs)):
    lost = (set(side) - set(base)) - anchors
    print(f"{name}: added {len(set(side) - set(base))} objects, lost {len(lost)}")
    assert not lost, f"{name}'s added objects dropped: {sorted(lost)[:5]}"

roots_doc = next(f for f in merged if merged[f][0].startswith('--- !u!1660057539'))
roots = set(re.findall(r'\{fileID: (-?\d+)\}', '\n'.join(merged[roots_doc])))
for name, side in (("ours", ours), ("theirs", thrs)):
    sr = set(re.findall(r'\{fileID: (-?\d+)\}', '\n'.join(side[roots_doc])))
    assert sr <= roots, f"{name} lost scene roots: {sorted(sr - roots)}"
    print(f"{name}: {len(sr)} scene roots, all present ({len(roots)} merged)")

# The resolved panel must carry BOTH features.
panel = '\n'.join(merged[PANEL])
assert f'fileID: {THEIR_GATE}' in panel, "their OfflineUIGate lost"
assert 'fileID: 3611855818750974333' in panel, "our Image lost"
assert '6633029320521394826' not in panel, "the replaced RawImage survived"
assert THEIR_GATE in merged, "the gate's own document is missing"
print("Arcade_Panel: our Image + their OfflineUIGate, replaced RawImage gone")

out = emit(base_pre, merged, order) + '\n'
header_lines = sum(1 for ln in out.split('\n') if DOC.match(ln))
assert header_lines == len(merged), f"line-anchored header count {header_lines} != {len(merged)}"
assert out.endswith('\n') and not out.endswith('\n\n'), "trailing newline wrong"
open('/tmp/scenemerge/merged.unity', 'w', encoding='utf-8', errors='surrogateescape').write(out)
print(f"\nwrote merged.unity: {out.count(chr(10))} lines, {header_lines} documents")
