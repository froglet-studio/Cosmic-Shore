using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Jewel Wasp's live state to the fleet's ability row (design: <c>R_VesselActions/JEWEL_WASP.md</c> §7).
    /// Polled per frame from this vessel's OWN executor, a serialized reference on its prefab.
    /// <list type="bullet">
    /// <item><b>Charge</b>: the attack's recharge (the petal-stealing strike from inside a host).</item>
    /// <item><b>Time</b>: the speed burst's recharge in a host; the card's icon swaps to the burst while the wasp rides a
    /// creature (Garrett, 2026-11-14: "time ability that swaps icon in a fauna").</item>
    /// <item><b>Space / Mass</b>: the sting's own recharge while free, on the trigger that aims it (RT at fauna, LT at flora).</item>
    /// </list>
    /// The pilot gate runs every <see cref="Initialize"/>, above anything that depends on it.
    /// </summary>
    public sealed class JewelWaspHUDController : VesselHUDController
    {
        [Header("Jewel Wasp")]
        [Tooltip("This vessel's possession executor. Serialized rather than type-searched so an unwired HUD is visible in " +
                 "the inspector.")]
        [SerializeField] JewelWaspExecutor wasp;

        [Tooltip("The Time card's icon while the wasp rides a creature (the burst). Empty keeps the free icon until the " +
                 "/ability-icon round draws it.")]
        [SerializeField] Sprite timeInHostIcon;

        IVesselStatus _status;
        bool _isLocalPilotHud;
        Image _timeIcon;
        Sprite _timeFreeIcon;
        bool _showingHostIcon;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _status = vesselStatus;
            if (_timeIcon && _showingHostIcon) _timeIcon.sprite = _timeFreeIcon;   // a re-init starts free
            _showingHostIcon = false;
            _timeIcon = null;
            if (View && View.TryGetAbilityIcon(Element.Time, out var icon))
            {
                _timeIcon = icon;
                _timeFreeIcon = icon.sprite;
            }
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !wasp || !View) return;
            var mode = wasp.Mode;
            bool inHost = mode == JewelWaspExecutor.WaspMode.InFauna;
            bool free = mode == JewelWaspExecutor.WaspMode.Free;

            View.SetAbilityCooldown(Element.Charge, inHost ? wasp.AttackCooldown01 : 0f);
            View.SetAbilityCooldown(Element.Time, inHost ? wasp.BurstCooldown01 : 0f);
            float sting = free ? wasp.StingCooldown01 : 0f;
            View.SetAbilityCooldown(Element.Space, sting);
            View.SetAbilityCooldown(Element.Mass, sting);

            if (_timeIcon && timeInHostIcon && inHost != _showingHostIcon)
            {
                _timeIcon.sprite = inHost ? timeInHostIcon : _timeFreeIcon;
                _showingHostIcon = inHost;
            }
        }
    }
}
