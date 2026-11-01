using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
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
        [Tooltip("This vessel's executor: the spare rings, the guns' heat and the modes.")]
        [SerializeField] BrittleStarExecutor star;

        [Inject] GameDataSO _gameData;
        IVesselStatus _status;
        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _status = vesselStatus;

            if (!view) view = GetComponentInChildren<BrittleStarHUDView>(true);

            // The fleet's pilot gate (ThresherHUDController / StoatHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !star) return;
            view.SetCharge(star.SpareRings / (float)Mathf.Max(1, star.MaxSpareRings), star.Charge01 >= 1f);
            view.SetCreation(star.Creation01);
            view.SetCreationTrails(star.Creation01, star.BlockGirth01, ResolveDomainSignalColor());
            view.SetSpace(star.Heat01, star.Destruction01 >= 1f, star.Overheated);
            view.SetSpeed(star.Speed01);
            view.SetCords(star.CordsOut01, star.CordPull01, ResolveDomainSignalColor());
        }

        /// <summary>The pilot's domain at full brightness (<c>SO_ColorSet.GetDomainSignalColor</c>), as the Tiger Moth's and
        /// the Angler's HUDs read it; alpha 0 until the palette resolves (the view keeps its last colour).</summary>
        Color ResolveDomainSignalColor()
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            if (colorSet == null || _status == null) return new Color(0f, 0f, 0f, 0f);
            return colorSet.GetDomainSignalColor(_status.Domain);
        }
    }
}
