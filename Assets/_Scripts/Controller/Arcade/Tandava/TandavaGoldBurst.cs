using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tandava's one visual effect, and it is not fire (the prompter took fire out of the mode): a burst of GOLD PRISM
    /// debris - the platform's own prism death shards (<see cref="PrismDebris"/>, the batched entity debris every dying
    /// prism throws) painted in the Gold domain's prism colours - and a gold light over the place
    /// (<see cref="PrismLit.PublishLight(int, in LitVolume, float, Domains, float, bool)"/>, the controller's flash). It
    /// marks the moments the story turns: a form taken (thrown from the creature's own members, so the old shape seems to
    /// shatter into the new one) and a halo ring broken (thrown from where the ring stood).
    ///
    /// <para><b>Debris, not mass.</b> These are visual shards with no collider, no volume and no owner - nothing the cell
    /// counts, nothing a creature eats - so the effect cannot leak into the ecology it decorates. The shards fly and fade
    /// on their own (PrismExplosion's duration), which is the continuity law: nothing pops out.</para>
    /// </summary>
    public static class TandavaGoldBurst
    {
        // a fallback gold for a theme with no Gold colour set (the shipped OriginalColorSetSO's gold, normalised)
        static readonly Color s_bright = new(1f, 0.82f, 0.25f), s_dark = new(0.42f, 0.27f, 0.04f);

        /// <summary>
        /// Throw <paramref name="shards"/> gold shards from <paramref name="from"/> (round-robin; empty = a ball round
        /// <paramref name="centre"/>), each flying out from <paramref name="centre"/> at about <paramref name="speed"/>
        /// world units/s. Returns how many the debris service took (it drops them when it is off or saturated - an
        /// effect, never a requirement).
        /// </summary>
        public static int Burst(ThemeManagerDataContainerSO theme, Vector3 centre, IReadOnlyList<Vector3> from, int shards,
                                float speed, float scale, PrismKind kind = PrismKind.Plain)
        {
            Color bright = s_bright, dark = s_dark;
            if (theme && theme.ColorSet && theme.ColorSet.TryGetPrismKindColors(Domains.Gold, kind, out var b, out var d))
            {
                bright = b; dark = d;
            }
            int taken = 0;
            for (int k = 0; k < shards; k++)
            {
                Vector3 at = from is { Count: > 0 } ? from[k % from.Count] : centre + Random.insideUnitSphere * (4f * scale);
                Vector3 dir = at - centre;
                if (dir.sqrMagnitude < 1e-4f) dir = Random.onUnitSphere;
                dir = (dir.normalized + 0.4f * Random.onUnitSphere).normalized;
                Vector3 velocity = dir * (speed * Random.Range(0.6f, 1.25f));
                if (PrismDebris.TryRequestExplosion(at, Random.rotation, Vector3.one * scale, bright, dark, velocity, speed * 1.5f, kind))
                    taken++;
            }
            return taken;
        }
    }
}
