using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Butterfly's live state to <see cref="ButterflyHUDView"/> (design:
    /// <c>R_VesselActions/BUTTERFLY.md</c> §5).
    ///
    /// <para>Two signals, and they are driven differently on purpose:</para>
    /// <list type="bullet">
    /// <item><b>Wing energy</b> — event-driven off this vessel's OWN
    /// <c>ResourceSystem.OnResourceChanged</c>. Never reached for by type through the hierarchy: a
    /// HUD controller that hunts another vessel's component compiles, returns null on every vessel
    /// that is not carrying it, and leaves a dead gauge with no error (the Squirrel polled a
    /// Sparrow-only executor for its heat gauge for the component's entire life that way).</item>
    /// <item><b>Fold recharge</b> — polled per frame, because a cooldown is a CLOCK and there is no
    /// event to ride. It is one float per frame and it is the only feedback a refused press gets.
    /// The executor is a serialized reference on this vessel's prefab, so a missing wire is
    /// visible in the inspector rather than silently leaving the veil at zero — which would read
    /// as "always ready" on an ability that is usually recharging.</item>
    /// </list>
    ///
    /// <para>Bindings are ONE symmetric attach/detach pair. The detach in <see cref="Initialize"/>
    /// runs ABOVE the pilot gate and <see cref="OnDisable"/> is unconditional and idempotent, so a
    /// re-init that hands this vessel to an AI or a remote owner cannot strand the previous
    /// pilot's handlers.</para>
    /// </summary>
    public class ButterflyHUDController : VesselHUDController
    {
        [Header("Butterfly")]
        [SerializeField] ButterflyHUDView view;

        [Tooltip("This vessel's Fold executor. Serialized rather than type-searched so an " +
                 "unwired HUD is visible in the inspector.")]
        [SerializeField] FoldActionExecutor foldExecutor;

        [Tooltip("This vessel's Spread Wings executor — read for the Mass 5 'free spread' case, " +
                 "where the meter genuinely does not apply.")]
        [SerializeField] SpreadWingsActionExecutor spreadExecutor;

        [Header("Resource indices")]
        [Tooltip("Meter that holds wing energy (the Butterfly authors index 0, 'Wing Energy'). " +
                 "Must match SpreadWingsActionSO.resourceIndex — meters are addressed by " +
                 "serialized index fleet-wide and nothing catches a stale one.")]
        [SerializeField, Min(0)] int wingEnergyResourceIndex;

        ResourceSystem _resources;
        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = GetComponentInChildren<ButterflyHUDView>(true);

            // DETACH FIRST, and above the pilot gate: a swap re-runs Initialize on live
            // components, and gating the detach would strand the previous pilot's handlers the
            // moment a re-init hands this vessel to an AI or a remote owner.
            Unbind();

            // The fleet's pilot gate, verbatim (ScarabHUDController:85). Player-guarded because
            // both flags are default interface members routing through Player, which is null
            // between a despawn and the next pair-init.
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
            if (!_isLocalPilotHud) return;

            _resources = vesselStatus.ResourceSystem;
            Bind();
            PushWingEnergy();
        }

        void OnEnable()
        {
            if (_isLocalPilotHud) Bind();
        }

        // Unconditional and idempotent — the teardown half of the pair.
        void OnDisable() => Unbind();

        void Bind()
        {
            if (!_resources) return;
            _resources.OnResourceChanged -= HandleResourceChanged;
            _resources.OnResourceChanged += HandleResourceChanged;
        }

        void Unbind()
        {
            if (!_resources) return;
            _resources.OnResourceChanged -= HandleResourceChanged;
        }

        void HandleResourceChanged(int index, float current, float max)
        {
            if (index != wingEnergyResourceIndex) return;
            PushWingEnergy(current, max);
        }

        void PushWingEnergy()
        {
            if (!_resources) return;
            var list = _resources.Resources;
            if (list == null || wingEnergyResourceIndex >= list.Count) return;
            var r = list[wingEnergyResourceIndex];
            PushWingEnergy(r.CurrentAmount, r.MaxAmount);
        }

        void PushWingEnergy(float current, float max)
        {
            if (!view) return;
            // Mass 5 ("Mural") makes the spread free, so the meter stops meaning anything — the
            // gauge is pinned full rather than left draining against a cost nobody is paying.
            if (spreadExecutor && spreadExecutor.Energy01 >= 1f && max > 0f && current < max)
            {
                view.SetWingEnergy(1f);
                return;
            }
            view.SetWingEnergy(max > 0f ? current / max : 0f);
        }

        void Update()
        {
            if (!_isLocalPilotHud || !foldExecutor) return;
            // The fleet's clockwise depleting veil on the Time plate. A LOCKED card still draws
            // it — the veil sizes itself on the ability PLATE, not the icon — which is what lets
            // a vessel with no authored icon art still report its recharge.
            View?.SetAbilityCooldown(Element.Time, foldExecutor.CooldownRemaining01);
        }
    }
}
