using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.Gameplay;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Rhino's HUD: pushes the sword's state onto the fleet's four ability cards.
    ///
    /// <para><b>CHARGE card</b> - the sword's ENERGY economy (Charge scales what each cut banks; its
    /// level-5, Live Wire, keeps an energized blade lit). The card is lit while the blade is
    /// energized, the recharge veil sweeps over it through the energize cooldown, and an authored
    /// gauge (if the row has one) fills with stored energy.</para>
    ///
    /// <para><b>MASS card</b> - lit while the sword is laying its afterimages (the window after an
    /// energize, and the whole of an omni-crystal surge).</para>
    ///
    /// <para><b>TIME card</b> - lit while the Time level-5 ward, <b>Momentum</b>, is HOLDING: the
    /// upgrade is active and the Rhino is above 70% of its top speed, so danger prisms cannot drain
    /// it. Lit means "you are warded right now", which is the one thing a pilot flying into danger
    /// mass needs to know.</para>
    ///
    /// <para>Polled rather than event-driven, deliberately: every value is a read of state the sword
    /// driver already owns, so there is no subscription to leak or double across a re-initialise -
    /// the asymmetric subscribe/unsubscribe pair this replaced did both, depending on whether the
    /// re-init flipped the pilot between local and AI. Each push is change-detected, so a steady
    /// card costs a compare per frame.</para>
    /// </summary>
    public sealed class RhinoVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private RhinoVesselHUDView view;

        [Header("Rhino – Scene Refs")]
        [Tooltip("The sword's brain (energy, energize phase). On the Rhino root's ScaleSkimmerObject.")]
        [FormerlySerializedAs("growSkimmerExecutor")]
        [SerializeField] private ShieldSkimmerScaleDriver swordDriver;

        [Tooltip("The Mass ability (afterimages), beside the sword driver. Found under the vessel when empty.")]
        [SerializeField] private RhinoSwordAfterimage afterimage;

        [Tooltip("The Time level-5 ward (VesselElementalImmunity on the root, above 70% of top speed). " +
                 "Found on the vessel when empty.")]
        [SerializeField] private VesselElementalImmunity speedWard;

        IVesselStatus _vesselStatus;

        bool _chargeLit;
        float _chargeVeil = -1f;
        float _chargeGauge = -1f;
        bool _massLit;
        bool _timeLit;

        bool IsHudAllowed => _vesselStatus is { IsInitializedAsAI: false, IsLocalUser: true };

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _vesselStatus = vesselStatus;

            if (!view)
                view = View as RhinoVesselHUDView;

            // Explicit Unity-null tests, not `??` (a missing component can be a fake-null object).
            var root = vesselStatus?.ShipTransform;
            if (!afterimage && root) afterimage = root.GetComponentInChildren<RhinoSwordAfterimage>(true);
            if (!speedWard && root) speedWard = root.GetComponentInChildren<VesselElementalImmunity>(true);

            // A re-initialise (menu vessel swap, pilot swap) starts the cards from a known state so
            // the change detection below pushes everything once.
            _chargeLit = false;
            _chargeVeil = -1f;
            _chargeGauge = -1f;
            _massLit = false;
            _timeLit = false;
            if (!view) return;
            view.SetAbilityPressed(Element.Charge, false);
            view.SetAbilityPressed(Element.Mass, false);
            view.SetAbilityPressed(Element.Time, false);
        }

        void Update()
        {
            if (!view || !IsHudAllowed) return;

            bool massLit = afterimage && afterimage.IsLaying;
            if (massLit != _massLit)
            {
                _massLit = massLit;
                view.SetAbilityPressed(Element.Mass, massLit);
            }

            bool timeLit = speedWard && speedWard.IsHolding;
            if (timeLit != _timeLit)
            {
                _timeLit = timeLit;
                view.SetAbilityPressed(Element.Time, timeLit);
            }

            if (!swordDriver) return;

            bool lit = swordDriver.IsEnergized;
            if (lit != _chargeLit)
            {
                _chargeLit = lit;
                view.SetAbilityPressed(Element.Charge, lit);
            }

            float veil = swordDriver.EnergizeCooldownRemaining01;
            if (!Mathf.Approximately(veil, _chargeVeil))
            {
                _chargeVeil = veil;
                view.SetAbilityCooldown(Element.Charge, veil);
            }

            float energy = swordDriver.Energy01;
            if (!Mathf.Approximately(energy, _chargeGauge) && view.TryGetAbilityGauge(Element.Charge, out var gauge))
            {
                _chargeGauge = energy;
                gauge.fillAmount = energy;
            }
        }
    }
}
