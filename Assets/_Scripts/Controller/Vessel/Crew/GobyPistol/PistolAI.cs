using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Pistol's AI: inputs only (it presses what a player presses). Tiny Wings dives (<see cref="TinyWingsDive"/>: down
    /// the slopes, let go up them, and in the air dive only when that lands it smoother on the back of a hill), jumps (only
    /// with the aimed jump on) at the densest mass ahead in its plane, fires the Snap (the vortex ring) into a crowd, and fires
    /// it at once when the Goby's locked rival is on its screen (the ring then homes on it). Easy / Medium / Hard are the
    /// Vessel Studio's universal levels (late notice and a misjudged height, <c>SkimRaceDifficulty.asset</c>). No scene: the
    /// caller supplies what the Pistol sees in <see cref="Vis"/>.
    /// </summary>
    public sealed class PistolAI
    {
        /// <summary>How late the AI notices a new target, and how often it misjudges its height.</summary>
        public readonly struct Difficulty
        {
            public readonly float Late, Mis;
            public Difficulty(float late, float mis) { Late = late; Mis = mis; }
            public static readonly Difficulty Hard = new(0f, 0f), Medium = new(0.25f, 0.045f), Easy = new(0.5f, 0.099f);
        }

        /// <summary>One prism the Pistol can see, in its frame (x along, y up, d out of the plane).</summary>
        public struct Seen { public float X, Y, D; }

        /// <summary>The numbers this AI reads.</summary>
        public struct Settings { public float pLaunchRange, pVortexR, pVortexCapture, bodyHalfDepth, pGravity, pDiveMul; public bool aimedJump; }

        /// <summary>The lab's <c>mulberry32</c>, bit for bit.</summary>
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
        float _noticeAt, _misDy; int _key = -1; bool _mis, _outVortex;

        public PistolAI(uint seed) { _rng = new Mulberry32(seed); }

        /// <summary>
        /// How a Tiny Wings player dives (1) or lets go (0). On the ground: dive down every slope, let go up every slope, so
        /// the valley's speed carries it up and the crest throws it. In the air: dive only if falling faster lands it at a
        /// smaller angle to the ground (the back of a hill: a smooth slide) than falling as it is.
        /// </summary>
        public static float TinyWingsDive(PistolBodyMath.State p, in PistolPlane.Frame f, float gravity, float diveMul)
        {
            if (p.grounded) { PistolPlane.Ground(f, p.x, out float slope); return slope < -0.02f ? 1f : 0f; }
            return LandingAngle(f, p.x, p.y, p.vx, p.vy, gravity * diveMul) < LandingAngle(f, p.x, p.y, p.vx, p.vy, gravity) ? 1f : 0f;
        }

        /// <summary>The angle, degrees, between the velocity and the ground where a fall from (x, y) at (vx, vy) under
        /// <paramref name="gravity"/> lands (90 if it does not land within four seconds).</summary>
        static float LandingAngle(in PistolPlane.Frame f, float x, float y, float vx, float vy, float gravity)
        {
            const float h = 1f / 60f;
            for (int i = 0; i < 240; i++)
            {
                vy -= gravity * h; x += vx * h; y += vy * h;
                float yg = PistolPlane.Ground(f, x, out float m);
                if (y > yg) continue;
                float c = 1f / Mathf.Sqrt(1f + m * m), sp = Mathf.Sqrt(vx * vx + vy * vy), vt = vx * c + vy * m * c;
                return sp > 1e-6f ? Mathf.Acos(Mathf.Clamp(vt / sp, -1f, 1f)) * Mathf.Rad2Deg : 0f;
            }
            return 90f;
        }

        /// <summary>Forget held presses (a press carried across a seat change is not a press).</summary>
        public void ReleaseAll() { _outVortex = false; }

        /// <summary>One step's input. <paramref name="lockedInView"/>: the Goby's locked rival is on the Pistol's screen.</summary>
        public PistolBodyMath.Input Think(PistolBodyMath.State p, in PistolPlane.Frame f, float t, Difficulty lvl, in Settings s, bool lockedInView)
        {
            var inp = new PistolBodyMath.Input();
            inp.Dive = TinyWingsDive(p, f, s.pGravity, s.pDiveMul);

            // the jump (only with the aimed jump on): at the densest mass ahead in reach. Otherwise the AI flies as a player
            // does, off the hills, by letting go of the dive on an upslope
            if (s.aimedJump && p.launch > 0 && t - p.lastLaunchAt > 1.2f)
            {
                int best = -1, bestScore = 0;
                for (int i = 0; i < Vis.Count; i++)
                {
                    var v = Vis[i];
                    if (Mathf.Abs(v.D) > s.bodyHalfDepth || v.X < p.x + 30f || v.X > p.x + s.pLaunchRange || Mathf.Abs(v.Y - p.y) > 160f) continue;
                    int score = 0;
                    for (int j = 0; j < Vis.Count; j++) if (Mathf.Abs(Vis[j].X - v.X) < 30f && Mathf.Abs(Vis[j].Y - v.Y) < 30f) score++;
                    if (score > bestScore) { bestScore = score; best = i; }
                }
                if (best >= 0 && bestScore >= 3)
                {
                    if (best != _key) { _key = best; _noticeAt = t + (lvl.Late > 0f ? lvl.Late * (0.5f + _rng.Next()) : 0f); _mis = _rng.Next() < lvl.Mis; _misDy = (_rng.Next() < 0.5f ? -1f : 1f) * 45f; }
                    if (t >= _noticeAt) { var v = Vis[best]; inp.HasLaunch = true; inp.Launch = new Vector2(v.X - p.x, v.Y + (_mis ? _misDy : 0f) - p.y); }
                }
            }

            // the Snap: at once at a locked rival on screen (the ring homes), else into a crowd ahead
            bool vortex = false; Vector2 aim = new(1f, 0f);
            if (t >= p.vortexAt)
            {
                if (lockedInView) vortex = true;
                else
                {
                    int bs = 0;
                    for (int i = 0; i < Vis.Count; i++)
                    {
                        var v = Vis[i];
                        if (v.X < p.x + 20f || v.X > p.x + 200f || Mathf.Abs(v.Y - p.y) > 120f) continue;
                        int sc = 0;
                        for (int j = 0; j < Vis.Count; j++)
                            if (Mathf.Abs(Vis[j].X - v.X) < s.pVortexR + s.pVortexCapture && Mathf.Abs(Vis[j].Y - v.Y) < s.pVortexR + s.pVortexCapture) sc++;
                        if (sc > bs) { bs = sc; aim = new Vector2(v.X - p.x, v.Y - p.y); }
                    }
                    vortex = bs >= 4;
                }
            }
            inp.Snap = vortex && !_outVortex; inp.SnapAim = aim;
            _outVortex = vortex;
            return inp;
        }
    }
}
