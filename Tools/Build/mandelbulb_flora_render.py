#!/usr/bin/env python3
"""Rasterise oriented prism boxes with a depth buffer and a light. Squares cannot show
the one thing a multi-scale rule is for (that no two prisms share a frame), so these are
real boxes."""
import sys, math, zlib, struct

def png(path, w, h, rgb):
    raw = b''.join(b'\x00' + bytes(rgb[y*w*3:(y+1)*w*3]) for y in range(h))
    def chunk(t, d):
        c = t + d
        return struct.pack('>I', len(d)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    hdr = struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', hdr)
                           + chunk(b'IDAT', zlib.compress(raw, 6)) + chunk(b'IEND', b''))

def norm(v):
    m = math.sqrt(sum(x*x for x in v)) or 1.0
    return [x/m for x in v]
def cross(a,b): return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
def dot(a,b): return sum(x*y for x,y in zip(a,b))

def render(boxes, path, W=900, H=900, yaw=0.6, pitch=0.25, bg=(8,9,14)):
    """Oriented BOXES with a depth buffer and a light — never screen-aligned squares, which
    cannot show the one thing a multi-scale rule is for: that no two prisms share a frame."""
    prisms = [(b[0],
               b[1][2],                     # forward is the box's long axis
               b[1][1],                     # up is the surface normal
               b[2][2] * 2, i, 1.0)
              for i, b in enumerate(boxes)]
    thick = None
    return _render(prisms, path, W, H, yaw, pitch, boxes, bg)


def _render(prisms, path, W, H, yaw, pitch, boxes, bg):
    # camera
    cd = norm([math.cos(pitch)*math.cos(yaw), math.cos(pitch)*math.sin(yaw), math.sin(pitch)])
    ext = max((max(abs(c) for c in p[0]) for p in prisms), default=1.0)
    dist = ext*3.2
    eye = [c*dist for c in cd]
    fwd = [-c for c in cd]
    right = norm(cross(fwd, [0,0,1]))
    up = cross(right, fwd)
    f = 1.5*W/2

    zbuf = [1e30]*(W*H)
    col = [bg[i%3] for i in range(W*H*3)]
    light = norm([0.4, 0.5, 0.8])

    def project(p):
        d = [p[i]-eye[i] for i in range(3)]
        z = dot(d, fwd)
        if z <= 1e-4: return None
        return (W/2 + f*dot(d,right)/z, H/2 - f*dot(d,up)/z, z)

    tris = []
    for (centre, fdir, udir, length, curve, girth), box in zip(prisms, boxes):
        a = norm(fdir); n = norm(udir)
        b = norm(cross(n, a)); n = cross(a, b)
        hx, hy, hz = box[2][2], box[2][0], box[2][1]
        verts = []
        for sx in (-1,1):
            for sy in (-1,1):
                for sz in (-1,1):
                    verts.append([centre[i] + a[i]*sx*hx + b[i]*sy*hy + n[i]*sz*hz for i in range(3)])
        # 6 faces as quads (indices into verts, bit order sx,sy,sz)
        faces = [((0,1,3,2), [-a[i] for i in range(3)]), ((4,6,7,5), a),
                 ((0,4,5,1), [-b[i] for i in range(3)]), ((2,3,7,6), b),
                 ((0,2,6,4), [-n[i] for i in range(3)]), ((1,5,7,3), n)]
        for idx, fn in faces:
            tris.append((idx, fn, verts, curve))

    for idx, fn, verts, curve in tris:
        pts = [project(verts[i]) for i in idx]
        if any(p is None for p in pts): continue
        lam = max(0.0, dot(norm(fn), light))
        r = math.sqrt(sum(c*c for c in verts[0]))
        u = max(0.0, min(1.0, (r/ext - 0.45)/0.55))
        base = (0.15+0.85*u, 0.45+0.35*u, 0.95-0.45*u)
        sh = 0.14 + 0.86*lam
        rgb = tuple(min(255, int(255*c*sh)) for c in base)
        for tri in ((0,1,2),(0,2,3)):
            p0,p1,p2 = pts[tri[0]],pts[tri[1]],pts[tri[2]]
            minx = max(0,int(min(p0[0],p1[0],p2[0]))); maxx = min(W-1,int(max(p0[0],p1[0],p2[0]))+1)
            miny = max(0,int(min(p0[1],p1[1],p2[1]))); maxy = min(H-1,int(max(p0[1],p1[1],p2[1]))+1)
            if maxx<minx or maxy<miny: continue
            d = (p1[1]-p2[1])*(p0[0]-p2[0]) + (p2[0]-p1[0])*(p0[1]-p2[1])
            if abs(d) < 1e-9: continue
            for y in range(miny, maxy+1):
                for x in range(minx, maxx+1):
                    px, py = x+0.5, y+0.5
                    w0 = ((p1[1]-p2[1])*(px-p2[0]) + (p2[0]-p1[0])*(py-p2[1]))/d
                    w1 = ((p2[1]-p0[1])*(px-p2[0]) + (p0[0]-p2[0])*(py-p2[1]))/d
                    w2 = 1-w0-w1
                    if w0<0 or w1<0 or w2<0: continue
                    z = w0*p0[2]+w1*p1[2]+w2*p2[2]
                    o = y*W+x
                    if z < zbuf[o]:
                        zbuf[o] = z
                        col[o*3],col[o*3+1],col[o*3+2] = rgb
    png(path, W, H, col)

def load(path):
    out = []
    for line in open(path):
        t = line.split()
        if not t or t[0] != 'p': continue
        length = float(t[8]); girth = float(t[9]); curve = int(t[11])
        c = [float(t[13]), float(t[14]), float(t[15])]
        fd = [float(t[16]), float(t[17]), float(t[18])]
        ud = [float(t[19]), float(t[20]), float(t[21])]
        out.append((c, fd, ud, length, curve, girth))
    return out



# ── judging sheet ──────────────────────────────────────────────────────────────
#
# One PNG, four views: the plant across the arena (the silhouette), a few radii out (the
# structure), the polar view (where an n-fold symmetry either reads or does not) and a
# flight-through close-up. The HEART is drawn as a small bright marker at the origin, because
# "the spindles almost connect to their crystal" is a claim about a distance and a render
# with no crystal in it cannot show it.

def heart_boxes(radius):
    """Three orthogonal slabs crossing at the origin: reads as a small bright core."""
    r = max(0.05, radius)
    ax = ([1, 0, 0], [0, 1, 0], [0, 0, 1])
    out = []
    for k in range(3):
        a, b, c = ax[k], ax[(k + 1) % 3], ax[(k + 2) % 3]
        out.append(([0.0, 0.0, 0.0], (b, c, a), (r * 0.35, r * 0.35, r)))
    return out


def _view(boxes, W, H, yaw, pitch, dist_factor, heart, bg, dist_abs=None):
    """Rasterise one view and return (rgb, W, H). Same camera model as render().

    `dist_abs` frames on an ABSOLUTE eye distance instead of a multiple of the plant's
    own extent — which is what a close-up needs, because a framing expressed as a
    fraction of the subject cannot hold a fixed world span across four elements whose
    plants differ in size."""
    hb = heart_boxes(heart) if heart and heart > 0 else []
    allb = list(boxes) + hb
    prisms = [(b[0], b[1][2], b[1][1], b[2][2] * 2, i, 1.0) for i, b in enumerate(allb)]
    cd = norm([math.cos(pitch) * math.cos(yaw), math.cos(pitch) * math.sin(yaw), math.sin(pitch)])
    ext = max((max(abs(c) for c in p[0]) for p in prisms[:len(boxes)]), default=1.0)
    dist = dist_abs if dist_abs else ext * dist_factor
    eye = [c * dist for c in cd]
    fwd = [-c for c in cd]
    upref = [0, 0, 1] if abs(cd[2]) < 0.95 else [0, 1, 0]
    right = norm(cross(fwd, upref))
    up = cross(right, fwd)
    f = 1.5 * W / 2
    zbuf = [1e30] * (W * H)
    col = [bg[i % 3] for i in range(W * H * 3)]
    light = norm([0.4, 0.5, 0.8])

    def project(p):
        d = [p[i] - eye[i] for i in range(3)]
        z = dot(d, fwd)
        if z <= 1e-4:
            return None
        return (W / 2 + f * dot(d, right) / z, H / 2 - f * dot(d, up) / z, z)

    nheart = len(boxes)
    for idx_box, ((centre, fdir, udir, length, curve, girth), box) in enumerate(zip(prisms, allb)):
        a = norm(fdir); n = norm(udir)
        b = norm(cross(n, a)); n = cross(a, b)
        hx, hy, hz = box[2][2], box[2][0], box[2][1]
        verts = []
        for sx in (-1, 1):
            for sy in (-1, 1):
                for sz in (-1, 1):
                    verts.append([centre[i] + a[i] * sx * hx + b[i] * sy * hy + n[i] * sz * hz for i in range(3)])
        faces = [((0, 1, 3, 2), [-a[i] for i in range(3)]), ((4, 6, 7, 5), a),
                 ((0, 4, 5, 1), [-b[i] for i in range(3)]), ((2, 3, 7, 6), b),
                 ((0, 2, 6, 4), [-n[i] for i in range(3)]), ((1, 5, 7, 3), n)]
        is_heart = idx_box >= nheart
        for idx, fn in faces:
            pts = [project(verts[i]) for i in idx]
            if any(p is None for p in pts):
                continue
            lam = max(0.0, dot(norm(fn), light))
            if is_heart:
                base = (1.0, 0.95, 0.6)
                sh = 0.6 + 0.4 * lam
            else:
                r = math.sqrt(sum(c * c for c in verts[0]))
                u = max(0.0, min(1.0, (r / ext - 0.45) / 0.55))
                base = (0.15 + 0.85 * u, 0.45 + 0.35 * u, 0.95 - 0.45 * u)
                sh = 0.14 + 0.86 * lam
            rgb = tuple(min(255, int(255 * c * sh)) for c in base)
            for tri in ((0, 1, 2), (0, 2, 3)):
                p0, p1, p2 = pts[tri[0]], pts[tri[1]], pts[tri[2]]
                minx = max(0, int(min(p0[0], p1[0], p2[0]))); maxx = min(W - 1, int(max(p0[0], p1[0], p2[0])) + 1)
                miny = max(0, int(min(p0[1], p1[1], p2[1]))); maxy = min(H - 1, int(max(p0[1], p1[1], p2[1])) + 1)
                if maxx < minx or maxy < miny:
                    continue
                d = (p1[1] - p2[1]) * (p0[0] - p2[0]) + (p2[0] - p1[0]) * (p0[1] - p2[1])
                if abs(d) < 1e-9:
                    continue
                for y in range(miny, maxy + 1):
                    for x in range(minx, maxx + 1):
                        px, py = x + 0.5, y + 0.5
                        w0 = ((p1[1] - p2[1]) * (px - p2[0]) + (p2[0] - p1[0]) * (py - p2[1])) / d
                        w1 = ((p2[1] - p0[1]) * (px - p2[0]) + (p0[0] - p2[0]) * (py - p2[1])) / d
                        w2 = 1 - w0 - w1
                        if w0 < 0 or w1 < 0 or w2 < 0:
                            continue
                        z = w0 * p0[2] + w1 * p1[2] + w2 * p2[2]
                        o = y * W + x
                        if z < zbuf[o]:
                            zbuf[o] = z
                            col[o * 3], col[o * 3 + 1], col[o * 3 + 2] = rgb
    return col


SHEET_VIEWS = (
    # (label, yaw, pitch, dist_factor): arena silhouette, structure, the pole, a fly-through
    ("arena", 0.6, 0.25, 3.2),
    ("near", 0.6, 0.25, 1.9),
    ("pole", 0.0, 1.45, 1.9),
    ("inside", 2.4, -0.15, 1.45),
)


def render_sheet(boxes, path, heart=0.0, tile=700, bg=(8, 9, 14), views=SHEET_VIEWS):
    """A 2x2 sheet of the four judging views into one PNG."""
    cols = 2
    rows = (len(views) + cols - 1) // cols
    W, H = tile * cols, tile * rows
    out = [bg[i % 3] for i in range(W * H * 3)]
    for k, (label, yaw, pitch, dist) in enumerate(views):
        col = _view(boxes, tile, tile, yaw, pitch, dist, heart, bg)
        ox, oy = (k % cols) * tile, (k // cols) * tile
        for y in range(tile):
            src = y * tile * 3
            dst = ((oy + y) * W + ox) * 3
            out[dst:dst + tile * 3] = col[src:src + tile * 3]
    png(path, W, H, out)
    return path


def render_closeup(boxes, path, span=30.0, heart=0.0, W=900, H=900,
                   yaw=0.6, pitch=0.25, bg=(8, 9, 14)):
    """One view framed on an ABSOLUTE world SPAN centred on the origin.

    The projection is `f = 1.5 * W / 2`, so a point at lateral offset X and depth z lands at
    the frame edge when X / z = 1 / 1.5 — the half-width at the centre plane is therefore
    `dist / 1.5`, and a frame `span` units across wants `dist = 0.75 * span`. Everything
    outside simply falls off the sides, which is what a fly-through looks like."""
    col = _view(boxes, W, H, yaw, pitch, 0.0, heart, bg, dist_abs=0.75 * span)
    png(path, W, H, col)
    return path
