using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Brittle Star's live state to <see cref="BrittleStarHUDView"/> (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §4).
    ///
    /// <para>Every signal is POLLED per frame from this vessel's OWN executor — a serialized reference on its prefab,
    /// never reached for by type through the hierarchy (vessel rule 14), so an unwired HUD shows in the inspector. The
    /// quantities are eased switches with no event behind them, so polling is the honest drive.</para>
    ///
    /// <para>The pilot gate runs every <see cref="Initialize"/>, above anything that depends on it, so a re-init that hands
    /// this vessel to an AI or a remote owner stops driving the HUD (rule 5).</para>
    /// </summary>
    public class BrittleStarHUDController : VesselHUDController
    {
        [Header("Brittle Star")]
        [SerializeField] BrittleStarHUDView view;
        [Tooltip("This vessel's executor: the ring, the charge, the modes and Overdrive.")]
        [SerializeField] BrittleStarExecutor star;

        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = GetComponentInChildren<BrittleStarHUDView>(true);

            // The fleet's pilot gate (ThresherHUDController / StoatHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !star) return;
            bool overdrive = star.Overdrive;
            view.SetRing(star.RingPresence01, star.Charges > 0);
            view.SetCreation(star.Creation01, overdrive);
            view.SetDestruction(star.Destruction01, overdrive);
            view.SetSpeed(star.Speed01, overdrive);
        }
    }
}
