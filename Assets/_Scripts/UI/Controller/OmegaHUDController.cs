using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Drives an Omega crew's four gauges (OMEGA_CREW.md §4). The crews wear the Stoat's HUD (<see cref="StoatHUDView"/>,
    /// four gauges in charge, mass, space, time order) until they have their own: Charge = the weapon (a cannon's charge,
    /// else how far its cooldown has run); Mass = the omega meter; Space = apart, how close the crew is to gather range
    /// (full when every member is in range), combined, the dock; Time = the finisher, lit when the meter can pay for it.
    /// Polled per frame from this vessel's own crew, a serialized reference (vessel rule 14); the pilot gate runs every
    /// <see cref="Initialize"/> (rule 5).
    /// </summary>
    public class OmegaHUDController : VesselHUDController
    {
        [Header("Omega")]
        [SerializeField] StoatHUDView view;
        [Tooltip("This hull's crew (the meter, the form, the seats).")]
        [SerializeField] OmegaCrew crew;

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
            if (!_isLocalPilotHud || !view || !crew || !crew.HumanAboard) return;
            view.SetRipple(crew.Weapon01);
            view.SetMode(crew.Meter01);
            view.SetAccretion(crew.Gather01);
            view.SetTime(crew.Finisher01, crew.FinisherReady);
        }
    }
}
