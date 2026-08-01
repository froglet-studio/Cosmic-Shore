using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Orrery" - the celestial-clock cell environment (~34k prisms): flying INSIDE an
    /// astronomical machine. A porous golden sun-shell (super-shielded core ring) sits at the
    /// centre of seven tilted orbit rings, each carrying a phyllotaxis-shelled planet (outer
    /// ones with moons; the fourth ringed like Saturn); armature spokes reach from the sun to
    /// every planet; a zodiac band of twelve polygon constellation-gates circles the rim; gear
    /// rosettes, swinging pendulum chains, a star-mote firmament, and one eccentric comet
    /// whose spark tail is the machine's only danger. Brass gold + steel blue; ruby jewel
    /// bearings (shielded) anchor each ring. The airiest cell - all lines and voids.
    /// </summary>
    public class SpawnableOrrery : CellEnvironmentSpawnableBase
    {
        protected override int DefaultSeed => 31;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableOrrery), 2);

        static readonly (float R, float tiltDeg, Domains dom)[] RingDefs =
        {
            (95f, 8f, Domains.Blue), (148f, -12f, Domains.Gold), (200f, 22f, Domains.Blue),
            (252f, -8f, Domains.Jade), (304f, 15f, Domains.Blue), (356f, -18f, Domains.Gold),
            (408f, 26f, Domains.Jade),
        };

        static readonly Domains[] PlanetDoms =
        {
            Domains.Ruby, Domains.Jade, Domains.Gold, Domains.Blue, Domains.Jade, Domains.Ruby, Domains.Gold,
        };

        protected override void BuildEnvironment()
        {
            BuildSun();
            BuildRingsAndPlanets();
            BuildZodiac();
            BuildComet();
            BuildGears();
            BuildPendulums();
            BuildSuspensionHoop();
            BuildStars();
        }

        /// <summary>Phyllotaxis point on the unit sphere.</summary>
        static Vector3 SpherePoint(int i, int n)
        {
            float u = (i + 0.5f) / n;
            float y = 1f - 2f * u;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = i * GoldenAngle;
            return new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
        }

        /// <summary>Pane tangent to a sphere at direction d (thin axis along the normal).</summary>
        static Quaternion ShellRot(Vector3 d, int i)
        {
            float a = i * GoldenAngle;
            return SpawnPoint.LookRotation(d, new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)));
        }

        void BuildSun()
        {
            const int n = 3400;
            for (int i = 0; i < n; i++)
            {
                var d = SpherePoint(i, n);
                var p = d * 46f;
                if (N01(p.x * 0.06f, p.y * 0.06f, p.z * 0.06f, 0) < 0.30f) continue; // flame gaps
                Emit(p, ShellRot(d, i), Jit(new Vector3(4.6f, 4.6f, 0.8f), 0.2f), Domains.Gold);
            }
            for (int i = 0; i < 36; i++)
            {
                float a = 2f * Mathf.PI * i / 36f;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Emit(radial * 20f, SpawnPoint.LookRotation(new Vector3(-radial.z, 0f, radial.x), radial),
                    new Vector3(3f, 3f, 3f), Domains.Gold, PrismKind.SuperShielded);
            }
        }

        void BuildRingsAndPlanets()
        {
            for (int ri = 0; ri < RingDefs.Length; ri++)
            {
                var (R, tiltDeg, dom) = RingDefs[ri];
                float tr = tiltDeg * Mathf.Deg2Rad;

                // Rail-pair orbit ring (strands chained along the circumference).
                int n = (int)(2f * Mathf.PI * R / 1.7f);
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n;
                    float x = R * Mathf.Cos(a), zz = R * Mathf.Sin(a);
                    var tangent = new Vector3(-Mathf.Sin(a), Mathf.Cos(a) * Mathf.Sin(tr), Mathf.Cos(a) * Mathf.Cos(tr));
                    for (int pair = -1; pair <= 1; pair += 2)
                        Emit(new Vector3(x, zz * Mathf.Sin(tr) + pair * 1.9f * Mathf.Cos(tr), zz * Mathf.Cos(tr)),
                            SpawnPoint.LookRotation(tangent, Vector3.up), new Vector3(1.2f, 1f, 4.6f), dom);
                }

                // Jewel bearing anchor (shielded).
                float ba = ri * 2.31f;
                float bx = R * Mathf.Cos(ba), bz0 = R * Mathf.Sin(ba);
                Emit(new Vector3(bx, bz0 * Mathf.Sin(tr), bz0 * Mathf.Cos(tr)), Quaternion.identity,
                    new Vector3(2.4f, 2.4f, 2.4f), Domains.Ruby, PrismKind.Shielded);

                // Planet (phyllotaxis shell) + shielded core.
                float pa = ri * GoldenAngle * 4.1f;
                float px = R * Mathf.Cos(pa), pz0 = R * Mathf.Sin(pa);
                var pc = new Vector3(px, pz0 * Mathf.Sin(tr), pz0 * Mathf.Cos(tr));
                float pr = 10f + ri * 2.6f;
                Emit(pc, Quaternion.identity, new Vector3(2.6f, 2.6f, 2.6f), Domains.Gold, PrismKind.Shielded);
                int np = 560 + ri * 170;
                for (int i = 0; i < np; i++)
                {
                    var d = SpherePoint(i, np);
                    Emit(pc + d * pr, ShellRot(d, i), Jit(new Vector3(2.6f, 2.6f, 0.7f), 0.2f), PlanetDoms[ri]);
                }

                // Moons for the outer planets.
                if (ri >= 2)
                {
                    for (int mo = 0; mo < ri - 1; mo++)
                    {
                        float ma = mo * 2.4f + ri;
                        var mc = pc + new Vector3(Mathf.Cos(ma), 0.4f * Mathf.Sin(ma * 2f), Mathf.Sin(ma)) * (pr + 9f + mo * 6f);
                        for (int i = 0; i < 150; i++)
                        {
                            var d = SpherePoint(i, 150);
                            Emit(mc + d * 4.2f, ShellRot(d, i), new Vector3(1.6f, 1.6f, 0.6f), Domains.Blue);
                        }
                    }
                }

                // Saturn ring on the fourth planet.
                if (ri == 3)
                {
                    for (int i = 0; i < 240; i++)
                    {
                        float a = 2f * Mathf.PI * i / 240f;
                        float rr = pr + 6f + 3f * Hash01(i * 7);
                        var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                        Emit(pc + new Vector3(rr * Mathf.Cos(a), 0.15f * rr * Mathf.Sin(a * 3f), rr * Mathf.Sin(a)),
                            SpawnPoint.LookRotation(tangent, Vector3.up), new Vector3(1.6f, 0.5f, 2.6f), Domains.Gold);
                    }
                }

                // Armature spoke sun -> planet.
                int ns = (int)(R / 3.4f);
                var spokeRot = SpawnPoint.LookRotation(pc.normalized, Vector3.up);
                // Dock at the sun shell (r=46) instead of skewering the core fly-in space.
                int i0 = Mathf.Max(2, Mathf.CeilToInt(ns * 50f / R));
                for (int i = i0; i < ns; i++)
                {
                    float t = i / (float)ns;
                    float x = R * t * Mathf.Cos(pa), zz = R * t * Mathf.Sin(pa);
                    Emit(new Vector3(x, zz * Mathf.Sin(tr), zz * Mathf.Cos(tr)), spokeRot,
                        new Vector3(1.3f, 1.3f, 5.8f), i % 2 != 0 ? Domains.Gold : Domains.Blue);
                }
            }
        }

        void BuildZodiac()
        {
            for (int k = 0; k < 12; k++)
            {
                float a = 2f * Mathf.PI * k / 12f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                var cx = new Vector3(420f * ca, 0f, 420f * sa);
                Domains dom = (k % 4) switch { 0 => Domains.Jade, 1 => Domains.Ruby, 2 => Domains.Gold, _ => Domains.Blue };
                int sides = 3 + k % 4;
                for (int sd = 0; sd < sides; sd++)
                {
                    float a0 = 2f * Mathf.PI * sd / sides, a1 = 2f * Mathf.PI * (sd + 1) / sides;
                    float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0), c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);
                    // Straight CHORDS (lerped positions, not lerped angles) so triangle /
                    // square / pentagon / hexagon glyphs actually render as polygons.
                    var sideDir = new Vector3(-sa * (c1 - c0), s1 - s0, ca * (c1 - c0));
                    for (int i = 0; i < 16; i++)
                    {
                        float t = (i + 0.5f) / 16f;
                        float lx = 18f * Mathf.Lerp(c0, c1, t), ly = 18f * Mathf.Lerp(s0, s1, t);
                        Emit(cx + new Vector3(-sa * lx, ly, ca * lx),
                            SpawnPoint.LookRotation(sideDir, new Vector3(ca, 0f, sa)),
                            new Vector3(1.6f, 1.6f, 4.2f), dom);
                    }
                }
                for (int i = 0; i < 44; i++)
                {
                    float t = (i + 1) / 45f;
                    float aa = a + t * (2f * Mathf.PI / 12f);
                    var tangent = new Vector3(-Mathf.Sin(aa), 0f, Mathf.Cos(aa));
                    Emit(new Vector3(420f * Mathf.Cos(aa), 3f * Mathf.Sin(aa * 9f), 420f * Mathf.Sin(aa)),
                        SpawnPoint.LookRotation(tangent, Vector3.up), new Vector3(1.2f, 0.9f, 4.6f), Domains.Blue);
                }
            }
        }

        void BuildComet()
        {
            const int n = 330;
            Vector3 Orbit(float t) => new Vector3(430f * Mathf.Cos(t) - 160f, 60f * Mathf.Sin(t * 2f), 260f * Mathf.Sin(t));
            Vector3 prev = Orbit(-0.02f);
            for (int i = 0; i < n; i++)
            {
                float t = 2f * Mathf.PI * i / n;
                var p = Orbit(t);
                Emit(p, SpawnPoint.LookRotation(p - prev, Vector3.up), new Vector3(1.5f, 1.2f, 4.2f), Domains.Blue);
                prev = p;
            }
            // Head + spark tail (the machine's only danger, clearly comet-shaped).
            var hd = Orbit(0.9f);
            for (int i = 0; i < 140; i++)
            {
                float t = i / 140f;
                var off = new Vector3(28f * t * Mathf.Cos(0.9f + t * 0.8f), 10f * Mathf.Sin(t * 7f) * t,
                    -28f * t * Mathf.Sin(0.9f + t * 0.8f));
                Emit(hd + off, Quaternion.identity, new Vector3(1.2f, 1.2f, 2.6f * (1f - t * 0.5f)),
                    Domains.Ruby, i % 2 == 0 ? PrismKind.Danger : PrismKind.Plain);
            }
        }

        void BuildGears()
        {
            // Six meshed PAIRS (same plane, centre distance 48: tooth arms interleave, rims
            // clear) with the odd gear half-tooth phased - a legible gear train, not medallions.
            for (int k = 0; k < 12; k++)
            {
                int pair = k / 2;
                float a = pair * GoldenAngle * 2.9f;
                float rr = 125f + 45f * (pair % 3);
                var cx = new Vector3(rr * Mathf.Cos(a), (pair % 2 * 2 - 1) * 36f, rr * Mathf.Sin(a));
                if (k % 2 != 0)
                    cx += new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * 48f;
                float toothPhase = (k % 2) * (Mathf.PI / 11f);
                float rimPhase = (k % 2) * (Mathf.PI / 26f);
                for (int tt = 0; tt < 11; tt++)
                {
                    float ta = 2f * Mathf.PI * tt / 11f + toothPhase;
                    var radial = new Vector3(Mathf.Cos(ta), 0f, Mathf.Sin(ta));
                    for (int i = 0; i < 9; i++)
                    {
                        float r2 = 6f + i * 2.6f;
                        Emit(cx + new Vector3(r2 * radial.x, 0.4f * Mathf.Sin(ta * 11f), r2 * radial.z),
                            SpawnPoint.LookRotation(radial, Vector3.up), new Vector3(2f, 0.8f, 1.6f), Domains.Gold);
                    }
                }
                for (int i = 0; i < 26; i++)
                {
                    float ta = 2f * Mathf.PI * i / 26f + rimPhase;
                    var tangent = new Vector3(-Mathf.Sin(ta), 0f, Mathf.Cos(ta));
                    Emit(cx + new Vector3(22f * Mathf.Cos(ta), 0f, 22f * Mathf.Sin(ta)),
                        SpawnPoint.LookRotation(tangent, Vector3.up), new Vector3(1.6f, 0.9f, 2.6f), Domains.Ruby);
                }
            }
        }

        void BuildPendulums()
        {
            for (int k = 0; k < 9; k++)
            {
                float a = 2f * Mathf.PI * k / 9f;
                var top = new Vector3(120f * Mathf.Cos(a), 300f, 120f * Mathf.Sin(a));
                float swing = Mathf.Sin(k * 2.1f) * 38f;
                var swingDir = new Vector3(Mathf.Cos(a + 1.5f), 0f, Mathf.Sin(a + 1.5f));
                var swingSide = new Vector3(-Mathf.Sin(a + 1.5f), 0f, Mathf.Cos(a + 1.5f));
                for (int i = 0; i < 26; i++)
                {
                    float t = i / 25f;
                    // Links chain along the swing curve's tangent (same prism volume).
                    var tangentDir = swingDir * (2f * swing * t) + Vector3.down * 150f;
                    Emit(new Vector3(top.x + swing * t * t * swingDir.x, top.y - t * 150f,
                            top.z + swing * t * t * swingDir.z),
                        SpawnPoint.LookRotation(tangentDir, swingSide), new Vector3(1f, 1f, 3.2f), Domains.Blue);
                }
                var bob = new Vector3(top.x + swing * Mathf.Cos(a + 1.5f), top.y - 152f, top.z + swing * Mathf.Sin(a + 1.5f));
                for (int i = 0; i < 60; i++)
                {
                    var d = SpherePoint(i, 60);
                    Emit(bob + d * 5f, ShellRot(d, i), new Vector3(1.8f, 1.8f, 0.7f), Domains.Gold);
                }
            }
        }

        /// <summary>The canopy frame the pendulums hang FROM - a gold suspension hoop through
        /// the tops, reusing the orbit-rail vocabulary; also the machine's high skim line.</summary>
        void BuildSuspensionHoop()
        {
            int n = (int)(2f * Mathf.PI * 120f / 3.4f);
            for (int i = 0; i < n; i++)
            {
                float t = 2f * Mathf.PI * i / n;
                var tangent = new Vector3(-Mathf.Sin(t), 0f, Mathf.Cos(t));
                Emit(new Vector3(120f * Mathf.Cos(t), 300f, 120f * Mathf.Sin(t)),
                    SpawnPoint.LookRotation(tangent, Vector3.up), new Vector3(1.2f, 1f, 4.6f), Domains.Gold);
            }
        }

        void BuildStars()
        {
            int n = Scaled(6400);
            for (int i = 0; i < n; i++)
            {
                if (Hash01(i * 17 + _noiseSeed) < 0.38f) continue;
                var d = SpherePoint(i, n);
                var p = d * (445f + 35f * Hash01(i * 23));
                Emit(p, Quaternion.Euler(0f, Hash01(i * 29) * 360f, 0f), new Vector3(1.2f, 1.2f, 1.2f),
                    i % 5 != 0 ? Domains.Blue : Domains.Gold);
            }
        }
    }
}
