"""Headless phenotype renderer: genome -> clip uint8[T,144,256,3] at 15 fps of game time (CONTRACT §1).

Pipeline per frame (all OpenGL 3.3 on llvmpipe, offscreen):
  1. scene pass   - fullscreen raymarch of the compiled SDF scene + sky + volumetric glow, writes depth
  2. instance pass- instanced boxes/octas/darts/icosas (prism trails, vessel bodies, scattered crystals) with
                    neon edges, depth-tested against the raymarched scene
  3. bloom        - bright pass at 1/4 res, separable blur
  4. composite    - exposure, bloom, tonemap, saturation, contrast, vignette, grain, aberration, feedback
  5. hud          - 2D overlay primitives
"""
import atexit
import math
import os
import select
import shutil
import subprocess
import threading
import time

import numpy as np

from .ops import OPS
from . import glsl
from .sim import build_populations, World, Camera, MESHES, _d

W, H = 256, 144
FPS = 15
SUBSTEPS = 4  # 60 Hz game step, like Amoebius --frames
MAX_SHAPE_COST = 160  # inlined SDF nodes evaluated per raymarch step
MAX_TEX_COST = 400

_XVFB = None


def ensure_display():
    """Start a private Xvfb for this process (moderngl's standalone context needs a display).

    The server picks a FREE display itself (-displayfd): the old pid % 500 number collided, and a process whose server
    could not start borrowed another's. It also dies with the process that started it: pool workers killed on a
    timeout or a restart left theirs running (146 orphans holding 1.4 GB in the genome thread's container,
    2026-11-10). setpriv sets the parent-death signal with no Python in the child (a preexec_fn forces fork(), which
    can deadlock under JAX's threads); the signal is tied to the starting THREAD, so only the main thread's server
    gets it."""
    global _XVFB
    if os.environ.get("DISPLAY"):
        return
    if shutil.which("Xvfb") is None:
        return
    pre = ["setpriv", "--pdeathsig", "TERM", "--"] if (
        shutil.which("setpriv") and threading.current_thread() is threading.main_thread()) else []
    rd, wr = os.pipe()
    try:
        _XVFB = subprocess.Popen(pre + ["Xvfb", "-displayfd", str(wr), "-screen", "0", "64x64x24", "-nolisten", "tcp"],
                                 stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                 pass_fds=(wr,))
    finally:
        os.close(wr)
    buf, deadline = b"", time.time() + 15
    while not buf.endswith(b"\n") and time.time() < deadline:
        ready, _, _ = select.select([rd], [], [], max(0.0, deadline - time.time()))
        chunk = os.read(rd, 32) if ready else b""
        if not chunk:
            break
        buf += chunk
    os.close(rd)
    if buf.strip().isdigit():
        atexit.register(_XVFB.terminate)
        os.environ["DISPLAY"] = f":{int(buf.strip())}"
        return
    _XVFB.terminate()  # no -displayfd (old server): the old fixed number
    num = 90 + (os.getpid() % 500)
    _XVFB = subprocess.Popen(pre + ["Xvfb", f":{num}", "-screen", "0", "64x64x24", "-nolisten", "tcp"],
                             stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    atexit.register(_XVFB.terminate)
    os.environ["DISPLAY"] = f":{num}"
    time.sleep(0.4)


def _mesh(name):
    if name == "box":
        v = []
        faces = [((1, 0, 0), (0, 1, 0), (0, 0, 1)), ((-1, 0, 0), (0, 0, 1), (0, 1, 0)), ((0, 1, 0), (0, 0, 1), (1, 0, 0)),
                 ((0, -1, 0), (1, 0, 0), (0, 0, 1)), ((0, 0, 1), (1, 0, 0), (0, 1, 0)), ((0, 0, -1), (0, 1, 0), (1, 0, 0))]
        for nrm, a, b in faces:
            n_, a_, b_ = np.array(nrm, float), np.array(a, float), np.array(b, float)
            c = n_ * 0.5
            q = [c - a_ * .5 - b_ * .5, c + a_ * .5 - b_ * .5, c + a_ * .5 + b_ * .5, c - a_ * .5 + b_ * .5]
            for tri in ((0, 1, 2), (0, 2, 3)):
                for k in tri:
                    v.append(list(q[k]) + list(n_))
        return np.array(v, np.float32)
    if name == "octa":
        P = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]
        tris = [(0, 2, 4), (2, 1, 4), (1, 3, 4), (3, 0, 4), (2, 0, 5), (1, 2, 5), (3, 1, 5), (0, 3, 5)]
        return _flat(np.array(P, float) * 0.5, tris)
    if name == "dart":
        P = [(0, 0, 0.6), (-0.5, 0, -0.4), (0.5, 0, -0.4), (0, 0.25, -0.3), (0, -0.12, -0.35)]
        tris = [(0, 3, 1), (0, 2, 3), (0, 1, 4), (0, 4, 2), (1, 3, 2), (1, 2, 4)]
        return _flat(np.array(P, float), tris)
    if name == "icosa":
        t = (1 + 5 ** 0.5) / 2
        P = np.array([(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
                      (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)], float)
        P = P / np.linalg.norm(P, axis=1, keepdims=True) * 0.5
        tris = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
                (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
                (8, 6, 7), (9, 8, 1)]
        return _flat(P, tris)
    if name in ("ring", "hoop"):  # hoop: the thin wire ring of gates and crystal rings
        segs, sides, R, r = (16, 4, 0.42, 0.08) if name == "ring" else (40, 4, 0.42, 0.012)
        P, tris = [], []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            for j in range(sides):
                b = 2 * math.pi * j / sides
                P.append(((R + r * math.cos(b)) * math.cos(a), r * math.sin(b), (R + r * math.cos(b)) * math.sin(a)))
        v = []
        P = np.array(P, float)
        for i in range(segs):
            for j in range(sides):
                a0, a1 = i * sides + j, ((i + 1) % segs) * sides + j
                b0, b1 = i * sides + (j + 1) % sides, ((i + 1) % segs) * sides + (j + 1) % sides
                for tri in ((a0, a1, b1), (a0, b1, b0)):
                    p0, p1, p2 = P[list(tri)]
                    n = np.cross(p1 - p0, p2 - p0)
                    c = (p0 + p1 + p2) / 3
                    ring_c = np.array([c[0], 0, c[2]]) / (np.hypot(c[0], c[2]) + 1e-9) * R
                    if np.dot(n, c - ring_c) < 0:
                        n = -n
                        tri = (tri[0], tri[2], tri[1])
                    n = n / (np.linalg.norm(n) + 1e-9)
                    for k in tri:
                        v.append(list(P[k]) + list(n))
        return np.array(v, np.float32)
    if name == "stella":  # stellated octahedron: the mesh the game draws super-shielded prisms with
        A = np.array([(1, 1, 1), (1, -1, -1), (-1, 1, -1), (-1, -1, 1)], float) * 0.5
        tris = [(0, 1, 2), (0, 3, 1), (0, 2, 3), (1, 3, 2)]
        return np.concatenate([_flat(A, tris), _flat(-A, tris)], 0)
    if name == "pyramid":
        P = np.array([(-0.5, -0.5, -0.5), (0.5, -0.5, -0.5), (0.5, -0.5, 0.5), (-0.5, -0.5, 0.5), (0, 0.5, 0)], float)
        tris = [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4), (0, 2, 1), (0, 3, 2)]
        return _flat(P, tris)
    raise KeyError(name)


def _flat(P, tris):
    v = []
    for a, b, c in tris:
        n = np.cross(P[b] - P[a], P[c] - P[a])
        if np.dot(n, P[a] + P[b] + P[c]) < 0:
            b, c = c, b
            n = -n
        n = n / (np.linalg.norm(n) + 1e-9)
        for k in (a, b, c):
            v.append(list(P[k]) + list(n))
    return np.array(v, np.float32)


VS_QUAD = """#version 330
in vec2 p; void main(){ gl_Position = vec4(p, 0.0, 1.0); }"""

VS_INST = """#version 330
in vec3 vp; in vec3 vn;
in vec3 ip; in vec3 ix; in vec3 iy; in vec3 iz; in vec4 ic; in float ie; in vec3 id;
uniform mat4 uPV; uniform vec3 uEye;
out vec3 fN; out vec3 fL; out vec4 fC; out float fE; out vec3 fW; out vec3 fD; out float fNear;
void main(){
  vec3 w = ip + ix*vp.x + iy*vp.y + iz*vp.z;
  fD = id; fNear = 0.0;
  if(ie < -200.0){ // the game's prism graph (Distance Spread And Colors): faces pushed out with camera distance
    vec3 dc = ip - uEye; float nd = dot(dc, dc)/100000.0; fNear = nd;
    vec3 far = 0.1*vec3(50.0, 35.0, 20.0);
    vec3 k = nd > 1.0 ? far : mix(vec3(-7.0), far, nd);
    vec3 sv = max(k, vec3(0.1));
    w += normalize(ix)*sv.x*vn.x + normalize(iy)*sv.y*vn.y + normalize(iz)*sv.z*vn.z; }
  vec3 n = normalize(ix*vn.x/max(dot(ix,ix),1e-8) + iy*vn.y/max(dot(iy,iy),1e-8) + iz*vn.z/max(dot(iz,iz),1e-8));
  fN = n; fL = vp; fC = ic; fE = ie; fW = w;
  gl_Position = uPV*vec4(w, 1.0);
}"""

FS_INST = """#version 330
in vec3 fN; in vec3 fL; in vec4 fC; in float fE; in vec3 fW; in vec3 fD; in float fNear;
uniform vec3 uLightDir, uLightCol, uFogCol, uEye; uniform float uFog, uAmbient;
out vec4 oCol;
void main(){
  vec3 n = normalize(fN);
  float dif = max(dot(n, uLightDir), 0.0);
  vec3 a = abs(fL)*2.0; // 0 at centre, 1 at faces (box)
  float m1 = max(a.x, max(a.y, a.z));
  float m2 = a.x + a.y + a.z - m1 - min(a.x, min(a.y, a.z)); // second largest
  float edge = smoothstep(0.78, 0.98, m2);
  vec3 col;
  if(fE < -200.0){ // the game's prism graph: face -> rim by fresnel, the rim sinking to the face with distance
    float x = abs(dot(n, normalize(uEye - fW))); // closed meshes behind a depth test: the visible face looks at the eye
    vec3 bright = mix(fC.rgb, fD, fNear > 1.0 ? 0.9 : fNear*0.9);
    oCol = vec4(mix(fD, bright, pow(1.0 - x, -fE - 200.0)), 1.0); return;
  } else if(fE < -100.0){ // game prism: a dim face in its own hue, a bright fresnel rim (PrismMaterial)
    float fr = pow(1.0 - abs(dot(n, normalize(uEye - fW))), -fE - 100.0);
    col = mix(fC.rgb*0.22, fC.rgb*(1.0 + fC.a), clamp(fr + edge*0.35, 0.0, 1.0));
  } else if(fE < 0.0){ // ag.rim: unlit fresnel, a bright rim in the instance colour over a dark complementary face
    float fr = pow(1.0 - abs(dot(n, normalize(uEye - fW))), -fE);
    float mx = max(max(fC.r, fC.g), max(fC.b, 1e-4));
    col = mix((vec3(1.0) - fC.rgb/mx)*0.06, fC.rgb*(1.0 + fC.a), fr);
  } else {
    col = fC.rgb*(uLightCol*dif*0.6 + uAmbient) + fC.rgb*fC.a + fC.rgb*edge*fE*2.0;
  }
  float dist = length(fW - uEye);
  col = mix(col, uFogCol, 1.0-exp(-uFog*dist));
  oCol = vec4(col, 1.0);
}"""

FS_BRIGHT = """#version 330
uniform sampler2D uSrc; uniform float uThresh; uniform vec2 uRes; out vec4 oCol;
void main(){ vec2 uv = gl_FragCoord.xy/uRes; vec3 c = vec3(0.0);
  for(int y=0;y<2;y++) for(int x=0;x<2;x++) c += texture(uSrc, uv + (vec2(x,y)-0.5)/(uRes*2.0)).rgb;
  c *= 0.25; float l = max(c.r, max(c.g, c.b)); oCol = vec4(c*max(l-uThresh, 0.0)/max(l, 1e-4), 1.0); }"""

FS_BLUR = """#version 330
uniform sampler2D uSrc; uniform vec2 uDir; uniform vec2 uRes; out vec4 oCol;
void main(){ vec2 uv = gl_FragCoord.xy/uRes; vec3 c = vec3(0.0); float ws = 0.0;
  for(int i=-6;i<=6;i++){ float w = exp(-float(i*i)/18.0); c += texture(uSrc, uv + uDir*float(i)/uRes).rgb*w; ws += w; }
  oCol = vec4(c/ws, 1.0); }"""

FS_UPRE = """#version 330
uniform sampler2D uSrc; uniform vec2 uRes; uniform float uThreshold, uKnee; out vec4 oCol;
void main(){ vec2 uv = gl_FragCoord.xy/uRes; vec3 c = min(texture(uSrc, uv).rgb, vec3(65472.0));
  float br = max(c.r, max(c.g, c.b)); float soft = clamp(br-uThreshold+uKnee, 0.0, 2.0*uKnee);
  soft = soft*soft/(4.0*uKnee+1e-4); c *= max(br-uThreshold, soft)/max(br, 1e-4); oCol = vec4(max(c, 0.0), 1.0); }"""

FS_UBLUR = """#version 330
uniform sampler2D uSrc; uniform vec2 uRes; uniform vec2 uStep; out vec4 oCol;
void main(){ vec2 uv = gl_FragCoord.xy/uRes; vec3 c = texture(uSrc, uv).rgb*0.22702703;
  c += (texture(uSrc, uv-uStep).rgb + texture(uSrc, uv+uStep).rgb)*0.19459459;
  c += (texture(uSrc, uv-2.0*uStep).rgb + texture(uSrc, uv+2.0*uStep).rgb)*0.12162162;
  c += (texture(uSrc, uv-3.0*uStep).rgb + texture(uSrc, uv+3.0*uStep).rgb)*0.05405405;
  c += (texture(uSrc, uv-4.0*uStep).rgb + texture(uSrc, uv+4.0*uStep).rgb)*0.01621622;
  oCol = vec4(c, 1.0); }"""

FS_UUP = """#version 330
uniform sampler2D uHigh, uLow; uniform vec2 uRes; uniform float uScatter; out vec4 oCol;
void main(){ vec2 uv = gl_FragCoord.xy/uRes; oCol = vec4(mix(texture(uHigh, uv).rgb, texture(uLow, uv).rgb, uScatter), 1.0); }"""

FS_COMP = """#version 330
uniform int uMode;
uniform sampler2D uScene, uBloom, uPrev; uniform vec2 uRes; uniform float uExposure, uBloomK, uSat, uContrast,
  uVignette, uFeedback, uGrain, uAberr, uTime;
uniform vec4 uPanini; // distance, crop scale, view extents x, y (URP PaniniProjection, Panini_Generic); x = 0: off
out vec4 oCol;
float h21(vec2 p){ vec3 p3 = fract(vec3(p.xyx)*0.1031); p3 += dot(p3, p3.yzx+33.33); return fract((p3.x+p3.y)*p3.z); }
vec2 panini(vec2 uv){ if(uPanini.x <= 0.0) return uv;
  float d = uPanini.x; vec2 vp = (2.0*uv-1.0)*uPanini.zw*uPanini.y; float vd = 1.0+d;
  float hyp = vp.x*vp.x + vd*vd; float iD = vp.x*d; float disc = max(hyp - iD*iD, 0.0);
  float cdm = (-iD*vp.x + vd*sqrt(disc))/hyp; float cd = cdm + d;
  vec2 pp = vp*(cd/vd)/(cd - d); return pp/uPanini.zw*0.5+0.5; }
void main(){ vec2 so = gl_FragCoord.xy/uRes; vec2 uv = panini(so); vec2 cc = uv-0.5;
  vec3 c;
  c.r = texture(uScene, uv + cc*uAberr).r; c.g = texture(uScene, uv).g; c.b = texture(uScene, uv - cc*uAberr).b;
  c = c*uExposure + texture(uBloom, uv).rgb*uBloomK;
  if(uMode == 1){ // post.urp: no tonemapper, the sRGB transfer, nothing else
    c = clamp(c, 0.0, 1.0); c = mix(c*12.92, 1.055*pow(c, vec3(1.0/2.4))-0.055, step(0.0031308, c));
    oCol = vec4(c, 1.0); return; }
  c = c*(2.51*c+0.03)/(c*(2.43*c+0.59)+0.14); // ACES fit
  float l = dot(c, vec3(0.299,0.587,0.114)); c = mix(vec3(l), c, uSat);
  c = clamp((c-0.5)*uContrast+0.5, 0.0, 1.0);
  vec2 vc = so-0.5; c *= 1.0 - uVignette*dot(vc, vc)*1.6;
  c += (h21(gl_FragCoord.xy + fract(uTime*7.13)*97.0)-0.5)*uGrain;
  c = pow(clamp(c, 0.0, 1.0), vec3(1.0/2.2));
  c = mix(c, texture(uPrev, so).rgb, uFeedback);
  oCol = vec4(c, 1.0); }"""

FS_HUD = """#version 330
#define NH 24
uniform vec2 uRes; uniform int uN; uniform vec4 uA[NH]; uniform vec4 uB[NH]; uniform vec4 uC[NH]; uniform vec4 uD[NH];
out vec4 oCol;
float h21(vec2 p){ vec3 p3 = fract(vec3(p.xyx)*0.1031); p3 += dot(p3, p3.yzx+33.33); return fract((p3.x+p3.y)*p3.z); }
void main(){ vec2 q = gl_FragCoord.xy/uRes; q.y = 1.0-q.y; vec2 asp = vec2(uRes.x/uRes.y, 1.0);
  vec4 acc = vec4(0.0);
  for(int i=0;i<NH;i++){ if(i>=uN) break;
    vec4 A = uA[i]; vec4 B = uB[i]; vec4 C = uC[i]; vec4 D = uD[i];
    // A: kind, cx, cy, alpha  B: w, h, p1, p2   C: rgb, p3   D: seed, rows, chars, unused
    vec2 c = A.yz; float m = 0.0;
    if(A.x < 0.5){ vec2 d = abs(q-c) - B.xy*0.5; float inside = step(max(d.x,d.y), 0.0);
      m = B.z > 0.0 ? inside*(1.0-step(max(d.x,d.y), -B.z)) : inside; }
    else if(A.x < 1.5){ vec2 d = (q-c)*asp; float r = length(d); float ang = atan(d.x, -d.y)/6.2831853+0.5;
      m = step(abs(r-B.x), B.y*0.5)*step(ang, B.z); }
    else if(A.x < 2.5){ vec2 d = (q-c)/B.xy + 0.5; if(all(greaterThanEqual(d, vec2(0.0))) && all(lessThan(d, vec2(1.0)))){
        float segs = max(B.z, 1.0); float s = fract(d.x*segs); float on = step(d.x, B.w);
        m = (segs > 1.5 ? step(0.12, s)*step(s, 0.88) : 1.0)*mix(0.25, 1.0, on); } }
    else { vec2 d = (q-c)/vec2(B.x*0.6*D.z, B.x*1.4*D.y) + 0.5;
      if(all(greaterThanEqual(d, vec2(0.0))) && all(lessThan(d, vec2(1.0)))){
        vec2 cell = floor(d*vec2(D.z, D.y)); vec2 f = fract(d*vec2(D.z, D.y)); vec2 px = floor(f*vec2(5.0,7.0));
        float glyph = step(0.5, h21(cell*13.1 + px + D.x)); float pad = step(0.0, px.x-0.0)*step(px.x, 3.0)*step(1.0, px.y)*step(px.y, 5.0);
        float blank = step(0.85, h21(cell + D.x*3.0)); m = glyph*pad*(1.0-blank); } }
    float a = clamp(m*A.w, 0.0, 1.0); acc.rgb = mix(acc.rgb, C.rgb, a); acc.a = acc.a + a*(1.0-acc.a);
  }
  oCol = acc; }"""


ANCHORS = [(0.0, 0.0), (0.5, 0.0), (1.0, 0.0), (0.0, 0.5), (0.5, 0.5), (1.0, 0.5), (0.0, 1.0), (0.5, 1.0), (1.0, 1.0)]


def hud_list(g, idx):
    items = []
    j = idx
    while j is not None and len(items) < 24:
        n = g.nodes[j]
        if OPS[n.op].out != "H":
            break
        d = _d(n)
        ax, ay = ANCHORS[int(d["anchor"])]
        cx = ax + d["x"] * (1 if ax < 0.75 else 1) + (0.0)
        cy = ay + d["y"]
        cx = min(max(cx, -0.2), 1.2)
        cy = min(max(cy, -0.2), 1.2)
        rgb = glsl.hsv(d)
        if n.op == "hud.rect":
            items.append(((0, cx, cy, d["alpha"]), (d["w"], d["hh"], d["outline"], 0), (*rgb, 0), (0, 0, 0, 0)))
        elif n.op == "hud.ring":
            items.append(((1, cx, cy, d["alpha"]), (d["r"], d["thick"], d["arc"], 0), (*rgb, 0), (0, 0, 0, 0)))
        elif n.op == "hud.bar":
            items.append(((2, cx, cy, d["alpha"]), (d["w"], d["hh"], d["segs"], d["fill"]), (*rgb, 0), (0, 0, 0, 0)))
        elif n.op == "hud.text":
            items.append(((3, cx, cy, d["alpha"]), (d["size"], 0, 0, 0), (*rgb, 0),
                          (d["seed"] * 1.37, max(1, d["rows"]), max(1, d["chars"]), 0)))
        j = n.inputs[0] if n.inputs else None
    items.reverse()
    return items


def look_at(eye, tgt):
    f = tgt - eye
    f = f / (np.linalg.norm(f) + 1e-9)
    up = np.array([0.0, 1.0, 0.0])
    if abs(f[1]) > 0.98:
        up = np.array([1.0, 0.0, 0.0])
    r = np.cross(f, up)
    r /= np.linalg.norm(r) + 1e-9
    u = np.cross(r, f)
    return r, u, f


def panini_params(d, crop, fovy, aspect):
    """URP PaniniProjection: (distance, crop scale, view extents) for the composite shader; distance 0 = off."""
    ey = math.tan(math.radians(fovy) / 2)
    ex = aspect * ey
    if d <= 0:
        return (0.0, 1.0, ex, ey)
    hyp = math.sqrt(ex * ex + 1.0)
    cdm = 1.0 / hyp
    cd = cdm + d
    cx, cy = ex * cdm * (1.0 + d) / cd, ey * cdm * (1.0 + d) / cd
    sf = min(cx / ex, cy / ey)
    s = 1.0 + (min(1.0, max(0.0, sf)) - 1.0) * crop
    return (float(d), float(s), float(ex), float(ey))


def perspective(fovy, aspect, n, fr):
    t = 1.0 / math.tan(math.radians(fovy) / 2)
    m = np.zeros((4, 4))
    m[0, 0] = t / aspect
    m[1, 1] = t
    m[2, 2] = (fr + n) / (n - fr)
    m[2, 3] = 2 * fr * n / (n - fr)
    m[3, 2] = -1
    return m


class Renderer:
    def __init__(self, w=W, h=H, ss=None):
        """ss = supersampling factor: render at (w*ss, h*ss) and box-filter down to (w, h), the way the Amoebius
        reels were downscaled from 1280x720. Default from AGENOME_SS (1). Measured 2026-11-10: 2x cuts the gradient
        distance by 1 to 2 for genomes not tuned at 1x and costs 2 to 2.7x the render time."""
        ensure_display()
        import moderngl
        self.mgl = moderngl
        self.ctx = moderngl.create_standalone_context()
        self.ss = max(1, int(os.environ.get("AGENOME_SS", "1") if ss is None else ss))
        self.out_w, self.out_h = w, h
        w, h = w * self.ss, h * self.ss
        self.w, self.h = w, h
        ctx = self.ctx
        self.quad = ctx.buffer(np.array([-1, -1, 1, -1, -1, 1, 1, 1], np.float32))
        self.scene_tex = ctx.texture((w, h), 4, dtype="f2")
        self.depth = ctx.depth_texture((w, h))
        self.scene_fbo = ctx.framebuffer([self.scene_tex], self.depth)
        bw, bh = max(1, w // 4), max(1, h // 4)
        self.bsize = (bw, bh)
        self.b1 = ctx.texture((bw, bh), 4, dtype="f2")
        self.b2 = ctx.texture((bw, bh), 4, dtype="f2")
        for t in (self.scene_tex, self.b1, self.b2):
            t.repeat_x = t.repeat_y = False
        self.f1 = ctx.framebuffer([self.b1])
        self.f2 = ctx.framebuffer([self.b2])
        self.out_tex = [ctx.texture((w, h), 4), ctx.texture((w, h), 4)]
        self.out_fbo = [ctx.framebuffer([t]) for t in self.out_tex]
        self.p_bright = ctx.program(vertex_shader=VS_QUAD, fragment_shader=FS_BRIGHT)
        self.p_blur = ctx.program(vertex_shader=VS_QUAD, fragment_shader=FS_BLUR)
        self.p_comp = ctx.program(vertex_shader=VS_QUAD, fragment_shader=FS_COMP)
        # post.urp pyramid: level i is the game's level i + log2(1280 / w) (its bloom is built at 1280x720, from half)
        self.u_shift = math.log2(1280.0 / w)
        self.u_n = max(1, int(round(6 - self.u_shift)))
        self.u_tex, self.u_fbo = [], []
        for i in range(self.u_n):
            lw, lh = max(1, (w // 2) >> i), max(1, (h // 2) >> i)
            trio = [ctx.texture((lw, lh), 4, dtype="f2") for _ in range(3)]  # down, scratch, up
            for t in trio:
                t.repeat_x = t.repeat_y = False
            self.u_tex.append(trio)
            self.u_fbo.append([ctx.framebuffer([t]) for t in trio])
        self.u_pre_tex = ctx.texture((w, h), 4, dtype="f2")
        self.u_pre_tex.repeat_x = self.u_pre_tex.repeat_y = False
        self.u_pre_fbo = ctx.framebuffer([self.u_pre_tex])
        self.u_top_tex = ctx.texture((w, h), 4, dtype="f2")
        self.u_top_tex.repeat_x = self.u_top_tex.repeat_y = False
        self.u_top_fbo = ctx.framebuffer([self.u_top_tex])
        self.p_upre = ctx.program(vertex_shader=VS_QUAD, fragment_shader=FS_UPRE)
        self.p_ublur = ctx.program(vertex_shader=VS_QUAD, fragment_shader=FS_UBLUR)
        self.p_uup = ctx.program(vertex_shader=VS_QUAD, fragment_shader=FS_UUP)
        self.v_upre = ctx.vertex_array(self.p_upre, [(self.quad, "2f", "p")])
        self.v_ublur = ctx.vertex_array(self.p_ublur, [(self.quad, "2f", "p")])
        self.v_uup = ctx.vertex_array(self.p_uup, [(self.quad, "2f", "p")])
        self.p_hud = ctx.program(vertex_shader=VS_QUAD, fragment_shader=FS_HUD)
        self.p_inst = ctx.program(vertex_shader=VS_INST, fragment_shader=FS_INST)
        self.v_bright = ctx.vertex_array(self.p_bright, [(self.quad, "2f", "p")])
        self.v_blur = ctx.vertex_array(self.p_blur, [(self.quad, "2f", "p")])
        self.v_comp = ctx.vertex_array(self.p_comp, [(self.quad, "2f", "p")])
        self.v_hud = ctx.vertex_array(self.p_hud, [(self.quad, "2f", "p")])
        self.meshes = {m: ctx.buffer(_mesh(m).tobytes()) for m in MESHES}
        self.mesh_n = {m: len(_mesh(m)) for m in MESHES}
        self.inst_buf = ctx.buffer(reserve=4 * 20 * 32768, dynamic=True)
        self.inst_vao = {m: ctx.vertex_array(self.p_inst, [(self.meshes[m], "3f 3f", "vp", "vn"),
                                                           (self.inst_buf, "3f 3f 3f 3f 4f 1f 3f/i", "ip", "ix", "iy", "iz",
                                                            "ic", "ie", "id")]) for m in MESHES}
        self._prog_cache = {}

    def _urp_bloom(self, post):
        """URP's bloom as the engine builds it (PostPass.cs): soft-knee prefilter, a gaussian while halving, then back
        up blending each level with the one below by the scatter. Levels finer than this render's own are the
        prefiltered image itself, weighted as the game's chain would weight them."""
        ctx, TS = self.ctx, self.mgl.TRIANGLE_STRIP
        thr = float(post["thresh"])
        thr = ((thr + 0.055) / 1.055) ** 2.4 if thr > 0.04045 else thr / 12.92
        sc = 0.05 + 0.9 * float(post["scatter"])
        ex = float(post["exposure"])
        self.scene_tex.use(0)
        self.u_pre_fbo.use()
        for k, v in (("uSrc", 0), ("uRes", (float(self.w), float(self.h))), ("uThreshold", thr / ex), ("uKnee", 0.5 * thr / ex)):
            self._set(self.p_upre, k, v)
        self.v_upre.render(TS)
        src = self.u_pre_tex
        for i in range(self.u_n):
            down, scratch, up = self.u_tex[i]
            fd, fs, fu = self.u_fbo[i]
            res = tuple(float(x) for x in down.size)
            src.use(0)
            fs.use()
            self._set(self.p_ublur, "uSrc", 0)
            self._set(self.p_ublur, "uRes", res)
            self._set(self.p_ublur, "uStep", (1.0 / res[0], 0.0))
            self.v_ublur.render(TS)
            scratch.use(0)
            fd.use()
            self._set(self.p_ublur, "uStep", (0.0, 1.0 / res[1]))
            self.v_ublur.render(TS)
            src = down
        low = self.u_tex[-1][0]
        self._set(self.p_uup, "uHigh", 0)
        self._set(self.p_uup, "uLow", 1)
        for i in range(self.u_n - 2, -1, -1):
            down, scratch, up = self.u_tex[i]
            self._set(self.p_uup, "uRes", tuple(float(x) for x in down.size))
            self._set(self.p_uup, "uScatter", sc)
            down.use(0)
            low.use(1)
            self.u_fbo[i][2].use()
            self.v_uup.render(TS)
            low = up
        # the game's finer levels: each would be (1 - sc) of itself over sc of the rest
        self._set(self.p_uup, "uRes", (float(self.w), float(self.h)))
        self._set(self.p_uup, "uScatter", sc ** max(0.0, self.u_shift))
        self.u_pre_tex.use(0)
        low.use(1)
        self.u_top_fbo.use()
        self.v_uup.render(TS)

    def scene_program(self, src):
        key = hash(src)
        p = self._prog_cache.get(key)
        if p is None:
            if len(self._prog_cache) > 64:
                for k, (pp, vv) in list(self._prog_cache.items())[:32]:
                    vv.release()
                    pp.release()
                    del self._prog_cache[k]
            pp = self.ctx.program(vertex_shader=VS_QUAD, fragment_shader=src)
            vv = self.ctx.vertex_array(pp, [(self.quad, "2f", "p")])
            p = self._prog_cache[key] = (pp, vv)
        return p

    @staticmethod
    def _set(prog, name, value):
        if name in prog:
            prog[name].value = value

    def render(self, g, n_frames=60, return_state=False, t0=None, t_shift=0.0, replay=None):
        """Render genome g to uint8 [n_frames, H, W, 3] at 15 fps of game time, starting at its warmup (or t0) plus
        t_shift seconds (a later window of the same phenotype, for judging more than its first 4 seconds)."""
        g = g.copy().ensure_root()
        cs, ct = g.cost()
        if cs > MAX_SHAPE_COST or ct > MAX_TEX_COST:
            raise ValueError(f"too costly to render: shape tree {cs}, texture trees {ct}")
        root = g.root()
        live = g.live()
        rn = g.nodes[root]
        rd = _d(rn)
        s_idx, a_idx, c_idx, p_idx, t_idx, h_idx = (rn.inputs + [None] * 6)[:6]
        src = glsl.scene_shader(g, live, s_idx, t_idx)
        prog, vao = self.scene_program(src)
        pops = build_populations(g, a_idx, gseed=g.seed)
        world = World(pops)
        cam = Camera(g, c_idx, world, seed=g.seed)
        # clear the feedback buffers: without this a clip's first frames carried the PREVIOUS genome's last frame
        # through uPrev, so a score depended on what the worker rendered before (found by the operator thread: one
        # genome rendered twice differed by 1.2/255 over its first frames, enough to move the judge at feedback 0.3+)
        for fb in self.out_fbo:
            fb.clear(0.0, 0.0, 0.0, 1.0)
        post = _d(g.nodes[p_idx]) if p_idx is not None else _d(type("N", (), {"op": "post.grade", "params": [p.default for p in OPS["post.grade"].params]})())
        urp = p_idx is not None and g.nodes[p_idx].op == "post.urp"
        if urp:
            post = dict(post, bloom=post["intensity"] * post["exposure"], sat=1.0, contrast=1.0, vignette=0.0, feedback=0.0, grain=0.0,
                        aberr=0.0, bloomrad=1.0)
        hud = hud_list(g, h_idx)
        dt = 1.0 / (FPS * SUBSTEPS)
        warm = (rd["warmup"] if t0 is None else t0) + float(t_shift)
        steps_warm = int(warm / dt)
        # the cap bounds a warmup alone (the out.warmup byte spans 0-30 s) plus the requested window shift; a flat 3600
        # (60 s) silently moved any window whose warmup + t_shift went past 60 s back to 60 s (code review 2026-11-10)
        # replay: a recorded session drives camera and instances (replay.frame(fi) -> t, eye, r, u, fwd, fov, inst)
        for _ in range(0 if replay is not None else min(steps_warm, 3600 + int(max(0.0, float(t_shift)) / dt))):
            world.step(dt)
            cam.update(world.t, dt)
        th, ph = rd["ltheta"], rd["lphi"]
        L = np.array([math.sin(th) * math.cos(ph), math.cos(th), math.sin(th) * math.sin(ph)], np.float32)
        Lc = glsl.hsv(rd, "l")
        fog_c = glsl.hsv(post, "f")
        fov = cam.fov()
        far, near = float(rd["far"]), 0.05
        frames = np.zeros((n_frames, self.h, self.w, 3), np.uint8)
        states = []
        ctx = self.ctx
        fb_i = 0
        # static uniforms
        for name, val in (("uRes", (float(self.w), float(self.h))), ("uSky", float(rd["sky"])),
                          ("uSteps", int(rd["steps"])), ("uLightDir", tuple(L)), ("uLightCol", tuple(Lc)),
                          ("uAmbient", float(rd["ambient"])), ("uFogCol", tuple(fog_c)), ("uFog", float(post["fog"])),
                          ("uNear", near), ("uFar", far), ("uTanHalf", math.tan(math.radians(fov) / 2))):
            self._set(prog, name, val)
        pi = self.p_inst
        for name, val in (("uLightDir", tuple(L)), ("uLightCol", tuple(Lc)), ("uFogCol", tuple(fog_c)),
                          ("uFog", float(post["fog"])), ("uAmbient", float(rd["ambient"]))):
            self._set(pi, name, val)
        pc = self.p_comp
        for name, val in (("uRes", (float(self.w), float(self.h))), ("uExposure", float(post["exposure"])),
                          ("uBloomK", float(post["bloom"])), ("uSat", float(post["sat"])),
                          ("uContrast", float(post["contrast"])), ("uVignette", float(post["vignette"])),
                          ("uFeedback", float(post["feedback"])), ("uGrain", float(post["grain"])),
                          ("uAberr", float(post["aberr"]))):
            self._set(pc, name, val)
        self._set(pc, "uMode", 1 if urp else 0)
        pd, crop = cam.panini()
        self._set(pc, "uPanini", panini_params(pd, crop, fov, self.w / self.h))
        self._set(self.p_bright, "uThresh", float(post["thresh"]))
        self._set(self.p_bright, "uRes", tuple(float(x) for x in self.bsize))
        self._set(self.p_blur, "uRes", tuple(float(x) for x in self.bsize))
        nh = len(hud)
        if nh:
            hp = self.p_hud
            self._set(hp, "uRes", (float(self.w), float(self.h)))
            self._set(hp, "uN", nh)
            pad = [((0, 0, 0, 0),) * 4] * (24 - nh)
            allh = hud + pad
            for k, nm in enumerate("ABCD"):
                if f"u{nm}" in hp:
                    hp[f"u{nm}"].value = [tuple(float(x) for x in it[k]) for it in allh]
        for fi in range(n_frames):
            if replay is not None:
                rf = replay.frame(fi)
                t, eye, r, u, fwd = rf["t"], np.asarray(rf["eye"], np.float32), rf["r"], rf["u"], rf["fwd"]
                tgt = eye + fwd
                if rf.get("fov"):
                    fov = float(rf["fov"])
                    self._set(prog, "uTanHalf", math.tan(math.radians(fov) / 2))
                    self._set(pc, "uPanini", panini_params(pd, crop, fov, self.w / self.h))
            else:
                for _ in range(SUBSTEPS):
                    world.step(dt)
                    eye, tgt = cam.update(world.t, dt)
                t = world.t
                r, u, fwd = look_at(eye, tgt) if cam.basis is None else cam.basis
                if cam.tunnel() is not None:
                    fov = cam.fov_now()
                    self._set(prog, "uTanHalf", math.tan(math.radians(fov) / 2))
                    self._set(pc, "uPanini", panini_params(cam.panini()[0], 1.0, fov, self.w / self.h))
            # ---- scene pass
            self.scene_fbo.use()
            ctx.viewport = (0, 0, self.w, self.h)
            ctx.enable(self.mgl.DEPTH_TEST)
            ctx.depth_func = "<="
            self.scene_fbo.clear(0, 0, 0, 1, depth=1.0)
            self._set(prog, "uTime", float(t))
            self._set(prog, "uEye", tuple(float(x) for x in eye))
            if "uCam" in prog:
                prog["uCam"].value = tuple(float(x) for x in np.concatenate([r, u, fwd]))
            ctx.depth_func = "1"
            vao.render(self.mgl.TRIANGLE_STRIP)
            ctx.depth_func = "<"
            # ---- instances
            inst = rf["inst"] if replay is not None else world.instances()
            V = np.eye(4)
            V[0, :3], V[1, :3], V[2, :3] = r, u, -fwd
            V[:3, 3] = -V[:3, :3] @ eye
            PV = perspective(fov, self.w / self.h, near, far) @ V
            pi["uPV"].write(PV.T.astype(np.float32).tobytes())
            self._set(pi, "uEye", tuple(float(x) for x in eye))
            n_inst_total = 0
            for m in MESHES:
                arr = inst.get(m)
                if arr is None or not len(arr):
                    continue
                arr = np.asarray(arr[:32768], np.float32)
                if arr.shape[1] == 17:  # no second colour: the face colour column is unused outside the prism graph
                    arr = np.concatenate([arr, np.zeros((len(arr), 3), np.float32)], 1)
                self.inst_buf.write(np.ascontiguousarray(arr, np.float32).tobytes())
                self.inst_vao[m].render(self.mgl.TRIANGLES, vertices=self.mesh_n[m], instances=len(arr))
                n_inst_total += len(arr)
            ctx.disable(self.mgl.DEPTH_TEST)
            if urp:
                self._urp_bloom(post)
                bloom_tex = self.u_top_tex
            else:
                # ---- bloom
                self.scene_tex.use(0)
                self.p_bright["uSrc"].value = 0
                self.f1.use()
                ctx.viewport = (0, 0, *self.bsize)
                self.v_bright.render(self.mgl.TRIANGLE_STRIP)
                rad = float(post["bloomrad"])
                for _ in range(2):
                    self.b1.use(0)
                    self.p_blur["uSrc"].value = 0
                    self.p_blur["uDir"].value = (rad, 0.0)
                    self.f2.use()
                    self.v_blur.render(self.mgl.TRIANGLE_STRIP)
                    self.b2.use(0)
                    self.p_blur["uDir"].value = (0.0, rad)
                    self.f1.use()
                    self.v_blur.render(self.mgl.TRIANGLE_STRIP)
                bloom_tex = self.b1
            # ---- composite
            dst = self.out_fbo[fb_i]
            prev = self.out_tex[1 - fb_i]
            dst.use()
            ctx.viewport = (0, 0, self.w, self.h)
            self.scene_tex.use(0)
            bloom_tex.use(1)
            prev.use(2)
            self._set(pc, "uScene", 0)
            self._set(pc, "uBloom", 1)
            self._set(pc, "uPrev", 2)
            self._set(pc, "uTime", float(t))
            self.v_comp.render(self.mgl.TRIANGLE_STRIP)
            # ---- hud
            if nh:
                ctx.enable(self.mgl.BLEND)
                ctx.blend_func = self.mgl.SRC_ALPHA, self.mgl.ONE_MINUS_SRC_ALPHA
                self.v_hud.render(self.mgl.TRIANGLE_STRIP)
                ctx.disable(self.mgl.BLEND)
            buf = dst.read(components=3)
            frames[fi] = np.frombuffer(buf, np.uint8).reshape(self.h, self.w, 3)[::-1]
            fb_i = 1 - fb_i
            if return_state:
                states.append({"f": fi, "t": t, "camera": {"pos": eye.tolist(), "tgt": tgt.tolist(), "fov": fov},
                               "instances": n_inst_total,
                               "movers": [len(p.pos) for p in world.movers]})
        if self.ss > 1:
            k = self.ss
            frames = frames.reshape(n_frames, self.out_h, k, self.out_w, k, 3).mean(axis=(2, 4), dtype=np.float32)
            frames = np.clip(np.round(frames), 0, 255).astype(np.uint8)
        if return_state:
            return frames, states
        return frames


def save_clip(frames, folder, meta=None, mp4=True):
    os.makedirs(folder, exist_ok=True)
    np.save(os.path.join(folder, "frames.npy"), frames)
    if meta is not None:
        import json
        with open(os.path.join(folder, "meta.json"), "w") as f:
            json.dump(meta, f, indent=1)
    if mp4 and shutil.which("ffmpeg"):
        p = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s",
                              f"{frames.shape[2]}x{frames.shape[1]}", "-r", str(FPS), "-i", "-", "-vf",
                              "scale=768:432:flags=neighbor", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20",
                              os.path.join(folder, "clip.mp4")], stdin=subprocess.PIPE)
        p.communicate(frames.tobytes())


def contact_sheet(frames, path, cols=4, picks=None):
    from PIL import Image
    T = len(frames)
    if picks is None:
        picks = np.linspace(0, T - 1, cols * 2).astype(int)
    rows = int(math.ceil(len(picks) / cols))
    h, w = frames.shape[1:3]
    sheet = Image.new("RGB", (cols * w, rows * h))
    for k, i in enumerate(picks):
        sheet.paste(Image.fromarray(frames[i]), ((k % cols) * w, (k // cols) * h))
    sheet.save(path)
    return path
