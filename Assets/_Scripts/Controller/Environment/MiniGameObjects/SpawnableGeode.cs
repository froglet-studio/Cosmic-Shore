using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Geode" - the crystal-cathedral cell environment (~34k prisms) and the rotation's
    /// angular, serene pole: a colossal geode cracked open along a flyable 110-unit rift. Two
    /// rough husk hemispheres carry inward crystal linings; giant druse spike clusters grow
    /// toward the centre (their first spike tipped with a super-shielded stella - the "perfect
    /// crystals"); a floating heart-crystal cluster with an orbiting shard ring holds the
    /// middle; banded agate ribbons wrap the outside equator; crystal dust drifts through the
    /// cavity and straight light shafts cross it through the crack. Amethyst blue + garnet
    /// ruby + citrine gold. No danger anywhere - stillness is the identity.
    /// </summary>
    public class SpawnableGeode : CellEnvironmentSpawnableBase
    {
        const float HuskR = 250f;
        const float CrackGap = 110f;

        protected override int DefaultSeed => 67;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableGeode), 3);

        static readonly Domains[] CrystalDoms = { Domains.Ruby, Domains.Blue, Domains.Gold };

        protected override void BuildEnvironment()
        {
            BuildHusks();
            BuildDruses();
            BuildHeart();
            BuildAgateBands();
            BuildDust();
            BuildLightShafts();
        }

        static Vector3 SpherePoint(int i, int n)
        {
            float u = (i + 0.5f) / n;
            float y = 1f - 2f * u;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = i * GoldenAngle;
            return new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
        }

        void BuildHusks()
        {
            for (int half = 0; half < 2; half++)
            {
                float sign = half == 0 ? 1f : -1f;
                const int n = 9400;
                for (int i = 0; i < n; i++)
                {
                    var d = SpherePoint(i, n);
                    if (sign * d.x < 0.09f) continue; // this hemisphere only, with crack margin
                    float wob = 1f + 0.10f * N01(d.x * 3f + half * 9f, d.y * 3f, d.z * 3f, 0);
                    var p = new Vector3(d.x * HuskR * wob + sign * CrackGap * 0.5f, d.y * HuskR * wob, d.z * HuskR * wob);
                    float az = i * GoldenAngle;
                    var rot = SpawnPoint.LookRotation(d, new Vector3(-Mathf.Sin(az), 0f, Mathf.Cos(az)));
                    Emit(p, rot, Jit(new Vector3(5.6f, 5.2f, 1.3f), 0.3f), Domains.Blue);

                    // Inner crystal lining: an inward-pointing shard behind most husk plates.
                    if (Hash01(i * 13 + half * 7 + _noiseSeed) < 0.78f)
                    {
                        var q = new Vector3(d.x * (HuskR * wob - 9f) + sign * CrackGap * 0.5f,
                            d.y * (HuskR * wob - 9f), d.z * (HuskR * wob - 9f));
                        Emit(q, SpawnPoint.LookRotation(-d, new Vector3(-Mathf.Sin(az), 0f, Mathf.Cos(az))),
                            new Vector3(1.4f, 1.4f, 4.6f), CrystalDoms[i % 3]);
                    }
                }
            }
        }

        void BuildDruses()
        {
            for (int k = 0; k < 24; k++)
            {
                float u = Hash01(k * 77 + _noiseSeed);
                float a = k * GoldenAngle * 2.3f;
                float y = (u * 2f - 1f) * 0.85f;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var d = new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
                float sign = d.x >= 0f ? 1f : -1f;
                var basePos = new Vector3(d.x * HuskR * 0.96f + sign * CrackGap * 0.5f, d.y * HuskR * 0.96f, d.z * HuskR * 0.96f);
                var inward = (-basePos).normalized;
                int spikes = 6 + (int)(5f * Hash01(k * 13));
                for (int sp = 0; sp < spikes; sp++)
                {
                    float sa = sp * GoldenAngle;
                    var tilt = (inward + new Vector3(Mathf.Cos(sa), Mathf.Sin(sa), Mathf.Cos(sa + 2f)) * 0.35f).normalized;
                    var spikeRot = SpawnPoint.LookRotation(tilt, Vector3.up);
                    int len = (int)(34f + 30f * Hash01(k * 7 + sp));
                    for (int i = 0; i < len; i++)
                    {
                        float t = i / (float)len;
                        float w = 2.6f * (1f - t * 0.75f);
                        Emit(basePos + tilt * (t * len * 2.2f), spikeRot, new Vector3(w, w, 3.4f), CrystalDoms[(k + sp) % 3]);
                    }
                    // ONLY the first spike of each cluster is the "perfect crystal" stella -
                    // 24 landmarks total; shielding every tip buried them (and doubled the
                    // always-on mesh-collider ration).
                    Emit(basePos + tilt * (len * 2.2f), spikeRot, new Vector3(2f, 2f, 2f),
                        CrystalDoms[(k + sp) % 3], sp == 0 ? PrismKind.SuperShielded : PrismKind.Plain);
                }
            }
        }

        void BuildHeart()
        {
            for (int sp = 0; sp < 12; sp++)
            {
                float sa = sp * GoldenAngle;
                var tilt = new Vector3(Mathf.Cos(sa), Mathf.Sin(sa * 1.7f), Mathf.Sin(sa)).normalized;
                var rot = SpawnPoint.LookRotation(tilt, Vector3.up);
                for (int i = 0; i < 20; i++)
                {
                    float t = i / 20f;
                    float w = 3.2f * (1f - t * 0.6f);
                    Emit(tilt * (t * 38f), rot, new Vector3(w, w, 4f),
                        sp % 3 != 0 ? Domains.Gold : Domains.Ruby);
                }
                Emit(tilt * 38f, rot, new Vector3(2.4f, 2.4f, 2.4f), Domains.Gold, PrismKind.Shielded);
            }
            for (int i = 0; i < 90; i++)
            {
                float a = 2f * Mathf.PI * i / 90f;
                // Chain along the true 3D derivative (the wave's vertical pitch reaches ~43
                // degrees) so the orbit reads as one continuous undulating strand.
                var deriv = new Vector3(-58f * Mathf.Sin(a), 54f * Mathf.Cos(a * 3f), 58f * Mathf.Cos(a));
                Emit(new Vector3(58f * Mathf.Cos(a), 18f * Mathf.Sin(a * 3f), 58f * Mathf.Sin(a)),
                    SpawnPoint.LookRotation(deriv, Vector3.up), new Vector3(1.3f, 1f, 3.4f), Domains.Blue);
            }
        }

        void BuildAgateBands()
        {
            var bandDoms = new[] { Domains.Blue, Domains.Ruby, Domains.Gold, Domains.Blue, Domains.Gold, Domains.Ruby, Domains.Blue };
            for (int band = 0; band < 7; band++)
            {
                float R = HuskR * 1.06f + band * 7f;
                float yb = (band - 2) * 26f;
                int n = (int)(2f * Mathf.PI * R / 2.5f);
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n;
                    float x = R * Mathf.Cos(a);
                    if (Mathf.Abs(x) < CrackGap * 0.42f) continue; // bands break at the crack
                    // Full husk-half offset + 15 extra radius clears the wobbled shell at the
                    // x-poles; thin axis = radial normal with the 3.6 axis chained along the
                    // band (shingle idiom) so the ribbon wraps continuously.
                    float xoff = x > 0f ? CrackGap * 0.5f : -CrackGap * 0.5f;
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    Emit(new Vector3((R + 15f) * Mathf.Cos(a) + xoff, yb + 9f * Mathf.Sin(a * 2f + band), (R + 15f) * Mathf.Sin(a)),
                        SpawnPoint.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), tangent),
                        new Vector3(2.2f, 3.6f, 1f), bandDoms[band]);
                }
            }
        }

        void BuildDust()
        {
            int n = Scaled(6100);
            for (int i = 0; i < n; i++)
            {
                if (Hash01(i * 13) < 0.3f) continue;
                float a = i * GoldenAngle * 1.7f;
                float rr = 40f + 170f * Mathf.Sqrt(Hash01(i * 7));
                float y = (Hash01(i * 11) * 2f - 1f) * 160f;
                Emit(new Vector3(rr * Mathf.Cos(a) * 0.9f, y, rr * Mathf.Sin(a)),
                    Quaternion.Euler(0f, Hash01(i * 3 + _noiseSeed) * 360f, 0f),
                    new Vector3(0.9f, 0.9f, 1.8f), CrystalDoms[i % 3]);
            }
        }

        void BuildLightShafts()
        {
            // God-rays knife diagonally DOWN the rift slab (|x| < ~77) from the upper rim
            // past the heart's airspace - telegraphing the crack as THE entrance from
            // outside and lighting the cathedral interior. Never pierces the husk walls.
            var dir = new Vector3(30f, -520f, 130f);
            var shaftRot = SpawnPoint.LookRotation(dir, Vector3.up);
            for (int k = 0; k < 12; k++)
            {
                float x0 = -33f + 6f * k;
                float z0 = -170f + 28f * k;
                for (int i = 0; i < 46; i++)
                {
                    float t = i / 45f;
                    Emit(new Vector3(x0 + 30f * t, 265f - 520f * t, z0 + 130f * t),
                        shaftRot, new Vector3(1f, 1f, 5.2f), k % 2 != 0 ? Domains.Gold : Domains.Blue);
                }
            }
        }
    }
}
