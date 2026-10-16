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
    /// <para><b>Each cone has its own energy cell</b> (design ask, 2026-10-16). Spraying drains it -
    /// a full cell lasts <c>cellSeconds</c> at the smallest cone and empties
    /// <c>largeFlameDrainMultiplier</c> (1.5x) faster at full pressure - and it refills while that
    /// cone is not spraying, after <c>cellRechargeDelay</c>. An empty cell cuts its cone out until
    /// the trigger is pulled again with charge in it. The whips' glow dims as its cell empties
    /// (<see cref="GrizzlyClawWhips"/>); <see cref="CellFill"/> is the HUD read.
    /// Simulated on every peer from the replicated presses and analog, like the spray itself.</para>
    ///
    /// <para>The two cones sit <c>sideOffset</c> to each side and are turned <c>splayDegrees</c>
    /// outward, so they spread apart rather than overlapping ahead of the nose.</para>
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
            public GrizzlyClawWhips Whips;
            public float Fill = 1f;                 // energy cell, 0..1
            public float IdleSince = float.NegativeInfinity;
            public GrizzlyFlamethrowerActionSO Config;   // last config sprayed, for the recharge
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
            foreach (var spray in _sprays) spray.Fill = 1f;

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

        /// <summary>HUD read: a trigger's claw cell, 0 (empty) .. 1 (full).</summary>
        public float CellFill(GrizzlyBombActionSO.TriggerSide side) => _sprays[(int)side].Fill;

        /// <summary>Idle cells refill here; spraying cells drain in <see cref="SprayAsync"/>.</summary>
        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var spray in _sprays)
            {
                if (spray.Cts != null || spray.Fill >= 1f || !spray.Config) continue;
                if (Time.time - spray.IdleSince < spray.Config.CellRechargeDelay) continue;
                spray.Fill = Mathf.Min(1f, spray.Fill + spray.Config.CellRechargePerSecond * dt);
            }
        }

        public void BeginSpray(GrizzlyFlamethrowerActionSO so, IVesselStatus status, GrizzlyBombActionSO.TriggerSide? side)
        {
            if (!so || status == null) return;
            int slot = SlotOf(side);
            EndSpray(side);

            var spray = _sprays[slot];
            spray.Config = so;
            if (spray.Fill <= 0f) return;   // empty cell: nothing until it recharges
            spray.Cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            // The look: four stiff red whips fanned across the same cone IgniteCone filters on
            // (GrizzlyClawWhips), so the visual cannot drift from what actually catches fire.
            var vesselTf = status.Transform;
            if (vesselTf != null)
                spray.Whips = GrizzlyClawWhips.EnsureFor(vesselTf, WhipSetName(slot));

            bool wasSpraying = AnySpraying(slot);
            if (!wasSpraying) OnSprayChanged?.Invoke(true);
            SprayAsync(so, status, side, spray.Cts.Token).Forget();
        }

        public void EndSpray(GrizzlyBombActionSO.TriggerSide? side)
        {
            var spray = _sprays[SlotOf(side)];
            spray.Whips?.Hide();
            if (spray.Cts == null) return;
            spray.Cts.Cancel();
            spray.Cts.Dispose();
            spray.Cts = null;
            spray.IdleSince = Time.time;
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

        static string WhipSetName(int slot) => slot switch
        {
            0 => "PlasmaClawWhips_L",
            1 => "PlasmaClawWhips_R",
            _ => "PlasmaClawWhips",
        };

        /// <summary>The cone's apex in the hull's local space: its own side of the front.</summary>
        static Vector3 LocalApex(GrizzlyFlamethrowerActionSO so, int slot)
        {
            float x = slot switch { 0 => -so.SideOffset, 1 => so.SideOffset, _ => 0f };
            return new Vector3(x, 0f, slot == 2 ? 0f : so.NoseOffset);
        }

        /// <summary>The cone's outward turn about the hull's up, degrees: LT left, RT right.</summary>
        static float Yaw(GrizzlyFlamethrowerActionSO so, int slot) => slot switch
        {
            0 => -so.SplayDegrees,
            1 => so.SplayDegrees,
            _ => 0f,
        };

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

                    // The cell: bigger flames burn it faster. Empty cuts the cone out.
                    spray.Fill = Mathf.Max(0f, spray.Fill - so.CellDrainPerSecond(pressure) * Time.deltaTime);

                    var vesselTf = status?.Transform;
                    if (spray.Whips && vesselTf)
                        spray.Whips.Show(so, vesselTf.TransformPoint(LocalApex(so, SlotOf(side))),
                                         vesselTf.rotation * Quaternion.Euler(0f, Yaw(so, SlotOf(side)), 0f),
                                         range, halfAngle, spray.Fill, Time.deltaTime);
                    if (spray.Fill <= 0f)
                    {
                        EndSpray(side);
                        return;
                    }

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
            var forward = vesselTf.rotation * (Quaternion.Euler(0f, Yaw(so, slot), 0f) * Vector3.forward);

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
