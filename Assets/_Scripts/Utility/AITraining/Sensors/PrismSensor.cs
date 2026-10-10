using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Prism scan around the vessel through <see cref="PrismSpatialIndex"/>, the
    /// canonical index of prism mass. Runs at most once per frame and reuses one
    /// scratch list, so the per-frame cost is the query plus a sort of what it
    /// returned.
    ///
    /// It used to be a <c>Physics.OverlapSphereNonAlloc</c>. That query misses the
    /// mass a pilot most needs to see: a prism's collider is off for 0.6 s after it
    /// is laid, so a trail being laid right in front of the hull was invisible, the
    /// NonAlloc buffer truncated dense cells silently, and every hit paid a
    /// <c>GetComponentInParent</c>. The index sees every LIVE prism (active, not
    /// destroyed) the moment it registers, returns the <see cref="Prism"/> directly,
    /// and is the spatial API the rest of the game queries.
    ///
    /// Prisms beyond <see cref="MaxRange"/> are never reported, so the policy layer
    /// does not filter. Sorted nearest-first so policies that take only the closest
    /// few can early-out cleanly. The same <see cref="PrismInfo"/> contract as
    /// before: position, forward, range, domain and hostility only - no reference
    /// the policies could write through.
    /// </summary>
    public class PrismSensor : ITrainingSensor
    {
        public float MaxRange = 120f;
        public int MaxPrisms = 32;

        readonly List<Prism> _query = new(128);
        readonly List<PrismInfo> _scratch = new(128);

        IVessel _vessel;

        public void Bind(IVessel vessel) => _vessel = vessel;
        public void OnEpisodeStart() { }

        public void Sample(DecisionContext ctx)
        {
            if (_vessel == null) return;
            ctx.NearbyPrisms.Clear();
            _scratch.Clear();

            // No index means no cell has registered mass yet (the first frames of
            // a scene, or an edit-mode harness). An empty neighbourhood is the
            // honest answer, not a fault.
            var index = PrismSpatialIndex.Instance;
            if (index == null) return;

            index.QuerySphere(ctx.Position, MaxRange, _query);
            for (int i = 0; i < _query.Count; i++)
            {
                var prism = _query[i];
                // The query is an unordered snapshot; a prism can be consumed
                // between the query and this read, so guard as collider
                // snapshots required.
                if (prism == null) continue;

                Vector3 pos = prism.transform.position;
                float range = (pos - ctx.Position).magnitude;
                if (range > MaxRange) continue;

                Domains pd = prism.Domain;
                bool hostile = ctx.MyDomain != Domains.Blue
                            && pd != Domains.Blue
                            && pd != ctx.MyDomain;

                _scratch.Add(new PrismInfo
                {
                    Position = pos,
                    Forward = prism.transform.forward,
                    Range = range,
                    Domain = pd,
                    IsHostile = hostile
                });
            }

            // Closest-first selection. The list is small, so the sort is cheap.
            _scratch.Sort((a, b) => a.Range.CompareTo(b.Range));
            int take = Mathf.Min(_scratch.Count, MaxPrisms);
            for (int i = 0; i < take; i++) ctx.NearbyPrisms.Add(_scratch[i]);
        }
    }
}
