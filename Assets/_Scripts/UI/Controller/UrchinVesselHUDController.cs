using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Drives <see cref="UrchinVesselHUDView"/>. Reads only the Urchin's OWN state — the
    /// vessel status it was initialized with, the ResourceSystem hanging off it, and the track
    /// executor serialized on this prefab — so there is no way for it to bind a gauge to a
    /// component another vessel carries.
    ///
    /// The four-icon ability row and its level-5 upgrade signalling are handled entirely by
    /// <see cref="VesselHUDController"/> and <see cref="VesselHUDView"/>; the row itself is
    /// authored into <c>UrchinHUDVariant.prefab</c> by <c>Tools/Build/author_urchin_hud.py</c>
    /// in <c>VesselHUDView.AbilityDisplayOrder</c> (charge, mass, space, time). Three live
    /// signals sit on top of it:
    /// <list type="bullet">
    /// <item><b>Ammo</b> (the Charge card's gauge) — pushed from <c>OnResourceChanged</c>.</item>
    /// <item><b>Riding</b> (the Mass card's gauge) — polled; see <see cref="Update"/>.</item>
    /// <item><b>Track recharge</b> (the Space card's veil) — a cooldown is a CLOCK with no event
    /// behind it, so it is polled too. It is the only feedback a refused Track press gets.</item>
    /// <item><b>Spike charge</b> (a ring around the Charge icon) — the hold-to-charge burst's
    /// progress, read off the vessel's OWN spike executor. Also a clock, also polled.</item>
    /// </list>
    /// </summary>
    public class UrchinVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] UrchinVesselHUDView view;

        [Tooltip("Index into ResourceSystem.Resources for the spike ammo the volley spends " +
                 "and the trail ride refills. Must match UrchinSpikeActionSO.ammoIndex and " +
                 "GunVesselTransformer.ammoIndex - they are the same meter.")]
        [SerializeField] int ammoIndex = 0;

        [Tooltip("This vessel's Track Projector executor. Serialized rather than type-searched so " +
                 "an unwired HUD is visible in the inspector; drives the Space card's recharge veil.")]
        [SerializeField] UrchinTrackActionExecutor trackExecutor;

        [Tooltip("This vessel's Chain Spikes executor. Serialized rather than type-searched so " +
                 "an unwired HUD is visible in the inspector; drives the Charge card's charge ring.")]
        [SerializeField] UrchinSpikeActionExecutor spikeExecutor;

        IVesselStatus _status;
        ResourceSystem _resources;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = View as UrchinVesselHUDView;

            // Detach first, unconditionally and ABOVE the pilot gate, so re-initializing a LIVE
            // component (a vessel swap, a Cellular Duel ownership change, a hand-over to an AI)
            // cannot strand the previous pilot's handler on this resource system.
            Unbind();
            _status = null;

            // A hold in progress belongs to the previous pilot; never carry its ring across.
            if (view) view.SetSpikeCharge(false, 0f);

            // A HUD is for a human at this machine. An AI or a remote replica carries the same
            // components and must not drive local UI. Player-guarded because both flags are
            // default interface members routing through Player, which is null between a despawn
            // and the next pair-init (the fleet gate, as in ButterflyHUDController).
            if (vesselStatus == null || vesselStatus.Player == null ||
                vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser)
                return;

            _status = vesselStatus;

            _resources = _status.ResourceSystem;
            if (_resources)
            {
                _resources.OnResourceChanged += HandleResourceChanged;
                PushAmmo();   // seed, so the gauge is right before the first change
            }
        }

        // OnDisable only - it always precedes OnDestroy on an enabled component. Declaring an
        // OnDestroy here would HIDE VesselHUDController's private one (Unity invokes the most
        // derived message method), skipping its action-handler and upgrade unsubscriptions.
        void OnDisable() => Unbind();

        void Unbind()
        {
            // A REFERENCE test on purpose: a ResourceSystem destroyed before this HUD still holds
            // our delegate, and detaching from a destroyed object is safe where skipping it is not.
            if (ReferenceEquals(_resources, null)) return;
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
            if (!view || !_resources || _resources.Resources == null) return;
            if (ammoIndex < 0 || ammoIndex >= _resources.Resources.Count) return;
            var ammo = _resources.Resources[ammoIndex];
            view.SetAmmo(ammo.CurrentAmount / Mathf.Max(0.0001f, ammo.MaxAmount));
        }

        /// <summary>
        /// Riding is polled rather than pushed because <c>IVesselStatus.IsAttached</c> is a
        /// plain flag with no change event — it is written by an impact effect and cleared by
        /// the Slip ability, neither of which raises anything. Cheap (one bool read) and honest;
        /// if a change event is ever added, move this onto it. The track's recharge and the spike
        /// charge are polled for the same reason: each is a clock, not an event.
        /// </summary>
        void Update()
        {
            if (_status == null) return;

            if (view) view.SetRiding(_status.IsAttached);

            // The fleet's clockwise depleting veil on the Space plate.
            if (trackExecutor)
                View?.SetAbilityCooldown(Element.Space, trackExecutor.CooldownRemaining01);

            // The hold-to-charge burst. Armed, not merely charging: below the minimum hold a
            // release is a tap the press already paid for, and the ring would promise a burst.
            if (spikeExecutor && view)
                view.SetSpikeCharge(spikeExecutor.IsChargeArmed, spikeExecutor.ChargeProgress01);
        }
    }
}
