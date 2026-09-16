using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
namespace CosmicShore.UI
{
    public class SerpentVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private SerpentVesselHUDView view;

        [Header("Boost (charges)")]
        [SerializeField] private ConsumeBoostActionExecutor consumeBoostExecutor;

        [Header("Shields")]
        [SerializeField] private int shieldResourceIndex;

        [Header("Sniper")]
        [Tooltip("Drives the CHARGE card's cooldown veil. Resolved from the registry when left " +
                 "empty; a Serpent without the ability simply shows no veil.")]
        [SerializeField] private SniperShotActionExecutor sniperShotExecutor;

        IVesselStatus  _status;
        ResourceSystem _rs;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _status = vesselStatus;

            if (!view)
                view = View as SerpentVesselHUDView;

            if (!view) return;

            Subscribe();
        }


        void Subscribe()
        {
            if (_status.IsInitializedAsAI || !_status.IsLocalUser) return;
            
            if (consumeBoostExecutor == null || sniperShotExecutor == null)
            {
                var registry = _status?.ShipTransform
                    ? _status.ShipTransform.GetComponentInChildren<ActionExecutorRegistry>(true)
                    : null;

                if (registry != null)
                {
                    if (consumeBoostExecutor == null)
                        consumeBoostExecutor = registry.Get<ConsumeBoostActionExecutor>();
                    if (sniperShotExecutor == null)
                        sniperShotExecutor = registry.Get<SniperShotActionExecutor>();
                }
            }

            _rs = _status?.ResourceSystem;
            if (_rs != null)
            {
                _rs.OnResourceChanged += HandleResourceChanged;
                PushInitialShields();
            }

            if (consumeBoostExecutor == null || view == null) return;
            consumeBoostExecutor.OnChargesSnapshot += HandleBoostSnapshot;
            consumeBoostExecutor.OnChargeConsumed  += HandleBoostChargeConsumed;

            HandleBoostSnapshot(
                consumeBoostExecutor.AvailableCharges,
                consumeBoostExecutor.MaxCharges
            );
        }

        void OnDisable()
        {
            if (_rs != null)
                _rs.OnResourceChanged -= HandleResourceChanged;

            if (consumeBoostExecutor != null)
            {
                consumeBoostExecutor.OnChargesSnapshot -= HandleBoostSnapshot;
                consumeBoostExecutor.OnChargeConsumed  -= HandleBoostChargeConsumed;
            }

            if (view != null)
                view.ResetBoostPips();
        }

        // ---------- Shields ----------

        void HandleResourceChanged(int index, float current, float max)
        {
            if (!view) return;
            if (index != shieldResourceIndex || max <= 0f) return;

            var norm   = Mathf.Clamp01(current / max);
            var   shields = Mathf.Clamp(Mathf.FloorToInt(norm * 4f + 0.0001f), 0, 4);
            view.SetShieldCount(shields);
        }

        void PushInitialShields()
        {
            if (_rs == null || view == null) return;
            if ((uint)shieldResourceIndex >= _rs.Resources.Count) return;

            var r    = _rs.Resources[shieldResourceIndex];
            var norm = (r.MaxAmount <= 0f) ? 0f : Mathf.Clamp01(r.CurrentAmount / r.MaxAmount);
            var shields = Mathf.Clamp(Mathf.FloorToInt(norm * 4f), 0, 4);

            view.SetShieldCount(shields);
        }

        // ---------- Boost pips ----------

        void HandleBoostSnapshot(int available, int max)
        {
            if (!view) return;
            view.ApplyBoostSnapshot(available, max);
        }

        void HandleBoostChargeConsumed(int pipIndex, float duration)
        {
            if (!view) return;
            view.AnimateBoostChargeConsumed(pipIndex, duration);
        }

        // ---------- Sniper cooldown ----------

        /// <summary>
        /// Push the sniper's recovery onto the CHARGE card's veil.
        ///
        /// <para>Polled rather than event-driven, deliberately: the cooldown is a CLOCK, not a
        /// resource, so there is no per-change event to subscribe to and a meter that only moved
        /// when something was raised would sit still for twelve seconds and then jump. It is a
        /// VALUE the vessel pushes (<c>VesselHUDView.SetAbilityCooldown</c>) rather than an Image
        /// the HUD binds, so there is no per-vessel artwork to author and nothing to wire.</para>
        ///
        /// <para>Gated the way <see cref="Subscribe"/> is — a HUD belongs to the pilot looking at
        /// it, and an AI's or a remote replica's cooldown is not this screen's business.</para>
        /// </summary>
        void Update()
        {
            if (!view || sniperShotExecutor == null || _status == null) return;
            if (_status.IsInitializedAsAI || !_status.IsLocalUser) return;

            view.SetAbilityCooldown(Element.Charge, sniperShotExecutor.CooldownRemaining01);
        }
    }
}