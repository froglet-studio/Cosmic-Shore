using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
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
    /// <item><b>Drop recharge</b> (the Time card) — a cooldown is a CLOCK and there is no event to
    /// ride. It is the feedback a refused Drop press gets. The same poll drives the card's parts
    /// (<see cref="ButterflyHUDView.SetDrop"/>): dropping, ready, and whether the wormhole tolls.</item>
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

        [Tooltip("This vessel's Drop executor (the Tiger Moth's Time ability, R_VesselActions/TIGER_MOTH.md). " +
                 "Serialized rather than type-searched so an unwired HUD is visible in the inspector.")]
        [SerializeField] TigerMothDropExecutor dropExecutor;

        [Tooltip("This vessel's mode executor (Mass mode / Dust mode on the right trigger).")]
        [SerializeField] SpreadWingsActionExecutor spreadExecutor;

        [Inject] GameDataSO _gameData;
        IVesselStatus _status;
        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _status = vesselStatus;

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
            if (dropExecutor)
            {
                View?.SetAbilityCooldown(Element.Time, dropExecutor.CooldownRemaining01);
            }

            if (view)
            {
                view.SetPilotColors(ResolveDomainSignalColor(), ResolveShieldedColor());
                bool tollGate = TigerMothDropActionSO.TollsRivals(_status);
                float flash = view.HitFlashSeconds;
                bool tolled = dropExecutor && Time.time - dropExecutor.LastTollTime < flash;
                if (dropExecutor) view.SetDrop(dropExecutor.IsDropping, dropExecutor.IsReady, tollGate, tolled);
                var dust = spreadExecutor ? spreadExecutor.DustField : null;
                bool hit = dust && Time.time - dust.LastBiteTime < flash;
                view.SetScaleDust(spreadExecutor && spreadExecutor.IsDustMode, hit, tollGate, tolled);
            }
        }

        /// <summary>The pilot's shielded colour, the dust's (<c>GetShieldedSignalColor</c>, the Angler's read); alpha 0
        /// until the theme resolves or for the unteamed Blue domain.</summary>
        Color ResolveShieldedColor()
        {
            var theme = _gameData?.ThemeManagerData;
            if (theme == null || _status == null || _status.Domain == Domains.Blue) return new Color(0f, 0f, 0f, 0f);
            return theme.GetShieldedSignalColor(_status.Domain);
        }

        /// <summary>The pilot's domain at full brightness (<c>SO_ColorSet.GetDomainSignalColor</c>), as the Angler's and
        /// the Serpent's HUDs read it; alpha 0 until the palette resolves (the view keeps its last colour).</summary>
        Color ResolveDomainSignalColor()
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            if (colorSet == null || _status == null) return new Color(0f, 0f, 0f, 0f);
            return colorSet.GetDomainSignalColor(_status.Domain);
        }
    }
}
