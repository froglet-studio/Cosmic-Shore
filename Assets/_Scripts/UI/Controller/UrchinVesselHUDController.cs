using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Drives <see cref="UrchinVesselHUDView"/>. Reads only the Urchin's OWN state — the
    /// vessel status it was initialized with and the ResourceSystem hanging off it — so there
    /// is no way for it to bind a gauge to a component another vessel carries.
    ///
    /// The four-icon ability row and its level-5 upgrade signalling are handled entirely by
    /// <see cref="VesselHUDController"/> and <see cref="VesselHUDView"/>; nothing is needed
    /// here for them beyond authoring the four icons on the prefab in
    /// <c>VesselHUDView.AbilityDisplayOrder</c> (charge, mass, space, time).
    /// </summary>
    public class UrchinVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] UrchinVesselHUDView view;

        [Tooltip("Index into ResourceSystem.Resources for the spike ammo the volley spends " +
                 "and the trail ride refills. Must match UrchinSpikeActionSO.ammoIndex and " +
                 "GunVesselTransformer.ammoIndex - they are the same meter.")]
        [SerializeField] int ammoIndex = 0;

        IVesselStatus _status;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = View as UrchinVesselHUDView;

            // A HUD is for a human at this machine. An AI or a remote replica carries the same
            // components and must not drive local UI.
            if (vesselStatus == null || vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser)
            {
                _status = null;
                return;
            }

            _status = vesselStatus;

            // Detach first, unconditionally and above the pilot gate, so re-initializing a LIVE
            // component (a vessel swap, a Cellular Duel ownership change) cannot strand the
            // previous pilot's handler on this resource system.
            Unbind();
            _resources = _status.ResourceSystem;
            if (_resources != null)
            {
                _resources.OnResourceChanged += HandleResourceChanged;
                PushAmmo();   // seed, so the gauge is right before the first change
            }
        }

        ResourceSystem _resources;

        void OnDisable() => Unbind();
        void OnDestroy() => Unbind();

        void Unbind()
        {
            if (_resources == null) return;
            _resources.OnResourceChanged -= HandleResourceChanged;
            _resources = null;
        }

        void HandleResourceChanged(int index, float current, float max)
        {
            if (index != ammoIndex || !view) return;
            view.SetAmmo(current / Mathf.Max(0.0001f, max));
        }

        void PushAmmo()
        {
            if (!view || _resources?.Resources == null) return;
            if (ammoIndex < 0 || ammoIndex >= _resources.Resources.Count) return;
            var ammo = _resources.Resources[ammoIndex];
            view.SetAmmo(ammo.CurrentAmount / Mathf.Max(0.0001f, ammo.MaxAmount));
        }

        /// <summary>
        /// Riding is polled rather than pushed because <c>IVesselStatus.IsAttached</c> is a
        /// plain flag with no change event — it is written by an impact effect and cleared by
        /// the Slip ability, neither of which raises anything. Cheap (one bool read) and honest;
        /// if a change event is ever added, move this onto it.
        /// </summary>
        void Update()
        {
            if (_status == null || !view) return;
            view.SetRiding(_status.IsAttached);
        }
    }
}
