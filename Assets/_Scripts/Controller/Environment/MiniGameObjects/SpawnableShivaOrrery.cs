using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shiva's Cell's planets (Tandava, Assets/_Scripts/Controller/Arcade/TANDAVA.md §0): the planet view the brief asked
    /// for ("maybe we can use the orrery"), without the orrery's centre - <see cref="SpawnableOrrery"/> puts a sun-shell
    /// at the middle and runs armature spokes from it to every planet, and the middle is where he sits. Seven
    /// phyllotaxis-shelled planets on tilted orbits round him, out past his shield (760-1480 u, the lab's orrery,
    /// Docs/Studios/ShivaLab/sim.js layPlanets), each orbit traced by a dotted line of small prisms; the fourth is ringed
    /// like Saturn. Every pane is laid flush (the orrery's pane rule, Docs/ECOSYSTEM.md §49.10). The planets stand where
    /// they are laid: an environment is laid once and baked.
    /// </summary>
    public class SpawnableShivaOrrery : CellEnvironmentSpawnableBase
    {
        protected override int DefaultSeed => 69;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableShivaOrrery), 1);
        protected override bool AdmitsAuthoredPrismScale => true;

        const float GoldenAngle = 2.39996323f;
        const float PaneThick = 0.84f;

        // radius, planet size, tilt (deg), node (deg), phase (deg), domain - the lab's seven (760 + 120 k u)
        static readonly (float R, float size, float tilt, float node, float phase, Domains dom)[] Planets =
        {
            (760f, 26f, 18f, 30f, 10f, Domains.Gold), (880f, 38f, -14f, 95f, 140f, Domains.Jade), (1000f, 50f, 24f, 160f, 250f, Domains.Ruby),
            (1120f, 74f, -9f, 210f, 55f, Domains.Blue), (1240f, 30f, 12f, 280f, 300f, Domains.Gold), (1360f, 42f, -20f, 330f, 190f, Domains.Jade),
            (1480f, 54f, 8f, 15f, 95f, Domains.Ruby),
        };

        protected override void BuildEnvironment()
        {
            for (int k = 0; k < Planets.Length; k++)
            {
                var (R, size, tilt, node, phase, dom) = Planets[k];
                var c = OrbitPoint(R, tilt, node, phase * Mathf.Deg2Rad);
                Shell(c, size, Mathf.RoundToInt(4f * Mathf.PI * size * size / 81f), dom);
                if (k == 3) SaturnRing(c, size, dom);
                Orbit(R, tilt, node, dom, c, size);
            }
        }

        static Vector3 OrbitPoint(float R, float tiltDeg, float nodeDeg, float a)
        {
            float tr = tiltDeg * Mathf.Deg2Rad, nr = nodeDeg * Mathf.Deg2Rad;
            float x = R * Mathf.Cos(a), z = R * Mathf.Sin(a), y = z * Mathf.Sin(tr), zz = z * Mathf.Cos(tr);
            return new Vector3(x * Mathf.Cos(nr) - zz * Mathf.Sin(nr), y, x * Mathf.Sin(nr) + zz * Mathf.Cos(nr));
        }

        /// <summary>A phyllotaxis shell of flush panes (the orrery's FlushPane at its planets' pack, 0.645).</summary>
        void Shell(Vector3 c, float r, int n, Domains dom)
        {
            n = Mathf.Max(24, n);
            float pane = 0.645f * r * Mathf.Sqrt(4f * Mathf.PI / n);
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n, y = 1f - 2f * u, rr = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)), a = i * GoldenAngle;
                var d = new Vector3(rr * Mathf.Cos(a), y, rr * Mathf.Sin(a));
                var rot = Quaternion.LookRotation(d, new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)));
                Emit(c + d * r, rot, new Vector3(pane, pane, PaneThick), dom);
            }
        }

        void SaturnRing(Vector3 c, float r, Domains dom)
        {
            var tilt = Quaternion.Euler(68f, 0f, 20f);
            for (int band = 0; band < 2; band++)
            {
                float rr = r * (1.55f + band * 0.45f);
                int n = Mathf.RoundToInt(2f * Mathf.PI * rr / 7f);
                for (int i = 0; i < n; i++)
                {
                    float a = i * 2f * Mathf.PI / n;
                    var p = tilt * new Vector3(rr * Mathf.Cos(a), 0f, rr * Mathf.Sin(a));
                    Emit(c + p, Quaternion.LookRotation(tilt * Vector3.up, p), new Vector3(4.6f, 4.6f, PaneThick), dom);
                }
            }
        }

        /// <summary>The orbit, dotted: a small prism every ~26 u round the planet's path, clear of the planet itself.</summary>
        void Orbit(float R, float tilt, float node, Domains dom, Vector3 planet, float size)
        {
            int n = Mathf.RoundToInt(2f * Mathf.PI * R / 26f);
            for (int i = 0; i < n; i++)
            {
                float a = i * 2f * Mathf.PI / n;
                var p = OrbitPoint(R, tilt, node, a);
                if ((p - planet).sqrMagnitude < (size * 2.4f) * (size * 2.4f)) continue;
                var t = OrbitPoint(R, tilt, node, a + 0.01f) - p;
                Emit(p, Quaternion.LookRotation(t.normalized), new Vector3(1.4f, 1.4f, 6f), dom);
            }
        }
    }
}
