using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One Ark across a party (Docs/Voyages/PARTY.md §2). Every machine sails its own Ark through its own copy of the
    /// worlds (the same seed, the same origin), and each one starts moving when its own load veil drops, which is seconds
    /// apart between machines. The host sends where its Ark is a few times a second (<see cref="VoyageCourseLink.SendArkPose"/>,
    /// on the network clock); a friend's machine pulls its own Ark toward that pose, predicted forward by the message's age,
    /// so the Ark every pilot escorts is in the same place on every screen. Only poses of the same leg are matched (a
    /// friend still at the last stop is not dragged into the next leg), and a gap past <see cref="GiveUpDistance"/> is left
    /// alone: the two worlds disagree, and dragging the Ark across them would only tear it out of its own.
    /// </summary>
    public sealed class VoyageArkSync
    {
        /// <summary>The host sends this often.</summary>
        public const float SendSeconds = 0.2f;
        /// <summary>A friend's Ark closes this share of the gap per second's worth of 1/time constant.</summary>
        const float CatchUpSeconds = 0.6f;
        /// <summary>Under this gap (units) the two Arks are the same Ark: no pull, so nothing jitters.</summary>
        const float Deadband = 2f;
        /// <summary>Over this gap the worlds disagree: the friend's Ark sails its own course.</summary>
        public const float GiveUpDistance = 2500f;
        /// <summary>A pose older than this is not followed (the host stopped sending: its Ark is held or gone).</summary>
        const double StaleSeconds = 1.5;

        float _nextSend;
        Vector3 _lastPosition;
        float _lastPositionAt = -1f;

        VoyageArkPose _pose;
        bool _hasPose;
        bool _listening;

        /// <summary>The gap (units) between this machine's Ark and the host's when last measured (a friend; 0 on the host).</summary>
        public float LastGap { get; private set; }

        /// <summary>Start hearing the host's poses (a friend's machine). Safe to call again.</summary>
        public void Listen()
        {
            if (_listening) return;
            _listening = true;
            _hasPose = false;
            VoyageCourseLink.ArkPoseReceived += OnPose;
        }

        /// <summary>Stop hearing them (the voyage ended).</summary>
        public void Stop()
        {
            if (_listening) VoyageCourseLink.ArkPoseReceived -= OnPose;
            _listening = false;
            _hasPose = false;
            _lastPositionAt = -1f;
            LastGap = 0f;
        }

        void OnPose(VoyageArkPose pose)
        {
            if (_hasPose && pose.ServerTime < _pose.ServerTime) return;   // an older pose arrived late
            _pose = pose;
            _hasPose = true;
        }

        /// <summary>
        /// Once a frame. The host (<paramref name="leader"/>) sends its Ark's pose; a friend pulls its Ark toward the host's.
        /// <paramref name="legSerial"/> is this machine's leg (an escort is always leg 1). <paramref name="anchorLeft"/> is
        /// the host's anchor window left at a Crystal Run cell (0 under way): an anchored Ark is not under way, but the
        /// host keeps sending so every friend's window ends with its own.
        /// </summary>
        public void Tick(Ark ark, bool leader, int legSerial, float anchorLeft = 0f)
        {
            if (!ark) return;
            if (leader)
            {
                if (ark.IsUnderway || anchorLeft > 0f) Send(ark, legSerial, anchorLeft);
                return;
            }
            if (ark.IsUnderway) Follow(ark, legSerial);
        }

        /// <summary>
        /// A friend: what the host's last fresh pose of <paramref name="legSerial"/> says of its anchor. False when there
        /// is no fresh pose of that leg; otherwise <paramref name="left"/> is the host's window left now (0: sailing).
        /// </summary>
        public bool TryHostAnchor(int legSerial, out float left)
        {
            left = 0f;
            if (!_hasPose || _pose.LegSerial != legSerial) return false;
            double age = VoyageCourseLink.ServerTimeNow - _pose.ServerTime;
            if (age < 0) age = 0;
            if (age > StaleSeconds) return false;
            left = _pose.AnchorLeft > 0f ? Mathf.Max(0.01f, _pose.AnchorLeft - (float)age) : 0f;
            return true;
        }

        void Send(Ark ark, int legSerial, float anchorLeft)
        {
            if (!VoyageCourseLink.HostWithParty) return;
            float now = Time.unscaledTime;
            if (now < _nextSend) return;
            _nextSend = now + SendSeconds;
            var p = ark.Position;
            var v = anchorLeft > 0f ? Vector3.zero : _lastPositionAt >= 0f && now > _lastPositionAt
                ? (p - _lastPosition) / (now - _lastPositionAt)
                : ark.Forward * ark.Speed;
            _lastPosition = p;
            _lastPositionAt = now;
            VoyageCourseLink.SendArkPose(new VoyageArkPose(legSerial, VoyageCourseLink.ServerTimeNow, p, ark.transform.rotation, v, anchorLeft));
        }

        void Follow(Ark ark, int legSerial)
        {
            if (!_hasPose || _pose.LegSerial != legSerial || ark.IsHullLost) return;
            double age = VoyageCourseLink.ServerTimeNow - _pose.ServerTime;
            if (age < 0) age = 0;
            if (age > StaleSeconds) return;
            var target = _pose.Position + _pose.Velocity * (float)age;
            float gap = Vector3.Distance(ark.Position, target);
            LastGap = gap;
            if (gap < Deadband || gap > GiveUpDistance) return;
            float blend = 1f - Mathf.Exp(-Time.deltaTime / CatchUpSeconds);
            ark.FollowPose(target, _pose.Rotation, blend);
        }
    }
}
