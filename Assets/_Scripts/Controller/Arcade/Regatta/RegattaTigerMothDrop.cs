using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Tiger Moth's Drop for an AUTOPILOT in Regatta, after Waystation's long-drop coach (<c>WaystationController</c>).
    /// Before a leg worth a drop the pilot steers its heading square to the next ring and ROLLS its belly onto it
    /// (<see cref="AIPilot.MarkBellyTarget"/>), presses the Drop when the belly is on, and lets go short of the ring's
    /// plane so it flies through the mouth. Waystation also lines up by PITCH when the ring is near the nose; on Regatta's
    /// open legs that pitch-up overshot and never dropped (2026-11-17, belly 74 to 120 degrees), so this coach always rolls.
    /// Inputs only: the aim is a steering point AIPilot flies at, the press goes through the hull's own action handler.
    /// Every other hull is untouched (it binds no <see cref="TigerMothDropActionSO"/>).
    /// </summary>
    [Serializable]
    public sealed class RegattaTigerMothDrop
    {
        [Tooltip("Shortest leg the autopilot spends a drop on (u). Regatta's legs run 461-858 u, so this sits under them.")]
        [Min(0f)] public float MinDistance = 350f;

        [Tooltip("Let go this many units SHORT of the ring's plane, so the hull flies through the mouth after the drop.")]
        [Min(0f)] public float LandShort = 77.4f;

        [Tooltip("How far short the drop lets go for the aim it carries: this many times the sideways miss (distance x " +
                 "sin(belly error)). A copy of the Tiger Moth's drop drive, trained in Waystation " +
                 "(WaystationController.aiDropMissMargin); Regatta trains no AI. 2 until 2026-11-19, then run3's 1.02.")]
        [Min(0f)] public float MissMargin = 1.02f;

        [Tooltip("Seconds before a pilot tries again after a drop or a line-up it gave up on.")]
        [Min(0.1f)] public float RetrySeconds = 1.5f;

        [Tooltip("How closely the BELLY must be on the ring to drop.")]
        [Range(1f, 30f)] public float DropAimDegrees = 9.87f;

        [Tooltip("Line the belly up only when the ring is within this many degrees of the nose (further round, fly on).")]
        [Range(60f, 170f)] public float RollMaxDegrees = 125f;

        [Tooltip("Longest a pilot spends lining up before it gives up and flies on.")]
        [Min(0.5f)] public float AimMaxSeconds = 6f;

        const float Lead = 500f;

        readonly Dictionary<IPlayer, float> _aimSince = new();
        readonly Dictionary<IPlayer, float> _releaseAt = new();
        readonly Dictionary<IPlayer, float> _retryAt = new();
        readonly Dictionary<Transform, TigerMothDropExecutor> _executors = new();

        /// <summary>True while <paramref name="pilot"/> is lining its belly up (its aim is then a lead, not a place).</summary>
        public bool IsAiming(IPlayer pilot) => pilot != null && _aimSince.ContainsKey(pilot);

        /// <summary>The steering point while lining up for a drop to <paramref name="ring"/>; false when no drop is wanted.</summary>
        public bool TryAim(IPlayer pilot, IVesselStatus status, Vector3 ring, out Vector3 target)
        {
            target = default;
            if (status == null || !Wanted(pilot, status, ring, out var toRing))
            {
                Forget(pilot);
                return false;
            }
            Transform hull = status.Transform;
            Vector3 square = Vector3.ProjectOnPlane(hull.forward, toRing);
            target = hull.position + (square.sqrMagnitude > 1e-6f ? square.normalized : hull.up) * Lead;
            return true;
        }

        /// <summary>The belly's target while lining up by roll.</summary>
        public bool TryBelly(IPlayer pilot, IVesselStatus status, Vector3 ring, out Vector3 worldDirection)
        {
            worldDirection = default;
            if (status == null || !IsAiming(pilot)) return false;
            worldDirection = ring - status.Transform.position;
            return true;
        }

        /// <summary>The server tick: release drops that have run their hold, and press for pilots whose belly is on.</summary>
        public void Tick(IReadOnlyList<IPlayer> players, Func<IPlayer, Transform> nextRing)
        {
            if (players == null) return;
            float now = Time.time;
            for (int i = 0; i < players.Count; i++)
            {
                var pilot = players[i];
                var status = pilot?.Vessel?.VesselStatus;
                var handler = status?.ActionHandler;
                if (handler == null) continue;
                if (!(status is Component c) || !c.TryGetComponent(out AIPilot autopilot) || !autopilot.AutoPilotEnabled)
                {
                    if (_releaseAt.ContainsKey(pilot)) Release(pilot);
                    continue;
                }
                if (_releaseAt.TryGetValue(pilot, out float releaseAt))
                {
                    if (now >= releaseAt) Release(pilot);
                    continue;
                }
                if (_retryAt.TryGetValue(pilot, out float retryAt) && now < retryAt) continue;
                if (!handler.TryGetBoundAction<TigerMothDropActionSO>(out var drop, out var input)) continue;
                var ring = nextRing(pilot);
                if (!ring) continue;
                Vector3 toRing = ring.position - status.Transform.position;
                float distance = toRing.magnitude;
                if (distance < MinDistance) continue;
                float aim = TigerMothDropMath.BellyAimError(status.Transform.rotation, toRing);
                if (aim > DropAimDegrees) continue;
                float along = distance * Mathf.Cos(aim * Mathf.Deg2Rad) - LandShort
                              - MissMargin * distance * Mathf.Sin(aim * Mathf.Deg2Rad);
                _releaseAt[pilot] = now + TigerMothDropMath.HoldSecondsFor(Mathf.Max(0f, along), drop.DropSpeed,
                    drop.DropEaseSeconds, drop.LandSeconds, status.Speed);
                _retryAt[pilot] = now + RetrySeconds;
                Forget(pilot);
                handler.PerformShipControllerActionsReplicated(input);
            }
        }

        /// <summary>Let every held drop go (the whistle, a despawn).</summary>
        public void ReleaseAll()
        {
            foreach (var pilot in new List<IPlayer>(_releaseAt.Keys)) Release(pilot);
            _releaseAt.Clear();
            _retryAt.Clear();
            _aimSince.Clear();
            _executors.Clear();
        }

        bool Wanted(IPlayer pilot, IVesselStatus status, Vector3 ring, out Vector3 toRing)
        {
            toRing = default;
            float now = Time.time;
            var handler = status.ActionHandler;
            if (pilot == null || handler == null || !handler.TryGetBoundAction<TigerMothDropActionSO>(out _, out _))
                return false;
            var executor = ExecutorOf(status.Transform);
            if (!executor || executor.IsDropping || !executor.IsReady) return false;
            if (_releaseAt.ContainsKey(pilot)) return false;
            if (_retryAt.TryGetValue(pilot, out float retryAt) && now < retryAt) return false;
            toRing = ring - status.Transform.position;
            if (toRing.magnitude < MinDistance) return false;
            if (_aimSince.TryGetValue(pilot, out float since))
            {
                if (now - since <= AimMaxSeconds) return true;
                Forget(pilot);
                _retryAt[pilot] = now + RetrySeconds;
                return false;
            }
            float off = Vector3.Angle(status.Transform.forward, toRing);
            if (off > RollMaxDegrees) return false;
            _aimSince[pilot] = now;
            return true;
        }

        void Forget(IPlayer pilot)
        {
            if (pilot == null) return;
            _aimSince.Remove(pilot);
        }

        void Release(IPlayer pilot)
        {
            _releaseAt.Remove(pilot);
            var handler = pilot?.Vessel?.VesselStatus?.ActionHandler;
            if (handler != null && handler.TryGetBoundAction<TigerMothDropActionSO>(out _, out var input))
                handler.StopShipControllerActionsReplicated(input);
        }

        TigerMothDropExecutor ExecutorOf(Transform hull)
        {
            if (!hull) return null;
            if (_executors.TryGetValue(hull, out var cached) && cached) return cached;
            var found = hull.GetComponentInChildren<TigerMothDropExecutor>(true);
            _executors[hull] = found;
            return found;
        }
    }
}
