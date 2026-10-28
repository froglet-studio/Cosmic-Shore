using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino's OMNI-CRYSTAL ability, the <b>Surge</b> (reworked 2026-10-26, Garrett: "zoom out, re
    /// center the camera on the rhino and surround it with swords. they should still be controlled by
    /// the triggers. this can be an 8 second state"): collecting an omni crystal, for
    /// <see cref="surgeSeconds"/>, holds the blade ENERGIZED with no stance,
    /// raises a ring of copies of the blade round the hull that the triggers swing with it, and pulls
    /// the pilot's camera straight back and onto the hull so the whole ring is on screen
    /// (<see cref="RhinoSurgeSwordRing"/>), and lays the Mass afterimages THROUGHOUT the surge. No element
    /// buff and no energy fill (Garrett, 2026-10-26: "Omnicrystal doesn't need the element buff, lets
    /// discard the whole notion of energy as a resource on the rhino"), and no burst: the blade's length
    /// is Space's alone. When the surge's energize goes out, the ordinary
    /// afterimage window opens after it. See <c>R_VesselActions/RHINO_ENERGY_SWORD.md</c> § "The
    /// omni-crystal Surge".
    ///
    /// <para><b>Where it runs.</b> Omni-crystal collection resolves on the server, which runs this, and
    /// replays it on a remote pilot's own machine (<c>OmniCrystalImpactor.ReplayOnRemoteOwner</c>). The
    /// surge is owner-authoritative (<see cref="IRhinoSwordState.Surge"/> is a no-op off the owner) and
    /// replicates from there.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "RhinoVesselSurgeByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/RhinoVesselSurgeByCrystalEffectSO")]
    public sealed class RhinoVesselSurgeByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Header("Surge")]
        [Tooltip("Seconds the surge lasts: the blade held energized, the sword ring raised round the hull, " +
                 "the camera pulled back onto it, and afterimages laid.")]
        [SerializeField, Min(0.1f)] float surgeSeconds = 8f;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor && vesselImpactor.Vessel != null ? vesselImpactor.Vessel.VesselStatus : null;
            if (status == null) return;

            var blade = status.NearFieldSkimmer;
            var sword = blade ? blade.SwordState : null;
            sword?.Surge(surgeSeconds);
        }
    }
}
