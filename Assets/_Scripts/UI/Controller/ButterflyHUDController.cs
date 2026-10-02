using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Butterfly's live state to <see cref="ButterflyHUDView"/> (design:
    /// <c>R_VesselActions/BUTTERFLY.md</c> §5).
    ///
    /// <para>Two signals, both POLLED per frame from this vessel's OWN executors, which are
    /// serialized references on its prefab — never reached for by type through the hierarchy. A
    /// HUD controller that hunts another vessel's component compiles, returns null on every vessel
    /// that is not carrying it, and leaves a dead gauge with no error (the Squirrel polled a
    /// Sparrow-only executor for its heat gauge for the component's entire life that way).</para>
    /// <list type="bullet">
    /// <item><b>Mode</b> (the Mass card) — <see cref="SpreadWingsActionExecutor.MassMode01"/>. It is
    /// an EASED value with no event behind it, so polling is the honest drive.</item>
    /// <item><b>Fold recharge</b> (the Time card) — a cooldown is a CLOCK and there is no event to
    /// ride. It is the only feedback a refused Fold press gets.</item>
    /// </list>
    ///
    /// <para>The wing-energy meter this used to bind is gone — the right trigger is a mode switch
    /// now, not a metered hold — so there is no resource subscription left to pair. The pilot gate
    /// still runs every <see cref="Initialize"/>, above anything that depends on it, so a re-init
    /// that hands this vessel to an AI or a remote owner stops driving the HUD at once.</para>
    /// </summary>
    public class ButterflyHUDController : VesselHUDController
    {
        [Header("Butterfly")]
        [SerializeField] ButterflyHUDView view;

        [Tooltip("This vessel's Fold executor. Serialized rather than type-searched so an " +
                 "unwired HUD is visible in the inspector.")]
        [SerializeField] FoldActionExecutor foldExecutor;

        [Tooltip("This vessel's mode executor (Mass mode / Dust mode on the right trigger).")]
        [SerializeField] SpreadWingsActionExecutor spreadExecutor;

        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = GetComponentInChildren<ButterflyHUDView>(true);

            // The fleet's pilot gate, verbatim (ScarabHUDController:85). Player-guarded because
            // both flags are default interface members routing through Player, which is null
            // between a despawn and the next pair-init.
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud) return;

            if (view && spreadExecutor) view.SetMassMode(spreadExecutor.MassMode01);

            // The fleet's clockwise depleting veil on the Time plate. A LOCKED card still draws
            // it — the veil sizes itself on the ability PLATE, not the icon — which is what lets
            // a vessel with no authored icon art still report its recharge.
            if (foldExecutor)
                View?.SetAbilityCooldown(Element.Time, foldExecutor.CooldownRemaining01);
        }
    }
}
