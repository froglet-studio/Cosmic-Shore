#!/usr/bin/env python3
"""Offline Regatta race for the Rhino AI: the first ramp driver against RampBoostAIPolicySO's brake-on-the-ramp drive.

A MODEL, not the game: a point mass on the Regatta course generator's rings (situation_features.course_from), steering
proportional to the angle error at the Rhino's turn rate ((v x 0.2 + 90) deg/s at full stick), the ramp executor's rates
(climb 220, bleed 300 while graded, coast 120 once ended; x16.8 over a 50 u/s cruise, plateau 0.3, grace band 1) and the
gate steering's approach point / mouth (through the ring's centre, as Regatta flies it; THROUGH_CENTRE = False for
the axis point the other gate races use) / commit distance. It has no orbit break-off, so a pilot that circles a point
stalls here where the game would break off. Use it to compare drives and pick defaults; the editor is the measurement.

The Python driver below MIRRORS RampBoostAIPolicySO.Driver.Tick: change both together.

    python3 Tools/Build/rhino_drive_sim.py            # old vs new on I1-I4
    python3 Tools/Build/rhino_drive_sim.py --sweep    # the corner-speed / payback / release / margin grid
    python3 Tools/Build/rhino_drive_sim.py --headlong # the drive on Headlong's 12 courses under each gate steering
    python3 Tools/Build/rhino_drive_sim.py --self-test
    python3 Tools/Build/rhino_drive_sim.py --nose-rate 1.5             # + the rows with the game's nose lag (1.5/s), lead off
    python3 Tools/Build/rhino_drive_sim.py --nose-lead 2.5             # + the lag rows and the AI nose lead 2.5 on top of the lag
    python3 Tools/Build/rhino_drive_sim.py --headlong --nose-lead 2.5  # the same extra rows on Headlong's 12 courses

STEERING (2026-11-06): the gate steering is now a named profile (STEERING_PROFILES: Headlong's as shipped, Headlong with
AICommitFromApproachPoint on, Regatta's), each with AIPilot's orbit break-off. STEERING = None is the legacy model.
Docs/Reports/RHINO_HEADLONG_I4_STALLS.md has what it found: the I4 stall was the steering, not the drive.

NOSE LAG AND THE AI NOSE LEAD (2026-11-07; Tools/Build/ai_nose_lead.py is the one shared piece): race() takes two knobs,
both OFF by default, so every number above and every caller (race_models.py's "rhino_drive", drive_fit) races byte for
byte what it raced before.
- nose_rate=None is today's model: ONE heading is the nose, the stick's command and the velocity at once, turned straight
  at the aim by rotate_toward. That lag-free model is the one the measurements were calibrated on, and it is known to be
  optimistic (Tools/Compute/evaluators/redline_fit.py records that the no-lag Manta model raced about 3x faster than the
  AI Mantas in the editor's recorded races). A number is the nose follow rate (1.5 = VesselTransformer.LERP_AMOUNT, every
  AI hull): the stick turns a COMMAND direction (the same rate-limited rotate_toward, the same err/20 stick, judged from
  the NOSE as the game judges it today), the nose follows the command first-order each frame,
  fwd = rotate_toward(fwd, cmd, ang(fwd, cmd) x min(1, nose_rate x dt)), and the hull flies along the NOSE. That lagged
  form is the game's steering defect made visible: judged from the nose, the pilot keeps winding the command on while the
  nose is still catching up, the command overshoots, then the nose does, and the hull crosses the ring's plane beside
  the mouth. 0, inf or a rate that closes the whole gap in one step (nose_rate x dt >= 1) is no lag: today's model.
- nose_lead=None is off; a gain, a {"gain", "max_extra_deg"} dict or a NoseLead (ai_nose_lead.NoseLead.from_config) is
  AIPilot.NoseLeadGain on top of the lag. The effective gain is computed ONCE a race from this race's own nose rate
  (AIPilot.EffectiveNoseLeadGain: max(1, gain x 1.5 / nose_rate), and 0 with no lag, so a lag-free race is the same
  race with the lead on or off), and each frame the pilot judges its error from the COMMAND and wants the nose turned
  toward the steer direction by that gain times its error, past it (at most max_extra_deg, never past 170 deg:
  ai_nose_lead.slerp_beyond = AIPilot.NoseLeadDirection); the err/20 stick, the driver's clip and the command update are
  the lines above, untouched. The lead stays on through the orbit break-off's escape leg (the stick is still computed
  there, aimed at the escape bearing), as the game's does; this model has no drift, rail or shell mode to switch it off
  in. The ramp drive still reads the NOSE's angle to the objective and the stick the pilot outputs, as
  RampBoostAIPolicySO.Driver does.
  MEASURED 2026-11-07 (`--nose-lead 2.5`, the default table and --headlong): the lag alone takes the new drive's Regatta
  races from 44.9 / 62.9 / 50.1 / 55.4 s (I1-I4, no missed mouth) to 103.7 / 114.2 / 151.7 / 151.1 s with 0 / 0 / 17 / 21
  missed mouths, and on Headlong's twelve courses it strands the legacy steering on all twelve (nine finishes today) and
  the shipped steering on eleven (five), while headlong_on still finishes all twelve, in 104-276 s with 104 missed mouths
  in all (49-78 s and none today). The 2.5 lead on top of the lag recovers the Regatta races: 87.7 / 91.1 / 99.5 / 109.2 s
  with no missed mouth (the old driver: 80.9 / 93.8 / 100.9 / 96.3 s, but 2 / 3 / 3 / 1 misses); gain 1, judging from the
  command without the pull past the aim, is 88.1 / 138.1 / 143.0 / 189.6 s with 0 / 11 / 8 / 32 misses, so the pull is
  what clears them. On Headlong it does not recover: headlong_on with the lead finishes all twelve in 87-262 s, faster on
  eight courses and 61 s faster over the twelve, but with 120 missed mouths against 104; the legacy steering finishes I1
  seeds 2 and 3 and is stranded on the other ten, the shipped steering on all twelve. The self-test holds the knobs'
  default to today's numbers (TODAY), the lag rows (LAGGED), the Regatta claim with its controls, and the Headlong finding.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import situation_features as sf
from ai_nose_lead import NoseLead, judge_and_want, NOSE_FOLLOW   # the AI nose lead (AIPilot.NoseLeadGain), one shared piece

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
    share, hold, rest, rearm, gain = 0.9, 0.5, 0.3, 0.5, 50
    corner, mincorner, margin, lead, gshare, steerfloor, slew, crel = 600, 160, 120, 0.25, 0.9, 0.3, 4, 30
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

THROUGH_CENTRE = True   # GateRaceController in Regatta (AICommitFromApproachPoint): through the ring's centre

# The gate steering a race flies (GateRaceController.ArmRacer), per mode. STEERING = None is the model as it stood until
# 2026-11-06 (Regatta's 480 / 420 / 320 with the approach-point commit, in EVERY mode, no break-off), kept byte for byte so
# every number the evaluators have published still reproduces. A named profile is the game, line for line:
#   lead / commit / through   the scene's aiApproachLead / aiCommitDistance / aiThroughDistance
#   from_approach             GateRaceController.AICommitFromApproachPoint: commit = min(commit, lead + R/2), the lead capped
#                             at 0.8 x commit (ApproachLeadFor), and a crossing of the ring's plane OUTSIDE the mouth
#                             re-latches the side and calls AIPilot.BreakOffNow
#   centre                    the through point on the line through the ring's centre (Regatta) or on its axis (the rest)
#   break_off                 AIPilot.UpdateOrbitBreak: the turning-circle test (only when not closing, cos 0.5, capture 18)
#                             and the OrbitDetector (360 deg swept with no 10% gain in range), then an escape leg (heading +
#                             0.35 x away) until the objective is max(2R - 18, 1.5 s x speed) away, 0.6-6 s
# Headlong ships with AICommitFromApproachPoint OFF (MinigameHeadlong.unity: 260 / 300 / 220), so after a missed mouth its
# through point is on the pilot's own side and only the break-off gets it out; the legacy model had neither, and stalled.
STEERING = None
STEERING_PROFILES = {
    "regatta":  {"lead": 480.0, "commit": 420.0, "through": 320.0, "from_approach": True,  "centre": True,  "break_off": True},
    "headlong": {"lead": 300.0, "commit": 260.0, "through": 220.0, "from_approach": False, "centre": False, "break_off": True},
    # Headlong with AICommitFromApproachPoint ON (as every other gate race but Headlong has it in code, 2026-11-06): the
    # proposal this model tests, not what ships.
    "headlong_on": {"lead": 300.0, "commit": 260.0, "through": 220.0, "from_approach": True, "centre": True, "break_off": True},
}
CAPTURE, CLOSING_COS, RUN_S, AWAY, BREAK_MIN, BREAK_MAX = 18.0, 0.5, 1.5, 0.35, 0.6, 6.0   # AIPilot's serialized defaults


class Orbit:   # PursuitReachability.OrbitDetector (sweep 360, progress 0.9, jump 1.6), as regatta_line_sim ports it
    def __init__(s): s.reset()
    def reset(s): s.has = False; s.swept = 0.0
    def tick(s, to):
        d = norm(to)
        if d < 1e-6: s.reset(); return False
        b = mul(to, 1 / d)
        if not s.has:
            s.has, s.last, s.best, s.lastd, s.swept = True, b, d, d, 0.0; return False
        if d > s.lastd * 1.6:
            s.last, s.best, s.lastd, s.swept = b, d, d, 0.0; return False
        s.swept += ang(s.last, b); s.last, s.lastd = b, d
        if d < s.best * 0.9:
            s.best, s.swept = d, 0.0; return False
        return s.swept >= 360


def inside_turning_circle(to, heading, R, capture=CAPTURE):   # PursuitReachability.IsInsideTurningCircle
    if R <= 0 or norm(to) < 1e-6: return False
    f = unit(heading)
    lat = sub(to, mul(f, dot(to, f)))
    if norm(lat) < 1e-6: return False
    return norm(sub(to, mul(unit(lat), R))) < max(0.0, R - capture)

def race(kind, intensity, use_next=True, T=400, dt=1/60, verbose=False, nose_rate=None, nose_lead=None):
    """(seconds for 3 laps or None = stalled, rings threaded, top speed); race.breaks / race.misses are set as well.
    nose_rate: None (today: the heading turns at once) or the nose follow rate per second (1.5 = the game's AI hulls);
    nose_lead: AIPilot.NoseLeadGain on top of the lag (None = off, a gain, {"gain", "max_extra_deg"} or a NoseLead)."""
    C=sf.course_from(sf.load_course_js(), intensity)
    gates=C['gates']; n=len(gates); laps=3; total=n*laps
    P=[g['p'] for g in gates]; A=[unit(g['a']) for g in gates]
    pos=add(P[0], mul(A[0], 1500)); fwd=unit(sub(P[0],pos)); speed=50.0
    # the nose lag and the AI nose lead, both OFF by default (the docstring): a rate of 0, inf or one that closes the whole
    # gap in a step is no lag, today's model; the effective gain is AIPilot.EffectiveNoseLeadGain from THIS race's rate, once
    follow=float(nose_rate) if nose_rate is not None and 0<nose_rate<math.inf and nose_rate*dt<1.0 else None
    cmd=list(fwd)   # the stick's command; it is the nose itself until a nose_rate splits them
    lead_knob=NoseLead.from_config(nose_lead)
    g_lead=lead_knob.effective(follow) if follow is not None else 0.0
    pol=Policy(); ex=Exec(); drv=(NewDriver if kind=='new' else OldDriver)(pol,ex)
    G=STEERING_PROFILES[STEERING] if STEERING else None
    idx=0; locked=-1; side=1; t=0.0; stick=0.0; trace=[]; maxv=0
    orbit=Orbit(); extending=False; ext_t=0.0; race.breaks=0; race.misses=0
    while t<T and idx<total:
        g=idx%n; ring=P[g]; ax=A[g]
        omega=(speed*0.2+90)  # deg/s at full stick
        R=speed/math.radians(omega)
        if idx!=locked:
            locked=idx; side=1 if dot(sub(pos,ring),ax)>=0 else -1
            leg_from=P[(idx-1)%n] if idx>0 else pos
            if G is None: lead=min(480,0.4*norm(sub(leg_from,ring)))
            else:
                lead=min(G['lead'],0.4*norm(sub(leg_from,ring)))
                if G['from_approach']: lead=min(lead,0.8*G['commit'])
        if G is None:
            commit=min(420, lead+0.5*R)
            approaching=norm(sub(ring,pos))>commit and norm(sub(add(ring, mul(ax, side*lead)),pos))>max(60,0.5*R)
            through=add(ring, mul(unit(sub(ring,pos)), 320)) if THROUGH_CENTRE else sub(ring, mul(ax, side*320))
        else:
            commit=min(G['commit'], lead+0.5*R) if G['from_approach'] else G['commit']
            approaching=norm(sub(ring,pos))>commit
            through=(add(ring, mul(unit(sub(ring,pos)), G['through'])) if G['centre'] and norm(sub(ring,pos))>1
                     else sub(ring, mul(ax, side*G['through'])))
        if approaching: target=add(ring, mul(ax, side*lead)); after=through
        else: target=through; after=P[(idx+1)%n] if idx+1<total else None
        to=sub(target,pos)
        # AIPilot.UpdateOrbitBreak: while it says break off, the stick flies an escape bearing, not the objective
        steer_to=to
        if G is not None and G['break_off']:
            run=max(max(0.0,2*R-CAPTURE), speed*RUN_S)
            if extending:
                ext_t+=dt
                if (norm(to)>=run and ext_t>=BREAK_MIN) or ext_t>=BREAK_MAX: extending=False; orbit.reset()
            else:
                closing=dot(unit(to),fwd)>CLOSING_COS
                unreachable=(not closing) and inside_turning_circle(to,fwd,R)
                orbiting=orbit.tick(to)
                if unreachable or orbiting: extending=True; ext_t=0.0; race.breaks+=1; orbit.reset()
            if extending: steer_to=mul(unit(add(fwd, mul(unit(to), -AWAY))), 2*run if run>1 else 1000)
        nl = sub(after,target) if (after is not None and use_next) else None
        c={'angle':ang(fwd,to),'stick':stick,'dist':norm(to),'speed':speed,
           'corner': ang(to,nl) if nl is not None else -1}
        # executor's tracked target (last frame's input)
        drv_tracked = getattr(race,'_tracked',speed)
        drv.tick(c,t,dt,drv_tracked)
        # pilot steering: proportional on the angle error, judged from the nose; with the nose lead (AIPilot.TryNoseLead)
        # judged from the COMMAND, wanting the nose turned toward the steer direction by g_lead x its error, past it
        judge,want=judge_and_want(fwd,cmd,steer_to,g_lead,lead_knob.max_extra_deg) if g_lead>0 else (fwd,steer_to)
        err=ang(judge,want); s=min(1.0, err/20.0)
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
        if follow is None:
            fwd=rotate_toward(fwd, want, math.radians((speed*0.2+90)*s)*dt)   # today: the heading IS the command
        else:
            # the game: the stick turns the COMMAND at the hull's rate, the nose follows it first-order
            # (VesselTransformer.LERP_AMOUNT) and the hull flies along the nose
            cmd=rotate_toward(cmd, want, math.radians((speed*0.2+90)*s)*dt)
            fwd=rotate_toward(fwd, cmd, math.radians(ang(fwd,cmd))*min(1.0, follow*dt))
        newpos=add(pos,mul(fwd,speed*dt))
        # threading: crossing the plane inside the mouth
        a0=dot(sub(pos,ring),ax); a1=dot(sub(newpos,ring),ax)
        if a0*a1<=0 and a0!=a1:
            k=a0/(a0-a1); hit=add(pos,mul(sub(newpos,pos),k))
            if norm(sub(hit,ring))<=gates[g]['r']: idx+=1
            else:
                race.misses+=1
                if G is not None and G['from_approach']:   # re-latch from where the pilot is, and AIPilot.BreakOffNow
                    locked=-1
                    if G['break_off']: extending=True; ext_t=0.0; race.breaks+=1; orbit.reset()
        pos=newpos; t+=dt
    race._tracked=50
    return (round(t,1) if idx>=total else None), idx, round(maxv)

def _sweep_one(args):
    corner, gain, crel, margin = args[:4]
    knobs = dict(zip(("nose_rate", "nose_lead"), args[4:6]))   # the nose lag / lead, when the sweep carries them
    Policy.corner, Policy.gain, Policy.crel, Policy.margin = corner, gain, crel, margin
    res = [race('new', I, T=200, **knobs) for I in (1, 2, 3, 4)]
    return sum(r[0] if r[0] else 200 + (24 - r[1]) * 10 for r in res), args, [r[0] for r in res]


def sweep(nose_rate=None, nose_lead=None):
    import itertools
    from concurrent.futures import ProcessPoolExecutor
    grid = list(itertools.product((600, 900, 1300, 3000), (0, 50, 150), (30, 50, 80, 120), (0, 120)))
    if nose_rate is not None or nose_lead is not None:
        if nose_rate is None: nose_rate = NOSE_FOLLOW   # a lead has nothing to do on a lag-free nose: the fleet lag
        lead_cfg = NoseLead.from_config(nose_lead).to_config() if nose_lead is not None else None
        grid = [cell + (nose_rate, lead_cfg) for cell in grid]
    with ProcessPoolExecutor() as ex:
        out = sorted(ex.map(_sweep_one, grid))
    print("total s (I1-I4; a stall costs 200 + 10 per ring left), (cornerSpeed, minSpeedGain, cornerReleaseDegrees, brakeMarginUnits"
          + (", nose_rate, nose_lead" if nose_rate is not None or nose_lead is not None else "") + "), per course")
    for o in out[:12]:
        print(o)


def _headlong_courses():
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Compute", "evaluators"))
    import course_port as cp
    return [(i, k, cp.course(cp.headlong_settings(i), cp.seed_for_intensity(20260915 + 104729 * k, i), rails=False))
            for i in (1, 2, 3, 4) for k in (1, 2, 3)]   # drive_fit's Headlong courses (seeds 1-3)


def _race_on(course, intensity, steering, T=400, nose_rate=None, nose_lead=None):
    global STEERING, THROUGH_CENTRE
    old = (STEERING, THROUGH_CENTRE, sf.load_course_js, sf.course_from)
    STEERING, THROUGH_CENTRE = steering, False
    sf.load_course_js, sf.course_from = (lambda *a, **k: {}), (lambda C, i: course)
    try:
        r = race('new', intensity, T=T, nose_rate=nose_rate, nose_lead=nose_lead)
        return r, race.breaks, race.misses
    finally:
        STEERING, THROUGH_CENTRE, sf.load_course_js, sf.course_from = old


def _knob_rows(nose_rate, nose_lead):
    """The extra rows --nose-rate / --nose-lead add to a table: (label, race kwargs) for the lag alone and, with a lead,
    the lead on top of it. A lead without a rate is raced at the fleet lag (a lag-free nose has nothing to lead)."""
    if nose_rate is None and nose_lead is None:
        return []
    rate = NOSE_FOLLOW if nose_rate is None else nose_rate
    rows = [(f"nose lag {rate:g}/s", dict(nose_rate=rate))]
    if nose_lead is not None:
        lead = NoseLead.from_config(nose_lead)
        rows.append((f"nose lag {rate:g}/s + AI nose lead {lead.gain:g} (+{lead.max_extra_deg:g} deg max, effective gain "
                     f"{lead.effective(rate):g})", dict(nose_rate=rate, nose_lead=lead)))
    return rows


def headlong_table(nose_rate=None, nose_lead=None):
    print("Policy defaults (the job's init) on Headlong I1-I4 x seeds 1-3: seconds / rings of 24 (break-offs, misses)")
    courses = _headlong_courses()
    for label, kw in [("", {})] + _knob_rows(nose_rate, nose_lead):
        if label:
            print(f"-- {label}:")
        for steering in (None, "headlong", "headlong_on"):
            row = []
            for i, k, c in courses:
                (t, rings, _), b, m = _race_on(c, i, steering, **kw)
                row.append(f"I{i}s{k} {t}/{rings} ({b},{m})")
            print(f"{str(steering):12s}", "  ".join(row))


# Today's numbers with both knobs OFF (2026-11-07, `python3 Tools/Build/rhino_drive_sim.py`): (kind, intensity, use_next) ->
# (seconds, rings, top speed). The self-test holds the knobs' default to them, so the lag and the lead cannot move a
# number nobody switched on.
TODAY = {
    ('old', 1, True): (43.8, 24, 813), ('new', 1, True): (44.9, 24, 655), ('new', 1, False): (49.9, 24, 589),
    ('old', 2, True): (58.8, 24, 813), ('new', 2, True): (62.9, 24, 655), ('new', 2, False): (62.6, 24, 589),
    ('old', 3, True): (53.7, 24, 813), ('new', 3, True): (50.1, 24, 655), ('new', 3, False): (55.5, 24, 618),
    ('old', 4, True): (51.8, 24, 813), ('new', 4, True): (55.4, 24, 655), ('new', 4, False): (57.3, 24, 596),
}
# ...and the new drive under the game's nose lag (nose_rate 1.5, lead off; `--nose-rate 1.5`): today's steering through
# the lag, the defect this model did not show before. intensity -> ((seconds, rings, top speed), missed mouths)
LAGGED = {1: ((103.7, 24, 655), 0), 2: ((114.2, 24, 655), 0), 3: ((151.7, 24, 655), 17), 4: ((151.1, 24, 655), 21)}


def self_test():
    bad = []
    o = Orbit()
    if not any(o.tick([100 * math.cos(math.radians(a)), 0, 100 * math.sin(math.radians(a))]) for a in range(0, 400, 2)):
        bad.append("orbit detector never fires on a constant-range sweep")
    o = Orbit()
    if any(o.tick([0, 0, 1000 - 5 * i]) for i in range(150)):
        bad.append("orbit detector fired on a straight approach")
    if inside_turning_circle([0, 0, 500], [0, 0, 1], 100) or not inside_turning_circle([60, 0, 40], [0, 0, 1], 100):
        bad.append("turning-circle test")
    courses = _headlong_courses()
    i4s3 = next(c for i, k, c in courses if (i, k) == (4, 3))
    # the legacy model still reproduces what was published on it (the job's init stalls at ring 2 of I4 seed 3)
    (t, rings, _), _, _ = _race_on(i4s3, 4, None)
    if t is not None or rings != 2:
        bad.append(f"legacy model moved: I4 seed 3 gave {t} / {rings} (published: a stall at 2 rings)")
    # negative control: Headlong's shipped steering strands the pilot after a missed mouth, break-off and all
    (t, rings, _), b, m = _race_on(i4s3, 4, "headlong")
    if t is not None or m == 0:
        bad.append(f"Headlong's shipped steering no longer stalls after a miss on I4 seed 3 ({t}, {rings} rings, {m} misses)")
    # the fix: with the approach-point commit and the re-latch, the same drive finishes all twelve courses
    dnf = [(i, k) for i, k, c in courses if _race_on(c, i, "headlong_on")[0][0] is None]
    if dnf:
        bad.append(f"headlong_on did not finish {dnf}")
    # the nose lag and the AI nose lead (ai_nose_lead.py) are OFF by default: with the knobs unset every race of the
    # default table is today's (TODAY)...
    for (kind, I, nxt), want in TODAY.items():
        got = race(kind, I, use_next=nxt)
        if got != want:
            bad.append(f"{kind} I{I} use_next={nxt} with the knobs unset: {got} is not today's {want}")
    # ...NoseLead() (gain 0) and the other off forms are the unset knob, and on a lag-free nose (nose_rate None, 0, -1,
    # inf or a rate that closes the whole gap in one step: today's model) the lead has nothing to lead and changes nothing
    for I in (1, 2, 3, 4):
        for kw in ({"nose_lead": NoseLead()}, {"nose_lead": 2.5}, {"nose_rate": 0, "nose_lead": 2.5}):
            if race('new', I, **kw) != TODAY[('new', I, True)]:
                bad.append(f"new I{I} with {kw}: not today's race {TODAY[('new', I, True)]}")
    for kw in ({"nose_lead": 0}, {"nose_lead": {}}, {"nose_lead": False}, {"nose_rate": -1},
               {"nose_rate": float("inf"), "nose_lead": 2.5}, {"nose_rate": 60.0, "nose_lead": 2.5}):
        if race('new', 1, **kw) != TODAY[('new', 1, True)]:
            bad.append(f"new I1 with {kw}: not today's race")
    # the game's nose lag (nose_rate 1.5), lead off, is today's steering through the lag and shows the defect: slower at
    # every intensity, 17 / 21 missed mouths at I3 / I4 (LAGGED)...
    lagged = {}
    for I, want in LAGGED.items():
        got = race('new', I, nose_rate=1.5); lagged[I] = (got, race.misses)
        if lagged[I] != want:
            bad.append(f"new I{I} under the nose lag: {lagged[I]} is not {want}")
        if got[0] is None or got[0] <= TODAY[('new', I, True)][0]:
            bad.append(f"new I{I}: the nose lag did not slow the race ({got} vs today's {TODAY[('new', I, True)]})")
    if race('new', 3, nose_rate=1.5, nose_lead=NoseLead()) != lagged[3][0] or race.misses != lagged[3][1]:
        bad.append("NoseLead() (gain 0) is not the unset lead under the lag")
    # ...and the AI nose lead 2.5 on top of the lag recovers: faster than the lag alone at every intensity, no missed mouth
    led = {}
    for I in (1, 2, 3, 4):
        got = race('new', I, nose_rate=1.5, nose_lead=2.5); led[I] = (got, race.misses)
        if got[0] is None or got[0] >= lagged[I][0][0] or race.misses != 0:
            bad.append(f"new I{I}: the 2.5 lead did not recover the lagged race: {led[I]} vs the lag alone {lagged[I]}")
    if race('new', 1, nose_rate=1.5, nose_lead={"nose_lead": 2.5}) != led[1][0]:
        bad.append("{'nose_lead': 2.5} is not the 2.5 lead")
    # the controls: gain 1 (judged from the command, with no pull past the aim) is weaker at I3 and I4, slower with more
    # misses; it IS the lead with no room past the aim (max_extra_deg 0, the same wanted direction); and on a 9/s nose the
    # 2.5 gain floors to 1 (AIPilot.EffectiveNoseLeadGain from the race's own rate) and still changes that race
    for I in (3, 4):
        g1 = race('new', I, nose_rate=1.5, nose_lead=1.0); g1m = race.misses
        if g1[0] is None or g1[0] <= led[I][0][0] or g1m < led[I][1] + 5:
            bad.append(f"new I{I}: gain 1.0 (no pull past the aim) rivals the 2.5 lead: {g1} / {g1m} misses vs {led[I]}")
        e0 = race('new', I, nose_rate=1.5, nose_lead=NoseLead(2.5, 0.0)); e0m = race.misses
        if (e0, e0m) != (g1, g1m):
            bad.append(f"new I{I}: the lead with no room past the aim (max_extra_deg 0) is not gain 1.0")
    quick, quick_led = race('new', 1, nose_rate=9.0), race('new', 1, nose_rate=9.0, nose_lead=2.5)
    if quick_led != race('new', 1, nose_rate=9.0, nose_lead=1.0) or quick_led == quick:
        bad.append("on a 9/s nose the 2.5 lead is not gain 1 judged from the command, or it did not change the race")
    # what the lead does NOT do here (the docstring, 2026-11-07): on Headlong I3 seed 2 under the lag, headlong_on finishes
    # in 275.8 s with 21 missed mouths, and the 2.5 lead finishes it with about as many: the lead clears the Regatta misses,
    # not Headlong's. A port that changes this re-measures the docstring.
    i3s2 = next(c for i, k, c in courses if (i, k) == (3, 2))
    (t, rings, _), _, m = _race_on(i3s2, 3, "headlong_on", nose_rate=1.5)
    if (t, rings, m) != (275.8, 24, 21):
        bad.append(f"headlong_on I3 seed 2 under the lag moved: {t} / {rings} rings / {m} misses (was 275.8 / 24 / 21)")
    (t2, _, _), _, m2 = _race_on(i3s2, 3, "headlong_on", nose_rate=1.5, nose_lead=2.5)
    if t2 is None or m2 < 0.75 * m:
        bad.append(f"the 2.5 lead now clears Headlong I3 seed 2's lag misses ({t2} s / {m2} vs {t} s / {m}): re-measure the docstring")
    print("rhino_drive_sim self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


def knob_args(argv):
    """--nose-rate R (the nose follow rate per second; 1.5 is the game's AI hulls) and --nose-lead G[,EXTRA] (the AI nose
    lead's gain at the fleet lag and, after a comma, how far past the aim it may place the command, 40 deg unless given),
    or None for each: off, today's model."""
    rate = float(argv[argv.index("--nose-rate") + 1]) if "--nose-rate" in argv else None
    lead = None
    if "--nose-lead" in argv:
        parts = argv[argv.index("--nose-lead") + 1].split(",")
        lead = NoseLead(float(parts[0]), float(parts[1]) if len(parts) > 1 else None)
    return rate, lead


def _table_row(I, **kw):
    cells = []
    for name, kind, nxt in (("old", 'old', True), ("new", 'new', True), ("new without corner info", 'new', False)):
        r = race(kind, I, use_next=nxt, **kw)
        cells.append(f" {name} {r} m{race.misses}")
    return f"I{I}:" + " ".join(cells)


if __name__ == '__main__':
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    nose_rate, nose_lead = knob_args(sys.argv)
    if "--headlong" in sys.argv:
        headlong_table(nose_rate, nose_lead); sys.exit(0)
    if "--sweep" in sys.argv:
        sweep(nose_rate, nose_lead)
    else:
        print("(seconds for 3 laps or None = stalled, rings threaded, top speed)")
        for I in (1, 2, 3, 4):
            print(f"I{I}: old", race('old', I), " new", race('new', I), " new without corner info", race('new', I, use_next=False))
        for label, kw in _knob_rows(nose_rate, nose_lead):
            print(f"-- {label} (m = missed mouths):")
            for I in (1, 2, 3, 4):
                print(_table_row(I, **kw))
