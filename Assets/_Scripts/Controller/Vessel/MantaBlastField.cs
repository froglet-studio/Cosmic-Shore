using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every blast that can set off a Manta bomb, on THIS machine (Caleb, 2026-11-16: "Manta bomb and prism explosions
    /// should set each other off"). A Manta bloom registers itself as it spawns (<see cref="MantaBomb.SpawnBloom"/>,
    /// which runs on the planter's machine and on every peer's relayed copy alike), and an explosive prism's death
    /// registers its chain beat (<see cref="PrismExplosiveChain.Fire"/>). Each live <see cref="MantaBomb"/> asks
    /// <see cref="Reaches"/> every frame and goes off when a blast's growing front gets to it.
    ///
    /// <para><b>A blast only sets off bombs that were already there when it started.</b> Contagion plants bombs on
    /// everything a bloom catches; without this rule each of those would go off in the bloom that planted it and the
    /// cascade would never stop. A bomb jousted or dropped earlier, caught by a later blast, goes off as Caleb asked.</para>
    ///
    /// Explosive prisms in a blast need nothing here: a bloom already damages every rival prism it reaches, and a primed
    /// prism's death fires its own chain. Local and stateless across machines, like the chain itself.
    /// </summary>
    public static class MantaBlastField
    {
        struct Blast
        {
            public Vector3 Center;
            public float Radius;
            public float StartTime;
            public float GrowSeconds;
        }

        static readonly List<Blast> s_blasts = new();

        /// <summary>Seconds a blast is kept past its full size, so a bomb whose frame lands just after still hears it.</summary>
        const float LingerSeconds = 0.25f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_blasts.Clear();

        /// <summary>Live blasts - a diagnostics/test read.</summary>
        public static int Count => s_blasts.Count;

        /// <summary>
        /// A blast of <paramref name="radius"/> at <paramref name="center"/> starts at <paramref name="startTime"/> and
        /// grows to full size over <paramref name="growSeconds"/> on the bloom's own ease (0 = full at once).
        /// </summary>
        public static void Register(Vector3 center, float radius, float startTime, float growSeconds)
        {
            if (radius <= 0f) return;
            s_blasts.Add(new Blast { Center = center, Radius = radius, StartTime = startTime, GrowSeconds = Mathf.Max(0f, growSeconds) });
        }

        /// <summary>
        /// The front's radius <paramref name="elapsed"/> seconds after the blast started: the AOE bloom's own ease
        /// (<c>AOEExplosion</c>: radius x sin(t x pi/2)), or the full radius at once for a zero grow time.
        /// </summary>
        public static float FrontRadius(float radius, float growSeconds, float elapsed)
        {
            if (elapsed < 0f) return 0f;
            if (growSeconds <= 0f) return radius;
            return radius * Mathf.Sin(Mathf.Clamp01(elapsed / growSeconds) * Mathf.PI * 0.5f);
        }

        /// <summary>
        /// Has any blast that started after <paramref name="bornAt"/> reached <paramref name="position"/> by
        /// <paramref name="now"/>? Spent blasts are dropped as they are passed.
        /// </summary>
        public static bool Reaches(Vector3 position, float bornAt, float now)
        {
            for (int i = s_blasts.Count - 1; i >= 0; i--)
            {
                var b = s_blasts[i];
                if (now > b.StartTime + b.GrowSeconds + LingerSeconds) { s_blasts.RemoveAt(i); continue; }
                if (b.StartTime <= bornAt) continue;
                float r = FrontRadius(b.Radius, b.GrowSeconds, now - b.StartTime);
                if (r > 0f && (position - b.Center).sqrMagnitude <= r * r) return true;
            }
            return false;
        }

        /// <summary>Forgets every blast (tests).</summary>
        public static void Clear() => s_blasts.Clear();
    }
}
