using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE CLEANSE: the Tiger Moth's dust lifts an ALLY's silence and ends their temporary elemental
    /// debuffs (Garrett, 2026-10-29: "have the dust clear silence and debuffs of allies"). Three paths
    /// reach it, all the dust: the Dust-mode capsule sweeping an ally
    /// (<see cref="VesselCleanseAllyBySkimmerEffectSO"/>), the omni-crystal bloom's wavefront passing an
    /// ally or the pilot who set it off (<see cref="ButterflyBloomDust"/>), and a drop made in Dust mode,
    /// which falls through the pilot's own dust (<see cref="TigerMothDropExecutor"/>).
    ///
    /// <para><b>What it clears.</b> The silence window (<see cref="VesselSilence.Lift"/>) and every
    /// temporary negative elemental effect (<see cref="ResourceSystem.ClearElementalDebuffs"/>). Not a
    /// petal a rival took: that is a transfer, and handing it back would mint it.</para>
    ///
    /// <para><b>Every peer cleanses its own copy.</b> Silence is kept per peer (each refuses the same
    /// hits), and the dust's contacts are simulated on every peer, so each one lifts its own window.
    /// A silence extension still in flight when the dust lands re-silences the pilot on the peer it
    /// reaches late: the lift is not relayed (SILENCE.md §4 relays extensions only).</para>
    /// </summary>
    public static class ElementalCleanse
    {
        /// <summary>Cleanse <paramref name="status"/>. True when anything was lifted.</summary>
        public static bool Cleanse(IVesselStatus status)
        {
            if (status == null) return false;
            bool lifted = status.Player != null && VesselSilence.Lift(status.PlayerName);
            var resources = status.ResourceSystem;
            if (resources) lifted |= resources.ClearElementalDebuffs();
            return lifted;
        }

        /// <summary>True when <paramref name="target"/> is on <paramref name="pilot"/>'s side: the same
        /// domain, both seated. The pilot counts as their own ally (the bloom dusts its own pilot).
        /// Asks for a Player first: <see cref="IVesselStatus.Domain"/> logs when there is none.</summary>
        public static bool IsAlly(IVesselStatus pilot, IVesselStatus target)
        {
            if (pilot == null || target == null) return false;
            if (ReferenceEquals(pilot, target)) return true;
            return pilot.Player != null && target.Player != null && pilot.Domain == target.Domain;
        }
    }
}
