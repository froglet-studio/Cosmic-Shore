using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Yggdra" - the World-Tree cell environment (~35k prisms): Atlantis's tree, distilled
    /// and grown grander for the freestyle rotation. A braided fourteen-strand trunk rises
    /// from a dune bowl to a fibonacci canopy dome; root buttresses flare into the floor;
    /// golden-angle branches droop, split twice, hang air-root vines, and end in phyllotaxis
    /// blossom heads; kelp veils sway up from the bowl; firefly currents orbit the crown.
    /// Landmarks: a super-shielded heartwood ring, shielded fruit, sparse danger thorns on
    /// the low branches. The companion road-city is <see cref="SpawnableDaedala"/>.
    /// </summary>
    public class SpawnableYggdra : CellEnvironmentSpawnableBase
    {
        const float Floor = -190f;
        const float Crown = 265f;
        const float OuterR = 380f;

        protected override int DefaultSeed => 11;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableYggdra), 2);

        float BowlY(float r) { float t = r / OuterR; return Floor + t * t * 90f; }

        protected override void BuildEnvironment()
        {
            float baseY = Floor + 6f;
            float height = Crown - baseY;

            // Trunk: fourteen braided strands, ribbon planks long along the strand.
            for (int s = 0; s < 14; s++)
            {
                float phase = 2f * Mathf.PI * s / 14f;
                Domains dom = s % 3 != 0 ? Domains.Jade : Domains.Gold;
                Vector3 prev = Vector3.zero;
                for (int i = 0; i < 220; i++)
                {
                    float t = i / 219f;
                    float y = baseY + height * t;
                    float r = 44f * Mathf.Pow(1f - t, 1.7f) + 12f + 30f * Mathf.Max(0f, t - 0.86f) * 7f;
                    // Phase-locked over/under weave (adjacent strands alternate) + a small
                    // centred wander keeps every strand in its own azimuthal lane - the braid
                    // signature. Strand 0 is the gold KING STRAND: a clean, wide, radially-thin
                    // helix ribbon (identical prism volume) - the premier roots-to-crown skim
                    // road, readable against the noisy Jade braid by its colour and calm.
                    float ang, radius;
                    Vector3 scale;
                    if (s == 0)
                    {
                        ang = t * 4.6f;
                        radius = r;
                        scale = new Vector3(9.6f, 0.6f, 7.8f);
                    }
                    else
                    {
                        ang = phase + t * 4.6f + 0.16f * (2f * N01(t * 5f, s * 3f, 0f, 0) - 1f);
                        float weave = 6f * Mathf.Sin(t * 9f * Mathf.PI + s * Mathf.PI);
                        float sway = 4.4f * N01(t * 3f, s * 7f, 1f, 1) - 2.2f;
                        radius = r + sway + weave;
                        scale = Jit(new Vector3(3.2f, 1.8f, 7.8f));
                    }
                    var p = new Vector3(radius * Mathf.Cos(ang), y, radius * Mathf.Sin(ang));
                    Vector3 radial = new Vector3(p.x, 0f, p.z).normalized;
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(Vector3.up, radial)
                        : SpawnPoint.LookRotation(p - prev, radial);
                    Emit(p, rot, scale, dom);
                    prev = p;
                }
            }

            // Heartwood ring - the permanent bones of the world.
            float heartY = baseY + height * 0.4f;
            for (int i = 0; i < 48; i++)
            {
                float a = 2f * Mathf.PI * i / 48f;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                // Girdles the trunk just OUTSIDE the strand envelope (identical prism
                // volume, long axis chained around the circumference) so the permanent
                // landmark reads as a continuous gold belt, not dots buried in the braid.
                Emit(new Vector3(radial.x * 40f, heartY, radial.z * 40f),
                    SpawnPoint.LookRotation(tangent, radial),
                    new Vector3(2f, 2f, 8.192f), Domains.Gold, PrismKind.SuperShielded);
            }

            // Root buttresses.
            for (int b = 0; b < 10; b++)
            {
                float a0 = 2f * Mathf.PI * b / 10f;
                float len = 100f + 60f * Hash01(b * 77 + _noiseSeed);
                Vector3 prevMid = Vector3.zero;
                for (int i = 0; i < 58; i++)
                {
                    float t = i / 57f;
                    float rr = 27f + t * len;
                    float y = BowlY(rr) + 30f * (1f - t) * (1f - t) + 3f;
                    float a = a0 + 0.55f * Mathf.Sin(t * 2.6f + b);
                    var mid = new Vector3(rr * Mathf.Cos(a), y, rr * Mathf.Sin(a));
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), Vector3.up)
                        : SpawnPoint.LookRotation(mid - prevMid, Vector3.up);
                    for (int w = -1; w <= 1; w++)
                        Emit(new Vector3(mid.x - w * 4.4f * Mathf.Sin(a), mid.y, mid.z + w * 4.4f * Mathf.Cos(a)),
                            rot, Jit(new Vector3(4.8f, 1.6f, 2.8f)), Domains.Jade);
                    prevMid = mid;
                }
            }

            // Branches: golden-angle fan, two split generations, vines on every third.
            for (int b = 0; b < 34; b++)
            {
                float t0 = 0.40f + 0.57f * (b / 34f);
                float yb = baseY + height * t0;
                float ang = b * GoldenAngle;
                Branch(new Vector3(14f * Mathf.Cos(ang), yb, 14f * Mathf.Sin(ang)), ang,
                    165f * (1.18f - t0) + 48f, 58f * (1f - t0) + 18f, 2, b);
            }

            // Canopy dome.
            int leaves = Scaled(9400);
            for (int i = 0; i < leaves; i++)
            {
                float u = i / (float)leaves;
                float a = i * GoldenAngle;
                float rr = 180f * Mathf.Sqrt(u);
                float dome = 95f * Mathf.Cos(u * Mathf.PI * 0.5f);
                float wob = 18f * N01(Mathf.Cos(a) * 3f + 3f, u * 6f, Mathf.Sin(a) * 3f, 5);
                // Three vessel-sized partings spiral from the (solid) crown cap to the rim
                // so the canopy is a dappled roof with flyways, not a sealed shell.
                float lane = Mathf.Repeat(a + 0.8f * Mathf.Sqrt(u), 2f * Mathf.PI / 3f);
                if (rr > 45f && lane * rr < 13f) continue;
                var p = new Vector3(rr * Mathf.Cos(a), Crown - 50f + dome * 0.62f + wob - 32f * u, rr * Mathf.Sin(a));
                var normal = (Vector3.up + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.55f * u)).normalized;
                var azimuthal = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                Emit(p, SpawnPoint.LookRotation(normal, azimuthal),
                    Jit(new Vector3(6.4f, 4.2f, 0.7f)), i % 4 != 0 ? Domains.Jade : Domains.Gold);
                if (i % 150 == 75)
                    Emit(new Vector3(p.x, p.y - 6f, p.z), Quaternion.identity,
                        new Vector3(2.6f, 2.6f, 2.6f), Domains.Gold, PrismKind.Shielded);
            }

            // Kelp veils.
            for (int s = 0; s < 64; s++)
            {
                float a = s * GoldenAngle;
                float rr = 95f + (OuterR - 120f) * Mathf.Sqrt(Hash01(s * 911 + _noiseSeed));
                var basePos = new Vector3(rr * Mathf.Cos(a), BowlY(rr), rr * Mathf.Sin(a));
                float h = 160f + 140f * Hash01(s * 13 + 1);
                int segs = (int)(h / 3.4f);
                Vector3 p = basePos, prev = basePos;
                for (int i = 0; i < segs; i++)
                {
                    float t = i * 3.4f / h;
                    var drift = Curl(p, 0.007f, 9);
                    p = new Vector3(p.x + drift.x * 2.2f, basePos.y + h * t, p.z + drift.z * 2.2f);
                    var up = i == 0 ? Vector3.up : (p - prev).normalized;
                    Emit(p, SpawnPoint.LookRotation(up, Vector3.right),
                        Jit(new Vector3(1.4f, 0.7f, 3.2f), 0.15f),
                        (i / 7) % 2 == 0 ? Domains.Jade : Domains.Gold);
                    if (i % 3 == 1)
                    {
                        float side = (i / 3) % 2 == 0 ? 1f : -1f;
                        var lateral = Vector3.Cross(up, Vector3.up).sqrMagnitude > 0.01f
                            ? Vector3.Cross(up, Vector3.up).normalized : Vector3.right;
                        Emit(p + lateral * (2.6f * side), SpawnPoint.LookRotation(lateral * side, up),
                            new Vector3(1.6f, 0.6f, 3.4f), Domains.Jade);
                    }
                    prev = p;
                }
            }

            // Dune floor.
            for (int row = 0; row < 31; row++)
            {
                float R = 70f + (OuterR - 85f) * row / 31f;
                int n = (int)(2f * Mathf.PI * R / 9.8f);
                Domains dom = (row % 3) switch { 0 => Domains.Jade, 1 => Domains.Blue, _ => Domains.Gold };
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n;
                    float wob = 16f * N01(Mathf.Cos(a) * 3f + row * 0.31f, row * 0.5f, Mathf.Sin(a) * 3f, 13);
                    if (N01(a * 2.3f, row * 0.8f, 4.5f, 14) < 0.36f) continue;
                    float rr = R + wob;
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    Emit(new Vector3(rr * Mathf.Cos(a), BowlY(rr) - 2f, rr * Mathf.Sin(a)),
                        SpawnPoint.LookRotation(Vector3.up, tangent),
                        Jit(new Vector3(3.4f, 5.4f, 0.9f), 0.3f), dom);
                }
            }

            // Firefly currents orbiting the tree.
            int lines = Scaled(14);
            for (int s = 0; s < lines; s++)
            {
                float a = s * GoldenAngle * 3.7f;
                var p = new Vector3(300f * Mathf.Cos(a), 40f + 120f * Hash01(s * 29 + _noiseSeed) - 60f, 300f * Mathf.Sin(a));
                for (int i = 0; i < 190; i++)
                {
                    var v = Curl(p, 0.006f, 12);
                    var toCentre = new Vector3(-p.x, 0f, -p.z).normalized;
                    var orbit = new Vector3(-p.z, 0f, p.x).normalized;
                    var d = (v * 0.8f + toCentre * 0.28f + orbit * 0.7f).normalized;
                    if ((p.y + d.y * 5f < Floor + 12f && d.y < 0f) || (p.y + d.y * 5f > Crown + 30f && d.y > 0f))
                        d = new Vector3(d.x, -d.y, d.z);
                    p += d * 5f;
                    if (Mathf.Sqrt(p.x * p.x + p.z * p.z) > OuterR * 1.15f) break;
                    Emit(p, SpawnPoint.LookRotation(d, Vector3.up),
                        new Vector3(1.7f, 0.8f, 4f), s % 3 != 0 ? Domains.Gold : Domains.Blue);
                }
            }
        }

        void Branch(Vector3 root, float ang, float len, float droop, int level, int branchSeed)
        {
            int segs = Mathf.Max(9, (int)(len / 4.4f));
            var dir = new Vector3(Mathf.Cos(ang), 0.28f, Mathf.Sin(ang)).normalized;
            Vector3 p = root, prev = root;
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                p = root + dir * (len * t);
                p = new Vector3(p.x + 7f * N01(t * 4f, branchSeed * 3f, level * 9f, 2) - 3.5f, p.y - droop * t * t, p.z);
                Emit(p, SpawnPoint.LookRotation(p - prev, Vector3.up), Jit(new Vector3(
                    2.6f * (1f - 0.3f * t) + 0.8f, 1.5f, 4.6f * (1f - 0.4f * t) + 1.2f)), Domains.Jade);
                if (level == 2 && i % 8 == 4 && t > 0.3f && t < 0.85f)
                    Emit(new Vector3(p.x, p.y - 3.4f, p.z), Quaternion.identity,
                        new Vector3(1.3f, 3f, 1.3f), Domains.Ruby, PrismKind.Danger);
                prev = p;
            }

            int petals = level == 2 ? 30 : 16;
            for (int k = 0; k < petals; k++)
            {
                float a = k * GoldenAngle;
                float rr = 3.1f * Mathf.Sqrt(k + 0.5f);
                Emit(p + new Vector3(rr * Mathf.Cos(a), 0.4f * rr, rr * Mathf.Sin(a)),
                    SpawnPoint.LookRotation(new Vector3(Mathf.Cos(a), 0.35f, Mathf.Sin(a)), Vector3.up),
                    new Vector3(1.4f, 0.6f, 2f), Domains.Gold);
            }

            if (level > 0)
                for (int c = 0; c < 2; c++)
                    Branch(p, ang + (c * 2 - 1) * 0.85f + 0.22f * Mathf.Sin(branchSeed),
                        len * 0.56f, droop * 0.72f, level - 1, branchSeed * 5 + c + 1);

            // Air-root vine hanging from every third branch's midpoint.
            if (level >= 1 && branchSeed % 3 == 1)
            {
                var mid = root + dir * (len * 0.6f);
                mid = new Vector3(mid.x, mid.y - droop * 0.36f, mid.z);
                int vl = (int)(14f + 10f * Hash01(branchSeed * 7));
                for (int v = 0; v < vl; v++)
                    Emit(new Vector3(mid.x + 1.5f * Mathf.Sin(v * 0.7f + branchSeed), mid.y - v * 2.6f,
                            mid.z + 1.5f * Mathf.Cos(v * 0.5f)),
                        Quaternion.identity, new Vector3(0.8f, 2.4f, 0.8f), Domains.Jade);
            }
        }
    }
}
