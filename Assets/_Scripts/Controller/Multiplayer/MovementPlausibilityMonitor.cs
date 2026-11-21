using System.Collections.Generic;
using System.Text;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Host-side movement plausibility check (fair play, Threat Board T5; Docs/AntiCheat/README.md §4.2).
    ///
    /// <para><b>Why the host.</b> Vessels replicate owner-authoritatively (<c>ClientNetworkTransform</c>), so a
    /// modified client's speed or teleport is believed by everyone. The host sees every hull, so it measures each
    /// guest-owned hull's path speed over a short window and its discontinuities.</para>
    ///
    /// <para><b>Signals, never kicks.</b> A sustained speed above the hull's ceiling, or too many jumps a minute,
    /// raises a <see cref="FairPlaySignal"/> with the numbers that triggered it. Nothing is done to the player:
    /// signals feed the trust record once it exists, and a person reviews before any restriction (README §4.6).</para>
    ///
    /// <para><b>Observe first.</b> The transformer's speed has many terms (throttle, boost, element multipliers,
    /// speed modifiers, abilities, wormholes), so a ceiling computed from them would be wrong for some hull and
    /// flag honest players. Ceilings come from <see cref="MovementPlausibilityConfigSO"/>; until they are set,
    /// the monitor only measures. At each turn's end it logs (ArcadeMatch channel) the fastest sustained speed
    /// and jump count per hull, separately for guests and for the host's AI pilots, whose flying is honest by
    /// construction and calibrates the ceilings.</para>
    ///
    /// <para><b>AI-like steering (Threat Board T6).</b> Every client carries the AI pilots, and the AI writes the
    /// same input a human does, so a modified client can hand its own hull to the AI. The host cannot see inputs,
    /// only where each nose points, so it measures two things per hull: the share of samples where the nose is held
    /// still (humans pause between inputs; the AI corrects every frame) and the mean change in turn rate. AI hulls
    /// on the host calibrate both; until <see cref="MovementPlausibilityConfigSO.AiLikeCheckOn"/>, nothing is
    /// judged. Crew vessels are never judged, because a guest legitimately parks a crew hull on its AI
    /// (<see cref="CrewStations"/>), and neither is a guest who is riding another hull.</para>
    ///
    /// <para>The host's own hull is never judged: the host is the authority, and policing it from itself proves
    /// nothing (that is Threat Board T4). Hulls owned by the server (AI, or a human flying an AI hull through the
    /// pilot swap) are simulated here and cannot be spoofed, so they only calibrate.</para>
    /// </summary>
    public sealed class MovementPlausibilityMonitor : MonoBehaviour
    {
        sealed class Track
        {
            public IPlayer Player;
            public IVessel Vessel;
            public ulong Owner;
            public bool Judged;            // guest-owned: the check applies
            public Vector3[] Positions;
            public float[] Times;
            public int Head, Count;
            public float OverCeilingTime;
            public float LastSpeedSignalAt = float.NegativeInfinity;
            public float LastJumpSignalAt = float.NegativeInfinity;
            public float FirstSampleAt = -1f;
            public int Jumps;
            public float MaxSustained;
            public bool Seen;

            // Steering (T6): reset whenever the hull changes owner (pilot swap) or jumps.
            public bool SteerJudged;
            public bool CrewHull;           // read once: a crew hull may be parked on its AI by a guest
            public ulong SteerOwner = ulong.MaxValue;
            public SteeringProfile Steering;
            public bool AiLikeSignalled;    // once per turn
        }

        GameDataSO _gameData;
        MovementPlausibilityConfigSO _config;
        readonly Dictionary<IVessel, Track> _tracks = new();
        readonly List<IVessel> _stale = new();
        float _nextSampleAt;
        float _turnStartedAt;
        bool _subscribed;

        /// <summary>Attaches the monitor to <paramref name="host"/> on the server. Called once per game scene.</summary>
        public static MovementPlausibilityMonitor Attach(GameObject host, GameDataSO gameData)
        {
            if (!host.TryGetComponent(out MovementPlausibilityMonitor monitor))
                monitor = host.AddComponent<MovementPlausibilityMonitor>();
            monitor.Bind(gameData);
            return monitor;
        }

        void Bind(GameDataSO gameData)
        {
            _gameData = gameData;
            _config = MovementPlausibilityConfigSO.LoadOrDefault();
            if (_subscribed) return;
            _gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            _gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnded;
            _subscribed = true;
            _turnStartedAt = Time.time;
        }

        void OnDestroy()
        {
            if (!_subscribed) return;
            _gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            _gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnded;
            _subscribed = false;
        }

        void HandleTurnStarted()
        {
            _tracks.Clear();
            _turnStartedAt = Time.time;
        }

        void HandleTurnEnded() => LogTurnSummary();

        void Update()
        {
            if (_gameData == null) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            float now = Time.time;
            if (now < _nextSampleAt) return;
            _nextSampleAt = now + _config.SampleInterval;

            foreach (var t in _tracks.Values) t.Seen = false;

            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var vessel = player?.Vessel;
                if (vessel is not Component c || !c) continue;
                if (!c.TryGetComponent(out NetworkObject netObj) || !netObj.IsSpawned) continue;

                ulong owner = netObj.OwnerClientId;
                bool hostOwnHull = owner == NetworkManager.ServerClientId && !IsAI(player);
                if (hostOwnHull) continue;

                if (!_tracks.TryGetValue(vessel, out var track))
                {
                    track = NewTrack();
                    track.CrewHull = CrewStations.CrewOf(vessel) != null;
                    _tracks[vessel] = track;
                }
                track.Player = player;
                track.Vessel = vessel;
                track.Owner = owner;
                track.Judged = owner != NetworkManager.ServerClientId;
                track.Seen = true;
                if (track.SteerOwner != owner) ResetSteering(track, owner);
                track.SteerJudged = track.Judged && !IsAI(player) && ReferenceEquals(player.Vessel, vessel) &&
                                    !track.CrewHull &&
                                    !(player is Player human && CrewStations.IsRiding(human));
                Sample(track, vessel.Transform.position, vessel.Transform.forward, now);
            }

            // Hulls that left the match: drop them so a respawned object starts clean.
            _stale.Clear();
            foreach (var kv in _tracks) if (!kv.Value.Seen) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++) _tracks.Remove(_stale[i]);
        }

        Track NewTrack()
        {
            int size = Mathf.CeilToInt(_config.WindowSeconds / _config.SampleInterval) + 2;
            return new Track { Positions = new Vector3[size], Times = new float[size] };
        }

        static bool IsAI(IPlayer player) => player is Player p && p.NetIsAI.Value;

        void Sample(Track t, Vector3 position, Vector3 forward, float now)
        {
            if (t.FirstSampleAt < 0f) t.FirstSampleAt = now;

            if (t.Count > 0)
            {
                int last = (t.Head - 1 + t.Positions.Length) % t.Positions.Length;
                float dt = now - t.Times[last];
                if (dt <= 0f) return; // paused: nothing moved in game time
                float step = (position - t.Positions[last]).magnitude;
                if (step / dt > _config.JumpSpeed)
                {
                    // Respawn, wormhole, warp, or a teleport hack: restart the window, count the jump.
                    t.Jumps++;
                    t.Count = 0;
                    t.OverCeilingTime = 0f;
                    t.Steering.Restart(forward);
                    CheckJumpRate(t, now);
                }
                else SampleSteering(t, forward, dt, now);
            }
            else t.Steering.Restart(forward);

            t.Positions[t.Head] = position;
            t.Times[t.Head] = now;
            t.Head = (t.Head + 1) % t.Positions.Length;
            if (t.Count < t.Positions.Length) t.Count++;

            float speed = PathSpeed(t);
            if (speed < 0f) return;
            if (speed > t.MaxSustained) t.MaxSustained = speed;

            if (!t.Judged) return;
            float ceiling = _config.CeilingFor(t.Vessel.VesselStatus.VesselType);
            if (ceiling <= 0f) return;

            t.OverCeilingTime = speed > ceiling ? t.OverCeilingTime + _config.SampleInterval : 0f;
            if (t.OverCeilingTime >= _config.SustainSeconds &&
                now - t.LastSpeedSignalAt >= _config.SignalCooldownSeconds)
            {
                t.LastSpeedSignalAt = now;
                Raise(t, FairPlaySignalKind.SustainedSpeed, speed, ceiling, now);
            }
        }

        /// <summary>Path length over the window divided by its duration, or -1 until the window is full.</summary>
        float PathSpeed(Track t)
        {
            int n = t.Count, len = t.Positions.Length;
            if (n < 2) return -1f;
            int oldest = (t.Head - n + len) % len;
            int newest = (t.Head - 1 + len) % len;
            float span = t.Times[newest] - t.Times[oldest];
            if (span < _config.WindowSeconds * 0.8f) return -1f;

            float path = 0f;
            for (int k = 1; k < n; k++)
            {
                int a = (oldest + k - 1) % len, b = (oldest + k) % len;
                path += (t.Positions[b] - t.Positions[a]).magnitude;
            }
            // Drop the oldest sample so the window slides.
            if (span > _config.WindowSeconds) t.Count--;
            return path / span;
        }

        static void ResetSteering(Track t, ulong owner)
        {
            t.SteerOwner = owner;
            t.Steering = default;
            t.AiLikeSignalled = false;
        }

        void SampleSteering(Track t, Vector3 forward, float dt, float now)
        {
            t.Steering.Add(forward, dt, _config.SteerIdleTurnRate);

            if (!t.SteerJudged || t.AiLikeSignalled || !_config.AiLikeCheckOn) return;
            if (t.Steering.Seconds < _config.AiLikeMinSeconds) return;

            float idlePercent = t.Steering.IdlePercent, jerk = t.Steering.MeanTurnJerk;
            if (idlePercent > _config.AiLikeMaxIdlePercent || jerk > _config.AiLikeMaxTurnJerk) return;

            t.AiLikeSignalled = true;
            Raise(t, FairPlaySignalKind.AiLikeSteering, idlePercent, _config.AiLikeMaxIdlePercent, now);
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[FairPlay] AI-like steering: {t.Player.Name} mean turn jerk {jerk:0.#} (limit {_config.AiLikeMaxTurnJerk:0.#}) " +
                $"over {t.Steering.Seconds:0}s");
        }

        void CheckJumpRate(Track t, float now)
        {
            if (!t.Judged || _config.MaxJumpsPerMinute <= 0f) return;
            float minutes = (now - t.FirstSampleAt) / 60f;
            if (minutes < 0.5f) return;
            float rate = t.Jumps / minutes;
            if (rate > _config.MaxJumpsPerMinute && now - t.LastJumpSignalAt >= _config.SignalCooldownSeconds)
            {
                t.LastJumpSignalAt = now;
                Raise(t, FairPlaySignalKind.JumpRate, rate, _config.MaxJumpsPerMinute, now);
            }
        }

        void Raise(Track t, FairPlaySignalKind kind, float measured, float limit, float now)
        {
            var signal = new FairPlaySignal(kind, t.Player.Name, t.Owner, t.Vessel.VesselStatus.VesselType.ToString(),
                measured, limit, now - _turnStartedAt);
            FairPlaySignals.Raise(signal);
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[FairPlay] signal: {signal}");
        }

        void LogTurnSummary()
        {
            if (!CSDebug.IsVerbose(CSLogChannel.ArcadeMatch) || _tracks.Count == 0) return;
            var sb = new StringBuilder("[FairPlay] movement this turn (fastest sustained path speed, jumps; " +
                                       "steering: still %, mean turn jerk, seconds):");
            foreach (var t in _tracks.Values)
            {
                sb.Append("\n  ").Append(t.Judged ? "guest " : "AI    ")
                  .Append(t.Vessel.VesselStatus.VesselType).Append(' ')
                  .Append(t.Player.Name).Append(": ")
                  .Append(t.MaxSustained.ToString("0.#")).Append(" u/s, ")
                  .Append(t.Jumps).Append(" jumps; ")
                  .Append(t.Steering.IdlePercent.ToString("0.#")).Append("% still, ")
                  .Append(t.Steering.MeanTurnJerk.ToString("0.#")).Append(" deg/s2, ")
                  .Append(t.Steering.Seconds.ToString("0")).Append('s')
                  .Append(t.SteerJudged ? "" : " (not judged)");
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, sb.ToString());
        }
    }

    /// <summary>
    /// How a nose moves over time (Threat Board T6): the share of samples held still and the mean change in turn
    /// rate. Pure, so the edit-mode tests can feed it shapes of steering directly.
    /// </summary>
    public struct SteeringProfile
    {
        Vector3 _prevForward;
        float _prevTurnRate;
        int _state;                 // 0 no forward yet, 1 forward, 2 forward and turn rate
        int _samples, _idle, _jerkSamples;
        float _jerkSum, _seconds;

        /// <summary>Starts a new run of samples from <paramref name="forward"/> (after a jump), keeping the totals.</summary>
        public void Restart(Vector3 forward)
        {
            _prevForward = forward;
            _state = 1;
        }

        /// <summary>One sample <paramref name="dt"/> seconds after the last; a turn rate under
        /// <paramref name="idleTurnRate"/> (degrees per second) counts as still.</summary>
        public void Add(Vector3 forward, float dt, float idleTurnRate)
        {
            if (_state == 0 || dt <= 0f) { Restart(forward); return; }
            float turnRate = Vector3.Angle(_prevForward, forward) / dt;
            _prevForward = forward;
            _samples++;
            _seconds += dt;
            if (turnRate < idleTurnRate) _idle++;
            if (_state == 2)
            {
                _jerkSum += Mathf.Abs(turnRate - _prevTurnRate) / dt;
                _jerkSamples++;
            }
            _prevTurnRate = turnRate;
            _state = 2;
        }

        public int Samples => _samples;
        public float Seconds => _seconds;
        public float IdlePercent => _samples > 0 ? 100f * _idle / _samples : 0f;
        public float MeanTurnJerk => _jerkSamples > 0 ? _jerkSum / _jerkSamples : 0f;
    }
}
