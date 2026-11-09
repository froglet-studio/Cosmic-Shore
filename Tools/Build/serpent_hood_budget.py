#!/usr/bin/env python3
"""
The Python mirror of Hood's arena (GameModes.SerpentHood = 77): SerpentineGate.cs and SerpentHoodLayout.cs, ported line
for line, so the drawings, the generator's census (PhaseThresholds, the prism budget) and the game agree.

    python3 Tools/Build/serpent_hood_budget.py              # the census per intensity
    python3 Tools/Build/serpent_hood_budget.py --parity     # compile the SHIPPED C# (serpent_hood_harness) and compare
                                                            #   every prism, position and orientation, to this mirror
    python3 Tools/Build/serpent_hood_budget.py --self-test  # parity, plus a negative control that must fail it

A cover is a Serpentine ring shape (72 u ring, the shape of the intensity) bent into a hood (Warp): wider across,
tapering to one end, the sides curled back into a cup. Perches stand on a Fibonacci sphere round the crystal heart,
mouths to it; shutters (I3-I4) stand nearer the heart, turned across. Closed form, zero random draws.
"""
import json
import math
import os
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))

# ── SerpentineGate.cs, ported ────────────────────────────────────────────────
PITCH=12.0; BAR=3.0; CHORD=12.0; RIM=2.0; COIL_ARMS=6; IRIS_SWEEP=1.6; IRIS_W=7.0; IRIS_T=1.5
MAW_FANG=18.0; MAW_RING=10.0; MAW_SHORT=0.55; EXIT_MAX=26.0; EXIT_MARGIN=1.5
SHELL_RATIO=1.75; SHELL_FLARE=0.6; SHELL_PITCH=16.0; SHELL_W=8.5; SHELL_L=12.0; SHELL_T=1.5
HOOD=1.3; HOUSING=6.0; TWIST=1.7

def add(a,b): return (a[0]+b[0],a[1]+b[1],a[2]+b[2])
def mul(a,k): return (a[0]*k,a[1]*k,a[2]*k)
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def dot(a,b): return a[0]*b[0]+a[1]*b[1]+a[2]*b[2]
def norm(a):
    m=math.sqrt(dot(a,a)); return (a[0]/m,a[1]/m,a[2]/m)
def look(fwd,up):
    f=norm(fwd); r=cross(up,f)
    if dot(r,r)<1e-10: r=cross((1,0,0),f)
    r=norm(r); u=cross(f,r); return (r,u,f)

def core_for(i):
    return {1:(7,6.,7.),2:(3,5.,4.),3:(1,5.,0.)}.get(max(1,min(4,i)),(1,3.5,0.))
def core_reach(c):
    b,cube,sp=c; return (sp if b>1 else 0)+cube*0.5*math.sqrt(2)
def weave_start(i): return max(core_reach(core_for(i))+1.5,10.)
EXIT_BAND=core_reach((7,6.,7.))+1.5
def shape_for(i): return 'coil' if i<=2 else ('maw' if i==3 else 'iris')
def exit_count(shape): return 2 if shape=='coil' else 3
def exit_radius(R): return min(EXIT_MAX,(R-RIM-EXIT_BAND)*0.5-EXIT_MARGIN)
def exit_distance(R): return (EXIT_BAND+R-RIM)*0.5
def outer_reach(R,i):
    r=R*SHELL_RATIO*(HOOD if shape_for(i)=='maw' else 1); back=(R*SHELL_RATIO-R)*SHELL_FLARE
    return math.sqrt(r*r+back*back)+SHELL_L

def frame(axis):
    axis=norm(axis); s=cross(axis,(0,1,0))
    if dot(s,s)<1e-4: s=cross(axis,(1,0,0))
    s=norm(s); u=cross(s,axis); return s,u,axis

def build(centre,axis,R,gate,intensity,shape=None):
    shape=shape or shape_for(intensity); core=core_for(intensity); ws=weave_start(intensity)
    s,u,ax=frame(axis); twist=gate*TWIST; out=[]
    n=exit_count(shape); er=exit_radius(R)
    exits=[(math.cos(twist+.5+e*2*math.pi/n)*exit_distance(R),math.sin(twist+.5+e*2*math.pi/n)*exit_distance(R)) for e in range(n)]
    W=lambda p,al: add(add(add(centre,mul(s,p[0])),mul(u,p[1])),mul(ax,al))
    D=lambda d: add(mul(s,d[0]),mul(u,d[1]))
    def in_exit(p,c):
        return any((p[0]-x)**2+(p[1]-y)**2<(er+c)**2 for x,y in exits)
    def block(p,size,core_):
        out.append((W(p,0),look(ax,D((1,0))),(size,size,size),'plain',core_,False))
    def bar(a,b,al,w,t):
        d=(b[0]-a[0],b[1]-a[1]); L=math.hypot(*d)
        if L<.5: return
        c=w*.5; cuts=[0.,1.]; rr=er+c
        for x,y in exits:
            f=(a[0]-x,a[1]-y); qa=d[0]**2+d[1]**2; qb=2*(f[0]*d[0]+f[1]*d[1]); qc=f[0]**2+f[1]**2-rr*rr
            disc=qb*qb-4*qa*qc
            if disc<=0: continue
            sq=math.sqrt(disc)
            for tc in ((-qb-sq)/(2*qa),(-qb+sq)/(2*qa)):
                if 0<tc<1: cuts.append(tc)
        cuts.sort()
        for t0,t1 in zip(cuts,cuts[1:]):
            p0=(a[0]+d[0]*t0,a[1]+d[1]*t0); p1=(a[0]+d[0]*t1,a[1]+d[1]*t1); l=math.hypot(p1[0]-p0[0],p1[1]-p0[1])
            m=((p0[0]+p1[0])/2,(p0[1]+p1[1])/2)
            if l<1 or in_exit(m,c): continue
            out.append((W(m,al),look(D(((p1[0]-p0[0])/l,(p1[1]-p0[1])/l)),ax),(w,t,l),'danger',False,True))
    P=lambda r,a:(math.cos(a)*r,math.sin(a)*r)
    def spiral(th0,b,tEnd,w,t):
        tt=0.; prev=P(ws,th0)
        while tt<tEnd-1e-4:
            r=ws+b*tt; dt=min(tEnd-tt,CHORD/math.sqrt(r*r+b*b)); tt+=dt
            nx=P(ws+b*tt,th0+tt); bar(prev,nx,0,w,t); prev=nx
    def circle(r,al):
        k=max(6,math.ceil(2*math.pi*r/CHORD))
        for i in range(k): bar(P(r,i*2*math.pi/k),P(r,(i+1)*2*math.pi/k),al,BAR,BAR)
    def radial(a,r0,r1,al):
        d=(math.cos(a),math.sin(a)); pcs=max(1,math.ceil((r1-r0)/60))
        for i in range(pcs):
            x0=r0+(r1-r0)*i/pcs; x1=r0+(r1-r0)*(i+1)/pcs
            bar((d[0]*x0,d[1]*x0),(d[0]*x1,d[1]*x1),al,BAR,BAR)
    def housing():
        r=R+HOUSING; k=math.ceil(2*math.pi*r/(HOUSING*1.25))
        for i in range(k):
            p=P(r,i*2*math.pi/k); out.append((W(p,0),look(ax,D((p[0]/r,p[1]/r))),(HOUSING,)*3,'plain',False,False))
    def shell(stagger,stretch,first=0):
        ring=0; rho=R+SHELL_PITCH*.5
        while rho<=R*SHELL_RATIO:
            if ring>=first:
                k=math.ceil(2*math.pi*rho/SHELL_PITCH); off=math.pi/k if stagger and ring%2==1 else 0; back=-(rho-R)*SHELL_FLARE
                for i in range(k):
                    a=off+i*2*math.pi/k; rad=(math.cos(a),math.sin(a)); p=(rad[0]*rho*stretch,rad[1]*rho)
                    slope=add(D(rad),mul(ax,-SHELL_FLARE))
                    out.append((W(p,back),look(slope,ax),(SHELL_W,SHELL_T,SHELL_L),'plain',False,False))
            rho+=SHELL_PITCH; ring+=1
    b_,cube,sp=core
    if b_ in (1,7): block((0,0),cube,True)
    ob=6 if b_==7 else (3 if b_==3 else 0)
    for k in range(ob):
        a=twist+k*2*math.pi/ob; block((math.cos(a)*sp,math.sin(a)*sp),cube,True)
    if shape=='coil':
        b=PITCH*COIL_ARMS/(2*math.pi); tEnd=(R-RIM-ws)/b
        for arm in range(COIL_ARMS): spiral(twist+arm*2*math.pi/COIL_ARMS,b,tEnd,BAR,BAR)
        circle(R-RIM-BAR*.5,0); shell(True,1.)
    elif shape=='iris':
        b=(R-RIM-ws)/IRIS_SWEEP; bl=math.ceil(2*math.pi*b/PITCH)
        for k in range(bl): spiral(twist+k*2*math.pi/bl,b,IRIS_SWEEP,IRIS_W,IRIS_T)
        circle(R-RIM-BAR*.5,0); housing(); shell(False,1.,1)
    else:
        rim=R-RIM; f=math.ceil(2*math.pi*R/MAW_FANG); f+=f%2
        for k in range(f):
            a=twist+k*2*math.pi/f; radial(a,ws if k%2==0 else R*MAW_SHORT,rim,-BAR*.5)
        r=ws+MAW_RING*.5
        while r<=rim-BAR: circle(r,BAR*.5); r+=MAW_RING
        shell(True,HOOD)
    return out


def build_local_flat(R, gate, intensity):
    """SerpentineGate.Build at the origin, axis +z (the frame SerpentHoodLayout.BuildLocal uses)."""
    return build((0.0, 0.0, 0.0), (0.0, 0.0, 1.0), R, gate, intensity)


# ── SerpentHoodLayout.cs, ported ─────────────────────────────────────────────
COVER_R = 72.0
GOLDEN = 2.39996323
PHI = 1.6180339887
RUNGS = {   # perches, perch radius, shutters, shutter radius, wide, narrow, cup
    1: (8, 1500.0, 0, 0.0, 1.35, 0.55, 0.18),
    2: (12, 1350.0, 0, 0.0, 1.45, 0.50, 0.24),
    3: (18, 1250.0, 6, 700.0, 1.50, 0.45, 0.30),
    4: (20, 1120.0, 6, 660.0, 1.60, 0.40, 0.36),
}
LEVEL_NAMES = {1: "Wide Hood", 2: "Spread Hood", 3: "Fanged Hood", 4: "Closed Hood"}


def rung(i):
    return RUNGS[max(1, min(4, i))]


def fibonacci(i, n):
    y = 1 - 2 * (i + 0.5) / n
    r = math.sqrt(max(0.0, 1 - y * y))
    a = i * GOLDEN
    return (math.cos(a) * r, y, math.sin(a) * r)


def covers(intensity):
    """[(index, centre, axis, shutter)] - perches first, then shutters (SerpentHoodLayout.Covers)."""
    n, pr, m, sr = rung(intensity)[:4]
    out = []
    for i in range(n):
        d = fibonacci(i, n)
        out.append((len(out), mul(d, pr), mul(d, -1), False))
    for i in range(m):
        d = fibonacci(i, m)
        out.append((len(out), mul(d, sr), norm(cross(d, fibonacci((i + 1) % m, m))), True))
    return out


def warp(p, intensity, reach):
    wide, narrow, cup = rung(intensity)[4:]
    x, y, z = p
    v = y / reach
    k = wide - (wide - narrow) * min(1.0, -v) ** 1.3 if v < 0 else wide - 0.3 * v * v
    yy = y * (1.25 if v < 0 else 1.0)
    xx = x * k
    vp = max(0.0, v)
    return (xx, yy, z - cup * xx * xx / reach - 0.10 * vp * vp * reach)


def bend(p, d, intensity, reach):
    e = 0.25
    a = warp(add(p, mul(d, e)), intensity, reach)
    b = warp(add(p, mul(d, -e)), intensity, reach)
    return mul(add(a, mul(b, -1)), 1 / (2 * e))


def hood_frame(axis):
    f = norm(axis)
    s = cross(f, (0, 1, 0))
    if dot(s, s) < 1e-4:
        s = cross(f, (1, 0, 0))
    s = norm(s)
    return s, cross(s, f), f


def build_cover(index, centre, axis, intensity):
    """[(pos, fwd, up, scale, kind, core)] in world space, and the cover's reach (SerpentHoodLayout.Build)."""
    reach = outer_reach(COVER_R, intensity)
    s, u, f = hood_frame(axis)
    out, far = [], 0.0
    for pos, (_r, pu, pf), sc, kind, core, _weave in build_local_flat(COVER_R, index, intensity):
        q = warp(pos, intensity, reach)
        lf, lu = norm(bend(pos, pf, intensity, reach)), bend(pos, pu, intensity, reach)
        tf = lambda v: add(add(mul(s, v[0]), mul(u, v[1])), mul(f, v[2]))
        world = add(centre, tf(q))
        wf = tf(lf)
        wu = norm(cross(wf, cross(tf(lu), wf)))     # LookRotation's up: the given up, made square to forward
        out.append((world, wf, wu, sc, kind, core))
        far = max(far, math.dist(world, centre) + sc[2] * 0.5)
    return out, far


def field_slot(i, count, radius):
    t = i * PHI + 0.3
    t -= math.floor(t)
    return mul(fibonacci(i, count), radius * t ** (1 / 3))


def census(intensity):
    """(prisms, danger, volume, covers) laid at an intensity."""
    count = danger = 0
    volume = 0.0
    cs = covers(intensity)
    for index, c, ax, _ in cs:
        parts, _ = build_cover(index, c, ax, intensity)
        count += len(parts)
        danger += sum(1 for p in parts if p[4] == "danger")
        volume += sum(p[3][0] * p[3][1] * p[3][2] for p in parts)
    return count, danger, volume, len(cs)


# ── Parity with the shipped C# ───────────────────────────────────────────────
def csharp_parts(layout_override=None):
    env = dict(os.environ)
    cmd = ["bash", os.path.join(ROOT, "Tools/Build/serpent_hood_harness/run.sh"), "--parts"]
    out = subprocess.run(cmd, cwd=ROOT, env=env, capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit(f"serpent_hood_harness failed:\n{out.stderr[-2000:]}")
    return [json.loads(line) for line in out.stdout.splitlines() if line.strip()]


def parity_errors(lines, tol=0.05, mirror=None):
    mirror = mirror or sys.modules[__name__]
    errs, seen = [], 0
    for d in lines:
        if "field" in d:
            p = mirror.field_slot(d["field"], 24, 380.0)
            if math.dist(p, d["p"]) > tol:
                errs.append(f"field slot {d['field']}: C# {d['p']} vs Python {p}")
            continue
        i, k = d["i"], d["cover"]
        index, c, ax, shutter = mirror.covers(i)[k]
        if math.dist(c, d["c"]) > tol or math.dist(ax, d["axis"]) > 1e-3 or shutter != d["shutter"]:
            errs.append(f"I{i} cover {k}: placement differs (C# {d['c']} {d['axis']}, Python {c} {ax})")
            continue
        parts, reach = mirror.build_cover(index, c, ax, i)
        if len(parts) != len(d["parts"]):
            errs.append(f"I{i} cover {k}: {len(d['parts'])} prisms in C#, {len(parts)} in Python")
            continue
        if abs(reach - d["reach"]) > 0.1:
            errs.append(f"I{i} cover {k}: reach {d['reach']:.2f} in C#, {reach:.2f} in Python")
        for n, (cp, py) in enumerate(zip(d["parts"], parts)):
            pos, fwd, up, sc, kind, core = cp
            kind_py = 1 if py[4] == "danger" else 0
            if (math.dist(pos, py[0]) > tol or math.dist(fwd, py[1]) > 2e-3 or math.dist(up, py[2]) > 2e-3
                    or math.dist(sc, py[3]) > 1e-3 or kind != kind_py or core != (1 if py[5] else 0)):
                errs.append(f"I{i} cover {k} prism {n}: C# {cp} vs Python {py}")
                break
            seen += 1
    if seen == 0:
        errs.append("no prisms compared")
    return errs, seen


def main(argv):
    if "--parity" in argv or "--self-test" in argv:
        lines = csharp_parts()
        errs, seen = parity_errors(lines)
        if errs:
            print(f"PARITY FAILED ({len(errs)}):")
            for e in errs[:20]:
                print("  -", e)
            return 1
        print(f"parity: OK, {seen} prisms and 24 field slots match the shipped C#")
        if "--self-test" in argv:
            # Negative control: a mirror whose I3 perches stand 10 u farther out must fail parity.
            saved = RUNGS[3]
            RUNGS[3] = (saved[0], saved[1] + 10.0) + saved[2:]
            try:
                bad, _ = parity_errors(lines)
            finally:
                RUNGS[3] = saved
            if not bad:
                print("NEGATIVE CONTROL DID NOT FIRE: an I3 radius 10 u off passed parity")
                return 1
            print(f"negative control fired: an I3 radius 10 u off fails parity ({len(bad)} error(s))")
        return 0
    for i in range(1, 5):
        count, danger, volume, n = census(i)
        print(f"I{i} {LEVEL_NAMES[i]:<12} {n:2d} covers  {count:5d} prisms  {danger:5d} danger  volume {volume:,.0f}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
