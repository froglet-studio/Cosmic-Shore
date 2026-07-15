"""Bake converter strokes into a PaintingDefinitionSO .asset as authored strokes.
Authored strokes are the SO's highest-priority source (strokes -> sourceShape ->
preset), so baking needs zero runtime code. Painting conventions enforced here:
base at y=0, front +Z, flyable segments, Jade(1)/Ruby(2)/Gold(4) only."""
import re
import numpy as np

DOM = {'Jade': 1, 'Ruby': 2, 'Gold': 4}

def sanitize_name(name):
    """Stroke names go into the YAML unquoted — strip anything Unity's parser could
    misread (':' ends a key, '#' starts a comment, flow/anchor chars)."""
    clean = re.sub(r"[^A-Za-z0-9 _.\-]", "", str(name)).strip()
    return clean or "Stroke"

def bake(asset_path, strokes, round_dp=1):
    """strokes: list of (domain, name, points ndarray). Rewrites the asset's
    strokes: block in place, preserving every other field."""
    # rebase to y=0
    min_y = min(float(np.asarray(p)[:, 1].min()) for _, _, p in strokes)
    lines = ["  strokes:"]
    for dom, name, pts in strokes:
        lines.append(f"  - name: {sanitize_name(name)}")
        lines.append(f"    domain: {DOM[dom]}")
        lines.append("    points:")
        for q in np.asarray(pts):
            lines.append("    - {x: %s, y: %s, z: %s}" % (
                round(float(q[0]), round_dp), round(float(q[1]) - min_y, round_dp),
                round(float(q[2]), round_dp)))
    block = "\n".join(lines)

    src = open(asset_path).read().rstrip("\n").split("\n")
    out, i, replaced = [], 0, False
    while i < len(src):
        line = src[i]
        if line.startswith("  strokes:"):
            out.append(block)
            replaced = True
            i += 1
            # skip the old strokes block (any deeper-indented or list lines until the next top field)
            while i < len(src) and (src[i].startswith("  - ") or src[i].startswith("    ")):
                i += 1
            continue
        out.append(line)
        i += 1
    assert replaced, "no strokes: field found in asset"
    open(asset_path, "w").write("\n".join(out) + "\n")
    npts = sum(len(p) for _, _, p in strokes)
    return len(strokes), npts

def validate(strokes, W):
    allp = np.vstack([np.asarray(p) for _, _, p in strokes])
    segs = np.concatenate([np.linalg.norm(np.diff(np.asarray(p), axis=0), axis=1)
                           for _, _, p in strokes])
    assert all(d in DOM for d, _, _ in strokes)
    assert segs.min() > 4.0, f"degenerate segment {segs.min():.2f}"
    assert segs.max() < 0.65 * W, f"unflyable segment {segs.max():.1f}"
    bb = allp.max(0) - allp.min(0)
    assert min(bb) > 0.04 * W, f"planar: bbox {bb}"
    return dict(strokes=len(strokes), pts=len(allp), pathW=float(segs.sum() / W),
                minSeg=float(segs.min()), maxSeg=float(segs.max()),
                bboxW=tuple(round(float(e) / W, 2) for e in bb))
