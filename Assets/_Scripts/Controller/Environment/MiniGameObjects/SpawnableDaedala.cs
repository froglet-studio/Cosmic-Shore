using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Daedala" - the road-city cell environment (~34k prisms): Atlantis's built half,
    /// expanded into a full Escher marble city. Four undulating ring-terraces climb toward
    /// the rim, joined by spiral stairs and studded with colonnade gates; TWO interleaved
    /// trefoil causeways of opposite chirality roll a full twist each; triumphal arches,
    /// radial aqueduct spans, helical minaret towers, plaza rosettes, floating melt-atolls
    /// with nautilus crowns, and catenary lantern strings knit it together. Marble gold +
    /// lapis blue; shielded capitals/finials/shrines are the jewels. No danger - the city
    /// is the safe wonder; its risk is getting lost.
    /// </summary>
    public class SpawnableDaedala : CellEnvironmentSpawnableBase
    {
        protected override int DefaultSeed => 23;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableDaedala), 3);

        static readonly (float R, float yc, Domains dom)[] Rings =
        {
            (150f, -95f, Domains.Gold),
            (230f, -25f, Domains.Blue),
            (310f, 45f, Domains.Gold),
            (390f, 115f, Domains.Blue),
        };

        protected override void BuildEnvironment()
        {
            BuildTerraces();
            BuildCauseways();
            BuildArches();
            BuildAqueducts();
            BuildTowersAndLanterns();
            BuildPlazas();
            BuildAtolls();
        }

        void BuildTerraces()
        {
            for (int ri = 0; ri < Rings.Length; ri++)
            {
                var (R, yc, dom) = Rings[ri];
                int n = (int)(2f * Mathf.PI * R / 3.5f);
                int gateEvery = Mathf.Max(1, n / 11);
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n;
                    float und = 22f * Mathf.Sin(a * 3f + ri * 1.7f)
                                + 9f * N01(Mathf.Cos(a) * 4f + 9f, ri * 5f, Mathf.Sin(a) * 4f, 3);
                    float y = yc + und;
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var deckRot = SpawnPoint.LookRotation(Vector3.up, tangent);
                    for (int w = 0; w < 5; w++)
                    {
                        float rr = R + (w - 2) * 6.2f;
                        Emit(new Vector3(rr * Mathf.Cos(a), y + (w % 2) * 0.5f, rr * Mathf.Sin(a)),
                            deckRot, Jit(new Vector3(4f, 6.2f, 1f), 0.12f), dom);
                    }
                    if (i % gateEvery == 0)
                        Gate(R, a, y, ri % 2 != 0 ? Domains.Jade : dom);
                }

                if (ri < Rings.Length - 1)
                {
                    var next = Rings[ri + 1];
                    for (int s = 0; s < 7; s++)
                        Stair(R, yc, next.R, next.yc, 2f * Mathf.PI * s / 7f + ri * 0.6f, ri);
                }
            }
        }

        void Gate(float R, float a, float y, Domains dom)
        {
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            var center = new Vector3(R * ca, y, R * sa);
            var tangent = new Vector3(-sa, 0f, ca);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 basePos = center + tangent * (9.5f * side);
                float yaw = RangeF(0f, 360f);
                for (int h = 0; h < 11; h++)
                    Emit(basePos + new Vector3(0f, 2f + h * 2.6f, 0f), Quaternion.Euler(0f, yaw, 0f),
                        Jit(new Vector3(2.3f, 2.5f, 2.3f), 0.1f), dom);
                Emit(basePos + new Vector3(0f, 2f + 11 * 2.6f, 0f), Quaternion.identity,
                    new Vector3(2.8f, 2.8f, 2.8f), Domains.Gold, PrismKind.Shielded);
            }
            for (int k = 0; k < 9; k++)
            {
                float t = k / 8f;
                float x = Mathf.Lerp(-9.5f, 9.5f, t);
                Emit(center + tangent * x + new Vector3(0f, 33f + 6f * Mathf.Sin(t * Mathf.PI), 0f),
                    SpawnPoint.LookRotation(tangent, Vector3.up), new Vector3(2.3f, 1.2f, 3.2f), dom);
            }
        }

        /// <summary>The terrace deck's undulation at angle <paramref name="a"/> on ring
        /// <paramref name="ring"/> - identical math/args to BuildTerraces, so stairs and
        /// plazas dock onto the exact deterministic deck height instead of floating beside it.</summary>
        float Und(float a, int ring) =>
            22f * Mathf.Sin(a * 3f + ring * 1.7f)
            + 9f * N01(Mathf.Cos(a) * 4f + 9f, ring * 5f, Mathf.Sin(a) * 4f, 3);

        void Stair(float R1, float y1, float R2, float y2, float a0, int ri)
        {
            Domains dom = ri % 2 != 0 ? Domains.Gold : Domains.Blue;
            Vector3 prevMid = Vector3.zero;
            for (int i = 0; i < 52; i++)
            {
                float t = i / 51f;
                float a = a0 + t * 1.9f;
                float R = Mathf.Lerp(R1, R2, t);
                float y = Mathf.Lerp(y1 + Und(a0, ri), y2 + Und(a0 + 1.9f, ri + 1), t) + 6f * Mathf.Sin(t * Mathf.PI);
                var mid = new Vector3(R * Mathf.Cos(a), y, R * Mathf.Sin(a));
                Quaternion rot = i == 0
                    ? SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up)
                    : SpawnPoint.LookRotation(mid - prevMid, Vector3.up);
                for (int w = -1; w <= 1; w++)
                {
                    float rr = R + w * 4.6f;
                    Emit(new Vector3(rr * Mathf.Cos(a), y, rr * Mathf.Sin(a)), rot,
                        Jit(new Vector3(4.4f, 0.9f, 3f), 0.1f), dom);
                }
                prevMid = mid;
            }
        }

        void BuildCauseways()
        {
            // Twin trefoils, opposite chirality, offset heights - the city's woven arteries.
            for (int cw = 0; cw < 2; cw++)
            {
                float scale = cw == 0 ? 120f : 88f;
                float yoff = cw == 0 ? 25f : 60f;
                float chir = cw == 0 ? 1f : -1f;
                Domains deckDom = cw == 0 ? Domains.Gold : Domains.Blue;
                Domains railDom = cw == 0 ? Domains.Blue : Domains.Gold;
                int n = Scaled(cw == 0 ? 1290 : 960);

                var pts = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    float t = 2f * Mathf.PI * i / n * chir;
                    pts[i] = new Vector3(
                        (Mathf.Sin(t) + 2f * Mathf.Sin(2f * t)) * scale,
                        -Mathf.Sin(3f * t) * scale * 0.9f + yoff,
                        (Mathf.Cos(t) - 2f * Mathf.Cos(2f * t)) * scale);
                }

                for (int i = 0; i < n; i++)
                {
                    Vector3 p = pts[i];
                    Vector3 tangent = (pts[(i + 1) % n] - p).normalized;
                    float theta = 2f * Mathf.PI * i / n;
                    Vector3 side = Vector3.Cross(tangent, Vector3.up);
                    side = side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.right;
                    Vector3 up = Vector3.Cross(side, tangent).normalized;
                    Vector3 rolledSide = side * Mathf.Cos(theta) + up * Mathf.Sin(theta);
                    Vector3 rolledNormal = Vector3.Cross(tangent, rolledSide).normalized;
                    for (int w = -2; w <= 2; w++)
                    {
                        Vector3 q = p + rolledSide * (w * 4.9f);
                        if (w == -2 || w == 2)
                        {
                            bool shrine = i % 40 == 0;
                            Emit(q, SpawnPoint.LookRotation(rolledNormal, tangent),
                                new Vector3(2.8f, 2f, 2.8f), railDom,
                                shrine ? PrismKind.Shielded : PrismKind.Plain);
                        }
                        else
                        {
                            Emit(q, SpawnPoint.LookRotation(rolledNormal, tangent),
                                Jit(new Vector3(4f, 5.6f, 1.1f), 0.12f), deckDom);
                        }
                    }
                }
            }
        }

        void BuildArches()
        {
            for (int k = 0; k < 18; k++)
            {
                float a = 2f * Mathf.PI * k / 18f + 0.31f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                var basePos = new Vector3(285f * ca, 46f, 285f * sa);
                var tangent = new Vector3(-sa, 0f, ca);
                Vector3 prev = basePos + tangent * -18f;
                for (int i = 0; i < 26; i++)
                {
                    float t = i / 25f;
                    float aa = Mathf.PI * t;
                    var p = basePos + tangent * (-18f * Mathf.Cos(aa)) + new Vector3(0f, 30f * Mathf.Sin(aa), 0f);
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(Vector3.up, new Vector3(ca, 0f, sa))
                        : SpawnPoint.LookRotation(p - prev, new Vector3(ca, 0f, sa));
                    Emit(p, rot, new Vector3(2.6f, 2.2f, 3.4f), Domains.Gold);
                    prev = p;
                }
                for (int side = -1; side <= 1; side += 2)
                    for (int h = 0; h < 6; h++)
                        Emit(basePos + tangent * (18f * side) + new Vector3(0f, h * 3.2f, 0f),
                            Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f),
                            Jit(new Vector3(3.2f, 3f, 3.2f), 0.12f), Domains.Blue);
            }
        }

        void BuildAqueducts()
        {
            for (int k = 0; k < 11; k++)
            {
                float a = 2f * Mathf.PI * k / 11f + 0.45f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                var prevMid = new Vector3(155f * ca, -90f, 155f * sa);
                for (int i = 0; i < 44; i++)
                {
                    float t = i / 43f;
                    float rr = Mathf.Lerp(155f, 385f, t);
                    float y = Mathf.Lerp(-90f, 112f, t) + 10f * Mathf.Sin(t * Mathf.PI * 3f);
                    var mid = new Vector3(rr * ca, y, rr * sa);
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(new Vector3(ca, 0.88f, sa), Vector3.up)
                        : SpawnPoint.LookRotation(mid - prevMid, Vector3.up);
                    prevMid = mid;
                    // Lapis water-channel rails on gold piers - the aqueduct holds the city's
                    // two-tone band; Jade stays reserved for the gate accents.
                    for (int w = -1; w <= 1; w += 2)
                        Emit(new Vector3(rr * ca - w * 3.4f * sa, y, rr * sa + w * 3.4f * ca),
                            rot, new Vector3(2.6f, 1.4f, 3.6f), Domains.Blue);
                    if (i % 5 == 2)
                        for (int hh = 0; hh < 4; hh++)
                            Emit(new Vector3(rr * ca, y - 4f - hh * 3.4f, rr * sa),
                                Quaternion.identity, new Vector3(1.8f, 3f, 1.8f), Domains.Gold);
                }
            }
        }

        void BuildTowersAndLanterns()
        {
            // Helical minarets.
            for (int k = 0; k < 14; k++)
            {
                float a = k * GoldenAngle * 2.3f;
                float rr = 185f + 170f * Hash01(k * 37 + _noiseSeed);
                var basePos = new Vector3(rr * Mathf.Cos(a), -60f + 120f * Hash01(k * 53), rr * Mathf.Sin(a));
                float h = 70f + 40f * Hash01(k * 11);
                int n = (int)(h / 1.4f);
                Vector3 prev = basePos;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)n;
                    float aa = t * 5.2f * Mathf.PI;
                    float r2 = 9f * (1f - t * 0.6f);
                    var p = basePos + new Vector3(r2 * Mathf.Cos(aa), h * t, r2 * Mathf.Sin(aa));
                    Emit(p, SpawnPoint.LookRotation(p - prev, Vector3.up),
                        new Vector3(2.6f, 1.2f, 3f), k % 2 != 0 ? Domains.Gold : Domains.Blue);
                    prev = p;
                }
                Emit(basePos + new Vector3(0f, h + 4f, 0f), Quaternion.identity,
                    new Vector3(2.6f, 2.6f, 2.6f), Domains.Ruby, PrismKind.Shielded);
            }

            // Catenary lantern strings tied tower-top to tower-top (varying stride so the
            // second lap of k picks different partner pairs instead of duplicating strings).
            for (int k = 0; k < 30; k++)
            {
                int i1 = k % 14;
                int stride = 3 + 4 * (k / 14);
                int i2 = (i1 + stride) % 14;
                float a1 = i1 * GoldenAngle * 2.3f, a2 = i2 * GoldenAngle * 2.3f;
                float r1 = 185f + 170f * Hash01(i1 * 37 + _noiseSeed);
                float r2 = 185f + 170f * Hash01(i2 * 37 + _noiseSeed);
                var p1 = new Vector3(r1 * Mathf.Cos(a1), -60f + 120f * Hash01(i1 * 53) + 70f + 40f * Hash01(i1 * 11), r1 * Mathf.Sin(a1));
                var p2 = new Vector3(r2 * Mathf.Cos(a2), -60f + 120f * Hash01(i2 * 53) + 70f + 40f * Hash01(i2 * 11), r2 * Mathf.Sin(a2));
                for (int i = 0; i < 34; i++)
                {
                    float t = i / 33f;
                    float sag = 28f * Mathf.Sin(t * Mathf.PI);
                    var p = new Vector3(Mathf.Lerp(p1.x, p2.x, t), Mathf.Lerp(p1.y, p2.y, t) - sag, Mathf.Lerp(p1.z, p2.z, t));
                    Domains dom = (i % 4) switch { 0 => Domains.Gold, 1 => Domains.Jade, 2 => Domains.Ruby, _ => Domains.Blue };
                    Emit(p, SpawnPoint.LookRotation(p2 - p1, Vector3.up), new Vector3(1.1f, 1.1f, 1.6f), dom);
                }
            }
        }

        void BuildPlazas()
        {
            for (int k = 0; k < 18; k++)
            {
                int ri = k % 4;
                var (R, yc, _) = Rings[ri];
                float a = 2f * Mathf.PI * Hash01(k * 61 + _noiseSeed);
                // Landing pads hover a consistent 8 units beside their road (docked to the
                // deck's undulation). Ruby stays exclusive to the shielded tower finials.
                var cx = new Vector3((R + 16f) * Mathf.Cos(a), yc + Und(a, ri) + 8f, (R + 16f) * Mathf.Sin(a));
                Domains dom = (k % 3) switch { 0 => Domains.Gold, 1 => Domains.Blue, _ => Domains.Jade };
                for (int i = 0; i < 64; i++)
                {
                    float aa = i * GoldenAngle;
                    float rr = 14f * Mathf.Sqrt(i / 64f);
                    Emit(cx + new Vector3(rr * Mathf.Cos(aa), 1.5f * Mathf.Sin(rr * 0.8f), rr * Mathf.Sin(aa)),
                        SpawnPoint.LookRotation(Vector3.up, new Vector3(Mathf.Cos(aa), 0f, Mathf.Sin(aa))),
                        new Vector3(2.8f, 2.8f, 0.6f), dom);
                }
            }
        }

        void BuildAtolls()
        {
            for (int k = 0; k < 15; k++)
            {
                float a = k * GoldenAngle * 2.1f;
                float rr = 170f + 200f * Hash01(k * 37 + _noiseSeed);
                float y = 150f + 120f * Hash01(k * 53 + 3);
                var cx = new Vector3(rr * Mathf.Cos(a), y, rr * Mathf.Sin(a));
                float R = 14f + 11f * Hash01(k * 11);
                Domains dom = k % 2 != 0 ? Domains.Blue : Domains.Gold;
                for (int L = 0; L < 8; L++)
                {
                    float t = L / 7f;
                    float r2 = R * (1f - t * 0.85f) * (1f + 0.25f * Mathf.Sin(t * 9f + k));
                    int m = Mathf.Max(6, (int)(2f * Mathf.PI * r2 / 3.4f));
                    for (int i = 0; i < m; i++)
                    {
                        float aa = 2f * Mathf.PI * i / m + L * 0.35f;
                        var radial = new Vector3(Mathf.Cos(aa), 0f, Mathf.Sin(aa));
                        Emit(cx + new Vector3(radial.x * r2, -t * R * 1.3f, radial.z * r2),
                            SpawnPoint.LookRotation(new Vector3(-radial.z, 0f, radial.x), radial),
                            Jit(new Vector3(3f, 2f, 3f), 0.25f), dom);
                    }
                }
                Vector3 prevS = cx;
                for (int i = 0; i < 40; i++)
                {
                    float t = i / 40f;
                    float aa = t * 2.4f * 2f * Mathf.PI;
                    float r2 = 1.6f * Mathf.Exp(0.22f * aa);
                    if (r2 > R * 1.5f) break;
                    var p = cx + new Vector3(r2 * Mathf.Cos(aa), R * 0.3f + t * R * 0.7f, r2 * Mathf.Sin(aa));
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(Vector3.forward, Vector3.up)
                        : SpawnPoint.LookRotation(p - prevS, Vector3.up);
                    Emit(p, rot, new Vector3(1.7f, 1.1f, 1f), Domains.Gold);
                    prevS = p;
                }
            }
        }
    }
}
