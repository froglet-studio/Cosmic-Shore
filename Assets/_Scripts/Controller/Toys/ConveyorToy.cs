using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The microscene conveyor toy: fly through it and a belt of little worlds — prism gate runs,
    /// helix weaves, tunnels, orchards, meadows, menageries — starts blooming in ahead of your
    /// flight path, scene after scene, like an open world crossed with an infinite runner. Once
    /// the pool is full the belt recycles the scene farthest behind you into a fresh arrangement
    /// ahead (a closed system: the same conserved mass, endlessly re-arranged). No score, no end
    /// condition — fly it forever; fly back through the toy after leaving freestyle to pick the
    /// ride back up.
    /// </summary>
    public class ConveyorToy : Toy
    {
        ConveyorConfig _cfg;
        MicrosceneConveyor _conveyor;

        public void Configure(ConveyorConfig cfg) => _cfg = cfg;

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (_cfg == null)
            {
                CSDebug.LogWarning("[ConveyorToy] No config assigned — nothing to run.");
                return;
            }
            if (localVessel?.Vessel == null) return;

            if (_conveyor)
            {
                _conveyor.Resume(localVessel);
                return;
            }

            if (!_cfg.PrismPrefab)
                CSDebug.LogWarning("[ConveyorToy] No prism prefab wired — scenes will carry only " +
                                   "crystals and lifeforms. Author the definition asset (or run " +
                                   "Tools > Cosmic Shore > Setup Freestyle Toybox) to wire one.");

            // Sibling of the toy under the toybox root (NOT a child of the toy — the toy's root
            // scale animates on bloom/rebloom and must never scale the belt's laid mass). Still
            // torn down with the toybox root on scene exit.
            var go = new GameObject("MicrosceneConveyor");
            go.transform.SetParent(transform.parent, false);
            _conveyor = go.AddComponent<MicrosceneConveyor>();
            _conveyor.Begin(_cfg, localVessel, Context?.IsFreestyleActive, Context?.GameData);
        }
    }
}
