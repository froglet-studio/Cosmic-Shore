using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Shrike's live state to <see cref="ShrikeHUDView"/> (design: <c>R_VesselActions/SHRIKE.md</c> §4).
    ///
    /// <para>Every signal is POLLED per frame from this vessel's OWN executor — a serialized reference on its prefab,
    /// never reached for by type through the hierarchy (vessel rule 14). The pilot gate runs every
    /// <see cref="Initialize"/>, so a re-init that hands this vessel to an AI or a remote owner stops driving the HUD.</para>
    /// </summary>
    public class ShrikeHUDController : VesselHUDController
    {
        [Header("Shrike")]
        [SerializeField] ShrikeHUDView view;
        [Tooltip("This vessel's executor: the mode, the cooldowns, the dock, the missiles.")]
        [SerializeField] ShrikeExecutor shrike;

        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = GetComponentInChildren<ShrikeHUDView>(true);

            // The fleet's pilot gate (BrittleStarHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !shrike) return;
            var mode = shrike.CurrentMode;
            int missiles = shrike.MissilesReady;
            view.SetMissileCount(missiles);
            view.SetCharge(missiles > 0 ? 1f : 0f, mode == ShrikeExecutor.Mode.Charge);
            view.SetMass(shrike.Nest01, shrike.Docked);
            view.SetSpace(shrike.Lance01, mode == ShrikeExecutor.Mode.Space);
            view.SetTime(shrike.Sling01, mode == ShrikeExecutor.Mode.Time);
        }
    }
}
