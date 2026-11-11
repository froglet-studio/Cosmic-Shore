"""Compile the T (texture) and S (shape) parts of a genome to GLSL, demoscene style.

Every texture node i becomes `vec3 t<i>(vec2 uv)`; every shape node i becomes `float sd<i>(vec3 p)` (distance,
with volumetric-glow side effects while marching) and `Mat mt<i>(vec3 p, vec3 n)` (material at a hit point).
"""
import math
import colorsys

from .ops import OPS


def f(x):
    x = float(x)
    if not math.isfinite(x):
        x = 0.0
    s = f"{x:.6g}" if "e" not in f"{x:.6g}" else f"{x:.8f}"
    # negatives in parentheses: a template "a-{f(x)}" printed "a--0.3", a GLSL syntax error (found by the ops thread)
    return f"({s})" if s.startswith("-") else s


def v3(a):
    return f"vec3({f(a[0])},{f(a[1])},{f(a[2])})"


def hsv(d, pre=""):
    h, s, v = d[pre + "h"], d[pre + "s"], d[pre + "v"]
    r, g, b = colorsys.hsv_to_rgb(h % 1.0, max(0.0, min(1.0, s)), 1.0)
    return (r * v, g * v, b * v)


PRELUDE = r"""
#version 330
uniform float uTime;
uniform vec2 uRes;
uniform vec3 uEye;
uniform mat3 uCam;
uniform float uTanHalf, uNear, uFar, uSky, uAmbient;
uniform int uSteps;
uniform vec3 uLightDir, uLightCol, uFogCol;
uniform float uFog;
out vec4 oCol;
struct Mat { vec3 alb; vec3 emi; float spec; };
vec3 gN = vec3(0.0, 0.0, 1.0); vec3 gV = vec3(0.0, 0.0, 1.0); // world normal and view direction at the shaded hit
float gGlowOn = 0.0;
vec3 gGlow = vec3(0.0);
const float PI = 3.14159265;
float h11(float p){ p = fract(p*0.1031); p *= p+33.33; p *= p+p; return fract(p); }
float h21(vec2 p){ vec3 p3 = fract(vec3(p.xyx)*0.1031); p3 += dot(p3, p3.yzx+33.33); return fract((p3.x+p3.y)*p3.z); }
float h31(vec3 p3){ p3 = fract(p3*0.1031); p3 += dot(p3, p3.zyx+31.32); return fract((p3.x+p3.y)*p3.z); }
vec2 h22(vec2 p){ vec3 p3 = fract(vec3(p.xyx)*vec3(.1031,.1030,.0973)); p3 += dot(p3, p3.yzx+33.33); return fract((p3.xx+p3.yz)*p3.zy); }
float vn2p(vec2 p, float per, float sd){ vec2 i = floor(p), fr = fract(p); vec2 u = fr*fr*(3.0-2.0*fr);
  float a = h21(mod(i, per)+sd), b = h21(mod(i+vec2(1,0), per)+sd), c = h21(mod(i+vec2(0,1), per)+sd), d = h21(mod(i+vec2(1,1), per)+sd);
  return mix(mix(a,b,u.x), mix(c,d,u.x), u.y); }
float fbm2(vec2 uv, float freq, int oct, float pers, float sd){ float s = 0.0, a = 1.0, w = 0.0, fq = freq;
  for(int i=0;i<8;i++){ if(i>=oct) break; s += a*vn2p(uv*fq, fq, sd+float(i)*17.0); w += a; a *= pers; fq *= 2.0; } return s/w; }
float vn3(vec3 p){ vec3 i = floor(p), fr = fract(p); vec3 u = fr*fr*(3.0-2.0*fr);
  return mix(mix(mix(h31(i), h31(i+vec3(1,0,0)), u.x), mix(h31(i+vec3(0,1,0)), h31(i+vec3(1,1,0)), u.x), u.y),
             mix(mix(h31(i+vec3(0,0,1)), h31(i+vec3(1,0,1)), u.x), mix(h31(i+vec3(0,1,1)), h31(i+vec3(1,1,1)), u.x), u.y), u.z); }
vec2 vor2(vec2 uv, float n, float sd){ vec2 p = uv*n; vec2 i = floor(p), fr = fract(p); float f1 = 9.0, f2 = 9.0;
  for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++){ vec2 g = vec2(x,y); vec2 o = h22(mod(i+g, n)+sd); float d = length(g+o-fr);
    if(d<f1){ f2 = f1; f1 = d; } else if(d<f2){ f2 = d; } } return vec2(f1, f2); }
vec3 rgb2hsv(vec3 c){ vec4 K = vec4(0.0, -1.0/3.0, 2.0/3.0, -1.0); vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
  vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r)); float d = q.x - min(q.w, q.y); float e = 1.0e-10;
  return vec3(abs(q.z + (q.w - q.y)/(6.0*d + e)), d/(q.x + e), q.x); }
vec3 hsv2rgb(vec3 c){ vec3 p = abs(fract(c.xxx + vec3(1.0, 2.0/3.0, 1.0/3.0))*6.0 - 3.0); return c.z*mix(vec3(1.0), clamp(p-1.0, 0.0, 1.0), c.y); }
float luma(vec3 c){ return dot(c, vec3(0.299, 0.587, 0.114)); }
float smin(float a, float b, float k){ if(k<=0.0011) return min(a,b); float h = clamp(0.5+0.5*(b-a)/k, 0.0, 1.0); return mix(b, a, h) - k*h*(1.0-h); }
float smax(float a, float b, float k){ return -smin(-a, -b, k); }
mat2 rot2(float a){ float c = cos(a), s = sin(a); return mat2(c, s, -s, c); }
vec3 rotax(vec3 p, int ax, float a){ if(ax==0) p.yz = rot2(a)*p.yz; else if(ax==1) p.xz = rot2(a)*p.xz; else p.xy = rot2(a)*p.xy; return p; }
Mat mdef(){ return Mat(vec3(0.5), vec3(0.0), 0.2); }
vec3 tri(vec3 p, vec3 n){ vec3 w = abs(n); w = pow(w, vec3(4.0)); return w/(w.x+w.y+w.z+1e-5); }

vec3 sdir(vec2 uv){ float a = (uv.x-0.5)*6.2831853, e = (uv.y-0.5)*3.14159265; return vec3(cos(e)*cos(a), sin(e), cos(e)*sin(a)); }
float hs31(vec3 p){ p = fract(p*vec3(0.1031,0.1030,0.0973)); p += dot(p, p.yxz+33.33); return fract((p.x+p.y)*p.z); }
vec3 hs33(vec3 p){ p = fract(p*vec3(0.1031,0.1030,0.0973)); p += dot(p, p.yxz+33.33);
  return fract(vec3((p.x+p.y)*p.z, (p.x+p.z)*p.y, (p.y+p.z)*p.x)); }
float svn3(vec3 p){ vec3 i = floor(p); vec3 f = fract(p); f = f*f*(3.0-2.0*f);
  return mix(mix(mix(hs31(i), hs31(i+vec3(1,0,0)), f.x), mix(hs31(i+vec3(0,1,0)), hs31(i+vec3(1,1,0)), f.x), f.y),
             mix(mix(hs31(i+vec3(0,0,1)), hs31(i+vec3(1,0,1)), f.x), mix(hs31(i+vec3(0,1,1)), hs31(i+vec3(1,1,1)), f.x), f.y), f.z); }
float fb3(vec3 p, int n){ float v = 0.0, a = 0.5; for(int i=0;i<n;i++){ v += a*svn3(p); p = p*2.0+vec3(100.0,0.0,0.0); a *= 0.5; } return v; }
float sat1(float x){ return clamp(x, 0.0, 1.0); }
vec3 starcol(float t){ vec3 c = mix(vec3(0.65,0.75,1.0), vec3(1.0,0.95,0.88), sat1(t*2.5));
  c = mix(c, vec3(1.0,0.78,0.4), sat1((t-0.5)*3.5)); return mix(c, vec3(1.0,0.45,0.25), sat1((t-0.82)*5.5)); }
vec3 skystars(vec3 dir, vec3 gn, float scale, float bright, float base, float boost, float conc){
  vec3 res = vec3(0.0); vec3 p = dir*scale; vec3 id = floor(p); vec3 f = fract(p)-0.5;
  for(int x=-1;x<=1;x++) for(int y=-1;y<=1;y++) for(int z=-1;z<=1;z++){
    vec3 nb = vec3(x,y,z); vec3 cid = id+nb; float h = hs31(cid);
    float gd = abs(dot(normalize((cid+0.5)/scale), gn));
    if(h > base + boost*pow(1.0-gd, conc)) continue;
    vec3 sp = nb + hs33(cid) - 0.5 - f; float dist = length(sp);
    float ty = hs31(cid+73.7), sz = hs31(cid+191.3), tp = hs31(cid+127.1); vec3 o = normalize(hs33(cid+53.0)-0.5);
    float al = dot(sp, o); vec3 pv = sp - al*o; float pe = length(pv); float ob; vec3 col;
    if(ty < 0.15){ ob = exp(-dist*dist*(600.0+sz*800.0));
      vec3 sa = normalize(cross(o, vec3(0.17,1.0,0.31))); vec3 sb = normalize(cross(o, sa));
      float dA = length(cross(sp, sa)), dB = length(cross(sp, sb));
      ob += (exp(-dA*dA*3000.0)+exp(-dB*dB*3000.0))*exp(-dist*12.0)*0.35; col = starcol(tp); }
    else if(ty < 0.40){ float el = 3.0+sz*5.0, sr = 10.0+sz*8.0;
      ob = exp(-(al*al*sr/el + pe*pe*sr*el)) + exp(-dist*dist*sr*2.0)*0.5; col = starcol(tp*0.6+0.2); }
    else if(ty < 0.60){ float sr = 6.0+sz*6.0; vec3 pd = normalize(pv+0.001); vec3 s2 = normalize(cross(o, pd));
      float ang = atan(dot(sp, s2), dot(sp, pd));
      ob = exp(-dist*dist*sr)*(sin(ang*2.0+dist*sr*0.8)*0.3+0.7)*0.4 + exp(-dist*dist*sr*8.0)*0.7; col = starcol(tp*0.5+0.25); }
    else if(ty < 0.78){ vec3 ao = (hs33(cid+97.3)-0.5)*0.15; float sr = 8.0+sz*12.0; float ad = length(sp+ao);
      float kd = length(sp-ao*2.0); ob = exp(-ad*ad*sr) + exp(-kd*kd*sr*3.0)*0.4; col = starcol(tp*0.7+0.15); }
    else { float el = 1.5+sz*3.0, sr = 12.0+sz*16.0;
      ob = exp(-(al*al*sr/el + pe*pe*sr*el)) + exp(-dist*dist*sr*3.0)*0.3; col = starcol(tp*0.4+0.3); }
    float tw = ty < 0.15 ? sin(uTime*1.06 + h*80.0)*0.25+0.75 : 1.0;
    res += ob*col*(0.15+h*h*3.0)*tw*bright; }
  return res; }
vec3 skyneb(vec3 dir, float scale, float dr, vec3 c1, vec3 c2, vec3 c3){
  vec3 p = dir*scale;
  vec3 w = vec3(svn3(p*1.5+vec3(dr*0.2,0,0)), svn3(p*1.5+vec3(5.2,1.3+dr*0.15,0)), svn3(p*1.5+vec3(2.1,0,7.8)));
  vec3 wp = p + (w-0.5)*1.4;
  float rm = smoothstep(0.3, 0.7, svn3(dir*1.3+vec3(42.0,17.0,91.0)));
  float a = smoothstep(0.28, 0.88, fb3(wp*2.0+vec3(dr,0,0), 4)); a *= a;
  float b = smoothstep(0.30, 0.90, fb3(wp*2.5+vec3(5.2,1.3+dr*0.7,9.1), 4)); b *= b;
  float c = smoothstep(0.29, 0.89, fb3(wp*3.0+vec3(3.7,8.4,2.6+dr*1.3), 4)); c *= c;
  a *= 0.3 + svn3(wp*4.5+vec3(11.3,0,dr*0.3)); b *= 0.3 + svn3(wp*5.0+vec3(0,13.7,dr*0.25)); c *= 0.3 + svn3(wp*5.5+vec3(0,dr*0.2,15.1));
  return (a*c1 + b*c2 + c*c3)*rm; }
float skyweb(vec3 p, float sharp, float interior){
  vec3 i = floor(p); vec3 f = fract(p); float m1 = 10.0, m2 = 10.0;
  for(int x=-1;x<=1;x++) for(int y=-1;y<=1;y++) for(int z=-1;z<=1;z++){ vec3 nb = vec3(x,y,z);
    vec3 df = nb + hs33(i+nb) - f; float d = dot(df, df); if(d<m1){ m2 = m1; m1 = d; } else if(d<m2){ m2 = d; } }
  m1 = sqrt(m1); m2 = sqrt(m2);
  return 1.0 - smoothstep(0.0, 1.0/sharp, m2-m1) + smoothstep(0.0, 0.5, m1)*interior; }
"""


def _sph(theta, phi):
    """Unit vector at polar angle theta from +y, azimuth phi from +x toward +z."""
    return (math.sin(theta) * math.cos(phi), math.cos(theta), math.sin(theta) * math.sin(phi))


def compile_textures(g, live):
    """Return GLSL source for every live texture node."""
    src = []
    SKYG = ("vec3(0.0,1.0,0.0)", "0.05", "0.0")  # galactic normal, width, drift: the first live sky.base sets them
    for i, n in enumerate(g.nodes):
        if i in live and n.op == "sky.base":
            o = OPS[n.op]
            d = {p.name: p.decode(b) for p, b in zip(o.params, n.params)}
            SKYG = (v3(_sph(d["ntheta"], d["nphi"])), f(d["width"]), f(d["drift"]))
            break
    for i, n in enumerate(g.nodes):
        if i not in live or OPS[n.op].out != "T":
            continue
        o = OPS[n.op]
        d = {p.name: p.decode(b) for p, b in zip(o.params, n.params)}
        ins = n.inputs

        def T(k, default="vec3(0.5)"):
            j = ins[k] if k < len(ins) else None
            return (lambda uv: f"t{j}({uv})") if j is not None else (lambda uv: default)

        body = ""
        op = n.op
        if op == "tex.flat":
            body = f"return {v3(hsv(d))};"
        elif op == "tex.gradient":
            a, b = v3(hsv(d, "a")), v3(hsv(d, "b"))
            ca, sa = math.cos(d["angle"]), math.sin(d["angle"])
            L = d["length"]
            m = d["mode"]
            if m == 0:
                x = f"dot(q, vec2({f(ca)},{f(sa)}))/{f(L)} + 0.5 + {f(d['pos'] * 0.5)}"
            elif m == 1:
                x = f"length(q)*2.0/{f(L)} + {f(d['pos'] * 0.5)}"
            else:
                x = f"0.5+0.5*cos(6.2831853*length(q)*2.0/{f(L)} + {f(d['pos'] * 3.14159)})"
            body = f"vec2 q = fract(uv)-0.5; float x = clamp({x}, 0.0, 1.0); return mix({a},{b},x);"
        elif op == "tex.perlin":
            a, b = v3(hsv(d, "a")), v3(hsv(d, "b"))
            fr = max(1, round(d["freq"]))
            body = (f"float x = fbm2(fract(uv), {f(fr)}, {int(d['oct'])}, {f(d['persist'])}, {f(d['seed'] * 7.31)});"
                    f" x = pow(clamp((x-0.5)*1.6+0.5, 0.0, 1.0), {f(d['gamma'])}); return mix({a},{b},x);")
        elif op == "tex.cell":
            a, b = v3(hsv(d, "a")), v3(hsv(d, "b"))
            cnt = max(1, round(d["count"] ** 0.5 * 1.5))
            m = d["mode"]
            x = ["clamp(v.x, 0.0, 1.0)", "clamp((v.y-v.x)*2.0, 0.0, 1.0)", "smoothstep(0.0, 0.08, v.y-v.x)"][m]
            body = (f"vec2 v = vor2(fract(uv), {f(cnt)}, {f(d['seed'] * 3.7)}); float x = pow({x}, {f(d['gamma'])});"
                    f" return mix({a},{b},x);")
        elif op == "tex.blend":
            A, B = T(0), T(1)
            m, amt = d["mode"], f(d["amount"])
            expr = ["a + b*{amt}", "mix(a, a*b, {amt})", "a - b*{amt}", "mix(a, max(a,b), {amt})",
                    "mix(a, min(a,b), {amt})", "mix(a, 1.0-(1.0-a)*(1.0-b), {amt})", "mix(a, b, {amt})"][m]
            body = f"vec3 a = {A('uv')}; vec3 b = {B('uv')}; return {expr.format(amt=amt)};"
        elif op == "tex.distort":
            A, B = T(0), T(1)
            body = f"vec3 b = {B('uv')}; return {A('uv + (b.rg-0.5)*' + f(2 * d['amount']))};"
        elif op == "tex.rotozoom":
            A = T(0)
            c, s = math.cos(d["angle"]), math.sin(d["angle"])
            z = d["zoom"]
            body = (f"vec2 q = (uv-0.5); q = mat2({f(c)},{f(s)},{f(-s)},{f(c)})*q*{f(z)} + 0.5 + vec2({f(d['tx'])},{f(d['ty'])});"
                    f" return {A('q')};")
        elif op == "tex.mirror":
            A = T(0)
            ax = int(d["axes"])
            fx = "1.0-abs(2.0*fract(uv.x)-1.0)" if ax in (0, 2) else "uv.x"
            fy = "1.0-abs(2.0*fract(uv.y)-1.0)" if ax in (1, 2) else "uv.y"
            body = f"vec2 q = vec2({fx}, {fy}); return {A('q')};"
        elif op == "tex.hscb":
            A = T(0)
            body = (f"vec3 c = rgb2hsv(max({A('uv')}, vec3(0.0))); c.x = fract(c.x + {f(d['hue'])}); c.y = clamp(c.y*{f(d['sat'])}, 0.0, 1.0);"
                    f" vec3 r = hsv2rgb(c); r = (r-0.5)*{f(d['contrast'])}+0.5; return max(r*{f(d['bright'])}, vec3(0.0));")
        elif op == "tex.colorize":
            A = T(0)
            body = f"float l = clamp(luma({A('uv')}), 0.0, 1.0); return mix({v3(hsv(d, 'a'))}, {v3(hsv(d, 'b'))}, l);"
        elif op == "tex.twirl":
            A = T(0)
            body = (f"vec2 q = fract(uv)-0.5; float r = length(q); float a = {f(d['strength'])}*pow(max(0.0, 1.0-r/{f(d['radius'])}), 2.0);"
                    f" q = rot2(a)*q; return {A('q+0.5')};")
        elif op == "tex.glowrect":
            A = T(0)
            body = (f"vec2 q = (fract(uv)-vec2({f(d['cx'])},{f(d['cy'])}))/vec2({f(d['rx'])},{f(d['ry'])});"
                    f" float gl = pow(max(0.0, 1.0-length(q)), {f(d['power'])}); return {A('uv')} + {v3(hsv(d))}*gl;")
        elif op == "tex.bricks":
            a, b = v3(hsv(d, "a")), v3(hsv(d, "b"))
            tx, ty = int(d["tx"]), int(d["ty"])
            body = (f"vec2 q = fract(uv)*vec2({tx}.0,{ty}.0); float row = floor(q.y); q.x += mod(row, 2.0)*0.5;"
                    f" vec2 id = floor(q); vec2 fr = fract(q); float fu = {f(d['fuge'])};"
                    f" float m = step(fu, fr.x)*step(fu, fr.y); return mix({a}*0.3, mix({a},{b},h21(mod(id, vec2({tx}.0,{ty}.0))+{f(d['seed'])})), m);")
        elif op.startswith("sky."):
            A = T(0, "vec3(0.0)")
            pre = f"vec3 dir = sdir(uv); vec3 gn = {SKYG[0]}; float gw = {SKYG[1]}; float dr = uTime*{SKYG[2]}; vec3 c = {A('uv')};"
            if op == "sky.base":
                body = f"vec3 dir = sdir(uv); return {v3(hsv(d, 'd'))} + {v3(hsv(d, 'a'))}*(0.5+0.5*svn3(dir*2.5+uTime*{f(d['drift'] * 1.5)}));"
            elif op == "sky.band":
                body = pre + (f" float ds = abs(dot(dir, gn)); float w = gw*(1.0+(svn3(dir*{f(d['nscale'])}+dr*0.5)-0.5)*{f(d['nstr'])});"
                              f" float b = exp(-ds*ds/(2.0*w*w))*(0.6+0.4*fb3(dir*10.0+dr*0.2, 3));"
                              f" return c + b*mix({v3(hsv(d, 'c'))}, {v3(hsv(d, 'e'))}, exp(-ds*ds/(2.0*w*w*0.09)));")
            elif op == "sky.stars":
                body = pre + (f" return c + skystars(dir, gn, {f(d['density'])}, {f(d['bright'])}, {f(d['base'])},"
                              f" {f(d['boost'])}, {f(d['conc'])});")
            elif op == "sky.nebula":
                body = pre + (f" return c + skyneb(dir, {f(d['scale'])}, dr, {v3(hsv(d, 'a'))}, {v3(hsv(d, 'b'))},"
                              f" {v3(hsv(d, 'c'))});")
            elif op == "sky.dust":
                body = pre + (f" float du = smoothstep(0.3, 0.7, fb3(dir*{f(d['scale'] * 1.8)}+vec3(0.0,dr*0.3,0.0), 3));"
                              f" float gm = 1.0-smoothstep(0.0, gw*2.5, abs(dot(dir, gn)));"
                              f" return c*max(1.0-du*{f(d['strength'])}*gm, 0.25);")
            elif op == "sky.core":
                cd = _sph(d["theta"], d["phi"])
                body = pre + (f" vec3 cd = {v3(cd)}; float gd = abs(dot(dir, gn)); float on = exp(-gd*gd/(2.0*gw*gw));"
                              f" float cdt = dot(dir, cd); float inn = smoothstep({f(1 - d['size'])}, 1.0, cdt); inn *= inn*on;"
                              f" float ha = smoothstep({f(1 - d['halo'])}, 1.0, cdt)*on*(0.6+0.4*svn3(dir*8.0+cd*3.0));"
                              f" return c + inn*{v3(hsv(d, 'c'))} + ha*{v3(hsv(d, 'h'))};")
            elif op == "sky.galaxy":
                ad = _sph(d["theta"], d["phi"])
                sz = d["size"]
                body = pre + (f" vec3 ad = {v3(ad)}; vec3 up = normalize(cross(ad, vec3(0.13,1.0,0.24))); vec3 rt = normalize(cross(up, ad));"
                              f" float fc = sat1((dot(dir, ad)-0.7)*5.0); if(fc < 0.001) return c;"
                              f" float eu = dot(dir, rt), ev = dot(dir, up)*3.2; float r2 = eu*eu+ev*ev; float s2 = {f(sz * sz)};"
                              f" float r = sqrt(r2); float ph = atan(ev, eu)*2.0 + r*{f(15.0 / sz)};"
                              f" float dk = exp(-r2/(s2*0.25))*(0.4+0.6*svn3(vec3(sin(ph), cos(ph), r*{f(10.0 / sz)}+3.7)));"
                              f" dk *= 1.0-0.35*exp(-ev*ev/(s2*0.003));"
                              f" return c + (dk*{v3(hsv(d, 'c'))} + exp(-r2/(s2*0.008))*{v3(hsv(d, 'n'))})*fc;")
            elif op == "sky.web":
                body = pre + (f" return c + skyweb(dir*{f(d['scale'])}+vec3(dr*0.3,0.0,dr*0.2), {f(d['sharp'])},"
                              f" {f(d['interior'])})*{v3(hsv(d))};")
            elif op == "sky.haze":
                body = pre + (f" float at = exp(-abs(dir.y-{f(d['height'])})*{f(d['falloff'])})*(0.7+0.3*svn3(dir*3.0+vec3(17.3,0.0,41.7)));"
                              f" return c + {v3(hsv(d))}*at;")
        else:
            body = "return vec3(0.5);"
        src.append(f"vec3 t{i}(vec2 uv){{ {body} }}")
    return "\n".join(src)


def _axis(d):
    return int(d.get("axis", 1))


def compile_shapes(g, live):
    """Return GLSL for every live shape node: sd<i>(p) and mt<i>(p,n)."""
    src = []
    for i, n in enumerate(g.nodes):
        if i not in live or OPS[n.op].out != "S":
            continue
        o = OPS[n.op]
        d = {p.name: p.decode(b) for p, b in zip(o.params, n.params)}
        ins = n.inputs
        a = ins[0] if len(ins) > 0 else None
        b = ins[1] if len(ins) > 1 else None
        SA = (lambda q: f"sd{a}({q})") if a is not None else (lambda q: "1e9")
        SB = (lambda q: f"sd{b}({q})") if b is not None else (lambda q: "1e9")
        MA = (lambda q, nn="n": f"mt{a}({q},{nn})") if a is not None else (lambda q, nn="n": "mdef()")
        MB = (lambda q, nn="n": f"mt{b}({q},{nn})") if b is not None else (lambda q, nn="n": "mdef()")
        op = n.op
        sd = mt = None
        if op == "sdf.sphere":
            sd = f"return length(p)-{f(d['r'])};"
        elif op == "sdf.box":
            s = (d["sx"], d["sy"], d["sz"])
            r = d["round"] * min(s)
            sd = f"vec3 q = abs(p)-{v3([x - r for x in s])}; return length(max(q,0.0))+min(max(q.x,max(q.y,q.z)),0.0)-{f(r)};"
        elif op == "sdf.torus":
            R = d["R"]
            sd = f"vec2 q = vec2(length(p.xz)-{f(R)}, p.y); return length(q)-{f(R * d['r'])};"
        elif op == "sdf.cylinder":
            sd = f"vec2 dd = abs(vec2(length(p.xz), p.y)) - vec2({f(d['r'])},{f(d['h'])}); return min(max(dd.x,dd.y),0.0)+length(max(dd,0.0));"
        elif op == "sdf.octa":
            sd = f"vec3 q = abs(p); return (q.x+q.y+q.z-{f(d['r'])})*0.57735027;"
        elif op == "sdf.gyroid":
            s = d["scale"]
            sd = (f"float gy = dot(sin(p*{f(s)}), cos(p.yzx*{f(s)})); float dd = (abs(gy)-{f(d['thick'])})/({f(s)}*1.8);"
                  f" return max(dd, length(p)-{f(d['bound'])});")
        elif op == "sdf.plane":
            th, ph = d["theta"], d["phi"]
            nn = (math.sin(th) * math.cos(ph), math.cos(th), math.sin(th) * math.sin(ph))
            sd = f"return dot(p, {v3(nn)}) + {f(d['offset'])};"
        elif op == "sdf.shell":
            sd = f"return abs({SA('p')})-{f(d['thick'])};"
            mt = f"return {MA('p')};"
        elif op == "sdf.invert":
            sd = f"return -{SA('p')};"
            mt = f"return {MA('p')};"
        elif op in ("sdf.union", "sdf.subtract", "sdf.intersect"):
            k = d["k"]
            if op == "sdf.union":
                sd = f"return smin({SA('p')}, {SB('p')}, {f(k)});"
                mt = f"return ({SA('p')} < {SB('p')}) ? {MA('p')} : {MB('p')};"
            elif op == "sdf.subtract":
                sd = f"return smax({SA('p')}, -{SB('p')}, {f(k)});"
                mt = f"return {MA('p')};"
            else:
                sd = f"return smax({SA('p')}, {SB('p')}, {f(k)});"
                mt = f"return ({SA('p')} > {SB('p')}) ? {MA('p')} : {MB('p')};"
        elif op == "sdf.translate":
            t = v3((d["x"], d["y"], d["z"]))
            sd = f"return {SA('p-' + t)};"
            mt = f"return {MA('p-' + t)};"
        elif op == "sdf.rotate":
            cx, sx, cy, sy, cz, sz = (math.cos(d["ax"]), math.sin(d["ax"]), math.cos(d["ay"]), math.sin(d["ay"]),
                                      math.cos(d["az"]), math.sin(d["az"]))
            # R = Rz*Ry*Rx ; inverse applied to p
            R = [[cz * cy, cz * sy * sx - sz * cx, cz * sy * cx + sz * sx],
                 [sz * cy, sz * sy * sx + cz * cx, sz * sy * cx - cz * sx],
                 [-sy, cy * sx, cy * cx]]
            # GLSL mat3 is column-major: mat3(c0, c1, c2); we want q = R^T p, so columns of R^T are rows of R
            m = "mat3(" + ",".join(f(R[r][c]) for c in range(3) for r in range(3)) + ")"
            # m (column-major of R) * p = R p ; transpose(m) * p = R^T p
            sd = f"return {SA('transpose(' + m + ')*p')};"
            mt = f"return {MA('transpose(' + m + ')*p', 'transpose(' + m + ')*n')};"
        elif op == "sdf.scale":
            s = d["s"]
            sd = f"return {SA('p/' + f(s))}*{f(s)};"
            mt = f"return {MA('p/' + f(s))};"
        elif op == "sdf.repeat":
            ax = int(d["axes"])
            per = (d["px"], d["py"], d["pz"])
            lines = "vec3 q = p;"
            for k, c in enumerate("xyz"):
                if ax & (1 << k):
                    lines += f" q.{c} = q.{c} - {f(per[k])}*floor(q.{c}/{f(per[k])}+0.5);"
            sd = f"{lines} return {SA('q')};"
            mt = f"{lines} return {MA('q')};"
        elif op == "sdf.polar":
            cnt = max(1, int(d["count"]))
            axis = _axis(d)
            pl = ["yz", "xz", "xy"][axis]
            lines = (f"vec3 q = p; float sec = 6.2831853/{cnt}.0; float an = atan(q.{pl[1]}, q.{pl[0]});"
                     f" an = mod(an + sec*0.5, sec) - sec*0.5; float rr = length(q.{pl}); q.{pl} = rr*vec2(cos(an), sin(an));")
            sd = f"{lines} return {SA('q')};"
            mt = f"{lines} return {MA('q')};"
        elif op == "sdf.twist":
            axis = _axis(d)
            c = "xyz"[axis]
            lines = f"vec3 q = rotax(p, {axis}, {f(d['amount'])}*p.{c});"
            sd = f"{lines} return {SA('q')}*0.6;"
            mt = f"{lines} return {MA('q')};"
        elif op == "sdf.displace":
            sd = f"return {SA('p')} + {f(d['amp'])}*(vn3(p*{f(d['freq'])}+{f(d['seed'] * 1.37)})-0.5)*2.0;"
            mt = f"return {MA('p')};"
        elif op == "sdf.mirror":
            ax = int(d["axes"])
            lines = "vec3 q = p;" + "".join(f" q.{c} = abs(q.{c});" for k, c in enumerate("xyz") if ax & (1 << k))
            sd = f"{lines} return {SA('q')};"
            mt = f"{lines} return {MA('q')};"
        elif op == "sdf.spin":
            axis = _axis(d)
            lines = f"vec3 q = rotax(p, {axis}, -{f(d['speed'])}*uTime); vec3 nq = rotax(n, {axis}, -{f(d['speed'])}*uTime);"
            sd = f"vec3 q = rotax(p, {axis}, -{f(d['speed'])}*uTime); return {SA('q')};"
            mt = f"{lines} return {MA('q', 'nq')};"
        elif op == "sdf.bob":
            axis = _axis(d)
            off = ["vec3(1,0,0)", "vec3(0,1,0)", "vec3(0,0,1)"][axis]
            lines = f"vec3 q = p - {off}*{f(d['amp'])}*sin(6.2831853*{f(d['freq'])}*uTime);"
            sd = f"{lines} return {SA('q')};"
            mt = f"{lines} return {MA('q')};"
        elif op == "sdf.rim":
            face, rim = hsv(d, "f"), hsv(d, "r")
            em = d["emit"]
            sd = f"return {SA('p')};"
            mt = (f"float fr = pow(1.0-clamp(dot(gN, gV), 0.0, 1.0), {f(d['power'])});"
                  f" return Mat(vec3(0.0), mix({v3(face)}, {v3(rim)}, fr)*{f(em)}, 0.0);")
        elif op == "sdf.material":
            alb = hsv(d, "a")
            emi = hsv(d, "e")
            em = d["emit"]
            glow = d["glow"] if n.params[[p.name for p in o.params].index("glow")] > 0 else 0.0
            gc = v3([c * glow for c in (emi if sum(emi) > 0.01 else alb)])
            gl = f" gGlow += gGlowOn*{gc}*exp(-{f(d['falloff'])}*max(dd,0.0));" if glow > 0 else ""
            sd = f"float dd = {SA('p')};{gl} return dd;"
            tex = ins[1] if len(ins) > 1 else None
            if tex is not None:
                ts = f(d["texscale"])
                tx = (f"vec3 w = tri(p, n); vec3 tc = t{tex}(p.yz*{ts})*w.x + t{tex}(p.xz*{ts})*w.y + t{tex}(p.xy*{ts})*w.z;"
                      f" vec3 tm = mix(vec3(1.0), tc, {f(d['texmix'])});")
            else:
                tx = "vec3 tm = vec3(1.0);"
            mt = f"{tx} return Mat({v3(alb)}*tm, {v3([c * em for c in emi])}*tm, {f(d['spec'])});"
        if sd is None:
            sd = "return 1e9;"
        if mt is None:
            mt = "return mdef();"
        src.append(f"float sd{i}(vec3 p){{ {sd} }}")
        src.append(f"Mat mt{i}(vec3 p, vec3 n){{ {mt} }}")
    return "\n".join(src)


def scene_shader(g, live, scene_idx, sky_idx):
    tex = compile_textures(g, live)
    shp = compile_shapes(g, live)
    SD = f"sd{scene_idx}(p)" if scene_idx is not None else "1e9"
    MT = f"mt{scene_idx}(p, n)" if scene_idx is not None else "mdef()"
    if sky_idx is not None:
        sky = (f"vec3 SKY(vec3 rd){{ vec2 uv = vec2(atan(rd.z, rd.x)/6.2831853+0.5, asin(clamp(rd.y,-1.0,1.0))/3.14159265+0.5);"
               f" return t{sky_idx}(uv); }}")
    else:
        sky = "vec3 SKY(vec3 rd){ return vec3(0.0); }"
    main = f"""
float SD(vec3 p){{ return {SD}; }}
Mat MT(vec3 p, vec3 n){{ return {MT}; }}
{sky}
vec3 NRM(vec3 p, float e){{ vec2 k = vec2(1.0,-1.0);
  return normalize(k.xyy*SD(p+k.xyy*e) + k.yyx*SD(p+k.yyx*e) + k.yxy*SD(p+k.yxy*e) + k.xxx*SD(p+k.xxx*e)); }}
void main(){{
  vec2 uv = (gl_FragCoord.xy/uRes)*2.0-1.0; uv.x *= uRes.x/uRes.y;
  vec3 rd = normalize(uCam*vec3(uv*uTanHalf, 1.0));
  float t = uNear; float hit = 0.0; gGlowOn = 1.0; gGlow = vec3(0.0);
  for(int i=0;i<256;i++){{ if(i>=uSteps) break; float h = SD(uEye+rd*t); if(abs(h) < 0.0012*t){{ hit = 1.0; break; }}
    t += max(h*0.9, 0.0008*t); if(t>uFar) break; }}
  gGlowOn = 0.0;
  vec3 col; float depth = 1.0;
  if(hit>0.5){{ vec3 p = uEye+rd*t; vec3 n = NRM(p, 0.0007*t+0.0005); gN = n; gV = -rd; Mat m = MT(p, n);
    float dif = max(dot(n, uLightDir), 0.0); float fr = pow(1.0-max(dot(n,-rd),0.0), 3.0);
    vec3 sp = uLightCol*pow(max(dot(reflect(-uLightDir, n), -rd), 0.0), 24.0)*m.spec;
    col = m.alb*(uLightCol*dif + uAmbient*(0.6+0.4*n.y)) + sp + m.emi + fr*0.15*uAmbient*m.alb;
    col = mix(col, uFogCol, 1.0-exp(-uFog*t));
    float z = t*dot(rd, uCam[2]);
    depth = clamp((((uFar+uNear)/(uFar-uNear)) - (2.0*uFar*uNear)/((uFar-uNear)*z))*0.5+0.5, 0.0, 1.0);
  }} else {{ col = mix(SKY(rd)*uSky, uFogCol, 1.0-exp(-uFog*uFar*0.5)); }}
  col += gGlow*(64.0/float(uSteps));
  oCol = vec4(col, 1.0); gl_FragDepth = depth;
}}
"""
    return PRELUDE + "\n" + tex + "\n" + shp + "\n" + main
