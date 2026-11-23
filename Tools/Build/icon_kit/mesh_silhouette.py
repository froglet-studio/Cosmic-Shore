"""A hull's REAL silhouette as a kit mask: its FBX mesh, parallel-projected from a chosen view, thickened a little.

Garrett, 2026-10-25, on a hand-simplified Squirrel: "the fidelity is too low. this reads too abstractly." So a hull
in an icon is its own geometry, not a drawing of it. `silhouette()` reads the binary FBX (the repo's codec, via
hull_view.polygons), rotates the mesh into a view (pitch = tipped toward the viewer from straight above, yaw =
turned about the view axis, nose to the right at pitch 0 / yaw 0), fills every polygon on the kit's supersampled
grid, optionally CLOSES small gaps (fusing wing slats) and DILATES by a fraction of an icon px so rib-thin parts
survive the 60 px card. Deterministic: the same FBX and numbers give the same mask, so --check holds.

The Squirrel's Crystal Joust is the first user (specs/squirrel.py, view D of the 2026-10-25 sheet: pitch 45, yaw 15,
+1.2 px). A hi-fi silhouette will fail the kit's `gap` gate on its ribs; the spec exempts that gate for the icon,
with the owner's name, instead of fattening the ribs until they stop being ribs.
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from canvas import N, SS

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
_CACHE = {}


def _polys(fbx_rel, take=None):
    key = (fbx_rel, take)
    if key not in _CACHE:
        if take:
            import mesh_pose
            _CACHE[key] = mesh_pose.posed_polygons(fbx_rel, take)   # the mesh skinned into an authored take (the fold)
        else:
            import hull_view
            polys, V = hull_view.polygons(os.path.join(ROOT, fbx_rel))
            _CACHE[key] = polys
    return _CACHE[key]


def silhouette(fbx_rel, pitch=0.0, yaw=0.0, box=114, cx=64.0, cy=64.0, dilate_px=0.0, close_px=0.0, roll=0.0, mirror=False, take=None):
    """Mask of the hull seen from above (file frame: +Y nose, +X pilot's right, +Z up), nose to the right on
    screen, tipped `pitch` degrees toward the viewer (positive = the nose end comes toward us) and turned `yaw`
    degrees clockwise on screen; fitted to `box` icon px about (cx, cy), then rolled `roll` degrees (clockwise on
    screen) about that centre - the in-plane turn of a hull that has been knocked. `mirror` flips it left-right
    (nose to the LEFT): the opponent in a nose-to-nose pair. `take` names an authored take to skin the mesh into
    first (mesh_pose.py: the Sparrow's SpeedUp folds its fans) - the bind pose when None."""
    pa, ya = math.radians(-pitch), math.radians(yaw)
    Ryaw = np.array([[math.cos(ya), -math.sin(ya), 0], [math.sin(ya), math.cos(ya), 0], [0, 0, 1]])
    Rpitch = np.array([[1, 0, 0], [0, math.cos(pa), -math.sin(pa)], [0, math.sin(pa), math.cos(pa)]])
    R = Rpitch @ Ryaw
    projected = []
    for p in _polys(fbx_rel, take):
        q = np.asarray(p, dtype=float)
        s = np.stack([q[:, 1], q[:, 0], q[:, 2]], axis=1)      # screen x = nose axis, screen y (down) = pilot's right
        projected.append(s @ R.T)
    allp = np.vstack(projected)
    lo, hi = allp.min(0), allp.max(0)
    ext = max(hi[0] - lo[0], hi[1] - lo[1])
    k = box * SS / ext
    mx, my = (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2
    im = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(im)
    ra = math.radians(roll)
    cr, sr = math.cos(ra), math.sin(ra)
    for q in projected:
        pts = []
        for v in q:
            x, y = (v[0] - mx) * k, (v[1] - my) * k
            if mirror:
                x = -x
            pts.append(((x * cr - y * sr) + cx * SS, (x * sr + y * cr) + cy * SS))
        d.polygon(pts, fill=255)
    if close_px:
        r = int(close_px * SS) | 1
        im = im.filter(ImageFilter.MaxFilter(r)).filter(ImageFilter.MinFilter(r))
    if dilate_px:
        r = int(dilate_px * SS) | 1
        im = im.filter(ImageFilter.MaxFilter(r))
    return np.asarray(im) > 127


def _rotation(pitch, yaw):
    pa, ya = math.radians(-pitch), math.radians(yaw)
    Ryaw = np.array([[math.cos(ya), -math.sin(ya), 0], [math.sin(ya), math.cos(ya), 0], [0, 0, 1]])
    Rpitch = np.array([[1, 0, 0], [0, math.cos(pa), -math.sin(pa)], [0, math.sin(pa), math.cos(pa)]])
    return Rpitch @ Ryaw


def nose_vertex(fbx_rel):
    """The mesh vertex furthest along the nose axis (file +Y)."""
    allv = np.vstack([np.asarray(p) for p in _polys(fbx_rel)])
    return allv[allv[:, 1].argmax()]


def project_point(fbx_rel, v, pitch=0.0, yaw=0.0, box=114, cx=64.0, cy=64.0, roll=0.0):
    """Where a mesh point lands, in icon px, for the same view and placement `silhouette()` uses."""
    R = _rotation(pitch, yaw)
    allv = np.vstack([np.asarray(p) for p in _polys(fbx_rel)])
    S = np.stack([allv[:, 1], allv[:, 0], allv[:, 2]], axis=1) @ R.T
    lo, hi = S.min(0), S.max(0)
    ext = max(hi[0] - lo[0], hi[1] - lo[1])
    k = box / ext
    mx, my = (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2
    s = np.array([v[1], v[0], v[2]]) @ R.T
    x, y = (s[0] - mx) * k, (s[1] - my) * k
    ra = math.radians(roll)
    return (x * math.cos(ra) - y * math.sin(ra) + cx, x * math.sin(ra) + y * math.cos(ra) + cy)


def views_sheet(fbx_rel, out, mirror=False, pitches=(0, 20, 45, 70, 90), yaws=(0, 15, 30), box=112, dilate_px=1.2, take=None):
    """A lettered grid of the hull from every (pitch, yaw) pair - the sheet the owner picks a view from (the Squirrel's
    'D', the Dolphin's 'A'). Check the hull's file frame in Docs/VesselModels/<Hull>.md §1.1 first: a hull whose
    mesh nose is -Y (the Dolphin, exported from Blender) needs mirror=True to put the nose on the right."""
    from PIL import ImageDraw
    import canvas as C
    views = [(p, y) for p in pitches for y in yaws]
    cols, tile = len(yaws), 128 + 8   # C.render gives the 128 px icon, not the supersampled canvas
    sheet = Image.new("RGBA", (cols * tile, (len(views) // cols) * tile), (14, 20, 44, 255))
    d = ImageDraw.Draw(sheet)
    for i, (p, y) in enumerate(views):
        im = C.render(silhouette(fbx_rel, pitch=p, yaw=y, box=box, dilate_px=dilate_px, mirror=mirror, take=take))
        x0, y0 = (i % cols) * tile, (i // cols) * tile
        sheet.alpha_composite(im, (x0 + 4, y0 + 4))
        d.text((x0 + 6, y0 + 4), f"{chr(65 + i)} pitch {p} yaw {y}", fill=(255, 220, 120, 255))
    sheet.save(out)
    return out


if __name__ == "__main__":
    import argparse
    ap = argparse.ArgumentParser(description="Render a hull's FBX from a grid of views to pick an icon view from.")
    ap.add_argument("fbx", help="repo-relative FBX path")
    ap.add_argument("--out", required=True)
    ap.add_argument("--mirror", action="store_true", help="nose to the right for a mesh whose nose is -Y in its own frame")
    ap.add_argument("--take", help="skin the mesh into this authored take first (mesh_pose.py --takes lists them)")
    a = ap.parse_args()
    print("wrote", views_sheet(a.fbx, a.out, mirror=a.mirror, take=a.take))
