using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// OverlapSphere-based prism scan around the vessel. Cheap; runs at most once per
    /// frame and reuses a non-allocating buffer.
    ///
    /// We ignore prisms beyond <see cref="MaxRange"/> entirely so the policy layer
    /// doesn't have to filter. Sorted-nearest-first so policies that take only the
    /// closest few items can early-out cleanly.
    /// </summary>
    public class PrismSensor : ITrainingSensor
    {
        public float MaxRange = 120f;
        public int MaxPrisms = 32;
        public LayerMask PrismLayerMask = ~0;

        readonly Collider[] _buffer = new Collider[64];
        readonly List<PrismInfo> _scratch = new(64);

        IVessel _vessel;

        public void Bind(IVessel vessel) => _vessel = vessel;
        public void OnEpisodeStart() { }

        public void Sample(DecisionContext ctx)
        {
            if (_vessel == null) return;
            ctx.NearbyPrisms.Clear();
            _scratch.Clear();

            int count = Physics.OverlapSphereNonAlloc(ctx.Position, MaxRange, _buffer, PrismLayerMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                var prism = col.GetComponentInParent<Prism>();
                if (prism == null) continue;

                Vector3 pos = prism.transform.position;
                float range = (pos - ctx.Position).magnitude;
                if (range > MaxRange) continue;

                Domains pd = prism.Domain;
                bool hostile = ctx.MyDomain != Domains.Unassigned
                            && pd != Domains.None
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

            // Closest-first selection. Insertion sort is fine — buffer is small.
            _scratch.Sort((a, b) => a.Range.CompareTo(b.Range));
            int take = Mathf.Min(_scratch.Count, MaxPrisms);
            for (int i = 0; i < take; i++) ctx.NearbyPrisms.Add(_scratch[i]);
        }
    }
}
