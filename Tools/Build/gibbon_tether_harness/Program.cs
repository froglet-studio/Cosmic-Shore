using System;
using UnityEngine;
using CosmicShore.Gameplay;

class Sim {
    // Gibbon tuning candidates (these become the C# field initializers + prefab values)
    public float stiffness = 9f;       // 1/s^2 on stretch
    public float damping   = 3.2f;     // 1/s on the RADIAL component
    public float maxStretch = 26f;
    public float breakStretch = 70f;
    public float reelRate = 34f;       // u/s of rest-length contraction
    public float speedCap = 260f;
    public float cruise   = 70f;
    public float maxSwingRate = 3.2f;  // rad/s
    public float hullR = 6f, anchorR = 3f, clearance = 4f;

    public Vector3 pos, vel;
    public Vector3 anchor; public float rest; public bool live;
    public float peakTension;

    public void Fire(Vector3 dir, float length) {
        anchor = pos + dir.normalized * length;
        rest = length; live = true;
    }

    // Mirrors GibbonVesselTransformer: external accel + reel, in the same order.
    public TetherSolver.LineStep Tick(float dt, bool reel) {
        if (!live) { pos = pos + vel*dt; return TetherSolver.LineStep.Slack; }
        if (reel) {
            float tan = TetherSolver.TangentialSpeed(pos, vel, anchor);
            float floorLen = TetherSolver.RestLengthFloor(hullR, anchorR, clearance, tan, maxSwingRate);
            float step = TetherSolver.ReelStep(vel.magnitude, speedCap, reelRate, dt);
            rest = TetherSolver.ApplyReel(rest, step, floorLen);
        }
        var s = TetherSolver.Step(pos, vel, anchor, rest, stiffness, damping, maxStretch, breakStretch, dt);
        if (s.Tension01 > peakTension) peakTension = s.Tension01;
        vel = vel + s.DeltaV;
        pos = pos + vel*dt;
        if (s.Break) live = false;
        return s;
    }
}

class P {
  static int fails = 0;
  static void Check(string name, bool ok, string detail) {
    Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + name + "   " + detail);
    if (!ok) fails++;
  }

  // Run a swing: fire laterally, reel for `hold` seconds, report.
  static (float endSpeed, float sweepDeg, float minLen, bool broke, float peakT)
  Swing(float dt, float entrySpeed, float hold, bool reel, float beamLen=140f) {
    var s = new Sim();
    s.pos = new Vector3(0,0,0);
    s.vel = new Vector3(0,0,entrySpeed);          // flying +Z
    s.Fire(new Vector3(1,0,0), beamLen);          // beam to the RIGHT (+X)
    Vector3 r0 = (s.pos - s.anchor).normalized;
    float minLen = 1e9f; bool broke=false;
    int steps = (int)(hold/dt);
    for (int i=0;i<steps;i++){
      var st = s.Tick(dt, reel);
      if (st.Break) broke = true;
      float L=(s.pos-s.anchor).magnitude; if (L<minLen) minLen=L;
      if (!s.live) break;
    }
    Vector3 r1 = (s.pos - s.anchor).normalized;
    float cos = Mathf.Clamp(Vector3.Dot(r0,r1),-1f,1f);
    float sweep = (float)(Math.Acos(cos)*180.0/Math.PI);
    return (s.vel.magnitude, sweep, minLen, broke, s.peakTension);
  }

  static void Main() {
    Console.WriteLine("=== GIBBON TETHER — driving the SHIPPED TetherSolver.cs ===\n");

    // T1 — a rope is silent while slack.
    var slack = TetherSolver.Step(new Vector3(0,0,0), new Vector3(0,0,70), new Vector3(100,0,0),
                                  200f, 9f, 3.2f, 26f, 70f, 1f/60f);
    Check("T1 slack line applies no force",
          slack.DeltaV.magnitude == 0f && slack.Tension01 == 0f, $"dv={slack.DeltaV.magnitude}");

    // T2 — NO REEL means NO free energy. A pure rope must not accelerate you.
    var a = Swing(1f/60f, 70f, 6f, reel:false);
    Check("T2 no-reel swing cannot gain speed",
          a.endSpeed <= 70f + 0.5f, $"70 -> {a.endSpeed:F2} u/s, swept {a.sweepDeg:F0}deg");

    // T3 — the reel IS the accelerator, and it beats cruise.
    var b = Swing(1f/60f, 70f, 2.5f, reel:true);
    Check("T3 reeling a swing exceeds flying speed",
          b.endSpeed > 70f*1.5f, $"70 -> {b.endSpeed:F2} u/s in 2.5s, swept {b.sweepDeg:F0}deg, minLen {b.minLen:F1}");

    // T4 — frame-rate identity. 30 vs 120 Hz must fly the same arc.
    var lo = Swing(1f/30f, 70f, 2.5f, reel:true);
    var hi = Swing(1f/120f,70f, 2.5f, reel:true);
    float dSpd = Mathf.Abs(lo.endSpeed-hi.endSpeed)/hi.endSpeed;
    float dSwp = Mathf.Abs(lo.sweepDeg-hi.sweepDeg);
    Check("T4 30Hz vs 120Hz agree",
          dSpd < 0.03f && dSwp < 4f, $"speed {dSpd*100:F2}% ({lo.endSpeed:F1} vs {hi.endSpeed:F1}), heading {dSwp:F2}deg");

    // T5 — the cap actually caps, over a long hold.
    var c = Swing(1f/60f, 70f, 25f, reel:true);
    Check("T5 speed cap holds over a long reel",
          c.endSpeed <= 260f*1.02f, $"{c.endSpeed:F1} u/s vs cap 260");

    // T6 — a hitch frame cannot inject energy (the explicit-Euler failure mode).
    {
      var s = new Sim(); s.pos=new Vector3(0,0,0); s.vel=new Vector3(0,0,70); s.Fire(new Vector3(1,0,0),140f);
      float worst = 0f; var rnd = new Random(7);
      for (int i=0;i<4000;i++){
        float dt = (i%20==0) ? 0.25f : 1f/60f;    // a 4 fps hitch every 20 frames
        s.Tick(dt, true);
        if (s.vel.magnitude > worst) worst = s.vel.magnitude;
        if (!s.live) break;
      }
      Check("T6 hitch frames cannot explode the spring",
            worst <= 260f*1.10f && !float.IsNaN(worst), $"worst speed over 4000 hitchy frames = {worst:F1}");
    }

    // T7 — the break rule fires instead of unbounded tension.
    {
      var s = new Sim(); s.pos=new Vector3(0,0,0); s.vel=new Vector3(0,0,900); s.Fire(new Vector3(1,0,0),60f);
      bool broke=false; for(int i=0;i<600 && s.live;i++){ if(s.Tick(1f/60f,false).Break) broke=true; }
      Check("T7 overloading a line breaks it", broke, "900 u/s into a 60u line snapped it");
    }

    // T8 — TWO lines are just addition, and they brake rather than explode.
    {
      var s = new Sim(); s.pos=new Vector3(0,0,0); s.vel=new Vector3(0,0,180);
      Vector3 aL = new Vector3(-140,0,60), aR = new Vector3(140,0,60);
      float restL=(s.pos-aL).magnitude, restR=(s.pos-aR).magnitude;
      float peak=0f;
      for(int i=0;i<300;i++){
        float dt=1f/60f;
        var l = TetherSolver.Step(s.pos,s.vel,aL,restL,9f,3.2f,26f,70f,dt);
        var r = TetherSolver.Step(s.pos,s.vel,aR,restR,9f,3.2f,26f,70f,dt);
        s.vel = s.vel + l.DeltaV + r.DeltaV;      // <- the entire two-tether case
        s.pos = s.pos + s.vel*dt;
        if (s.vel.magnitude>peak) peak=s.vel.magnitude;
      }
      Check("T8 two lines compose by addition, bounded",
            peak <= 180f*1.05f && !float.IsNaN(s.vel.magnitude),
            $"peak {peak:F1} from a 180 u/s entry between two anchors");
    }

    // T9 — the floor is a STOP, never a pay-out (the previous build's chatter bug).
    {
      float prev=40f, len=40f; bool grew=false;
      var s=new Sim(); s.pos=new Vector3(0,0,0); s.vel=new Vector3(0,0,250); s.Fire(new Vector3(1,0,0),40f);
      for(int i=0;i<900 && s.live;i++){ s.Tick(1f/60f,true); if(s.rest>prev+1e-4f) grew=true; prev=s.rest; len=s.rest; }
      Check("T9 the rest-length floor never pays a line out", !grew, $"rest fell monotonically to {len:F1}");
    }

    // T10 — the tangential component is untouched (this is what conserves the swing).
    {
      Vector3 pos=new Vector3(0,0,0), anc=new Vector3(100,0,0), v=new Vector3(0,0,70);
      float tanBefore = TetherSolver.TangentialSpeed(pos,v,anc);
      var st = TetherSolver.Step(pos,v,anc,40f,9f,3.2f,26f,70f,1f/60f);
      float tanAfter = TetherSolver.TangentialSpeed(pos,v+st.DeltaV,anc);
      Check("T10 a taut line does no tangential work",
            Mathf.Abs(tanAfter-tanBefore) < 1e-3f, $"{tanBefore:F4} -> {tanAfter:F4} u/s");
    }

    // T11 - with a long line and high angular momentum the CAP is what binds, not the floor.
    {
      var s = new Sim(); s.pos=new Vector3(0,0,0); s.vel=new Vector3(0,0,230); s.Fire(new Vector3(1,0,0),900f);
      float peak=0f; for(int i=0;i<60*60 && s.live;i++){ s.Tick(1f/60f,true); if(s.vel.magnitude>peak) peak=s.vel.magnitude; }
      Check("T11 the cap binds when the floor does not",
            peak <= 260f*1.02f, $"peak {peak:F1} u/s against cap 260");
    }

    Console.WriteLine("\n--- FEEL LADDER: four reeled swings, released at ~90deg of sweep ---");
    {
      var s = new Sim(); s.pos=new Vector3(0,0,0); s.vel=new Vector3(0,0,70f);
      Console.WriteLine($"  start                          {s.vel.magnitude,6:F1} u/s");
      for (int grab=1; grab<=4; grab++) {
        Vector3 side = (grab%2==1) ? new Vector3(1,0,0) : new Vector3(-1,0,0);
        Vector3 fwd = s.vel.normalized;
        Vector3 lat = Vector3.Cross(new Vector3(0,1,0), fwd).normalized;
        if (grab%2==0) lat = -lat;
        s.Fire(lat, 140f);
        Vector3 r0=(s.pos-s.anchor).normalized; float t=0f;
        for(int i=0;i<1200 && s.live;i++){
          s.Tick(1f/60f,true); t+=1f/60f;
          Vector3 r1=(s.pos-s.anchor).normalized;
          if (Math.Acos(Mathf.Clamp(Vector3.Dot(r0,r1),-1,1))*180.0/Math.PI >= 90.0) break;
        }
        s.live=false;
        Console.WriteLine($"  grab {grab} ({t:F2}s on the line)        {s.vel.magnitude,6:F1} u/s");
      }
      Check("LADDER four grabs beat cruise by >2x", s.vel.magnitude > 70f*2f, $"final {s.vel.magnitude:F1} u/s");
    }

    Console.WriteLine(fails==0 ? "\nALL GREEN" : $"\n{fails} FAILING");
    Environment.Exit(fails==0?0:1);
  }
}
