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
        length = float(t[6]); girth = float(t[7]); curve = int(t[8])
        c = [float(t[10]), float(t[11]), float(t[12])]
        fd = [float(t[13]), float(t[14]), float(t[15])]
        ud = [float(t[16]), float(t[17]), float(t[18])]
        out.append((c, fd, ud, length, curve, girth))
    return out

