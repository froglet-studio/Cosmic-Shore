using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
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

        [Inject] GameDataSO _gameData;
        IVesselStatus _status;
        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _status = vesselStatus;

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
            view.SetDomainColor(ResolveDomainSignalColor());
            view.SetKit(shrike.Painting && shrike.Locked && missiles > 0, shrike.Docked, shrike.FloraCount > 0,
                        shrike.Lancing, shrike.Tethering, shrike.Slinging);
        }

        /// <summary>The pilot's domain at full brightness (<c>SO_ColorSet.GetDomainSignalColor</c>), as the Manta's and
        /// the Brittle Star's HUDs read it; alpha 0 until the palette resolves (the view keeps its last colour).</summary>
        Color ResolveDomainSignalColor()
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            if (colorSet == null || _status == null) return new Color(0f, 0f, 0f, 0f);
            return colorSet.GetDomainSignalColor(_status.Domain);
        }
    }
}
