using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Angler AI's hunt, carried by the hull so every combat mode gets it (2026-11-19). An AI Angler in a mode whose
    /// objective is another pilot (<see cref="AIPilot.SeeksPlayers"/>: Broadside, Joust, Dog Fight) chased rivals and never
    /// pressed the eraser, the left trigger that sends its tamed creatures after a locked pilot, and never fetched the omni
    /// crystals a creature comes from - so it scored almost nothing (Broadside: 1.3 strikes a match). This is Kelpwood's
    /// hunter (<see cref="KelpwoodHunter"/>, the Angler's Charge game) at its Legendary rung, in the two pieces that travel:
    /// <list type="bullet">
    /// <item><b>Refuel</b>: short of creatures, fly to the nearest omni crystal (through the autopilot's external target,
    /// set only while refuelling and handed back after).</item>
    /// <item><b>Lock</b>: with a creature tamed and fewer hunters out than the cap, hold the eraser for a moment while the
    /// chased rival is in range and on the nose.</item>
    /// </list>
    /// <para>Input only, as a thumb is: it writes the left trigger on the hull's <c>InputStatus</c>. It stands down while a
    /// mode drives the autopilot itself (Kelpwood's hunter sets its own target provider), while a human flies, and in modes
    /// that do not seek pilots; the leapfrog's own policy (right trigger) is untouched.</para>
    /// </summary>
    public sealed class AnglerHuntAutopilot : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("The hull's autopilot (this GameObject's).")]
        [SerializeField] AIPilot autopilot;
        [Tooltip("The hull's VesselStatus (this GameObject's).")]
        [SerializeField] VesselStatus vesselStatus;

        [Header("Refuel")]
        [Tooltip("Fly to the nearest omni crystal while fewer creatures than this are tamed (Kelpwood aiRefuelBelowSchool).")]
        [SerializeField, Min(0)] int refuelBelowSchool = 2;

        [Header("Lock")]
        [Tooltip("Farthest rival (u) the eraser locks (Kelpwood aiHuntRange, Legendary).")]
        [SerializeField, Min(1f)] float huntRange = 900f;
        [Tooltip("The rival must be within this many degrees of the nose (Kelpwood aiLockGateDegrees is 1.5 at Legendary " +
                 "with its own aim; the platform's pursuit lags, so the gate is wider).")]
        [SerializeField, Range(0.5f, 45f)] float lockGateDegrees = 6f;
        [Tooltip("Seconds the eraser is held for one lock (Kelpwood aiLockHoldSeconds).")]
        [SerializeField, Min(0.05f)] float lockHoldSeconds = 0.3f;
        [Tooltip("Seconds between locks (Kelpwood aiLockIntervalSeconds, Legendary).")]
        [SerializeField, Min(0.05f)] float lockIntervalSeconds = 0.8f;
        [Tooltip("Never more hunters out than this (Kelpwood aiMaxHuntersOut, Legendary).")]
        [SerializeField, Min(1)] int maxHuntersOut = 4;

        AnglerCommandExecutor _angler;
        Func<Vector3> _refuelProvider;
        Vector3 _refuelPoint;
        bool _refuelling, _pressing;
        float _releaseAt, _nextLockAt;

        void Awake()
        {
            if (!autopilot) TryGetComponent(out autopilot);
            if (!vesselStatus) TryGetComponent(out vesselStatus);
            _angler = GetComponentInChildren<AnglerCommandExecutor>(true);
            _refuelProvider = () => _refuelPoint;
        }

        void OnDisable() => StandDown();

        void Update()
        {
            bool mine = _refuelling && autopilot && ReferenceEquals(autopilot.ExternalTargetProvider, _refuelProvider);
            if (!autopilot || !vesselStatus || !_angler || !autopilot.AutoPilotEnabled || !autopilot.SeeksPlayers
                || (autopilot.ExternalTargetProvider != null && !mine))
            {
                StandDown(handBack: mine);
                return;
            }

            float now = Time.time;
            if (_pressing && now >= _releaseAt) SetEraser(false);

            if (_angler.TamedCount < refuelBelowSchool && NearestOmniCrystal(transform.position) is Vector3 crystal)
            {
                _refuelPoint = crystal;
                if (!mine) autopilot.SetExternalTargetProvider(_refuelProvider);
                _refuelling = true;
            }
            else if (mine)
            {
                autopilot.ClearExternalTargetProvider();
                _refuelling = false;
            }

            TryLock(now);
        }

        void TryLock(float now)
        {
            if (_pressing || now < _nextLockAt || _angler.TamedCount <= 0 || _angler.HunterCount >= maxHuntersOut) return;
            if (VesselSilence.IsSilenced(vesselStatus)) return;
            if (_refuelling || NearestRivalInRange(transform.position) is not Vector3 rival) return;
            Vector3 to = rival - transform.position;
            if (to.sqrMagnitude < 1e-4f) return;
            if (Vector3.Angle(transform.forward, to) > lockGateDegrees) return;

            SetEraser(true);
            _releaseAt = now + lockHoldSeconds;
            _nextLockAt = now + lockIntervalSeconds;
        }

        Vector3? NearestRivalInRange(Vector3 from)
        {
            var players = autopilot.GameData ? autopilot.GameData.Players : null;
            if (players == null) return null;
            Vector3? best = null;
            float bestSqr = huntRange * huntRange;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                var tf = p?.Vessel?.Transform;
                if (!tf || p.Domain == ((IVesselStatus)vesselStatus).Domain || ReferenceEquals(tf, transform)) continue;
                float d = (tf.position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = tf.position;
            }
            return best;
        }

        void StandDown(bool handBack = false)
        {
            SetEraser(false);
            if (handBack && autopilot) autopilot.ClearExternalTargetProvider();
            _refuelling = false;
        }

        /// <summary>The eraser is the left trigger: the channel a pad writes, replicated to every peer.</summary>
        void SetEraser(bool held)
        {
            if (!held && !_pressing) return;
            _pressing = held;
            var input = vesselStatus ? ((IVesselStatus)vesselStatus).InputStatus : null;
            if (input != null) input.LeftTriggerAnalog = held ? 1f : 0f;
        }

        /// <summary>The nearest live OMNI crystal (one creature for an Angler), or null - KelpwoodHunter's rule.</summary>
        static Vector3? NearestOmniCrystal(Vector3 from)
        {
            Vector3? best = null;
            float bestSqr = float.MaxValue;
            var active = Crystal.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var c = active[i];
                if (!c || c.IsEmbedded || c.crystalProperties.IsElemental) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = c.transform.position;
            }
            return best;
        }
    }
}
