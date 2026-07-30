using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Caldera" - the fire-and-stone cell environment (~31k prisms) and the rotation's one
    /// danger-led biome. A terraced basalt volcano rises from a slab plain to a crater whose
    /// lava lake, falls, and meandering magma river are TRUE danger runs (telegraphed ruby
    /// ribbons - the expert skim lines); Giant's-Causeway basalt column clusters stagger
    /// across the plain like organ pipes; ember plumes spiral up from vents; obsidian shard
    /// arcs, tiered sulfur terraces, fumarole chimneys (shielded caps), and a drifting ash
    /// ring complete the forge. Basalt blue + fire ruby + sulfur/ember gold.
    /// </summary>
    public class SpawnableCaldera : CellEnvironmentSpawnableBase
    {
        const float Base = -180f;

        protected override int DefaultSeed => 53;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableCaldera), 3);

        protected override void BuildEnvironment()
        {
            BuildCone();
            BuildLava();
            BuildBasaltColumns();
            BuildEmberPlumes();
            BuildObsidianArcs();
            BuildSulfurTerraces();
            BuildFumaroles();
            BuildAshRing();
            BuildFloor();
        }

        void BuildCone()
        {
            for (int L = 0; L < 38; L++)
            {
                float t = L / 37f;
                float R = 155f * (1f - t) + 34f * t;
                float y = Base + t * 255f;
                int n = (int)(2f * Mathf.PI * R / 2f);
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n + L * 0.13f;
                    float wob = 6f * N01(Mathf.Cos(a) * 4f, L * 0.5f, Mathf.Sin(a) * 4f, 1);
                    if (N01(a * 2.6f, L * 0.7f, 3.3f, 2) < 0.08f) continue;
                    float rr = R + wob;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    // Plates shingle the slope: thin axis leans outward with the cone.
                    var normal = (radial + Vector3.up * 0.55f).normalized;
                    Emit(new Vector3(rr * radial.x, y + 2f * Mathf.Sin(a * 5f + L), rr * radial.z),
                        SpawnPoint.LookRotation(normal, new Vector3(-radial.z, 0f, radial.x)),
                        Jit(new Vector3(4.2f, 2.6f, 1.4f), 0.25f), Domains.Blue);
                }
            }
            // Crater rim spikes.
            for (int i = 0; i < 40; i++)
            {
                float a = 2f * Mathf.PI * i / 40f;
                Emit(new Vector3(37f * Mathf.Cos(a), 78f + 4f * Mathf.Sin(a * 7f), 37f * Mathf.Sin(a)),
                    Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), new Vector3(1.8f, 4.6f, 1.8f), Domains.Blue);
            }
        }

        void BuildLava()
        {
            // Lake: a danger disc filling the crater mouth.
            for (int i = 0; i < 210; i++)
            {
                float u = Hash01(i * 7 + _noiseSeed);
                float a = i * GoldenAngle;
                float rr = 30f * Mathf.Sqrt(u);
                Emit(new Vector3(rr * Mathf.Cos(a), 70f + 1.5f * Mathf.Sin(a * 5f), rr * Mathf.Sin(a)),
                    SpawnPoint.LookRotation(Vector3.up, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))),
                    new Vector3(2.8f, 0.7f, 2.8f), Domains.Ruby, PrismKind.Danger);
            }

            // Falls: ribbons over the rim, meandering down the slope into pools.
            for (int k = 0; k < 7; k++)
            {
                float a0 = 2f * Mathf.PI * k / 7f + 0.4f;
                Vector3 p = new Vector3(36f * Mathf.Cos(a0), 74f, 36f * Mathf.Sin(a0)), prev = p;
                for (int i = 0; i < 56; i++)
                {
                    float t = i / 55f;
                    float R = 34f + (150f - 34f) * t + 8f * Mathf.Sin(t * 6f + k);
                    float a = a0 + 0.35f * Mathf.Sin(t * 3.2f + k * 2f);
                    p = new Vector3(R * Mathf.Cos(a), 74f - t * 250f, R * Mathf.Sin(a));
                    Emit(p, SpawnPoint.LookRotation(p - prev, Vector3.up), new Vector3(2.2f, 1f, 3f),
                        Domains.Ruby, PrismKind.Danger);
                    prev = p;
                }
                for (int i = 0; i < 46; i++)
                {
                    float u = Hash01(k * 333 + i * 3);
                    float aa = i * GoldenAngle;
                    float rr = 14f * Mathf.Sqrt(u);
                    // Molten Danger core with a cooling Plain rim - a telegraphed edge to
                    // bail onto (per-prism parity is invisible at flight speed).
                    Emit(p + new Vector3(rr * Mathf.Cos(aa), -1f, rr * Mathf.Sin(aa)),
                        Quaternion.identity, new Vector3(2.6f, 0.6f, 2.6f),
                        Domains.Ruby, u < 0.5f ? PrismKind.Danger : PrismKind.Plain);
                }
            }

            // Magma river wandering out across the plain.
            var rp = new Vector3(150f * Mathf.Cos(0.4f) + 8f, Base + 2f, 150f * Mathf.Sin(0.4f) + 8f);
            Vector3 rprev = rp;
            for (int i = 0; i < 220; i++)
            {
                float a = 0.4f + 0.9f * Mathf.Sin(i * 0.09f) + i * 0.012f;
                rp += new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3.4f;
                if (rp.magnitude > 430f) break;
                var q = new Vector3(rp.x, Base + 2f + 0.5f * Mathf.Sin(i * 0.4f), rp.z);
                Emit(q, SpawnPoint.LookRotation(q - rprev, Vector3.up), new Vector3(2.4f, 0.7f, 3.2f),
                    Domains.Ruby, PrismKind.Danger);
                rprev = q;
            }
        }

        void BuildBasaltColumns()
        {
            for (int cl = 0; cl < 40; cl++)
            {
                float a = cl * GoldenAngle * 2.7f;
                float rr = 200f + 180f * Hash01(cl * 67 + _noiseSeed);
                var cx = new Vector3(rr * Mathf.Cos(a), Base, rr * Mathf.Sin(a));
                int cols = 7 + (int)(6f * Hash01(cl * 7));
                for (int co = 0; co < cols; co++)
                {
                    float ca = 2f * Mathf.PI * co / cols;
                    // Touching segments (solid pipes) on two rings whose gaps fit the
                    // vessel: inner ~8, outer ~19 - real weave-through slalom terrain.
                    var off = new Vector3(9.5f * Mathf.Cos(ca) * (1 + co % 2), 0f, 9.5f * Mathf.Sin(ca) * (1 + co % 2));
                    int height = 8 + (int)(14f * Hash01(cl * 13 + co));
                    float yaw = ca * Mathf.Rad2Deg;
                    for (int h = 0; h < height; h++)
                        Emit(cx + off + new Vector3(0f, h * 1.9f, 0f), Quaternion.Euler(0f, yaw, 0f),
                            Jit(new Vector3(3f, 1.7f, 3f), 0.08f), Domains.Blue);
                    Emit(cx + off + new Vector3(0f, height * 1.9f, 0f), Quaternion.Euler(0f, yaw, 0f),
                        new Vector3(3.2f, 0.8f, 3.2f), Domains.Gold);
                }
            }
        }

        void BuildEmberPlumes()
        {
            for (int k = 0; k < 13; k++)
            {
                float a = k * GoldenAngle * 3.1f;
                float rr = 90f + 160f * Hash01(k * 23 + _noiseSeed);
                var basePos = new Vector3(rr * Mathf.Cos(a), Base + 4f, rr * Mathf.Sin(a));
                Vector3 prev = basePos;
                for (int i = 0; i < 110; i++)
                {
                    float t = i / 109f;
                    float aa = t * 7f * Mathf.PI * (k % 2 != 0 ? 1f : -1f);
                    float r2 = 3f + t * 26f;
                    var p = basePos + new Vector3(r2 * Mathf.Cos(aa), t * 230f, r2 * Mathf.Sin(aa));
                    // Long axis chains along the rising spiral (radial up keeps roll stable on
                    // the near-vertical base); widening step spacing dissolves it into sparks.
                    Emit(p, SpawnPoint.LookRotation(p - prev, new Vector3(Mathf.Cos(aa), 0f, Mathf.Sin(aa))),
                        new Vector3(1.1f, 1.1f, 1.9f), i % 3 != 0 ? Domains.Gold : Domains.Ruby);
                    prev = p;
                }
            }
        }

        void BuildObsidianArcs()
        {
            for (int k = 0; k < 16; k++)
            {
                float a = k * GoldenAngle * 1.9f;
                float rr = 160f + 190f * Hash01(k * 43 + _noiseSeed);
                var basePos = new Vector3(rr * Mathf.Cos(a), Base + 2f, rr * Mathf.Sin(a));
                float span = 40f + 30f * Hash01(k * 7);
                float dirA = a + 1.2f;
                // The arc's constant lateral is never parallel to the steep tangent, so the
                // thin axis stays the blade normal along the whole span; the long axis chains
                // along the arc - 16 knife-edge ribbons to fly under and thread.
                var side = new Vector3(-Mathf.Sin(dirA), 0f, Mathf.Cos(dirA));
                Vector3 prev = basePos;
                for (int i = 0; i < 34; i++)
                {
                    float t = i / 33f;
                    var p = basePos + new Vector3(Mathf.Cos(dirA) * span * t, 55f * Mathf.Sin(t * Mathf.PI), Mathf.Sin(dirA) * span * t);
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(new Vector3(Mathf.Cos(dirA) * span, 55f * Mathf.PI, Mathf.Sin(dirA) * span), side)
                        : SpawnPoint.LookRotation(p - prev, side);
                    Emit(p, rot, new Vector3(0.9f, 1.6f, 3.8f), Domains.Blue);
                    prev = p;
                }
            }
        }

        void BuildSulfurTerraces()
        {
            for (int k = 0; k < 11; k++)
            {
                float a = k * GoldenAngle * 4.3f;
                float rr = 230f + 140f * Hash01(k * 17 + _noiseSeed);
                var cx = new Vector3(rr * Mathf.Cos(a), Base, rr * Mathf.Sin(a));
                for (int tier = 0; tier < 4; tier++)
                {
                    float R = 18f - tier * 3.6f;
                    float yy = tier * 4.2f;
                    int n = (int)(2f * Mathf.PI * R / 2.6f);
                    for (int i = 0; i < n; i++)
                    {
                        float aa = 2f * Mathf.PI * i / n;
                        float wr = R + 2.2f * Mathf.Sin(aa * 5f + tier);
                        var tangent = new Vector3(-Mathf.Sin(aa), 0f, Mathf.Cos(aa));
                        Emit(cx + new Vector3(wr * Mathf.Cos(aa), yy, wr * Mathf.Sin(aa)),
                            SpawnPoint.LookRotation(tangent, Vector3.up), new Vector3(1.9f, 1.3f, 1.9f), Domains.Gold);
                    }
                }
            }
        }

        void BuildFumaroles()
        {
            for (int k = 0; k < 16; k++)
            {
                float a = k * GoldenAngle * 3.7f;
                float rr = 140f + 220f * Hash01(k * 29 + _noiseSeed);
                var cx = new Vector3(rr * Mathf.Cos(a), Base, rr * Mathf.Sin(a));
                int height = (int)(12f + 14f * Hash01(k * 5));
                for (int h = 0; h < height; h++)
                    for (int j = 0; j < 5; j++)
                    {
                        float aa = 2f * Mathf.PI * j / 5f + h * 0.3f;
                        float r2 = 4.2f * (1f - h / (float)height * 0.5f);
                        Emit(cx + new Vector3(r2 * Mathf.Cos(aa), h * 2.6f, r2 * Mathf.Sin(aa)),
                            Quaternion.Euler(0f, aa * Mathf.Rad2Deg, 0f), new Vector3(1.4f, 1.5f, 1.4f), Domains.Blue);
                    }
                Emit(cx + new Vector3(0f, height * 2.6f + 2f, 0f), Quaternion.identity,
                    new Vector3(2.2f, 2.2f, 2.2f), Domains.Ruby, PrismKind.Shielded);
            }
        }

        void BuildAshRing()
        {
            int n = Scaled(6000);
            for (int i = 0; i < n; i++)
            {
                if (Hash01(i * 11) < 0.35f) continue;
                float a = Hash01(i * 3 + _noiseSeed) * 2f * Mathf.PI;
                float rr = 60f + 340f * Hash01(i * 7);
                Emit(new Vector3(rr * Mathf.Cos(a),
                        165f + 45f * N01(Mathf.Cos(a) * 3f, rr * 0.01f, Mathf.Sin(a) * 3f, 9), rr * Mathf.Sin(a)),
                    Quaternion.Euler(0f, Hash01(i * 13) * 360f, 0f),
                    new Vector3(3.2f, 0.8f, 2.4f), Domains.Blue);
            }
        }

        void BuildFloor()
        {
            for (int row = 0; row < 26; row++)
            {
                float R = 60f + row * 14.5f;
                int n = (int)(2f * Mathf.PI * R / 7.2f);
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n;
                    if (N01(a * 2.9f, row * 0.8f, 6.1f, 12) < 0.28f) continue;
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    Emit(new Vector3(R * Mathf.Cos(a), Base - 2f + 3f * N01(Mathf.Cos(a) * 5f, row, Mathf.Sin(a) * 5f, 13), R * Mathf.Sin(a)),
                        SpawnPoint.LookRotation(Vector3.up, tangent),
                        Jit(new Vector3(4.6f, 5.2f, 1f), 0.3f), Domains.Blue);
                }
            }
        }
    }
}
