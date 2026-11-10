"""hops: a plugin library of new operators for the agenome renderer (no edits to the agenome package).

    import hier.hops as hops; hops.install()      # idempotent

Adds
  T  tex.hx_fbm_warp   0x18  IQ domain-warped fbm (marbled nebula)
     tex.hx_spectral   0x19  spectral synthesis: K random-phase plane waves, amplitude |k|^-beta (planar or on the sphere)
     tex.hx_hexgrid    0x1A  thin hex / triangle / square line lattice with glow, over an optional input
     tex.hx_chladni    0x1B  Laplacian eigenmodes of the square (Chladni figures)
     tex.hx_sph        0x1C  real spherical harmonic Y_lm of the sky direction (eigenmodes of the sphere)
     tex.hx_beam       0x1D  great-circle light beams over an optional input (straight streaks in perspective)
     tex.hx_stars      0x1E  hashed 3D star field on the sky sphere over an optional input
     tex.hx_gabor      0x1F  sparse Gabor noise
  S  sdf.hx_pyramidfield 0x2D  plane / slab studded with a lattice of pyramids (prism walls)
  P  post.hx_fft       0x8C  numpy: reshape the radial power spectrum of each frame
     post.hx_pca       0x8D  numpy: PCA / Monge-Kantorovich linear colour transfer to a parametric target
     post.hx_dog       0x8E  numpy: difference-of-gaussians band contrast
     post.hx_histeq    0x8F  numpy: luma CDF match to a Beta(a, b) distribution

Numpy post ops chain through their single optional input slot ("p",): root.p -> post.X -> post.Y -> post.grade.
Renderer.render is wrapped: the chain is cut off, the GL pipeline renders with the post.grade found at the end of
the chain (or defaults), then the numpy ops run innermost first (the one wired to the root runs last).
"""
import math

import numpy as np

_INSTALLED = False

# ----------------------------------------------------------------------------------------------- op table
T_OPS, S_OPS, NP_POST = (), (), ()


def _build_ops():
    from agenome.ops import Op, P, _hsv
    pi = math.pi
    T = [
        Op(0x18, "tex.hx_fbm_warp", "T", (), (
            P("freq", "exp", 1, 16, 128), P("oct", "int", 1, 6, 204), P("warp", "lin", 0, 8, 96),
            P("seed", "int", 0, 255, 0), P("speed", "scub", 0, 0.05, 177), P("contrast", "exp", 1, 8, 113),
            P("gamma", "exp", 0.25, 4, 128), P("streak", "lin", 0, 4, 64))
            + _hsv("a", 168, 230, 69) + _hsv("b", 180, 190, 174) + _hsv("c", 160, 140, 204),
            "IQ domain-warped fbm f(p+w*r), r=fbm(p+w*q), q=fbm(p); 3-stop ramp (marbled nebula)", "hops"),
        Op(0x19, "tex.hx_spectral", "T", (), (
            P("K", "int", 1, 24, 200), P("beta", "lin", 0, 3, 85), P("fmin", "exp", 0.5, 32, 112),
            P("fmax", "exp", 1, 128, 150), P("angle", "lin", 0, pi, 0), P("spread", "lin", 0, 1, 255),
            P("seed", "int", 0, 255, 0), P("map", "enum", 0, 2, 255), P("speed", "scub", 0, 2, 165),
            P("contrast", "exp", 0.5, 8, 101), P("gamma", "exp", 0.25, 4, 128), P("mode", "enum", 0, 3, 0))
            + _hsv("a", 168, 230, 69) + _hsv("b", 178, 170, 186),
            "sum of K plane waves, |k| log-uniform in [fmin,fmax], amp |k|^-beta, orientation angle+spread; "
            "map 0 planar uv, 1 sky sphere; mode linear/ridged/billow", "hops (inverse FFT synthesis)"),
        Op(0x1A, "tex.hx_hexgrid", "T", ("t",), (
            P("kind", "enum", 0, 3, 128), P("scale", "exp", 2, 96, 145), P("width", "exp", 0.005, 0.3, 86),
            P("glow", "lin", 0, 1, 64), P("falloff", "exp", 2, 200, 150)) + _hsv("", 160, 204, 140) + (
            P("asp", "lin", 0.5, 3, 153), P("angle", "lin", 0, pi, 0), P("mode", "enum", 0, 3, 0),
            P("pulse", "lin", 0, 1, 40)),
            "line lattice (hex / triangle / square) over optional input: add / mask / screen", "hops"),
        Op(0x1B, "tex.hx_chladni", "T", (), (
            P("n", "int", 1, 12, 46), P("m", "int", 1, 12, 93), P("mix", "lin", -1, 1, 255),
            P("sharp", "exp", 2, 200, 128), P("rep", "int", 1, 8, 36), P("morph", "exp", 0.01, 2, 80),
            P("asp", "lin", 0.5, 3, 153), P("fill", "lin", 0, 1, 77))
            + _hsv("a", 170, 220, 145) + _hsv("b", 150, 150, 185),
            "cos(n pi x)cos(m pi y) + s cos(m pi x)cos(n pi y), s = mix*cos(morph t); nodal lines in b", "hops"),
        Op(0x1C, "tex.hx_sph", "T", (), (
            P("l", "int", 0, 8, 159), P("m", "int", -8, 8, 175), P("sharp", "exp", 1, 100, 204),
            P("tilt", "lin", 0, pi, 41), P("spin", "scub", 0, 1, 175), P("contrast", "exp", 0.5, 8, 150),
            P("line", "lin", 0, 2, 60))
            + _hsv("a", 180, 200, 174) + _hsv("b", 160, 220, 157) + _hsv("c", 140, 120, 185),
            "real spherical harmonic Y_lm of the sky direction: + lobes a, - lobes b, nodal lines c", "hops"),
        Op(0x1D, "tex.hx_beam", "T", ("t",), (
            P("count", "int", 1, 4, 85), P("az", "lin", 0, 2 * pi, 32), P("el", "lin", -pi / 2, pi / 2, 160),
            P("spacing", "lin", 0, 1.5, 60), P("width", "exp", 0.002, 0.2, 66), P("halo", "exp", 1, 40, 124),
            P("length", "exp", 0.2, 6.3, 187), P("intensity", "exp", 0.05, 20, 165), P("drift", "scub", 0, 0.5, 171),
            P("phase", "lin", 0, 2 * pi, 0))
            + _hsv("c", 31, 51, 235) + _hsv("g", 28, 153, 186),
            "great-circle light beams (straight streaks in perspective) added over optional input", "hops"),
        Op(0x1E, "tex.hx_stars", "T", ("t",), (
            P("density", "exp", 8, 300, 113), P("prob", "lin", 0, 1, 64), P("size", "exp", 0.02, 0.5, 200),
            P("bright", "exp", 0.1, 20, 130), P("bvar", "lin", 0, 1, 204)) + _hsv("", 153, 64, 196) + (
            P("huevar", "lin", 0, 1, 64), P("twinkle", "lin", 0, 1, 77), P("seed", "int", 0, 255, 0)),
            "sparse hashed stars on the direction sphere, added over optional input", "hops"),
        Op(0x1F, "tex.hx_gabor", "T", (), (
            P("freq", "exp", 0.5, 8, 128), P("cells", "exp", 2, 64, 120), P("angle", "lin", 0, pi, 40),
            P("spread", "lin", 0, 1, 180), P("density", "int", 1, 4, 170), P("seed", "int", 0, 255, 0),
            P("asp", "lin", 0.5, 3, 153), P("contrast", "exp", 0.5, 8, 101), P("gamma", "exp", 0.25, 4, 128),
            P("speed", "scub", 0, 3, 160)) + _hsv("a", 168, 230, 69) + _hsv("b", 176, 170, 186),
            "sparse Gabor noise: oriented band-limited impulses (freq, orientation, spread, density)", "hops"),
    ]
    S = [
        Op(0x2D, "sdf.hx_pyramidfield", "S", (), (
            P("pitch", "exp", 0.2, 60, 134), P("height", "lin", 0, 2, 102), P("fill", "lin", 0.3, 1, 255),
            P("jitter", "lin", 0, 1, 77), P("thick", "exp", 0.05, 100, 128), P("seed", "int", 0, 255, 0),
            P("axis", "enum", 0, 3, 128), P("gap", "exp", 0.5, 400, 0)),
            "half-space / slab (normal along axis) studded with a square lattice of pyramids; gap>0 (byte) "
            "mirrors it into a corridor of two inward-facing studded walls at +-gap", "hops"),
    ]
    Pp = [
        Op(0x8C, "post.hx_fft", "P", ("p",), (
            P("slope", "lin", -1.5, 1.5, 140), P("f0", "exp", 0.01, 0.5, 128), P("bgain", "lin", -1, 3, 96),
            P("fb", "exp", 0.01, 0.5, 110), P("bw", "exp", 0.005, 0.3, 128), P("oamt", "lin", 0, 1, 0),
            P("oang", "lin", 0, math.pi, 0), P("chan", "enum", 0, 2, 0), P("amount", "lin", 0, 1, 255)),
            "numpy: luma (or rgb) spectrum gain (f/f0)^slope * (1 + bgain*gauss(f-fb, bw)) * orientation", "hops"),
        Op(0x8D, "post.hx_pca", "P", ("p",), (
            P("mode", "enum", 0, 2, 0), P("amount", "lin", 0, 1, 179)) + _hsv("m", 170, 150, 137) + (
            P("s1", "exp", 0.01, 0.5, 181), P("s2", "exp", 0.01, 0.5, 72), P("s3", "exp", 0.01, 0.5, 26),
            P("ah", "hue", default=182), P("tilt", "lin", 0, 1, 89), P("roll", "lin", 0, math.pi, 0)),
            "numpy: PCA colour transfer to target mean (hsv) / eigen stds / principal axis leaning to hue ah",
            "hops (Monge-Kantorovich linear)"),
        Op(0x8E, "post.hx_dog", "P", ("p",), (
            P("s1", "exp", 0.5, 16, 40), P("s2", "exp", 1, 32, 110), P("gain", "lin", -1, 3, 100),
            P("chan", "enum", 0, 2, 0), P("amount", "lin", 0, 1, 255)),
            "numpy: x + gain*(G_s1 x - G_s2 x), band contrast", "hops"),
        Op(0x8F, "post.hx_histeq", "P", ("p",), (
            P("a", "exp", 0.3, 10, 99), P("b", "exp", 0.3, 10, 188), P("amount", "lin", 0, 1, 102),
            P("mode", "enum", 0, 2, 0)),
            "numpy: luma tone curve = CDF match to Beta(a,b), mixed by amount", "hops"),
    ]
    return T, S, Pp


# ----------------------------------------------------------------------------------------------- GLSL
HP_PRELUDE = r"""
vec3 hp_dir(vec2 uv){ float lon = (uv.x-0.5)*6.2831853; float lat = (uv.y-0.5)*3.14159265;
  return vec3(cos(lat)*cos(lon), sin(lat), cos(lat)*sin(lon)); }
vec3 hp_ramp3(vec3 a, vec3 b, vec3 c, float x){ x = clamp(x, 0.0, 1.0); return x < 0.5 ? mix(a, b, x*2.0) : mix(b, c, x*2.0-1.0); }
"""


def _dec(o, n):
    return {p.name: p.decode(b) for p, b in zip(o.params, n.params)}


def _rng(seed, salt):
    return np.random.RandomState((int(seed) * 7919 + salt) & 0x7FFFFFFF)


def _legendre_glsl(l, mm):
    """GLSL statements computing P_l^mm(x) into float P (x is a float in scope)."""
    c0 = (-1) ** mm
    for k in range(1, 2 * mm, 2):
        c0 *= k
    from agenome.glsl import f
    s = f"float pmm = {f(c0)}" + (f"*pow(max(1.0-x*x, 0.0), {f(mm * 0.5)});" if mm > 0 else ";")
    if l == mm:
        return s + " float P = pmm;"
    s += f" float pm1 = x*{f(2 * mm + 1)}*pmm;"
    if l == mm + 1:
        return s + " float P = pm1;"
    s += " float pll = 0.0;"
    for ll in range(mm + 2, l + 1):
        s += f" pll = (x*{f(2 * ll - 1)}*pm1 - {f(ll + mm - 1)}*pmm)/{f(ll - mm)}; pmm = pm1; pm1 = pll;"
    return s + " float P = pll;"


def _ylm_np(l, m, x, phi):
    mm = abs(m)
    pmm = (-1) ** mm * np.prod(np.arange(1, 2 * mm, 2)) * (1 - x * x) ** (mm / 2) if mm else np.ones_like(x)
    if l == mm:
        P = pmm
    else:
        pm1 = x * (2 * mm + 1) * pmm
        if l == mm + 1:
            P = pm1
        else:
            for ll in range(mm + 2, l + 1):
                pll = (x * (2 * ll - 1) * pm1 - (ll + mm - 1) * pmm) / (ll - mm)
                pmm, pm1 = pm1, pll
            P = pm1
    ang = np.cos(mm * phi) if m > 0 else (np.sin(mm * phi) if m < 0 else 1.0)
    return P * ang


def _gen_tex(op, i, n, o):
    from agenome.glsl import f, v3, hsv
    d = _dec(o, n)
    ins = n.inputs
    j = ins[0] if ins else None
    base = f"t{j}(uv)" if j is not None else "vec3(0.0)"

    if op == "tex.hx_fbm_warp":
        F = float(max(1, round(d["freq"])))
        O = int(d["oct"])
        W = 2.0 * d["warp"] / F
        S = d["seed"] * 7.31
        return (f"vec2 p = fract(uv); vec2 dr = vec2(uTime*{f(d['speed'])}, uTime*{f(d['speed'] * 0.37)});"
                f" vec2 q = vec2(fbm2(p+dr*0.3, {f(F)}, {O}, 0.5, {f(S)}), fbm2(p+vec2(0.31,0.17), {f(F)}, {O}, 0.5, {f(S + 11)})) - 0.5;"
                f" vec2 r = vec2(fbm2(p+{f(W)}*q+vec2(0.17,0.92)+dr, {f(F)}, {O}, 0.5, {f(S + 23)}),"
                f" fbm2(p+{f(W)}*q+vec2(0.83,0.28)-dr, {f(F)}, {O}, 0.5, {f(S + 37)})) - 0.5;"
                f" float v = fbm2(p+{f(W)}*r, {f(F)}, {O}, 0.5, {f(S + 51)});"
                f" float x = pow(clamp((v-0.5)*{f(d['contrast'])}+0.5, 0.0, 1.0), {f(d['gamma'])});"
                f" vec3 col = hp_ramp3({v3(hsv(d, 'a'))}, {v3(hsv(d, 'b'))}, {v3(hsv(d, 'c'))}, x);"
                f" col = mix(col, {v3(hsv(d, 'c'))}, clamp(abs(r.x)*{f(d['streak'])}*x, 0.0, 1.0)); return col;")

    if op == "tex.hx_spectral":
        rs = _rng(d["seed"], 11)
        NMAX = 24
        u = rs.rand(NMAX)
        ph = rs.rand(NMAX) * 2 * math.pi
        om = (0.5 + rs.rand(NMAX)) * np.where(rs.rand(NMAX) < 0.5, -1, 1)
        g2 = rs.randn(NMAX)
        g3 = rs.randn(NMAX, 3)
        K = int(d["K"])
        fmin, fmax = sorted((d["fmin"], d["fmax"]))
        fr = fmin * (fmax / fmin) ** u
        amp = fr ** (-d["beta"])
        amp = amp[:K]
        norm = math.sqrt(float(np.sum(amp ** 2)) / 2.0) + 1e-9
        sph = int(d["map"]) == 1
        sp = d["spread"]
        terms = []
        for k in range(K):
            a = amp[k] / norm
            w = d["speed"] * om[k]
            if sph:
                ax = np.array([math.cos(d["angle"]), 0.0, math.sin(d["angle"])])
                rv = g3[k] / (np.linalg.norm(g3[k]) + 1e-9)
                kd = ax * (1 - sp) + rv * sp
                kd = kd / (np.linalg.norm(kd) + 1e-9) * math.pi * fr[k]
                arg = f"dot(dd, {v3(kd)})"
            else:
                th = d["angle"] + g2[k] * sp * math.pi
                kd = (2 * math.pi * fr[k] * math.cos(th), 2 * math.pi * fr[k] * math.sin(th))
                arg = f"dot(uv, vec2({f(kd[0])},{f(kd[1])}))"
            terms.append(f"{f(a)}*sin({arg}+{f(ph[k])}+uTime*{f(w)})")
        pre = "vec3 dd = hp_dir(uv);" if sph else ""
        md = int(d["mode"])
        xm = ["0.5+v*{c}*0.18", "1.0-abs(v)*{c}*0.3", "abs(v)*{c}*0.3"][md].format(c=f(d["contrast"]))
        return (f"{pre} float v = {' + '.join(terms)}; float x = pow(clamp({xm}, 0.0, 1.0), {f(d['gamma'])});"
                f" return mix({v3(hsv(d, 'a'))}, {v3(hsv(d, 'b'))}, x);")

    if op == "tex.hx_hexgrid":
        c, s = math.cos(d["angle"]), math.sin(d["angle"])
        kind = int(d["kind"])
        if kind == 0:
            dist = ("vec2 s = vec2(1.0,1.7320508); vec4 hc = floor(vec4(p, p-vec2(0.5,1.0))/s.xyxy)+0.5;"
                    " vec4 h = vec4(p-hc.xy*s, p-(hc.zw+0.5)*s); vec2 g = (dot(h.xy,h.xy)<dot(h.zw,h.zw)) ? h.xy : h.zw;"
                    " vec2 a = abs(g); float dl = 0.5-max(dot(a, s*0.5), a.x);")
        elif kind == 1:
            dist = ("float d0 = abs(fract(p.y+0.5)-0.5); float d1 = abs(fract(dot(p, vec2(0.8660254,0.5))+0.5)-0.5);"
                    " float d2 = abs(fract(dot(p, vec2(0.8660254,-0.5))+0.5)-0.5); float dl = min(d0, min(d1, d2));")
        else:
            dist = "vec2 a = abs(fract(p+0.5)-0.5); float dl = min(a.x, a.y);"
        w = d["width"]
        col = v3(hsv(d))
        md = int(d["mode"])
        blend = ["b + {c}*L", "mix(b, {c}, clamp(L, 0.0, 1.0))", "1.0-(1.0-b)*(1.0-clamp({c}*L, 0.0, 1.0))"][md]
        return (f"vec2 p = mat2({f(c)},{f(s)},{f(-s)},{f(c)})*(uv*vec2({f(d['asp'])},1.0))*{f(d['scale'])}; {dist}"
                f" float L = smoothstep({f(w)}, 0.0, dl) + {f(d['glow'])}*exp(-dl*{f(d['falloff'])});"
                f" L *= 1.0 + {f(d['pulse'])}*sin(uTime*1.7 + p.x*0.37 + p.y*0.23);"
                f" vec3 b = {base}; return {blend.format(c=col)};")

    if op == "tex.hx_chladni":
        nn, mm = int(d["n"]), int(d["m"])
        return (f"vec2 q = uv*vec2({f(d['asp'])},1.0)*{f(d['rep'])}*3.14159265;"
                f" float sg = {f(d['mix'])}*cos(uTime*{f(d['morph'])});"
                f" float z = cos({nn}.0*q.x)*cos({mm}.0*q.y) + sg*cos({mm}.0*q.x)*cos({nn}.0*q.y);"
                f" float line = exp(-abs(z)*{f(d['sharp'])});"
                f" vec3 col = {v3(hsv(d, 'a'))}*max(0.0, 0.55 + {f(d['fill'])}*0.5*z);"
                f" return mix(col, {v3(hsv(d, 'b'))}, line);")

    if op == "tex.hx_sph":
        l = int(d["l"])
        m = max(-l, min(l, int(d["m"])))
        mm = abs(m)
        # normalisation by the numeric max of |Y| (so the colour ramp is independent of l, m)
        xs = np.linspace(-1, 1, 181)[:, None]
        ps = np.linspace(-math.pi, math.pi, 181)[None, :]
        mx = float(np.max(np.abs(_ylm_np(l, m, xs, ps)))) + 1e-9
        ang = f"cos({mm}.0*phi)" if m > 0 else (f"sin({mm}.0*phi)" if m < 0 else "1.0")
        return (f"vec3 dd = hp_dir(uv); dd.yz = rot2({f(d['tilt'])})*dd.yz; float x = dd.y;"
                f" float phi = atan(dd.z, dd.x) + uTime*{f(d['spin'])}; {_legendre_glsl(l, mm)}"
                f" float v = P*{ang}*{f(1.0 / mx)};"
                f" vec3 col = {v3(hsv(d, 'a'))}*clamp(v*{f(d['contrast'])}, 0.0, 1.0)"
                f" + {v3(hsv(d, 'b'))}*clamp(-v*{f(d['contrast'])}, 0.0, 1.0);"
                f" col += {v3(hsv(d, 'c'))}*{f(d['line'])}*exp(-abs(v)*{f(d['sharp'])}); return col;")

    if op == "tex.hx_beam":
        az, el = d["az"], d["el"]
        n0 = np.array([math.cos(el) * math.cos(az), math.sin(el), math.cos(el) * math.sin(az)])
        up = np.array([0.0, 1.0, 0.0])
        c0 = np.cross(n0, up)
        if np.linalg.norm(c0) < 1e-3:
            c0 = np.array([1.0, 0.0, 0.0])
        c0 /= np.linalg.norm(c0)
        b0 = np.cross(n0, c0)
        phs = d["phase"]
        c0 = c0 * math.cos(phs) + b0 * math.sin(phs)
        cc, hc = hsv(d, "c"), hsv(d, "g")
        w, hw, L, I = d["width"], d["width"] * d["halo"], d["length"], d["intensity"]
        parts = []
        for k in range(int(d["count"])):
            a = k * d["spacing"] * 2.4
            R = np.array([[math.cos(a), 0, -math.sin(a)], [0, 1, 0], [math.sin(a), 0, math.cos(a)]])
            nk, ck = R @ n0, R @ c0
            parts.append(f"{{ float s = dot(dd, {v3(nk)}); vec3 pr = dd - {v3(nk)}*s; float al = acos(clamp(dot(pr, {v3(ck)})/max(length(pr),1e-5), -1.0, 1.0));"
                         f" float lf = exp(-al*al*{f(1.0 / (L * L))}); acc += ({v3(cc)}*exp(-s*s*{f(1.0 / (w * w))}) + {v3(hc)}*0.35*exp(-abs(s)*{f(1.0 / hw)}))*lf; }}")
        return (f"vec3 dd = hp_dir(uv); dd.xz = rot2(uTime*{f(d['drift'])})*dd.xz; vec3 acc = vec3(0.0); {' '.join(parts)}"
                f" return {base} + acc*{f(I)};")

    if op == "tex.hx_stars":
        S = d["seed"] * 1.731
        sz = d["size"]
        return (f"vec3 p = hp_dir(uv)*{f(d['density'])}; vec3 c = floor(p); vec3 fp = fract(p);"
                f" vec3 r = vec3(h31(c+{f(S)}), h31(c+{f(S + 17.1)}), h31(c+{f(S + 31.7)}));"
                f" float dl = length(fp - (0.25+0.5*r)); float on = step(h31(c*1.37+{f(S + 5.3)}), {f(d['prob'])});"
                f" float r2 = h31(c+{f(S + 9.1)}); float r3 = h31(c+{f(S + 3.9)});"
                f" float b = {f(d['bright'])}*(1.0-{f(d['bvar'])}*r2)*(1.0+{f(d['twinkle'])}*sin(uTime*(2.0+5.0*r3)+r3*6.2831));"
                f" float I = on*b*exp(-dl*dl*{f(1.0 / (sz * sz))});"
                f" vec3 col = hsv2rgb(vec3(fract({f(d['h'])}+{f(d['huevar'])}*(r3-0.5)), {f(d['s'])}, {f(d['v'])}));"
                f" return {base} + col*I;")

    if op == "tex.hx_gabor":
        S = d["seed"] * 3.17
        F = d["freq"]
        D = int(d["density"])
        return (f"vec2 p = uv*vec2({f(d['asp'])},1.0)*{f(d['cells'])}; vec2 ip = floor(p); vec2 fp = fract(p); float sum = 0.0;"
                f" for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++){{ vec2 cl = ip+vec2(x,y);"
                f" for(int k=0;k<{D};k++){{ float fk = float(k)*13.7+{f(S)};"
                f" vec2 pos = vec2(h21(cl+fk), h21(cl+fk+7.3)); float wt = h21(cl+fk+3.1) < 0.5 ? -1.0 : 1.0;"
                f" float th = {f(d['angle'])} + {f(d['spread'] * math.pi)}*(h21(cl+fk+5.9)-0.5);"
                f" vec2 dv = fp - vec2(x,y) - pos; float env = exp(-4.0*dot(dv,dv));"
                f" sum += wt*env*cos(6.2831853*{f(F)}*dot(dv, vec2(cos(th), sin(th))) + h21(cl+fk+1.7)*6.2831 + uTime*{f(d['speed'])}); }} }}"
                f" float v = sum*{f(1.0 / math.sqrt(D * 0.6))};"
                f" float x = pow(clamp(0.5+v*{f(d['contrast'])}*0.25, 0.0, 1.0), {f(d['gamma'])});"
                f" return mix({v3(hsv(d, 'a'))}, {v3(hsv(d, 'b'))}, x);")
    return None


def _gen_shape(op, i, n, o):
    from agenome.glsl import f
    d = _dec(o, n)
    if op == "sdf.hx_pyramidfield":
        sw = ["p.yxz", "p.xyz", "p.xzy"][int(d["axis"])]
        P = d["pitch"]
        H = d["height"] * P * 0.5
        b = 0.5 * P * d["fill"]
        k = math.sqrt(1.0 + (H / b) ** 2)
        corr = f" q.y = {f(d['gap'])} - abs(q.y);" if n.params[[p.name for p in o.params].index("gap")] > 0 else ""
        return (f"vec3 q = {sw};{corr} vec2 c = floor(q.xz/{f(P)}+0.5); vec2 l = q.xz - c*{f(P)};"
                f" float hj = {f(H)}*(1.0-{f(d['jitter'])}*h21(c+{f(d['seed'] * 1.37)}));"
                f" float hf = hj*max(0.0, 1.0-max(abs(l.x), abs(l.y))/{f(b)});"
                f" return max((q.y-hf)/{f(k)}, -q.y-{f(d['thick'])});")
    return None


# ----------------------------------------------------------------------------------------------- numpy post
_W_LUMA = np.array([0.299, 0.587, 0.114], np.float32)
_cache = {}


def _freq_grid(h, w):
    key = ("fg", h, w)
    if key not in _cache:
        fy = np.fft.fftfreq(h)[:, None]
        fx = np.fft.rfftfreq(w)[None, :]
        _cache[key] = (np.sqrt(fx * fx + fy * fy), np.arctan2(fy, fx), fx, fy)
    return _cache[key]


def _apply_filter(x, G, chan, amount, pad=16):
    """x float32 [T,H,W,3] in 0..1; G transfer function on the padded rfft grid; luma (chan 0) or rgb (chan 1)."""
    if chan == 0:
        Y = x @ _W_LUMA
        Yp = np.pad(Y, ((0, 0), (pad, pad), (pad, pad)), mode="reflect")
        Y2 = np.fft.irfft2(np.fft.rfft2(Yp) * G, s=Yp.shape[1:])[:, pad:-pad, pad:-pad]
        return x + (amount * (Y2 - Y)).astype(np.float32)[..., None]
    xp = np.pad(x, ((0, 0), (pad, pad), (pad, pad), (0, 0)), mode="reflect")
    X2 = np.fft.irfft2(np.fft.rfft2(xp, axes=(1, 2)) * G[..., None], s=xp.shape[1:3], axes=(1, 2))
    X2 = X2[:, pad:-pad, pad:-pad]
    return x + (amount * (X2 - x)).astype(np.float32)


def _post_fft(x, d):
    T, H, W, _ = x.shape
    pad = 16
    fr, th, _, _ = _freq_grid(H + 2 * pad, W + 2 * pad)
    with np.errstate(divide="ignore"):
        G = np.where(fr > 0, (np.maximum(fr, 1e-6) / d["f0"]) ** d["slope"], 1.0)
    G = np.minimum(G, 6.0)
    G = G * (1.0 + d["bgain"] * np.exp(-0.5 * ((fr - d["fb"]) / d["bw"]) ** 2))
    G = G * (1.0 + d["oamt"] * np.cos(2 * (th - d["oang"])))
    G = np.maximum(G, 0.0)
    G[0, 0] = 1.0  # preserve the mean
    return _apply_filter(x, G, int(d["chan"]), d["amount"], pad)


def _post_dog(x, d):
    T, H, W, _ = x.shape
    pad = 16
    fr, _, _, _ = _freq_grid(H + 2 * pad, W + 2 * pad)
    s1, s2 = d["s1"], d["s2"]
    G1 = np.exp(-2 * math.pi ** 2 * s1 * s1 * fr * fr)
    G2 = np.exp(-2 * math.pi ** 2 * s2 * s2 * fr * fr)
    G = 1.0 + d["gain"] * (G1 - G2)
    return _apply_filter(x, np.maximum(G, 0.0), int(d["chan"]), d["amount"], pad)


def _hsv_rgb(h, s, v):
    import colorsys
    return np.array(colorsys.hsv_to_rgb(h % 1.0, max(0.0, min(1.0, s)), 1.0)) * v


def _post_pca(x, d):
    T = x.shape[0]
    mt = _hsv_rgb(d["mh"], d["ms"], d["mv"])
    gray = np.ones(3) / math.sqrt(3)
    hue = _hsv_rgb(d["ah"], 1.0, 1.0)
    ch = hue - gray * hue.dot(gray)
    ch /= np.linalg.norm(ch) + 1e-9
    t = d["tilt"]
    e1 = gray * (1 - t) + ch * t
    e1 /= np.linalg.norm(e1)
    e2 = ch - e1 * ch.dot(e1)
    if np.linalg.norm(e2) < 1e-4:
        e2 = gray - e1 * gray.dot(e1)
    e2 /= np.linalg.norm(e2)
    e3 = np.cross(e1, e2)
    r = d["roll"]
    e2, e3 = e2 * math.cos(r) + e3 * math.sin(r), -e2 * math.sin(r) + e3 * math.cos(r)
    E = np.stack([e1, e2, e3])  # rows: target axes
    st = np.array([d["s1"], d["s2"], d["s3"]])

    def matrix(m, C):
        lam, V = np.linalg.eigh(C)
        o = np.argsort(lam)[::-1]
        lam, V = np.maximum(lam[o], 1e-8), V[:, o]
        for k in range(3):
            if V[:, k].dot(E[k]) < 0:
                V[:, k] = -V[:, k]
        sc = np.minimum(st / np.sqrt(lam), 20.0)
        return (V * sc[None, :]) @ E  # (x-m) @ M + mt

    if int(d["mode"]) == 0:  # one transform for the clip (temporally stable)
        s = x[:, ::3, ::3].reshape(-1, 3).astype(np.float64)
        m = s.mean(0)
        M = matrix(m, np.cov(s.T))
        y = (x - m.astype(np.float32)) @ M.astype(np.float32) + mt.astype(np.float32)
    else:
        y = np.empty_like(x)
        for k in range(T):
            s = x[k, ::2, ::2].reshape(-1, 3).astype(np.float64)
            m = s.mean(0)
            M = matrix(m, np.cov(s.T))
            y[k] = (x[k] - m.astype(np.float32)) @ M.astype(np.float32) + mt.astype(np.float32)
    a = np.float32(d["amount"])
    return x + a * (y - x)


def _beta_ppf_table(a, b, n=1024):
    key = ("beta", round(a, 4), round(b, 4))
    if key not in _cache:
        xs = (np.arange(4096) + 0.5) / 4096
        pdf = np.exp((a - 1) * np.log(xs) + (b - 1) * np.log1p(-xs))
        cdf = np.cumsum(pdf)
        cdf = (cdf - cdf[0]) / (cdf[-1] - cdf[0])
        q = (np.arange(n) + 0.5) / n
        _cache[key] = np.interp(q, cdf, xs).astype(np.float32)
    return _cache[key]


def _post_histeq(x, d):
    nb = 1024
    ppf = _beta_ppf_table(d["a"], d["b"], nb)
    Y = np.clip(x @ _W_LUMA, 0, 1)
    idx = np.minimum((Y * nb).astype(np.int32), nb - 1)

    def lut_of(ix):
        h = np.bincount(ix.ravel(), minlength=nb).astype(np.float64)
        cdf = (np.cumsum(h) - 0.5 * h) / max(1.0, h.sum())
        return ppf[np.minimum((cdf * nb).astype(np.int32), nb - 1)]

    if int(d["mode"]) == 0:
        Y2 = lut_of(idx)[idx]
    else:
        Y2 = np.stack([lut_of(idx[k])[idx[k]] for k in range(len(x))])
    ratio = np.minimum(Y2 / np.maximum(Y, 0.02), 3.0)
    y = x * ratio[..., None]
    a = np.float32(d["amount"])
    return x + a * (y - x)


NP_FUNCS = {"post.hx_fft": _post_fft, "post.hx_pca": _post_pca, "post.hx_dog": _post_dog, "post.hx_histeq": _post_histeq}


def apply_post_chain(frames, nodes):
    """frames uint8 [T,H,W,3]; nodes = list of genome Nodes, applied in list order."""
    from agenome.ops import OPS
    if not nodes:
        return frames
    x = frames.astype(np.float32) * np.float32(1 / 255.0)
    for n in nodes:
        x = NP_FUNCS[n.op](x, _dec(OPS[n.op], n))
        x = np.nan_to_num(x, nan=0.0, posinf=1.0, neginf=0.0)
    return np.clip(x * 255.0 + 0.5, 0, 255).astype(np.uint8)


def split_post_chain(g):
    """On a copy of g: cut the numpy post ops out of the root's p chain. Returns (g2, [nodes outermost first]).

    Kept for callers that want the hx nodes alone; rendering goes through agenome/postchain.py, which cuts every
    family's numpy ops in one walk and applies them in chain order (the code review of 2026-11-10 measured that two
    family hooks, each cutting only its own ops, ran a mixed chain in the order the hooks were installed)."""
    from agenome.ops import OPS as _OPS
    g2 = g.copy().ensure_root()
    r = g2.root()
    rn = g2.nodes[r]
    if len(rn.inputs) < 4:
        return g2, []
    seq, seen, p = [], set(), rn.inputs[3]
    while p is not None and p not in seen and g2.nodes[p].op != "post.grade" and \
            getattr(_OPS.get(g2.nodes[p].op), "out", "P") == "P":
        seen.add(p)
        seq.append(p)
        ins = g2.nodes[p].inputs
        p = ins[0] if ins else None
    tail = p if (p is not None and g2.nodes[p].op == "post.grade") else None
    mine = [i for i in seq if g2.nodes[i].op in NP_FUNCS]
    if not mine:
        return g2, []
    rest = [i for i in seq if g2.nodes[i].op not in NP_FUNCS]
    link = rest + ([tail] if tail is not None else [])
    rn.inputs[3] = link[0] if link else None
    for k, i in enumerate(rest):
        nxt = link[k + 1] if k + 1 < len(link) else None
        if g2.nodes[i].inputs:
            g2.nodes[i].inputs[0] = nxt
        else:
            g2.nodes[i].inputs = [nxt]
    return g2, [g2.nodes[i] for i in mine]


# ----------------------------------------------------------------------------------------------- install
# names before 2026-11-10 18:15 (renamed to the hx_ namespace so they never clash with ops_ext / core names)
RENAMES = {'tex.fbm_warp': 'tex.hx_fbm_warp', 'tex.spectral': 'tex.hx_spectral', 'tex.hexgrid': 'tex.hx_hexgrid', 'tex.chladni': 'tex.hx_chladni', 'tex.sph': 'tex.hx_sph', 'tex.beam': 'tex.hx_beam', 'tex.stars': 'tex.hx_stars', 'tex.gabor': 'tex.hx_gabor', 'sdf.pyramidfield': 'sdf.hx_pyramidfield', 'post.fft': 'post.hx_fft', 'post.pca': 'post.hx_pca', 'post.dog': 'post.hx_dog', 'post.histeq': 'post.hx_histeq'}


def fix_json(d):
    for n in d.get("nodes", []):
        n["op"] = RENAMES.get(n["op"], n["op"])
    return d


def install():
    global _INSTALLED, T_OPS, S_OPS, NP_POST
    if _INSTALLED:
        return
    from agenome import genome as agen
    _orig_from_json = agen.Genome.from_json.__func__

    @classmethod
    def _from_json(cls, d):
        return _orig_from_json(cls, fix_json(d))
    agen.Genome.from_json = _from_json
    from agenome import ops as aops, glsl as aglsl, render as arender
    T, S, Pp = _build_ops()
    T_OPS, S_OPS, NP_POST = tuple(o.name for o in T), tuple(o.name for o in S), tuple(o.name for o in Pp)
    for o in T + S + Pp:
        if o.name in aops.OPS:
            continue
        assert o.code not in aops.BY_CODE, hex(o.code)
        names = [p.name for p in o.params]
        assert len(set(names)) == len(names), o.name
        aops.OPS_LIST.append(o)
        aops.OPS[o.name] = o
        aops.BY_CODE[o.code] = o

    new_t, new_s = set(T_OPS), set(S_OPS)
    orig_tex, orig_shp = aglsl.compile_textures, aglsl.compile_shapes

    def compile_textures(g, live):
        src = orig_tex(g, live)
        hit = False
        for i, n in enumerate(g.nodes):
            if i in live and n.op in new_t:
                body = _gen_tex(n.op, i, n, aops.OPS[n.op])
                if body is not None:
                    src = src.replace(f"vec3 t{i}(vec2 uv){{ return vec3(0.5); }}", f"vec3 t{i}(vec2 uv){{ {body} }}")
                    hit = True
        return (HP_PRELUDE + src) if hit else src

    def compile_shapes(g, live):
        src = orig_shp(g, live)
        for i, n in enumerate(g.nodes):
            if i in live and n.op in new_s:
                body = _gen_shape(n.op, i, n, aops.OPS[n.op])
                if body is not None:
                    src = src.replace(f"float sd{i}(vec3 p){{ return 1e9; }}", f"float sd{i}(vec3 p){{ {body} }}")
        return src

    compile_textures._hops_orig = orig_tex
    compile_shapes._hops_orig = orig_shp
    aglsl.compile_textures = compile_textures
    aglsl.compile_shapes = compile_shapes

    # numpy post ops: registered with the one post-chain executor (agenome/postchain.py), which cuts every family's
    # numpy ops out of the chain in one walk and applies them in chain order (until 2026-11-10 this module wrapped
    # Renderer.render itself, so in a chain mixed with ops_ext's post.bandgain the inner hook's family ran first)
    from agenome import postchain
    postchain.register("hx", NP_FUNCS, apply_post_chain)
    postchain.install(arender)
    _INSTALLED = True
