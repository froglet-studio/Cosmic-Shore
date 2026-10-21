using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Pistol's AI: inputs only (it presses what a player presses). Tiny Wings dives (heavy on the way down, light on
    /// the way up), launches at the densest cluster it can see (primed blocks count triple), ghosts through the Goby's
    /// trail, and fires the Snap (the vortex ring) into a crowd. Ported LINE FOR LINE from the lab's <c>pistolAI</c>
    /// (<c>Docs/Studios/VesselStudio/goby.html</c>), with the lab's Easy / Medium / Hard levels (late notice, misjudged
    /// height: the Skim Race's <c>SkimRaceDifficulty.asset</c> numbers). No scene: the caller supplies what the Pistol
    /// sees (the lab's <c>pistolScan</c>) through the scan callback.
    /// </summary>
    public sealed class PistolAI
    {
        /// <summary>The lab's <c>DIFF</c>: how late the AI notices a new target, and how often it misjudges its height.</summary>
        public readonly struct Difficulty
        {
            public readonly float Late, Mis;
            public Difficulty(float late, float mis) { Late = late; Mis = mis; }
            public static readonly Difficulty Hard = new(0f, 0f), Medium = new(0.25f, 0.045f), Easy = new(0.5f, 0.099f);
        }

        /// <summary>One prism the Pistol can see, in its plane (the lab's <c>B.vis</c> entries).</summary>
        public struct Seen { public int Id; public float X, H, D; public bool Primed, Friendly; }

        /// <summary>The lab's numbers this AI reads.</summary>
        public struct Settings { public float pLaunchRange, pVision, pVortexR, pVortexCapture, aiHighDive; }

        /// <summary>The lab's <c>mulberry32</c>, bit for bit (so a seed plays the same run in both).</summary>
        public sealed class Mulberry32
        {
            uint _a;
            public Mulberry32(uint seed) { _a = seed; }
            public float Next()
            {
                unchecked
                {
                    _a += 0x6D2B79F5u; uint t = _a;
                    t = (t ^ (t >> 15)) * (1u | t);
                    t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                    return (float)((t ^ (t >> 14)) / 4294967296.0);
                }
            }
        }

        public readonly List<Seen> Vis = new();
        readonly Mulberry32 _rng;
        float _scanAt, _noticeAt, _misDy; int _key = -1; bool _mis, _outGhost, _outVortex;

        /// <summary>For the harness's readout: the AI is still "noticing" its target, or has misjudged it.</summary>
        public bool Unnoticed { get; private set; }
        public bool Misjudged { get; private set; }
        /// <summary>Where the AI aimed its last launch in the plane (x, h), or null.</summary>
        public Vector2? LaunchAim { get; private set; }

        public PistolAI(uint seed) { _rng = new Mulberry32(seed); }

        /// <summary>Forget held presses (the lab does this on a swap: a press carried across the swap is not a press).</summary>
        public void ReleaseAll() { _outGhost = _outVortex = false; }

        /// <summary>The lab's <c>pistolAI</c>. <paramref name="scan"/> fills the list it is handed with what the Pistol
        /// sees (it is called at most every 0.1 s, on an emptied list).</summary>
        public PistolBodyMath.Input Think(PistolBodyMath.State p, PistolRibbon rb, float t, in Difficulty D, in Settings s, Action<List<Seen>> scan)
        {
            float gy = rb.Ground(p.x, out float slope);
            rb.Ground(p.x + Mathf.Max(25f, (p.grounded ? p.v : p.vx) * 0.3f), out float aSlope);
            // Tiny Wings: heavy on the way down, light on the way up
            bool dive = p.grounded ? slope < -0.02f : (p.vy < 0f && aSlope < -0.02f) || p.y - gy > s.aiHighDive;
            if (t >= _scanAt) { _scanAt = t + 0.1f; Vis.Clear(); scan(Vis); }
            var inp = new PistolBodyMath.Input { Dive = dive ? 1f : 0f };
            LaunchAim = null;
            if (p.launch > 0 && t - p.lastLaunchAt > 1.2f)
            {
                int best = -1, bs = 0;
                for (int j = 0; j < Vis.Count; j++)
                {
                    var v = Vis[j];
                    if (v.Friendly || v.X < p.x + 30f || v.X > p.x + s.pLaunchRange || Mathf.Abs(v.H - p.y) > 160f) continue;
                    int sc = v.Primed ? 3 : 0;
                    for (int o = 0; o < Vis.Count; o++) { var w = Vis[o]; if (!w.Friendly && Hypot(w.X - v.X, w.H - v.H) < 30f) sc++; }
                    if (sc > bs) { bs = sc; best = j; }
                }
                if (best >= 0 && bs >= 3)
                {
                    var b = Vis[best];
                    if (b.Id != _key) { _key = b.Id; _noticeAt = t + (D.Late > 0f ? D.Late * (0.5f + _rng.Next()) : 0f); _mis = _rng.Next() < D.Mis; _misDy = (_rng.Next() < 0.5f ? -1f : 1f) * 45f; }
                    Unnoticed = t < _noticeAt; Misjudged = _mis;
                    if (!Unnoticed) { float ay = b.H + (_mis ? _misDy : 0f); inp.HasLaunch = true; inp.Launch = new Vector2(b.X - p.x, ay - p.y); LaunchAim = new Vector2(b.X, ay); }
                }
            }
            bool ghost = false;
            if (t >= p.ghostAt)
                foreach (var v in Vis)
                    if (v.Friendly && Mathf.Abs(v.D) <= s.pVision && v.X > p.x && v.X - p.x < 60f && Mathf.Abs(v.H - p.y) < 25f) { ghost = true; break; }
            bool vortex = false; Vector2 aim = default;
            if (t >= p.vortexAt)
            {
                int best = -1, bs = 0;
                for (int j = 0; j < Vis.Count; j++)
                {
                    var v = Vis[j];
                    if (v.Friendly || v.X < p.x + 20f || v.X > p.x + 200f || Mathf.Abs(v.H - p.y) > 120f) continue;
                    int sc = 0;
                    for (int o = 0; o < Vis.Count; o++) { var w = Vis[o]; if (!w.Friendly && Hypot(w.X - v.X, w.H - v.H) < s.pVortexR + s.pVortexCapture) sc++; }
                    if (sc > bs) { bs = sc; best = j; }
                }
                if (best >= 0 && bs >= 4) { vortex = true; aim = new Vector2(Vis[best].X - p.x, Vis[best].H - p.y); }
            }
            // a press is a press: the AI lets go between shots
            inp.Ghost = ghost && !_outGhost;
            inp.Snap = vortex && !_outVortex; inp.SnapAim = inp.Snap ? aim : default;
            _outGhost = ghost; _outVortex = vortex;
            return inp;
        }

        /// <summary>How far (3D) from the Pistol the lab's scan looks before mapping a prism onto the ribbon.</summary>
        public static float ScanReach(in Settings s) => s.pLaunchRange + s.pVision + 60f;

        /// <summary>The lab's <c>pistolScan</c> for one prism: in <paramref name="reach"/> of the Pistol, on its screen
        /// (the ribbon from 80 u behind it to its launch range ahead), in its band, or primed (primed blocks show anywhere
        /// on the screen).</summary>
        public static bool TrySee(PistolRibbon rb, PistolBodyMath.State p, int id, Vector3 w, bool primed, bool friendly, float reach, in Settings s, out Seen seen)
        {
            seen = default;
            if ((w - p.wpos).sqrMagnitude > reach * reach) return false;
            var m = rb.Map(w, p.x - 80f, p.x + s.pLaunchRange + 60f); if (!m.Ok) return false;
            if (Mathf.Abs(m.D) > s.pVision && !primed) return false;
            seen = new Seen { Id = id, X = m.X, H = m.H, D = m.D, Primed = primed, Friendly = friendly };
            return true;
        }

        static float Hypot(float a, float b) => Mathf.Sqrt(a * a + b * b);
    }
}
