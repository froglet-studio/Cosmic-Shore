using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Jewel Wasp's live state to the fleet's ability row (design: <c>R_VesselActions/JEWEL_WASP.md</c> §7).
    /// Polled per frame from this vessel's OWN executor, a serialized reference on its prefab.
    /// <list type="bullet">
    /// <item><b>Charge</b>: the attack's recharge (the petal-stealing strike from inside a host).</item>
    /// <item><b>Time</b>: the speed burst's recharge in a host. The Time card swaps to the burst (Dash) and the Space card
    /// to the suction while the wasp rides a creature (Garrett, 2026-11-14: "time ability that swaps icon in a fauna"); the
    /// pictures are <see cref="JewelWaspHUDView"/>'s.</item>
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

        IVesselStatus _status;
        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _status = vesselStatus;
            if (View is JewelWaspHUDView waspView) waspView.SetInHost(false);   // a re-init starts free
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
            if (View is JewelWaspHUDView waspView) waspView.SetInHost(inHost);
        }
    }
}
