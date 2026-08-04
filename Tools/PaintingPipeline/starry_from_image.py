"""Starry Night, pulled from the reference: trace the painting's OWN stroke flow.

1. Structure tensor of the public-domain scan -> per-pixel stroke orientation
   (Van Gogh's impasto strokes create strong coherent orientation).
2. Seed streamlines where the painting has strokes (contrast/saturation), and
   integrate along the flow -> each output stroke follows a real painted stroke.
3. Colours quantized to the three domains by palette:
   Jade = the blue/cyan sky body, Gold = yellows (stars, moon, windows),
   Ruby = the dark cypress/village/hills mass.
4. Mapped into 3D as an immersive curved canvas (cylindrical bend, concave to
   the viewer) with luminance relief so stars/moon/cypress push forward.
"""
import math
import numpy as np
from PIL import Image

def build(img_path, W=1300.0, target_strokes=300, seed=7):
    img = Image.open(img_path).convert('RGB')
    # work at moderate res: orientation is a low-frequency field
    scale = 420 / max(img.size)
    img = img.resize((int(img.size[0]*scale), int(img.size[1]*scale)), Image.LANCZOS)
    A = np.asarray(img, dtype=np.float64) / 255.0
    Hpx, Wpx = A.shape[:2]

    R, G, B = A[..., 0], A[..., 1], A[..., 2]
    lum = 0.299*R + 0.587*G + 0.114*B
    mx = A.max(-1); mn = A.min(-1)
    sat = np.where(mx > 1e-6, (mx - mn) / np.maximum(mx, 1e-6), 0.0)

    # ── structure tensor orientation ──
    gy, gx = np.gradient(lum)
    Jxx, Jyy, Jxy = gx*gx, gy*gy, gx*gy
    def blur(F, n=6):
        for _ in range(n):
            F = (F + np.roll(F,1,0) + np.roll(F,-1,0) + np.roll(F,1,1) + np.roll(F,-1,1)) / 5.0
        return F
    Jxx, Jyy, Jxy = blur(Jxx), blur(Jyy), blur(Jxy)
    # orientation of least gradient change = stroke direction
    theta = 0.5 * np.arctan2(2*Jxy, (Jxx - Jyy)) + np.pi/2
    coher = np.sqrt((Jxx - Jyy)**2 + 4*Jxy**2) / np.maximum(Jxx + Jyy, 1e-9)
    energy = np.sqrt(Jxx + Jyy)

    # ── palette -> domain ──
    def domain_at(iy, ix):
        r, g, b = R[iy, ix], G[iy, ix], B[iy, ix]
        l = lum[iy, ix]
        if r > b and r > 0.25 and g > 0.6*r and b < 0.85*g:   # yellows / warm light
            return 'Gold'
        if l < 0.24:                                          # dark cypress/village mass
            return 'Ruby'
        return 'Jade'                                         # the blue sky body

    # ── seed points: probability from stroke energy, stratified grid + jitter ──
    rng = np.random.default_rng(seed)
    prob = (0.25 + 0.75*coher) * (energy / energy.max())
    prob = prob / prob.max()
    cell = 6
    seeds = []
    for cy in range(2, Hpx - 2, cell):
        for cx in range(2, Wpx - 2, cell):
            iy = min(Hpx-3, cy + rng.integers(0, cell))
            ix = min(Wpx-3, cx + rng.integers(0, cell))
            if rng.random() < prob[iy, ix] * 0.9 + 0.04:
                seeds.append((float(iy), float(ix)))
    rng.shuffle(seeds)

    # ── integrate streamlines along theta (bidirectional) ──
    def sample_theta(y, x):
        iy, ix = int(y), int(x)
        return theta[iy, ix]
    def trace(y, x):
        pts = [(y, x)]
        for sgn in (1, -1):
            yy, xx = y, x
            pdir = None
            for _ in range(rng.integers(5, 14)):
                th = sample_theta(yy, xx)
                d = np.array([math.sin(th), math.cos(th)]) * sgn
                if pdir is not None and np.dot(d, pdir) < 0: d = -d
                pdir = d
                yy += d[0]*2.2; xx += d[1]*2.2
                if not (2 <= yy < Hpx-2 and 2 <= xx < Wpx-2): break
                if sgn > 0: pts.append((yy, xx))
                else: pts.insert(0, (yy, xx))
        return pts

    used = np.zeros((Hpx, Wpx), dtype=bool)
    strokes2d = []
    for (sy, sx) in seeds:
        if len(strokes2d) >= target_strokes: break
        if used[int(sy), int(sx)]: continue
        pts = trace(sy, sx)
        if len(pts) < 4: continue
        for (yy, xx) in pts:
            used[max(0,int(yy)-1):int(yy)+2, max(0,int(xx)-1):int(xx)+2] = True
        mid = pts[len(pts)//2]
        dom = domain_at(int(mid[0]), int(mid[1]))
        strokes2d.append((dom, pts))

    # ── lift to the immersive curved canvas ──
    # canvas: width 1.4W bent on a cylinder (radius 1.1W, concave toward +Z viewer),
    # height ~1.1W, luminance relief pushes bright/dark features toward the viewer.
    canvasW = 1.40*W; canvasH = canvasW * Hpx / Wpx
    cylR = 1.10*W
    out = []
    for dom, pts in strokes2d:
        p3 = []
        for (yy, xx) in pts:
            u = (xx / Wpx - 0.5)                       # -0.5..0.5 across
            v = 1.0 - yy / Hpx                         # 0 bottom .. 1 top
            arc = u * canvasW / cylR                   # angle on the cylinder
            x = math.sin(arc) * cylR
            z = -(math.cos(arc) - 1.0) * cylR          # concave toward viewer (+Z)
            iy, ix = int(yy), int(xx)
            relief = (0.06 + 0.10 * lum[iy, ix] + 0.05 * (1.0 - coher[iy, ix])) * W * 0.55
            p3.append((x, v * canvasH, z + relief))
        out.append((dom, p3))
    return out

if __name__ == '__main__':
    s = build('campB/starry_night.jpg', W=1300.0)
    allp = [p for _, pts in s for p in pts]
    import numpy as _np
    P = _np.array(allp)
    segs = _np.concatenate([_np.linalg.norm(_np.diff(_np.array(p), axis=0), axis=1) for _, p in s])
    doms = {}
    for d, _ in s: doms[d] = doms.get(d, 0) + 1
    print(f"strokes={len(s)} pts={len(P)} pathW={segs.sum()/1300:.1f} "
          f"minSeg={segs.min():.1f} maxSeg={segs.max():.1f} doms={doms} "
          f"bboxW={tuple(round(float(e)/1300,2) for e in (P.max(0)-P.min(0)))} minY={P[:,1].min():.1f}")
