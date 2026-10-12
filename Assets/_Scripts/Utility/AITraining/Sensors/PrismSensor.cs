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
    /// It used to be a <c>Physics.OverlapSphereNonAlloc</c>. Moving to the index
    /// bought three things: no silent truncation of a dense cell to the 64-entry
    /// NonAlloc buffer, no <c>GetComponentInParent</c> per hit (the index returns the
    /// <see cref="Prism"/> directly), and the same spatial API and population
    /// (LIVE prisms: active, not destroyed, reservations excluded) the rest of the
    /// game queries, independent of physics layers.
    ///
    /// It did NOT close the spawn-window blind spot. A laid prism waits
    /// <see cref="Prism.waitTime"/> (0.6 s) in <c>CreateBlockCoroutine</c>, and
    /// <c>FinishCreation</c> then enables its collider and registers it in the index
    /// in the same call, so a prism laid right in front of the hull is invisible to
    /// this sensor for those 0.6 s exactly as it was to the collider scan. Seeing it
    /// earlier needs a registration (or a reservation-visible query) at lay time,
    /// which is a <see cref="PrismSpatialIndex"/> / <see cref="Prism"/> lifecycle
    /// change of its own.
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

            // Closest-first selection. _scratch holds every live prism within MaxRange
            // (the query has no cap), so this sort grows with the mass in range, as the
            // query grows with the index (README, Performance notes). Not profiled yet:
            // measure TrainingPilot.Update before replacing it with a bounded nearest-K pick.
            _scratch.Sort((a, b) => a.Range.CompareTo(b.Range));
            int take = Mathf.Min(_scratch.Count, MaxPrisms);
            for (int i = 0; i < take; i++) ctx.NearbyPrisms.Add(_scratch[i]);
        }
    }
}
