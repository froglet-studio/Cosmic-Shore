using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Drives the Flea's four gauges (FLEA.md §4). The Flea wears the Stoat's HUD (<see cref="StoatHUDView"/>, its four
    /// gauges in charge, mass, space, time order) until it has its own: Charge = the chain (a full card at six perfect
    /// bounces), Mass = speed against the cap, Space = the hop charge, Time = the perfect window after an RT press (lit
    /// while it is open). Polled per frame from this vessel's own transformer, a serialized reference (vessel rule 14);
    /// the pilot gate runs every <see cref="Initialize"/> (rule 5).
    /// </summary>
    public class FleaHUDController : VesselHUDController
    {
        [Header("Flea")]
        [SerializeField] StoatHUDView view;
        [Tooltip("This vessel's flight (the bounce, the chain, the hop).")]
        [SerializeField] FleaVesselTransformer flea;
        [Tooltip("Perfect bounces in a row that fill the Charge card.")]
        [SerializeField, Min(1)] int fullChain = 6;

        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            if (!view) view = GetComponentInChildren<StoatHUDView>(true);
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !flea) return;
            var s = flea.State;
            var p = flea.Params;
            float now = Time.time;
            view.SetRipple(Mathf.Clamp01(s.chain / (float)fullChain));
            view.SetMode(p.topSpeed > 0f ? Mathf.Clamp01(s.speed / p.topSpeed) : 0f);
            view.SetAccretion(s.charge);
            float half = p.window / 2f, sincePress = now - s.pressT;
            bool open = sincePress >= 0f && sincePress <= half;
            view.SetTime(open ? 1f - sincePress / half : 0f, open);
        }
    }
}
