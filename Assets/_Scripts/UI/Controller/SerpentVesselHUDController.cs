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

        [Header("Fuel pellets (Time)")]
        [Tooltip("The pellet executor whose fuel resource the TIME card's four pips read. " +
                 "Resolved from the registry when left empty.")]
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
            // Detach FIRST, above the pilot gate: a vessel swap re-runs Initialize on this live
            // component, and a re-init that hands the hull to an AI must not strand the handler.
            if (_rs != null) _rs.OnResourceChanged -= HandleResourceChanged;
            _rs = null;

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

            PushPelletFuel();
        }

        void OnDisable()
        {
            if (_rs != null)
                _rs.OnResourceChanged -= HandleResourceChanged;
        }

        // ---------- Shields ----------

        void HandleResourceChanged(int index, float current, float max)
        {
            if (!view) return;

            if (consumeBoostExecutor && index == consumeBoostExecutor.FuelResourceIndex)
            {
                PushPelletFuel();
                return;
            }

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

        // ---------- Fuel pellets ----------

        /// <summary>
        /// The TIME card's four pips ARE the fuel tank: pip n is lit once the tank holds n pellets,
        /// and the pellet currently refilling shows its partial fill. Read off the resource itself
        /// (<see cref="ConsumeBoostActionExecutor.PelletsHeld"/>), never off a counter of presses,
        /// so the readout and the gate that decides whether a press burns cannot disagree.
        /// </summary>
        void PushPelletFuel()
        {
            if (!view || !consumeBoostExecutor || !DrivesThisHud) return;
            view.SetPelletFuel(consumeBoostExecutor.PelletsHeld);
        }

        /// <summary>
        /// Whether this HUD should be driven at all — <see cref="Subscribe"/>'s gate, re-asked
        /// at the POINT OF USE.
        ///
        /// <para>The subscribe-time check is necessary and not sufficient, and the reason is a
        /// documented property of the spawn chain: on the host a server-owned AI Player carries
        /// the HOST's <c>OwnerClientId</c>, so <c>IsLocalUser</c> answers TRUE for it, and the
        /// only thing separating the two — <c>IsInitializedAsAI</c> — is written LATER than the
        /// HUD is built. An AI Serpent therefore subscribes, and then drives an inactive HUD for
        /// the rest of the match.</para>
        ///
        /// <para>Re-asking costs two field reads per boost charge and is correct whenever the
        /// answer arrives, however late. The view settles rather than animates for anything that
        /// still gets through, so the two guards cover different halves: this one stops the work,
        /// that one stops the error.</para>
        /// </summary>
        bool DrivesThisHud =>
            _status != null && !_status.IsInitializedAsAI && _status.IsLocalUser;

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