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
                    _tracks[vessel] = track;
                }
                track.Player = player;
                track.Vessel = vessel;
                track.Owner = owner;
                track.Judged = owner != NetworkManager.ServerClientId;
                track.Seen = true;
                Sample(track, vessel.Transform.position, now);
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

        void Sample(Track t, Vector3 position, float now)
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
                    CheckJumpRate(t, now);
                }
            }

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
            var sb = new StringBuilder("[FairPlay] movement this turn (fastest sustained path speed, jumps):");
            foreach (var t in _tracks.Values)
            {
                sb.Append("\n  ").Append(t.Judged ? "guest " : "AI    ")
                  .Append(t.Vessel.VesselStatus.VesselType).Append(' ')
                  .Append(t.Player.Name).Append(": ")
                  .Append(t.MaxSustained.ToString("0.#")).Append(" u/s, ")
                  .Append(t.Jumps).Append(" jumps");
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, sb.ToString());
        }
    }
}
