using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Plasma-claw spray: while held, scans a forward cone each tick (via
    /// PrismSpatialIndex.QuerySphere + angle filter) and ignites enemy prisms
    /// through PrismBurnManager. Free to use — no energy cost by design.
    /// Mass-5 state is sampled at IGNITE time: burns started while the upgrade is
    /// active convert (steal) on burnout even if the level later drops.
    ///
    /// <para><b>Two claws on the triggers</b> (design ask, 2026-10-16): LT's cone opens from the
    /// LEFT front of the hull and RT's from the RIGHT front, both forward, each held on its own.
    /// The trigger's LIVE pressure sizes its cone every tick
    /// (<see cref="GrizzlyFlamethrowerActionSO.RangeForPressure"/>); a device with no analog
    /// trigger sprays the full cone. A spray started with no side (the legacy single-button
    /// path) opens from the hull's centre at full size.</para>
    ///
    /// <para>Every spray ends when the active weapon changes away from the claw.</para>
    /// </summary>
    public sealed class GrizzlyFlamethrowerActionExecutor : ShipActionExecutorBase
    {
        /// <summary>HUD: spray active (any cone).</summary>
        public event Action<bool> OnSprayChanged;

        sealed class Spray
        {
            public CancellationTokenSource Cts;
            public GrizzlyClawConeVisual Cone;
        }

        // 0 = LT (left front), 1 = RT (right front), 2 = centre (no side).
        readonly Spray[] _sprays = { new(), new(), new() };

        IVesselStatus _status;
        GrizzlyWeaponModeExecutor _modes;
        readonly List<Prism> _scratch = new();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            EndAll();

            if (_modes != null) _modes.OnModeChanged -= HandleModeChanged;
            _modes = TryGetComponent<ActionExecutorRegistry>(out var registry)
                ? registry.Get<GrizzlyWeaponModeExecutor>()
                : null;
            if (_modes != null) _modes.OnModeChanged += HandleModeChanged;
        }

        void OnDisable() => EndAll();

        void OnDestroy()
        {
            if (_modes != null) _modes.OnModeChanged -= HandleModeChanged;
        }

        void HandleModeChanged(GrizzlyWeaponModeExecutor.WeaponMode mode)
        {
            if (mode != GrizzlyWeaponModeExecutor.WeaponMode.Flamethrower) EndAll();
        }

        static int SlotOf(GrizzlyBombActionSO.TriggerSide? side) => side.HasValue ? (int)side.Value : 2;

        public void BeginSpray(GrizzlyFlamethrowerActionSO so, IVesselStatus status, GrizzlyBombActionSO.TriggerSide? side)
        {
            if (!so || status == null) return;
            int slot = SlotOf(side);
            EndSpray(side);

            var spray = _sprays[slot];
            spray.Cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            // Draw the reach. The cone is built from the same Range and half-angle IgniteCone
            // filters on, so the visual cannot drift from what actually catches fire.
            var vesselTf = status.Transform;
            if (vesselTf != null)
            {
                spray.Cone = GrizzlyClawConeVisual.EnsureFor(vesselTf, ConeName(slot), LocalApex(so, slot));
                float pressure = PressureOf(status, side);
                spray.Cone?.Show(so.RangeForPressure(pressure), so.HalfAngleForPressure(pressure));
            }

            bool wasSpraying = AnySpraying(slot);
            if (!wasSpraying) OnSprayChanged?.Invoke(true);
            SprayAsync(so, status, side, spray.Cts.Token).Forget();
        }

        public void EndSpray(GrizzlyBombActionSO.TriggerSide? side)
        {
            var spray = _sprays[SlotOf(side)];
            spray.Cone?.Hide();
            if (spray.Cts == null) return;
            spray.Cts.Cancel();
            spray.Cts.Dispose();
            spray.Cts = null;
            if (!AnySpraying(-1)) OnSprayChanged?.Invoke(false);
        }

        void EndAll()
        {
            EndSpray(GrizzlyBombActionSO.TriggerSide.Left);
            EndSpray(GrizzlyBombActionSO.TriggerSide.Right);
            EndSpray(null);
        }

        bool AnySpraying(int except)
        {
            for (int i = 0; i < _sprays.Length; i++)
                if (i != except && _sprays[i].Cts != null) return true;
            return false;
        }

        static string ConeName(int slot) => slot switch
        {
            0 => "PlasmaClawCone_L",
            1 => "PlasmaClawCone_R",
            _ => "PlasmaClawCone",
        };

        /// <summary>The cone's apex in the hull's local space: its own side of the front.</summary>
        static Vector3 LocalApex(GrizzlyFlamethrowerActionSO so, int slot)
        {
            float x = slot switch { 0 => -so.SideOffset, 1 => so.SideOffset, _ => 0f };
            return new Vector3(x, 0f, slot == 2 ? 0f : so.NoseOffset);
        }

        /// <summary>The trigger's live pressure, 0..1. A device with no analog trigger (mouse,
        /// keyboard, touch, an autopilot) and the centred legacy spray are a FULL pull.</summary>
        static float PressureOf(IVesselStatus status, GrizzlyBombActionSO.TriggerSide? side)
        {
            var input = status?.InputStatus;
            if (!side.HasValue || input == null || input.ActiveInputDevice != InputDeviceType.Gamepad) return 1f;
            float analog = side.Value == GrizzlyBombActionSO.TriggerSide.Left
                ? input.LeftTriggerAnalog
                : input.RightTriggerAnalog;
            return Mathf.Clamp01(analog);
        }

        async UniTaskVoid SprayAsync(GrizzlyFlamethrowerActionSO so, IVesselStatus status,
            GrizzlyBombActionSO.TriggerSide? side, CancellationToken token)
        {
            try
            {
                float tickSeconds = so.IgniteTicksPerSecond > 0f ? 1f / so.IgniteTicksPerSecond : 0.25f;
                var burn = PrismBurnManager.EnsureInstance();
                var spray = _sprays[SlotOf(side)];
                float sinceIgnite = tickSeconds;   // ignite on the first frame

                while (!token.IsCancellationRequested)
                {
                    // The cone follows the pull every frame; the burn ticks at its own rate.
                    float pressure = PressureOf(status, side);
                    float range = so.RangeForPressure(pressure);
                    float halfAngle = so.HalfAngleForPressure(pressure);
                    spray.Cone?.Show(range, halfAngle);

                    sinceIgnite += Time.deltaTime;
                    if (sinceIgnite >= tickSeconds)
                    {
                        sinceIgnite = 0f;
                        IgniteCone(so, status, SlotOf(side), range, halfAngle, burn);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        void IgniteCone(GrizzlyFlamethrowerActionSO so, IVesselStatus status, int slot,
            float range, float halfAngle, PrismBurnManager burn)
        {
            var index = PrismSpatialIndex.Instance;
            var vesselTf = status?.Vessel?.Transform;
            if (index == null || burn == null || vesselTf == null) return;

            var origin = vesselTf.TransformPoint(LocalApex(so, slot));
            var forward = vesselTf.forward;

            // Sphere centered mid-cone, then angle-filter — QuerySphere is the only
            // spatial primitive and this keeps the scan cheap.
            _scratch.Clear();
            index.QuerySphere(origin + forward * (range * 0.5f), range * 0.6f, _scratch);

            bool convert = status.ElementalAbilityHandler != null &&
                           status.ElementalAbilityHandler.IsUpgradeActive(Element.Mass);
            float cosLimit = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            int ignited = 0;

            foreach (var prism in _scratch)
            {
                if (ignited >= so.IgnitesPerTick) break;
                if (prism == null || prism.destroyed) continue;
                if (prism.Domain == status.Domain) continue;

                var to = prism.transform.position - origin;
                float dist = to.magnitude;
                if (dist > range || dist < 0.01f) continue;
                if (Vector3.Dot(to / dist, forward) < cosLimit) continue;

                burn.Ignite(prism, status.PlayerName, status.Domain, convert);
                ignited++;
            }
        }
    }
}
