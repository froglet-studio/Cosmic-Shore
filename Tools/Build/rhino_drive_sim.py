#!/usr/bin/env python3
"""Offline Regatta race for the Rhino AI: the first ramp driver against RampBoostAIPolicySO's brake-on-the-ramp drive.

A MODEL, not the game: a point mass on the Regatta course generator's rings (situation_features.course_from), steering
proportional to the angle error at the Rhino's turn rate ((v x 0.2 + 90) deg/s at full stick), the ramp executor's rates
(climb 220, bleed 300 while graded, coast 120 once ended; x16.8 over a 50 u/s cruise, plateau 0.3, grace band 1) and the
gate steering's approach point / mouth / commit distance. It has no orbit break-off, so a pilot that circles a point
stalls here where the game would break off. Use it to compare drives and pick defaults; the editor is the measurement.

The Python driver below MIRRORS RampBoostAIPolicySO.Driver.Tick: change both together.

    python3 Tools/Build/rhino_drive_sim.py            # old vs new on I1-I4
    python3 Tools/Build/rhino_drive_sim.py --sweep    # the corner-speed / payback / release / margin grid
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf

def v(a): return list(a)
def add(a,b): return [x+y for x,y in zip(a,b)]
def sub(a,b): return [x-y for x,y in zip(a,b)]
def mul(a,k): return [x*k for x in a]
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def norm(a): return math.sqrt(dot(a,a))
def unit(a): n=norm(a); return [x/n for x in a] if n>1e-9 else [0,0,1]
def ang(a,b):
    na,nb=norm(a),norm(b)
    if na<1e-9 or nb<1e-9: return 0.0
    return math.degrees(math.acos(max(-1,min(1,dot(a,b)/(na*nb)))))
def rotate_toward(f, d, max_rad):
    a = math.radians(ang(f,d))
    if a < 1e-9: return f
    t = min(1.0, max_rad / a)
    # slerp
    so = math.sin(a)
    if so < 1e-6: return unit(d)
    return unit(add(mul(f, math.sin((1-t)*a)/so), mul(unit(d), math.sin(t*a)/so)))

PLATEAU, GRACE, MAXM, ACC, BLEED, RET, CRUISE = 0.3, 1.0, 16.8, 220.0, 300.0, 120.0, 50.0
def straight01(d): return 1 - min(1, max(0, (d-PLATEAU)/(GRACE-PLATEAU)))
def mult(d): return 1 + (MAXM-1)*straight01(d)

class Policy:  # base AIBoostPolicySO defaults + ramp fields
    align, ralign, stick, rstick, erun, rrun = 12, 25, 0.35, 0.6, 1.5, 0.5
    share, hold, rest, rearm, gain = 0.9, 0.5, 0.3, 0.5, 150
    corner, mincorner, margin, lead, gshare, steerfloor, slew, crel = 600, 160, 120, 0.25, 0.9, 0.3, 4, 50
    def is_straight(s,c): return c['angle'] <= s.align and c['stick'] <= s.stick
    def still(s,c): return c['angle'] <= s.ralign and c['stick'] <= s.rstick
    def runway(s,c): return c['dist']/max(c['speed'],1) >= s.erun
    def turn_ahead(s,c): return c['dist']/max(c['speed'],1) < s.rrun
    def corner_speed(s,deg):
        th = 90 if deg < 0 else min(179,max(1,deg))
        return max(s.mincorner, s.corner/math.tan(math.radians(th/2)))
    @staticmethod
    def brake_dist(v,t,d): return 0 if v<=t else (v*v-t*t)/(2*d)
    @staticmethod
    def peak(v0,ve,a,b,D):
        D=max(0,D); climb=math.sqrt(v0*v0+2*a*D)
        if climb<=ve: return max(v0,climb)
        return max(v0, math.sqrt((D+v0*v0/(2*a)+ve*ve/(2*b))/(1/(2*a)+1/(2*b))))
    def pays(s,c): 
        ex=s.corner_speed(c['corner']); room=c['dist']-s.margin-c['speed']*s.lead
        return s.peak(c['speed'],ex,ACC,BLEED,room)-c['speed']>=s.gain
    def brake_point(s,c):
        return c['dist']-s.margin-c['speed']*s.lead <= s.brake_dist(c['speed'], s.corner_speed(c['corner']), BLEED)

class Exec:
    def __init__(s): s.on=False
    def begin(s): s.on=True
    def end(s): s.on=False

class OldDriver:
    def __init__(s,p,ex): s.p,s.ex,s.ramping,s.changed=p,ex,False,-1e9
    throttle=-1
    def full(s): return s.ramping
    def limit(s): return 0.3*s.p.share if s.ramping else -1
    def tick(s,c,t,dt,tracked):
        p=s.p
        if s.ramping:
            if t-s.changed<p.hold: return
            if not p.still(c) or p.turn_ahead(c): s.ramping=False; s.changed=t   # StopAction: no effect on a graded ramp
            return
        if t-s.changed<p.rest: return
        if p.is_straight(c) and p.runway(c): s.ramping=True; s.changed=t; s.ex.begin()

class NewDriver:
    def __init__(s,p,ex): s.p,s.ex,s.mode,s.changed,s.armed,s.thr,s.wanted=p,ex,'off',-1e9,-1e9,1.0,0
    def full(s): return s.mode=='ramp'
    @property
    def throttle(s): return s.thr if s.mode=='brake' else -1
    def limit(s):
        if s.mode=='ramp': return 0.3*s.p.share
        if s.mode=='brake': return max(0, GRACE*s.p.gshare-(1-s.thr))
        return -1
    def set(s,m,t):
        was=s.mode; s.mode=m; s.changed=t
        if m=='brake': s.thr=1.0
        if m=='off': s.ex.end()
        elif was=='off': s.armed=t; s.ex.begin()
    def rearm(s,t):
        if not s.ex.on and t-s.armed>=s.p.rearm: s.armed=t; s.ex.begin()
    def tick(s,c,t,dt,tracked):
        p=s.p
        if s.mode=='off':
            if t-s.changed>=p.rest and p.is_straight(c) and p.runway(c) and p.pays(c): s.set('ramp',t)
            return
        if s.mode=='ramp':
            s.rearm(t)
            if t-s.changed<p.hold: return
            if not p.still(c) or p.turn_ahead(c) or p.brake_point(c): s.set('brake',t)
            return
        s.wanted=p.corner_speed(c['corner'])
        if c['angle']>=p.crel and c['speed']<=s.wanted*1.1: s.set('off',t); return
        if t-s.changed>=p.hold and p.is_straight(c) and p.runway(c) and not p.brake_point(c) and p.pays(c): s.set('ramp',t); return
        s.rearm(t)
        floor=1-max(0,GRACE*p.gshare-p.steerfloor)
        s.thr=min(1,max(floor, s.thr+(-1 if tracked>s.wanted else 1)*p.slew*dt))

def race(kind, intensity, use_next=True, T=400, dt=1/60, verbose=False):
    C=sf.course_from(sf.load_course_js(), intensity)
    gates=C['gates']; n=len(gates); laps=3; total=n*laps
    P=[g['p'] for g in gates]; A=[unit(g['a']) for g in gates]
    pos=add(P[0], mul(A[0], 1500)); fwd=unit(sub(P[0],pos)); speed=50.0
    pol=Policy(); ex=Exec(); drv=(NewDriver if kind=='new' else OldDriver)(pol,ex)
    idx=0; locked=-1; side=1; t=0.0; stick=0.0; trace=[]; maxv=0
    while t<T and idx<total:
        g=idx%n; ring=P[g]; ax=A[g]
        if idx!=locked:
            locked=idx; side=1 if dot(sub(pos,ring),ax)>=0 else -1
            leg_from=P[(idx-1)%n] if idx>0 else pos
            lead=min(480,0.4*norm(sub(leg_from,ring)))
        omega=(speed*0.2+90)  # deg/s at full stick
        R=speed/math.radians(omega)
        commit=min(420, lead+0.5*R)
        approaching=norm(sub(ring,pos))>commit and norm(sub(add(ring, mul(ax, side*lead)),pos))>max(60,0.5*R)
        through=sub(ring, mul(ax, side*320))
        if approaching: target=add(ring, mul(ax, side*lead)); after=through
        else: target=through; after=P[(idx+1)%n] if idx+1<total else None
        to=sub(target,pos)
        nl = sub(after,target) if (after is not None and use_next) else None
        c={'angle':ang(fwd,to),'stick':stick,'dist':norm(to),'speed':speed,
           'corner': ang(to,nl) if nl is not None else -1}
        # executor's tracked target (last frame's input)
        drv_tracked = getattr(race,'_tracked',speed)
        drv.tick(c,t,dt,drv_tracked)
        # pilot steering: proportional on the angle error
        err=ang(fwd,to); s=min(1.0, err/20.0)
        steer=2*s  # XSum + YDiff
        lim=drv.limit()
        if lim>=0 and steer>lim: s*=lim/steer; steer=lim
        stick=s
        x=1.0
        if drv.full(): x=1.0
        if drv.throttle>=0: x=drv.throttle
        # executor
        if ex.on:
            d=(1-x)+steer
            if d>=GRACE: ex.end()
        if ex.on:
            tgt=x*CRUISE*mult((1-x)+steer)
            rate=ACC if tgt>=speed else BLEED
        else:
            tgt=x*CRUISE
            rate=RET if speed>tgt else 400
        race._tracked=tgt
        speed=speed+max(-rate*dt,min(rate*dt,tgt-speed))
        maxv=max(maxv,speed)
        fwd=rotate_toward(fwd, to, math.radians((speed*0.2+90)*s)*dt)
        newpos=add(pos,mul(fwd,speed*dt))
        # threading: crossing the plane inside the mouth
        a0=dot(sub(pos,ring),ax); a1=dot(sub(newpos,ring),ax)
        if a0*a1<=0 and a0!=a1:
            k=a0/(a0-a1); hit=add(pos,mul(sub(newpos,pos),k))
            if norm(sub(hit,ring))<=gates[g]['r']: idx+=1
        pos=newpos; t+=dt
    race._tracked=50
    return (round(t,1) if idx>=total else None), idx, round(maxv)

def _sweep_one(args):
    corner, gain, crel, margin = args
    Policy.corner, Policy.gain, Policy.crel, Policy.margin = corner, gain, crel, margin
    res = [race('new', I, T=200) for I in (1, 2, 3)]
    return sum(r[0] if r[0] else 200 + (24 - r[1]) * 10 for r in res), args, [r[0] for r in res]


def sweep():
    import itertools
    from concurrent.futures import ProcessPoolExecutor
    grid = list(itertools.product((260, 360, 480, 600), (50, 150, 300), (30, 50, 80), (60, 120, 250)))
    with ProcessPoolExecutor() as ex:
        out = sorted(ex.map(_sweep_one, grid))
    print("total s (I1-I3; a stall costs 200 + 10 per ring left), (cornerSpeed, minSpeedGain, cornerReleaseDegrees, brakeMarginUnits), per course")
    for o in out[:12]:
        print(o)


if __name__ == '__main__':
    if "--sweep" in sys.argv:
        sweep()
    else:
        print("(seconds for 3 laps or None = stalled, rings threaded, top speed)")
        for I in (1, 2, 3, 4):
            print(f"I{I}: old", race('old', I), " new", race('new', I), " new without corner info", race('new', I, use_next=False))
