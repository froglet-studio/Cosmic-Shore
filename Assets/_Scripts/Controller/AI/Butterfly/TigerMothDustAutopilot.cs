using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Tiger Moth AI's dust hunt, carried by the hull so every combat mode gets it (2026-11-19). The Tiger Moth's one
    /// Charge weapon is the Scale Dust that hangs BELOW the hull in Dust mode, and <see cref="AIPilot"/> writes only a
    /// stick and a throttle, so in a mode whose objective is another pilot (<see cref="AIPilot.SeeksPlayers"/>: Broadside,
    /// Joust, Dog Fight) an AI Tiger Moth flew its whole match in Mass mode with the weapon off, and flew AT rivals, meeting
    /// them with its body (Broadside: 0 hits of any verb in 16 matches). This is Dustup's hunter
    /// (<see cref="DustupController"/>, the Tiger Moth's Charge game), carried by the hull: hold Dust mode through
    /// <see cref="ButterflyAutopilotModeDriver"/> and steer to a point ABOVE an intercept on the nearest rival, along the
    /// hull's own up axis, so the capsule passes over them.
    /// <para>Steering is the autopilot's external target, set only while it hunts and handed back after. It stands down
    /// while a mode drives the autopilot itself (Dustup, Regatta's Drop coach, Waystation), while a human flies, and in
    /// modes that do not seek pilots.</para>
    /// </summary>
    public sealed class TigerMothDustAutopilot : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("The hull's autopilot (this GameObject's).")]
        [SerializeField] AIPilot autopilot;
        [Tooltip("The hull's VesselStatus (this GameObject's).")]
        [SerializeField] VesselStatus vesselStatus;

        [Header("Hunt (DustupSettings' values)")]
        [Tooltip("Seconds between choices of which rival to hunt.")]
        [SerializeField, Min(0.1f)] float retargetSeconds = 1.5f;
        [Tooltip("Seconds of the rival's motion the intercept leads by.")]
        [SerializeField, Min(0f)] float interceptLeadSeconds = 0.5f;
        [Tooltip("A human rival reads as this many times closer than it is.")]
        [SerializeField, Min(1f)] float humanFocus = 3f;
        [Tooltip("How far above the rival (u, along the hull's own up) to fly: half the resting capsule, so the dust hangs on them.")]
        [SerializeField, Min(0f)] float dustHover = 30f;
        [Tooltip("Seconds before a mode press that did not take is tried again.")]
        [SerializeField, Min(0.1f)] float modeRetrySeconds = 1f;

        readonly ButterflyAutopilotModeDriver _modes = new();
        Func<Vector3> _provider;
        IPlayer _rival;
        float _nextSample;
        bool _hunting;

        void Awake()
        {
            if (!autopilot) TryGetComponent(out autopilot);
            if (!vesselStatus) TryGetComponent(out vesselStatus);
            _modes.RetrySeconds = modeRetrySeconds;
            _provider = AimPoint;
        }

        void OnDisable() => StandDown(handBack: _hunting);

        void Update()
        {
            bool mine = _hunting && autopilot && ReferenceEquals(autopilot.ExternalTargetProvider, _provider);
            if (!autopilot || !vesselStatus || !autopilot.AutoPilotEnabled || !autopilot.SeeksPlayers
                || (autopilot.ExternalTargetProvider != null && !mine))
            {
                if (_hunting) StandDown(handBack: mine);
                return;
            }
            if (!mine) autopilot.SetExternalTargetProvider(_provider);
            _hunting = true;
            var self = ((IVesselStatus)vesselStatus).Player;
            if (self != null) _modes.Drive(self, wantDust: true);
        }

        Vector3 AimPoint()
        {
            Vector3 selfPos = transform.position;
            var self = ((IVesselStatus)vesselStatus).Player;
            if (Time.time >= _nextSample || !IsLiveRival(_rival, self))
            {
                _nextSample = Time.time + retargetSeconds;
                _rival = NearestRival(self, selfPos);
            }
            var rivalTf = _rival?.Vessel?.Transform;
            if (rivalTf == null) return autopilot.TargetPosition;
            var status = _rival.Vessel.VesselStatus;
            Vector3 velocity = status != null ? status.Course * status.Speed : Vector3.zero;
            return rivalTf.position + velocity * interceptLeadSeconds + transform.up * dustHover;
        }

        IPlayer NearestRival(IPlayer self, Vector3 from)
        {
            var players = autopilot.GameData ? autopilot.GameData.Players : null;
            if (players == null) return null;
            IPlayer best = null;
            float bestScore = float.MaxValue, focusSqr = humanFocus * humanFocus;
            for (int i = 0; i < players.Count; i++)
            {
                var c = players[i];
                if (!IsLiveRival(c, self)) continue;
                float score = (c.Vessel.Transform.position - from).sqrMagnitude;
                if (!c.IsInitializedAsAI) score /= focusSqr;
                if (score >= bestScore) continue;
                bestScore = score;
                best = c;
            }
            return best;
        }

        bool IsLiveRival(IPlayer c, IPlayer self) =>
            c != null && !ReferenceEquals(c, self) && c.Domain != ((IVesselStatus)vesselStatus).Domain && c.Vessel?.Transform != null;

        void StandDown(bool handBack)
        {
            _modes.ReleaseAll();
            if (handBack && autopilot) autopilot.ClearExternalTargetProvider();
            _hunting = false;
            _rival = null;
        }
    }
}
