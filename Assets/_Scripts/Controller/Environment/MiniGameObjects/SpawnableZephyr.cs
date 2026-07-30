using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Zephyr" - the painted-sky cell environment (~33k prisms): pure weather, nothing
    /// built. Braided wind-rivers circle at three altitudes; two counter-chiral cyclones
    /// climb and sink; lenticular cloud banks drift (one thunderhead hides danger lightning
    /// bolts over a rain veil); a radiating sun disc and moon disc swirl Van-Gogh-style; a
    /// rolling swell sea anchors the horizon below. Starry-Night blue + gold, jade wisps.
    /// The flow cell - every family is a line to ride, and the only mass is cloud.
    /// </summary>
    public class SpawnableZephyr : CellEnvironmentSpawnableBase
    {
        const float Sea = -170f;

        protected override int DefaultSeed => 41;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableZephyr), 3);

        protected override void BuildEnvironment()
        {
            BuildWindRivers();
            BuildCyclones();
            BuildClouds();
            BuildDiscs();
            BuildSwellSea();
        }

        void BuildWindRivers()
        {
            var layers = new (float alt, Domains dom1, Domains dom2)[]
            {
                (-60f, Domains.Blue, Domains.Jade),
                (40f, Domains.Blue, Domains.Gold),
                (150f, Domains.Gold, Domains.Blue),
            };
            int rivers = Scaled(10);
            for (int layer = 0; layer < layers.Length; layer++)
            {
                var (alt, dom1, dom2) = layers[layer];
                for (int r = 0; r < rivers; r++)
                {
                    int sidx = layer * rivers + r;
                    float a = sidx * GoldenAngle * 2.9f;
                    var p = new Vector3(430f * Mathf.Cos(a), alt + 30f * Hash01(sidx * 29 + _noiseSeed) - 15f, 430f * Mathf.Sin(a));
                    for (int i = 0; i < 280; i++)
                    {
                        var v = Curl(p, 0.0052f, layer * 7);
                        var orbit = new Vector3(-p.z, 0f, p.x).normalized;
                        var toCentre = new Vector3(-p.x, 0f, -p.z).normalized;
                        var d = (v * 0.9f + orbit * 0.75f + toCentre * 0.22f).normalized;
                        d = new Vector3(d.x, d.y * 0.35f, d.z).normalized; // keep layers flat-ish
                        if ((p.y + d.y * 5.4f < alt - 45f && d.y < 0f) || (p.y + d.y * 5.4f > alt + 45f && d.y > 0f))
                            d = new Vector3(d.x, -d.y, d.z);
                        p += d * 5.4f;
                        if (Mathf.Sqrt(p.x * p.x + p.z * p.z) > 470f) break;
                        var flowRot = SpawnPoint.LookRotation(d, Vector3.up);
                        // Braid: four filaments coiling in the STREAMLINE frame (a true helix
                        // around the tangent at every heading - a world-frame offset collapses
                        // the coil whenever the flow runs along +/-x).
                        var right = flowRot * Vector3.right;
                        var upv = flowRot * Vector3.up;
                        for (int f = 0; f < 4; f++)
                        {
                            float fa = i * 0.35f + f * 1.5708f;
                            Emit(p + right * (Mathf.Cos(fa) * 3.2f) + upv * (Mathf.Sin(fa) * 2.2f),
                                flowRot, new Vector3(1.8f, 0.8f, 4.8f),
                                (i / 9 + f) % 3 != 0 ? dom1 : dom2);
                        }
                    }
                }
            }
        }

        void BuildCyclones()
        {
            // Anchored: one waterspout whose tip skims the swell and whose mouth opens at
            // cloud base (the missing sea-to-sky climb line), one funnel hung inverted from
            // the cloud deck, sinking. Quadratic lerp hugs the anchor before climbing.
            var defs = new (Vector3 cx, float chir, float yTip, float yMouth)[]
            {
                (new Vector3(190f, 0f, -140f), 1f, Sea + 18f, 120f),
                (new Vector3(-200f, 0f, 150f), -1f, 170f, -40f),
            };
            foreach (var (cx, chir, yTip, yMouth) in defs)
            {
                for (int arm = 0; arm < 11; arm++)
                {
                    float a0 = 2f * Mathf.PI * arm / 11f;
                    Vector3 prev = cx;
                    for (int i = 0; i < 210; i++)
                    {
                        float t = i / 209f;
                        float r = 8f + 150f * t;
                        float a = a0 + chir * t * 3.4f * Mathf.PI;
                        var p = new Vector3(cx.x + r * Mathf.Cos(a), Mathf.Lerp(yTip, yMouth, t * t), cx.z + r * Mathf.Sin(a));
                        Quaternion rot = i == 0
                            ? SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up)
                            : SpawnPoint.LookRotation(p - prev, Vector3.up);
                        Emit(p, rot, new Vector3(1.4f, 0.8f, 4.4f), arm % 2 != 0 ? Domains.Blue : Domains.Gold);
                        prev = p;
                    }
                }
            }
        }

        void BuildClouds()
        {
            int banks = 11;
            int m = Scaled(1150);
            for (int k = 0; k < banks; k++)
            {
                float a = k * GoldenAngle * 2.3f;
                float rr = 120f + 230f * Hash01(k * 67 + _noiseSeed);
                float cy = 60f + 130f * Hash01(k * 31);
                var cx = new Vector3(rr * Mathf.Cos(a), cy, rr * Mathf.Sin(a));
                float ex = 46f + 22f * Hash01(k * 7), ey = 13f + 6f * Hash01(k * 11), ez = 34f + 18f * Hash01(k * 13);
                for (int i = 0; i < m; i++)
                {
                    float u = Hash01(k * 997 + i * 7), v = Hash01(k * 991 + i * 11), w = Hash01(k * 983 + i * 13);
                    var p = new Vector3((u * 2f - 1f) * ex, (v * 2f - 1f) * ey, (w * 2f - 1f) * ez);
                    if (p.x / ex * (p.x / ex) + p.y / ey * (p.y / ey) + p.z / ez * (p.z / ez) > 1f) continue;
                    Emit(cx + p, SpawnPoint.LookRotation(Vector3.up, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))),
                        Jit(new Vector3(4.4f, 1.6f, 3.2f), 0.3f),
                        k == 3 && Hash01(i * 3) < 0.06f ? Domains.Ruby : Domains.Blue);
                }

                // The thunderhead: lightning bolts and a rain veil beneath.
                if (k == 3)
                {
                    for (int bolt = 0; bolt < 3; bolt++)
                    {
                        // Chained, hard-kinked, and striking the sea - unmistakably lightning
                        // against the strictly-vertical rain around it (same prism volume).
                        float startY = cx.y - ey - 4f;
                        float drop = (startY - (Sea + 12f)) / 26f;
                        var p = cx + new Vector3(20f * bolt - 20f, -ey - 4f, 10f * bolt - 10f);
                        for (int i = 0; i < 26; i++)
                        {
                            float kink = i % 3 == 0 ? 2.2f : 1f;
                            var step = new Vector3(
                                8f * (Hash01(bolt * 97 + i * 3) - 0.5f) * kink, -drop,
                                8f * (Hash01(bolt * 89 + i * 7) - 0.5f) * kink);
                            p += step;
                            Emit(p, SpawnPoint.LookRotation(step, Vector3.up),
                                new Vector3(1f, 1f, 3.2f), Domains.Ruby, PrismKind.Danger);
                        }
                    }
                    for (int rv = 0; rv < 100; rv++)
                    {
                        float rx = Hash01(k * 555 + rv * 3) * 2f - 1f, rz = Hash01(k * 551 + rv * 7) * 2f - 1f;
                        var basePos = cx + new Vector3(rx * ex * 0.8f, -ey - 2f, rz * ez * 0.8f);
                        for (int i = 0; i < 9; i++)
                            Emit(basePos + new Vector3(0f, -i * 4.2f, 0f), Quaternion.identity,
                                new Vector3(0.7f, 2.8f, 0.7f), Domains.Blue);
                    }
                }
            }
        }

        void BuildDiscs()
        {
            var defs = new (Vector3 dc, Domains dom, float rad)[]
            {
                (new Vector3(240f, 210f, 240f), Domains.Gold, 52f),
                (new Vector3(-260f, 190f, -230f), Domains.Blue, 40f),
            };
            for (int disc = 0; disc < defs.Length; disc++)
            {
                var (dc, dom, rad) = defs[disc];
                int body = (int)(rad * rad / 7f);
                for (int i = 0; i < body; i++)
                {
                    float u = Hash01(disc * 771 + i * 3);
                    float a = i * GoldenAngle;
                    float rr = rad * Mathf.Sqrt(u);
                    Emit(dc + new Vector3(rr * Mathf.Cos(a), 0.12f * rr * Mathf.Sin(a * 3f), rr * Mathf.Sin(a)),
                        SpawnPoint.LookRotation(Vector3.up, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))),
                        new Vector3(2.6f, 0.6f, 1.8f), dom);
                }
                Emit(dc + new Vector3(0f, 3f, 0f), Quaternion.identity, new Vector3(2.6f, 2.6f, 2.6f), dom, PrismKind.Shielded);
                for (int ray = 0; ray < 16; ray++)
                {
                    float a = 2f * Mathf.PI * ray / 16f;
                    for (int i = 0; i < 12; i++)
                    {
                        float rr = rad + 6f + i * 4.4f;
                        float aa = a + i * 0.14f; // swirl
                        var outward = new Vector3(Mathf.Cos(aa), 0f, Mathf.Sin(aa));
                        var tangent = new Vector3(-Mathf.Sin(aa), 0f, Mathf.Cos(aa));
                        // Long axis chains along the actual spiral stroke (radial step 4.4 +
                        // tangential step rr*0.14) - a curling brushstroke, not herringbone.
                        var chainDir = (outward * 4.4f + tangent * (rr * 0.14f)).normalized;
                        Emit(dc + new Vector3(rr * outward.x, 3f * Mathf.Sin(i * 0.9f + ray), rr * outward.z),
                            SpawnPoint.LookRotation(chainDir, Vector3.up), new Vector3(1.2f, 0.7f, 3.8f), dom);
                    }
                }
            }
        }

        void BuildSwellSea()
        {
            for (int row = 0; row < 30; row++)
            {
                float R = 60f + row * 13.5f;
                int n = (int)(2f * Mathf.PI * R / 7.4f);
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n;
                    float y = Sea + 11f * Mathf.Sin(a * 4f + row * 0.9f) + 5f * N01(Mathf.Cos(a) * 4f, row * 0.5f, Mathf.Sin(a) * 4f, 17);
                    if (N01(a * 3.1f, row * 0.7f, 8.5f, 18) < 0.30f) continue;
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    Emit(new Vector3(R * Mathf.Cos(a), y, R * Mathf.Sin(a)),
                        SpawnPoint.LookRotation(Vector3.up, tangent),
                        Jit(new Vector3(3.6f, 5.8f, 0.9f), 0.25f), row % 3 != 0 ? Domains.Blue : Domains.Jade);
                }
            }
        }
    }
}
