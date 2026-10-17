using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Plasma claw: while a trigger is held, its claw - four stiff red fingers (<see cref="GrizzlyClawWhips"/>) -
    /// SLICES every enemy prism a finger touches and starts the burn around each cut (<see cref="ContactAlongClaws"/>,
    /// 2026-10-17). There is
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
                int igniteBudget = 0;

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

                    // Every frame the fingers cut what they touch; the burn's budget refills each tick.
                    sinceIgnite += Time.deltaTime;
                    if (sinceIgnite >= tickSeconds)
                    {
                        sinceIgnite = 0f;
                        igniteBudget = so.IgnitesPerTick;
                    }
                    ContactAlongClaws(so, status, spray.Whips, burn, ref igniteBudget);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// THE CLAWS DO THE WORK (design asks, 2026-10-17: "get rid of the cone completely, allows the claws to do the
        /// work"; "make the plasma claw slice prisms as well as start the burn"). Every frame each finger's links are
        /// swept with <see cref="PrismSpatialIndex.QuerySegment"/> at <c>clawBurnRadius</c>, the Brittle Star cords'
        /// cut, and every enemy prism a finger touches is:
        /// <list type="bullet">
        /// <item><b>SLICED</b> (<see cref="Prism.Slice"/> along the plane the finger is sweeping, debris at the
        /// finger's own velocity) - the Brittle Star's cord cut, enemy mass only, super-shields refuse and a shield
        /// pops first; and</item>
        /// <item><b>the burn starts around the cut</b>: the prisms within <c>sliceIgniteRadius</c> of it are
        /// ignited (PrismBurnManager, so they spread and, with Mass 5, are stolen on burnout). A sliced prism is
        /// gone, so it cannot burn itself; its neighbours carry the fire.</item>
        /// </list>
        /// Ignites are budgeted at <c>ignitesPerTick</c> per claw per burn tick, slices at <c>slicesPerFrame</c>.
        /// With <c>sliceEnabled</c> off the claw only burns what it touches, as before. Runs on every peer from the
        /// replicated input, like the Brittle Star's cords.
        /// </summary>
        void ContactAlongClaws(GrizzlyFlamethrowerActionSO so, IVesselStatus status, GrizzlyClawWhips whips,
            PrismBurnManager burn, ref int igniteBudget)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable || burn == null || !whips || status == null) return;

            bool convert = status.ElementalAbilityHandler != null &&
                           status.ElementalAbilityHandler.IsUpgradeActive(Element.Mass);
            float radius = so.ClawBurnRadius;
            int sliced = 0;
            _touchedThisFrame.Clear();

            for (int c = 0; c < whips.CordCount; c++)
            {
                var cord = whips.Cord(c);
                if (cord == null || !cord.Seeded) continue;
                var pts = cord.Points;
                for (int k = 0; k < pts.Length - 1; k++)
                {
                    Vector3 a = pts[k], b = pts[k + 1];
                    _scratch.Clear();
                    index.QuerySegment(a, b, radius + CandidateExtent, _scratch);
                    if (_scratch.Count == 0) continue;
                    Vector3 ab = b - a;
                    float abLenSq = ab.sqrMagnitude;
                    for (int i = 0; i < _scratch.Count; i++)
                    {
                        var prism = _scratch[i];
                        if (prism == null || prism.destroyed || prism.Domain == status.Domain) continue;
                        if (!_touchedThisFrame.Add(prism)) continue;

                        Vector3 centre = prism.transform.position;
                        float t = abLenSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(centre - a, ab) / abLenSq) : 0f;
                        float contact = radius + 0.5f * prism.transform.lossyScale.magnitude;
                        Vector3 onFinger = a + ab * t;
                        if ((centre - onFinger).sqrMagnitude > contact * contact) continue;

                        if (!so.SliceEnabled)
                        {
                            if (igniteBudget <= 0) continue;
                            burn.Ignite(prism, status.PlayerName, status.Domain, convert);
                            igniteBudget--;
                            continue;
                        }

                        if (sliced >= so.SlicesPerFrame) continue;
                        if (prism.prismProperties is { IsSuperShielded: true }) continue;

                        // Light the neighbours first: the cut prism is about to be gone.
                        IgniteAround(onFinger, so, status, burn, convert, prism, ref igniteBudget);

                        Vector3 v = Vector3.Lerp(cord.NodeVelocity(k), cord.NodeVelocity(k + 1), t);
                        Vector3 cutNormal = Vector3.Cross(ab, v);
                        if (cutNormal.sqrMagnitude < 1e-8f) cutNormal = Vector3.Cross(ab, status.Transform.up);
                        if (cutNormal.sqrMagnitude < 1e-8f) cutNormal = Vector3.right;
                        prism.Slice(v * so.SliceDebrisRestitution, status.Domain, status.PlayerName,
                                    onFinger, cutNormal.normalized, debrisSpeedLimit: so.SliceDebrisSpeedLimit);
                        PlayAt(so.SliceEvent, onFinger);
                        sliced++;
                    }
                }
            }
            _scratch.Clear();
        }

        /// <summary>Ignite the enemy prisms within <c>sliceIgniteRadius</c> of a cut, within the budget.</summary>
        void IgniteAround(Vector3 point, GrizzlyFlamethrowerActionSO so, IVesselStatus status, PrismBurnManager burn,
            bool convert, Prism except, ref int igniteBudget)
        {
            if (igniteBudget <= 0 || so.SliceIgniteRadius <= 0f) return;
            _neighbours.Clear();
            PrismSpatialIndex.Instance.QuerySphere(point, so.SliceIgniteRadius, _neighbours);
            for (int i = 0; i < _neighbours.Count && igniteBudget > 0; i++)
            {
                var n = _neighbours[i];
                if (n == null || n == except || n.destroyed || n.Domain == status.Domain) continue;
                burn.Ignite(n, status.PlayerName, status.Domain, convert);
                igniteBudget--;
            }
            _neighbours.Clear();
        }

        static void PlayAt(FMODUnity.EventReference ev, Vector3 pos)
        {
            if (!ev.IsNull && AudioSystem.Instance)
                AudioSystem.Instance.PlaySFXEvent(ev, pos);
        }

        /// <summary>Padding on the candidate query so a large prism whose centre sits outside the finger's reach
        /// is still tested against its own size.</summary>
        const float CandidateExtent = 8f;
        readonly HashSet<Prism> _touchedThisFrame = new();
        readonly List<Prism> _neighbours = new();
    }
}
