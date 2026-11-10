"""Per-pixel GLSL texture ops (no baking): the cheap, animated, resolution-free ones.

tex.warpfbm   iq's domain-warped fbm f(p + k f(p + k f(p))) with time drift and a 3-colour mix: the HyperSea nebula in
              one node (the most common 4-6 node sub-graph of the Amoebius-like seeds)
tex.palette   iq cosine palette a + b cos(2 pi (c t + d)) applied to the input's luma (colour grade in one node)
tex.polar     rect <-> polar unwrap with repeats and twist (tunnels and rings from stripes)
tex.kaleido   dihedral n-fold mirror symmetry
tex.flowwarp  advect a along the gradient field of b's luma for n small steps (curly, marbled flow)
"""
import math

from agenome import glsl
from agenome.ops import Op, P

from .registry import register, hsvp

f = glsl.f
v3 = glsl.v3
TAU = 2 * math.pi


def g_warpfbm(i, d, ins, T):
    fr = max(1, round(d["freq"]))
    oc = int(d["oct"])
    sd = f(d["seed"] * 5.17)
    k1, k2 = f(d["warp"]), f(d["warp2"])
    dr = f(d["drift"] * 0.05)
    a, b, c = v3(glsl.hsv(d, "a")), v3(glsl.hsv(d, "b")), v3(glsl.hsv(d, "c"))
    F = lambda q, s: f"fbm2({q}, {f(fr)}, {oc}, 0.5, {sd}+{s})"
    return (f"vec2 p = fract(uv); float tt = uTime*{dr};"
            f" vec2 q = vec2({F('p+vec2(tt,0.0)', '0.0')}, {F('p+vec2(0.52,0.13)', '3.1')});"
            f" vec2 r = vec2({F('fract(p+' + k1 + '*q+vec2(0.17,0.92)+tt*0.6)', '7.7')}, {F('fract(p+' + k1 + '*q+vec2(0.83,0.28))', '11.3')});"
            f" float x = {F('fract(p+' + k2 + '*r)', '19.9')}; x = clamp((x-0.5)*{f(d['contrast'])}+0.5, 0.0, 1.0);"
            f" vec3 col = mix({a}, {b}, x); col = mix(col, {c}, clamp(length(q)*length(r)*1.2-0.3, 0.0, 1.0)*{f(d['cmix'])});"
            f" return col*(0.4+0.8*x);")


register(Op(0xB0, "tex.warpfbm", "T", (), (
    P("freq", "exp", 1, 16, 80), P("oct", "int", 1, 6, 180), P("warp", "lin", 0, 2, 100), P("warp2", "lin", 0, 2, 100),
    P("contrast", "exp", 0.5, 4, 128), P("cmix", "lin", 0, 1, 128), P("seed", "int", 0, 255, 0),
    P("drift", "scub", 0, 4, 150)) + hsvp("a", 175, 220, 20) + hsvp("b", 190, 160, 120) + hsvp("c", 215, 140, 200),
    "domain-warped fbm (Quilez): nebula / marble in one node, slowly drifting", "iq warp"),
    family="glsl", glsl_t=g_warpfbm)


def g_palette(i, d, ins, T):
    A = T(0)
    a = (d["ar"], d["ag"], d["ab"])
    b = (d["br"], d["bg"], d["bb"])
    c = d["freq"]
    ph = (d["ph"], d["ph"] + d["spread"], d["ph"] + 2 * d["spread"])
    return (f"float l = luma({A('uv')}); vec3 col = {v3(a)} + {v3(b)}*cos(6.2831853*({f(c)}*l + {v3(ph)}));"
            f" return max(col, vec3(0.0))*{f(d['gain'])};")


register(Op(0xB1, "tex.palette", "T", ("T",), (
    P("ar", "lin", 0, 1, 64), P("ag", "lin", 0, 1, 50), P("ab", "lin", 0, 1, 110), P("br", "lin", 0, 1, 64),
    P("bg", "lin", 0, 1, 50), P("bb", "lin", 0, 1, 100), P("freq", "exp", 0.25, 4, 128), P("ph", "lin", 0, 1, 128),
    P("spread", "lin", 0, 0.5, 50), P("gain", "exp", 0.25, 4, 128)),
    "cosine palette (Quilez) on the input's luma: a + b cos(2pi(freq l + phase_rgb))", "iq palettes"),
    family="glsl", glsl_t=g_palette)


def g_polar(i, d, ins, T):
    A = T(0)
    rep = max(1, int(d["repeat"]))
    if int(d["mode"]) == 0:  # rect -> polar (stripes become rings / spokes)
        return (f"vec2 q = fract(uv)-0.5; float r = length(q)*2.0; float a = atan(q.y, q.x)/6.2831853+0.5;"
                f" return {A(f'vec2(a*{rep}.0 + r*{f(d['twist'])}, pow(r, {f(d['power'])})*{f(d['zoom'])})')};")
    return (f"vec2 p = fract(uv); float a = p.x*6.2831853/{rep}.0; float r = pow(p.y, {f(d['power'])})*0.5*{f(d['zoom'])};"
            f" return {A(f'vec2(0.5+r*cos(a+{f(d['twist'])}*r), 0.5+r*sin(a+{f(d['twist'])}*r))')};")


register(Op(0xB2, "tex.polar", "T", ("T",), (
    P("mode", "enum", 0, 2, 0), P("repeat", "int", 1, 16, 30), P("twist", "scub", 0, 8, 128),
    P("power", "exp", 0.25, 4, 128), P("zoom", "exp", 0.25, 8, 128)),
    "polar unwrap / wrap with angular repeats and twist (werkkzeug Unwrap)", "Bitmap_Unwrap"),
    family="glsl", glsl_t=g_polar)


def g_kaleido(i, d, ins, T):
    A = T(0)
    n = max(1, int(d["count"]))
    return (f"vec2 q = fract(uv)-vec2({f(d['cx'])},{f(d['cy'])}); float sec = 3.14159265/{n}.0; float a = atan(q.y, q.x)+{f(d['angle'])};"
            f" a = mod(a, 2.0*sec); a = abs(a-sec); float r = length(q)*{f(d['zoom'])}; return {A('vec2(cos(a), sin(a))*r+0.5')};")


register(Op(0xB3, "tex.kaleido", "T", ("T",), (
    P("count", "int", 1, 16, 50), P("angle", "lin", 0, TAU, 0), P("zoom", "exp", 0.25, 4, 128),
    P("cx", "lin", 0, 1, 128), P("cy", "lin", 0, 1, 128)),
    "kaleidoscope: dihedral n-fold mirror symmetry", "kaleidoscope"), family="glsl", glsl_t=g_kaleido)


def g_flowwarp(i, d, ins, T):
    A, B = T(0), T(1)
    n = max(1, int(d["steps"]))
    e = 0.004
    st = f(d["amount"] / n)
    rot = d["swirl"]
    c, s = math.cos(rot), math.sin(rot)
    return (f"vec2 q = uv; for(int k=0;k<{n};k++){{ float l0 = luma({B('q')});"
            f" vec2 gr = vec2(luma({B(f'q+vec2({e},0.0)')})-l0, luma({B(f'q+vec2(0.0,{e})')})-l0)/{e};"
            f" gr = mat2({f(c)},{f(s)},{f(-s)},{f(c)})*gr; q += gr*{st}/(1.0+length(gr)*0.2); }} return {A('q')};")


register(Op(0xB4, "tex.flowwarp", "T", ("T", "T"), (
    P("steps", "int", 1, 6, 100), P("amount", "exp", 0.001, 0.2, 128), P("swirl", "lin", 0, math.pi, 128)),
    "advect a along b's luma gradient (swirl=pi/2: along its level lines): marbled, curly flow", "flow noise / LIC"),
    family="glsl", glsl_t=g_flowwarp)

DIR = ("vec2 e_ = (uv-0.5)*vec2(6.2831853, 3.14159265); vec3 dir = vec3(cos(e_.y)*cos(e_.x), sin(e_.y), cos(e_.y)*sin(e_.x));")


def g_kali(i, d, ins, T):
    it = int(d["iter"])
    vs = int(d["vol"])
    tile = f(d["tile"])
    tint = v3(glsl.hsv(d, "a"))
    return (f"{DIR} vec3 fr = vec3(1.0,0.5,0.5) + vec3(1.0,0.37,0.11)*uTime*{f(d['drift'] * 0.05)};"
            f" float s = 0.1, fade = 1.0; vec3 v = vec3(0.0);"
            f" for(int r=0;r<{vs};r++){{ vec3 p = fr + s*dir*{f(0.5 * d['zoom'])};"
            f" p = abs(vec3({tile}) - mod(p, vec3({tile}*2.0))); float pa = 0.0, a = 0.0;"
            f" for(int k=0;k<{it};k++){{ p = abs(p)/dot(p,p) - {f(d['form'])}; a += abs(length(p)-pa); pa = length(p); }}"
            f" float dm = max(0.0, {f(d['dark'])} - a*a*0.001); a *= a*a; if(r>{max(1, vs - 3)}) fade *= 1.0-dm;"
            f" v += fade; v += vec3(s, s*s, s*s*s*s)*a*{f(d['bright'])}*fade; fade *= {f(d['distfade'])}; s += {f(d['step'])}; }}"
            f" v = mix(vec3(length(v)), v, {f(d['sat'])})*0.01; return mix(v, luma(v)*{tint}*2.0, {f(d['tmix'])})*{f(d['gain'])};")


register(Op(0xB5, "tex.kali", "T", (), (
    P("form", "lin", 0.4, 0.6, 166), P("iter", "int", 4, 18, 146), P("vol", "int", 2, 16, 110),
    P("tile", "lin", 0.5, 1.0, 179), P("step", "exp", 0.05, 0.3, 64), P("bright", "exp", 0.0003, 0.01, 101),
    P("dark", "lin", 0, 1, 77), P("distfade", "lin", 0.5, 0.95, 105), P("sat", "lin", 0, 1, 217),
    P("zoom", "exp", 0.25, 4, 128), P("drift", "scub", 0, 4, 150), P("tmix", "lin", 0, 1, 150), P("gain", "exp", 0.1, 10, 128))
    + hsvp("a", 170, 160, 200),
    "Kali's Star Nest: volumetric abs(p)/dot(p,p) fractal along each sky direction: filaments, dust lanes, stars, drift",
    "Star Nest (Kali, Shadertoy XlfGRj)"), family="glsl", glsl_t=g_kali)


def g_galaxy(i, d, ins, T):
    B = T(0, "vec3(0.0)")
    lon, lat = d["lon"], d["lat"]
    c = (math.cos(lat) * math.cos(lon), math.sin(lat), math.cos(lat) * math.sin(lon))
    up = (0.0, 1.0, 0.0)
    e1 = (c[1] * up[2] - c[2] * up[1], c[2] * up[0] - c[0] * up[2], c[0] * up[1] - c[1] * up[0])
    n1 = math.sqrt(sum(x * x for x in e1)) or 1.0
    e1 = tuple(x / n1 for x in e1)
    e2 = (c[1] * e1[2] - c[2] * e1[1], c[2] * e1[0] - c[0] * e1[2], c[0] * e1[1] - c[1] * e1[0])
    core, arm = v3(glsl.hsv(d, "a")), v3(glsl.hsv(d, "b"))
    pa, inc = d["pa"], d["incl"]
    cp, sp = math.cos(pa), math.sin(pa)
    if int(d["mode"]) == 0:  # spiral galaxy decal, gnomonic projection around (lon, lat)
        dens = (f"float cz = dot(dir, {v3(c)}); if(cz <= 0.0) return base; vec2 q = vec2(dot(dir,{v3(e1)}), dot(dir,{v3(e2)}))/cz/{f(d['size'])};"
                f" q = mat2({f(cp)},{f(sp)},{f(-sp)},{f(cp)})*q; q.y /= {f(max(0.05, math.cos(inc)))}; float r = length(q)+1e-4; float th = atan(q.y, q.x);"
                f" float sp_ = pow(0.5+0.5*cos({int(d['arms'])}.0*(th - log(r)/{f(math.tan(d['pitch']))}) + uTime*{f(d['spin'] * 0.05)}), {f(d['sharp'])});"
                f" float dust = fbm2(fract(q*0.5+0.5), 8.0, 4, 0.5, {f(d['seed'] * 3.1)});"
                f" float bul = exp(-r*r/0.02); float disk = exp(-r*3.0)*sp_*(0.6+0.8*dust);")
    else:  # band: a Milky-Way-like great-circle band with a warm bulge at (lon, lat)
        dens = (f"float cz = dot(dir, {v3(c)}); float b = dot(dir, mat3({v3(e1)},{v3(e2)},{v3(c)})*vec3({f(-sp)},{f(cp)},0.0));"
                f" float r = acos(clamp(cz, -1.0, 1.0)); float w = {f(d['size'] * 0.25)};"
                f" float dust = fbm2(fract(uv*vec2(4.0,2.0)), 8.0, 5, 0.55, {f(d['seed'] * 3.1)});"
                f" float bul = exp(-r*r/(w*w*6.0))*exp(-b*b/(w*w*0.8)); float disk = exp(-b*b/(w*w))*(0.4+0.9*dust)*(1.0-smoothstep(0.0,0.5,abs(dust-0.5)*{f(d['sharp'] * 0.5)}));")
    return (f"{DIR} vec3 base = {B('uv')}; {dens} return base + ({core}*bul*{f(d['bulge'])} + {arm}*disk)*{f(d['gain'])};")


register(Op(0xB6, "tex.galaxy", "T", ("t",), (
    P("mode", "enum", 0, 2, 0), P("lon", "lin", 0, TAU, 40), P("lat", "lin", -1.4, 1.4, 150), P("size", "exp", 0.05, 2, 128),
    P("pa", "lin", 0, math.pi, 60), P("incl", "lin", 0, 1.5, 100), P("arms", "int", 1, 6, 60), P("pitch", "lin", 0.15, 1.2, 90),
    P("sharp", "exp", 0.5, 8, 128), P("spin", "scub", 0, 4, 140), P("bulge", "exp", 0.1, 10, 128), P("gain", "exp", 0.05, 8, 128),
    P("seed", "int", 0, 255, 0)) + hsvp("a", 25, 170, 230) + hsvp("b", 160, 150, 160),
    "galaxy over the input sky: log-spiral decal (arms, pitch, tilt, bulge, dust) or a Milky-Way band with a warm bulge",
    "log spiral / galactic band"), family="glsl", glsl_t=g_galaxy)
