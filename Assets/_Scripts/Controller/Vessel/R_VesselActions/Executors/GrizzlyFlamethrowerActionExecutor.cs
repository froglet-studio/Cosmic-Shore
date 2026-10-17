using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Plasma claw: while a trigger is held, its claw - four stiff red fingers (<see cref="GrizzlyClawWhips"/>) - sets
    /// fire to every enemy prism a finger touches (<see cref="IgniteAlongClaws"/>, through PrismBurnManager). There is
    /// NO cone (design ask, 2026-10-17: "get rid of the cone completely, allows the claws to do the work"). Free to
    /// use - no energy cost by design. Mass-5 state is sampled at IGNITE time: burns started while the upgrade is
    /// active convert (steal) on burnout even if the level later drops.
    ///
    /// <para><b>Two claws on the triggers</b> (2026-10-16): LT's claw roots at the LEFT front of the hull and RT's
    /// at the RIGHT front, both forward and turned <c>splayDegrees</c> outward, each held on its own. Each
    /// claw is tilted <c>tiltUpDegrees</c> up, so the two do not overlap from the chase camera. The trigger's
    /// LIVE pressure sets the claw's finger length and spread every frame
    /// (<see cref="GrizzlyFlamethrowerActionSO.RangeForPressure"/>, <c>SpreadForPressure</c>); a device with no
    /// analog trigger gets the full claw. A spray started with no side (the legacy single-button path) roots at the
    /// hull's centre at full size.</para>
    ///
    /// <para><b>Each claw has its own energy cell</b> (2026-10-16). Spraying drains it - a full cell lasts
    /// <c>cellSeconds</c> at the lightest pull and empties <c>largeFlameDrainMultiplier</c> (1.5x) faster at full
    /// pressure - and it refills while that claw is idle, after <c>cellRechargeDelay</c>. An empty cell cuts its
    /// claw out until the trigger is pulled again with charge in it. The fingers' glow dims as the cell empties;
    /// <see cref="CellFill"/> is the HUD read. Simulated on every peer from the replicated presses and analog.</para>
    ///
    /// <para>Every spray ends when the active weapon changes away from the claw.</para>
    /// </summary>
    public sealed class GrizzlyFlamethrowerActionExecutor : ShipActionExecutorBase
    {
        /// <summary>HUD: spray active (any claw).</summary>
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

            // The claws: four stiff red whips (GrizzlyClawWhips) that are both the look AND what burns.
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

        /// <summary>The claw's root in the hull's local space: its own side of the front.</summary>
        static Vector3 LocalApex(GrizzlyFlamethrowerActionSO so, int slot)
        {
            float x = slot switch { 0 => -so.SideOffset, 1 => so.SideOffset, _ => 0f };
            return new Vector3(x, 0f, slot == 2 ? 0f : so.NoseOffset);
        }

        /// <summary>The claw's outward turn about the hull's up, degrees: LT left, RT right.</summary>
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
                    // The claw follows the pull every frame; the burn ticks at its own rate.
                    float pressure = PressureOf(status, side);
                    float range = so.RangeForPressure(pressure);
                    float spread = so.SpreadForPressure(pressure);

                    // The cell: bigger flames burn it faster. Empty cuts the claw out.
                    spray.Fill = Mathf.Max(0f, spray.Fill - so.CellDrainPerSecond(pressure) * Time.deltaTime);

                    var vesselTf = status?.Transform;
                    if (spray.Whips && vesselTf)
                        spray.Whips.Show(so, vesselTf.TransformPoint(LocalApex(so, SlotOf(side))),
                                         vesselTf.rotation * Quaternion.Euler(-so.TiltUpDegrees, Yaw(so, SlotOf(side)), 0f),
                                         range, spread, spray.Fill, Time.deltaTime);
                    if (spray.Fill <= 0f)
                    {
                        EndSpray(side);
                        return;
                    }

                    sinceIgnite += Time.deltaTime;
                    if (sinceIgnite >= tickSeconds)
                    {
                        sinceIgnite = 0f;
                        IgniteAlongClaws(so, status, spray.Whips, burn);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// THE CLAWS DO THE WORK (design ask, 2026-10-17: "get rid of the cone completely, allows the claws to do the
        /// work"): every enemy prism a claw finger touches catches fire - each finger's links are swept with
        /// <see cref="PrismSpatialIndex.QuerySegment"/> at <c>clawBurnRadius</c>, the Brittle Star cords' cut. There is
        /// no cone: what you see is exactly what burns, and a finger that lags a turn burns where it lags.
        /// </summary>
        void IgniteAlongClaws(GrizzlyFlamethrowerActionSO so, IVesselStatus status, GrizzlyClawWhips whips,
            PrismBurnManager burn)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable || burn == null || !whips || status == null) return;

            bool convert = status.ElementalAbilityHandler != null &&
                           status.ElementalAbilityHandler.IsUpgradeActive(Element.Mass);
            float radius = so.ClawBurnRadius;
            int ignited = 0;
            _ignitedThisTick.Clear();

            for (int c = 0; c < whips.CordCount && ignited < so.IgnitesPerTick; c++)
            {
                var cord = whips.Cord(c);
                if (cord == null || !cord.Seeded) continue;
                var pts = cord.Points;
                for (int k = 0; k < pts.Length - 1 && ignited < so.IgnitesPerTick; k++)
                {
                    Vector3 a = pts[k], b = pts[k + 1];
                    _scratch.Clear();
                    index.QuerySegment(a, b, radius + CandidateExtent, _scratch);
                    Vector3 ab = b - a;
                    float abLenSq = ab.sqrMagnitude;
                    foreach (var prism in _scratch)
                    {
                        if (ignited >= so.IgnitesPerTick) break;
                        if (prism == null || prism.destroyed || prism.Domain == status.Domain) continue;
                        if (!_ignitedThisTick.Add(prism)) continue;

                        Vector3 centre = prism.transform.position;
                        float t = abLenSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(centre - a, ab) / abLenSq) : 0f;
                        float contact = radius + 0.5f * prism.transform.lossyScale.magnitude;
                        if ((centre - (a + ab * t)).sqrMagnitude > contact * contact) continue;

                        burn.Ignite(prism, status.PlayerName, status.Domain, convert);
                        ignited++;
                    }
                }
            }
            _scratch.Clear();
        }

        /// <summary>Padding on the candidate query so a large prism whose centre sits outside the finger's reach
        /// is still tested against its own size.</summary>
        const float CandidateExtent = 8f;
        readonly HashSet<Prism> _ignitedThisTick = new();
    }
}
