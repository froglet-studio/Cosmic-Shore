using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Ice Shelf's measurement of the mass STANDING: every live prism in the world, summed by the pilot who owns it.
    ///
    /// <para><b>Why the mode measures instead of trusting the running stat.</b> <c>StatsManager</c> moves a pilot's
    /// VolumeRemaining by events: += when a prism finishes creating, -= when one is destroyed. Two of the Orca's three
    /// producers retire prisms another way. The helm's dropped shield expires after <c>shieldLinger</c> and the fast
    /// gun's pen recycles its oldest stroke past <c>fastGunPenCap</c>; both hand the prism straight back to its pool
    /// (<c>Prism.ReturnToPool</c>), which raises no destroyed event (read in <c>OrcaCrew.ExpireDropped</c> /
    /// <c>RetireDropped</c> and <c>StatsManager.PrismDestroyed</c>; not run). Left to the events, every shield ever raised
    /// would count at the whistle. So the server counts what is actually there: every
    /// <see cref="IceShelfController"/> sample it walks the live prisms (<see cref="PrismSpatialIndex.CopyLivePrisms"/>,
    /// active and not destroyed) and SETS each pilot's VolumeRemaining to the volume they own. Assigning is right here
    /// (Glasshouse's "deltas, never assignment" rule is about crediting ONE producer beside another): the measurement
    /// covers every producer at once - trail, pen and shield.</para>
    ///
    /// <para>The shelf column is the share of that volume still in the helm's shield, laid or dropped (the shield's prisms
    /// carry the <c>::OrcaShield::</c> owner tag, <c>OrcaCrew.ShieldTag</c>).</para>
    /// </summary>
    public sealed class IceShelfCensus
    {
        /// <summary>The owner-id tag every prism of an Orca's shield carries (<c>OrcaCrew.ShieldTag</c>).</summary>
        public const string ShieldTag = "::OrcaShield::";

        readonly List<Prism> _live = new(4096);
        readonly Dictionary<string, float> _volume = new();
        readonly Dictionary<string, float> _shelf = new();

        /// <summary>The standing volume <paramref name="pilot"/> owned at the last <see cref="Measure"/>.</summary>
        public float Volume(string pilot) => pilot != null && _volume.TryGetValue(pilot, out var v) ? v : 0f;

        /// <summary>The part of <see cref="Volume"/> that was the helm's shield, carried or dropped.</summary>
        public float Shelf(string pilot) => pilot != null && _shelf.TryGetValue(pilot, out var v) ? v : 0f;

        public void Clear()
        {
            _volume.Clear();
            _shelf.Clear();
            _live.Clear();
        }

        /// <summary>
        /// Walks every live prism and sums its volume under its owner's name, for the names in
        /// <paramref name="pilots"/> only (environment mass has no roster owner and is nobody's).
        /// </summary>
        public void Measure(IReadOnlyList<string> pilots)
        {
            _volume.Clear();
            _shelf.Clear();
            if (pilots == null) return;
            for (int i = 0; i < pilots.Count; i++)
            {
                if (string.IsNullOrEmpty(pilots[i])) continue;
                _volume[pilots[i]] = 0f;
                _shelf[pilots[i]] = 0f;
            }

            var index = PrismSpatialIndex.Instance;
            if (!index) return;
            index.CopyLivePrisms(_live);
            for (int i = 0; i < _live.Count; i++)
            {
                var p = _live[i];
                if (!p || p.destroyed || p.prismProperties == null) continue;
                var owner = p.PlayerName;
                if (string.IsNullOrEmpty(owner) || !_volume.TryGetValue(owner, out var sum)) continue;
                float v = Mathf.Max(0f, p.prismProperties.volume);
                _volume[owner] = sum + v;
                if (IsShield(p.ownerID)) _shelf[owner] += v;
            }
            _live.Clear();
        }

        /// <summary>Is this owner id one of an Orca shield's prisms?</summary>
        public static bool IsShield(string ownerId) => ownerId != null && ownerId.Contains(ShieldTag);
    }
}
