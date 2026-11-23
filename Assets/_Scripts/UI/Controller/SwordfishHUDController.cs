using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Swordfish's HUD (its own since 2026-11-12; it ran the Rhino's controller until then): pushes the sword's
    /// state onto the four ability cards of <see cref="SwordfishVesselHUDView"/>. Polled and change-detected, as the
    /// Rhino's: every value is a read of state the sword already owns.
    ///
    /// <para><b>CHARGE</b> - the bill follows the real sword (<see cref="ShieldSwipeActionExecutor.CurrentPose"/>, in the
    /// Bill mapping: the side swing is the yaw over <c>BillSideYawDegrees</c>, the raise is the pitch over
    /// <c>BillRaiseDegrees</c>); lit while energized; the recharge veil is the energize cooldown; the plate the energize
    /// (<see cref="ShieldSkimmerScaleDriver.EnergizeGauge01"/>).</para>
    /// <para><b>MASS</b> - lit while the afterimages are laid, the copies in the pilot's domain colour.</para>
    /// <para><b>SPACE</b> - the bill's live length, the prism shattered once Shield Breaker is active.</para>
    /// <para><b>TIME</b> - Quickdraw: the plate is the swing's recovery draining away
    /// (<see cref="ShieldSwipeActionExecutor.SwipeRecoveryRemaining01"/>, shortened by Time); the swoosh brightens with
    /// Time's rate; the card flashes once and shows the swoosh back when Flurry (Time 5) unlocks.</para>
    /// </summary>
    public sealed class SwordfishHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private SwordfishVesselHUDView view;

        [Header("Swordfish – Scene Refs")]
        [Tooltip("The sword's brain (energize phase, surge). Found under the vessel when empty.")]
        [SerializeField] private ShieldSkimmerScaleDriver swordDriver;
        [Tooltip("The sword's swipe (pose, recovery, Quickdraw / Flurry). Found under the vessel when empty.")]
        [SerializeField] private ShieldSwipeActionExecutor swipe;
        [Tooltip("The Mass ability (afterimages). Found under the vessel when empty.")]
        [SerializeField] private SwordfishSwordAfterimage afterimage;

        [Inject] GameDataSO _gameData;

        IVesselStatus _vesselStatus;

        bool _chargeLit;
        float _chargeVeil = -1f;
        float _chargeGauge = -1f;
        bool _massLit;
        Color _massColor;
        bool _flurry;
        float _rate01 = -1f;
        float _timeGauge = -1f;

        bool IsHudAllowed => _vesselStatus is { IsInitializedAsAI: false, IsLocalUser: true };

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _vesselStatus = vesselStatus;
            if (!view) view = View as SwordfishVesselHUDView;

            var root = vesselStatus?.ShipTransform;
            if (!swordDriver && root) swordDriver = root.GetComponentInChildren<ShieldSkimmerScaleDriver>(true);
            if (!afterimage && root) afterimage = root.GetComponentInChildren<SwordfishSwordAfterimage>(true);
            if (!swipe && root) swipe = root.GetComponentInChildren<ShieldSwipeActionExecutor>(true);

            _chargeLit = false;
            _chargeVeil = -1f;
            _chargeGauge = -1f;
            _massLit = false;
            _massColor = default;
            _flurry = false;
            _rate01 = -1f;
            _timeGauge = -1f;
            if (!view) return;

            view.SetAbilityPressed(Element.Charge, false);
            view.SetBillLit(false);
            view.SetBillPose(0f, 0f);
            view.SetAbilityPressed(Element.Mass, false);
            view.SetAfterimage(false, ResolveDomainSignalColor());
            view.SetAbilityPressed(Element.Time, false);
            view.SetQuickdraw(false, 0f);
        }

        void Update()
        {
            if (!view || !IsHudAllowed) return;

            bool massLit = afterimage && afterimage.IsLaying;
            var massColor = ResolveDomainSignalColor();
            if (massLit != _massLit || massColor != _massColor)
            {
                if (massLit != _massLit) view.SetAbilityPressed(Element.Mass, massLit);
                _massLit = massLit;
                _massColor = massColor;
                view.SetAfterimage(massLit, massColor);
            }

            PushSwordToBill();
            PushQuickdraw();
            if (!swordDriver) return;

            // Re-asserted every frame while lit: the card is bound to the both-triggers input, so the base press path
            // darkens it the moment the stance is let go, while the energized tail still has seconds to run.
            bool lit = swordDriver.IsEnergized;
            if (lit || lit != _chargeLit)
            {
                if (lit != _chargeLit) view.SetBillLit(lit);
                _chargeLit = lit;
                view.SetAbilityPressed(Element.Charge, lit);
            }

            var skimmer = swordDriver.Skimmer;
            view.SetBladeReach(skimmer ? skimmer.ElementalScale01 : 0f, swordDriver.SuperShieldUpgradeActive, lit,
                ResolveShieldedColor());

            float veil = swordDriver.EnergizeCooldownRemaining01;
            if (!Mathf.Approximately(veil, _chargeVeil))
            {
                _chargeVeil = veil;
                view.SetAbilityCooldown(Element.Charge, veil);
            }

            float energize = swordDriver.EnergizeGauge01;
            if (!Mathf.Approximately(energize, _chargeGauge) && view.TryGetAbilityGauge(Element.Charge, out var gauge))
            {
                _chargeGauge = energize;
                gauge.fillAmount = energize;
            }
        }

        // The real sword's pose on the Charge card: the side swing (-1..1) and the raise (0..1), each a fraction of
        // its full travel in the Bill mapping (ShieldSwipeActionExecutor.BillChannels: pitch = -raise x BillRaiseDegrees).
        void PushSwordToBill()
        {
            var config = swipe ? swipe.Config : null;
            if (!config) return;
            var pose = swipe.CurrentPose;
            float side = config.BillSideYawDegrees != 0f ? pose.yaw / config.BillSideYawDegrees : 0f;
            float raise = config.BillRaiseDegrees != 0f ? -pose.pitch / config.BillRaiseDegrees : 0f;
            view.SetBillPose(side, raise);
        }

        // Quickdraw: the swing's recovery on the plate, the swoosh by Time's rate, the swoosh back under Flurry.
        void PushQuickdraw()
        {
            var config = swipe ? swipe.Config : null;
            if (!config) return;

            float recovery = swipe.SwipeRecoveryRemaining01;
            if (!Mathf.Approximately(recovery, _timeGauge) && view.TryGetAbilityGauge(Element.Time, out var gauge))
            {
                _timeGauge = recovery;
                gauge.fillAmount = recovery;
            }
            view.SetAbilityPressed(Element.Time, recovery > 0f);

            float scaled = config.SwipeCooldownSecondsFor(_vesselStatus);
            float rate = scaled > 0f ? config.SwipeCooldownSeconds / scaled : 1f;
            float rate01 = Mathf.Clamp01((rate - 1f) / 1.5f);
            bool flurry = config.FlurryActive(_vesselStatus);
            if (flurry && !_flurry) view.PlayAbilityFlash(Element.Time);
            if (flurry != _flurry || Mathf.Abs(rate01 - _rate01) > 0.01f)
            {
                _flurry = flurry;
                _rate01 = rate01;
                view.SetQuickdraw(flurry, rate01);
            }
        }

        Color ResolveDomainSignalColor()
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            if (colorSet == null || _vesselStatus == null) return Color.white;
            return colorSet.GetDomainSignalColor(_vesselStatus.Domain);
        }

        Color ResolveShieldedColor()
        {
            var theme = _gameData?.ThemeManagerData;
            if (theme == null || _vesselStatus == null || _vesselStatus.Domain == Domains.Blue) return new Color(0f, 0f, 0f, 0f);
            return theme.GetShieldedSignalColor(_vesselStatus.Domain);
        }
    }
}
