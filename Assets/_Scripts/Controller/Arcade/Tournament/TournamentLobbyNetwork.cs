using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Host-authoritative ready-up + countdown for the Maelstrom lobby / between-round hub. A
    /// scene-placed <see cref="NetworkBehaviour"/> - the Maelstrom scene loads through Netcode scene
    /// management (host-driven Single load), so this spawns on every peer.
    ///
    /// Behaviour (per the design):
    ///   • On entering the lobby/hub the round auto-starts after <see cref="autoStartSeconds"/> (30s).
    ///   • Once EVERY connected human client has tapped Ready, the deadline snaps in to
    ///     <see cref="allReadySeconds"/> (5s) from now.
    ///   • The deadline + ready tally are synced (<see cref="NetworkVariable{T}"/>) so every peer can
    ///     render the countdown - e.g. inside the Ready button - and the "x / y ready" line.
    ///   • When the deadline elapses the host draws + loads the next random game via
    ///     <see cref="TournamentController.BeginNextRound"/>.
    ///
    /// Arms and advances ONLY in <see cref="TournamentPhase.Lobby"/> (the intro lobby OR the
    /// between-round hub). It stays completely idle on the summary screen (phase Summary), which shares
    /// the same scene. <c>TournamentSceneView</c> reads the public state to drive the UI and calls
    /// <see cref="ToggleLocalReady"/> from the Ready button.
    /// </summary>
    public class TournamentLobbyNetwork : NetworkBehaviour
    {
        [Header("Countdown (seconds)")]
        [Tooltip("Auto-start delay when NOT everyone is ready.")]
        [SerializeField, Min(1f)] float autoStartSeconds = 30f;

        [Tooltip("Start delay once EVERY connected player has readied (snapped in from the auto-start delay).")]
        [SerializeField, Min(1f)] float allReadySeconds = 5f;

        // ServerTime (seconds) at which the round starts; 0 = not armed yet. Server-write, read by all.
        readonly NetworkVariable<double> _deadline = new(0d);

        // Ready tally, synced so clients can render "x / y ready".
        readonly NetworkVariable<int> _readyCount = new(0);
        readonly NetworkVariable<int> _totalPlayers = new(0);

        // Server-only: client ids that have readied up.
        readonly HashSet<ulong> _ready = new();

        bool _armed;       // server: countdown armed for this lobby instance
        bool _started;     // server: BeginNextRound already fired (one-shot)
        bool _localReady;  // this peer's own ready state (drives the button visual)

        public bool LocalReady => _localReady;
        public int ReadyCount => _readyCount.Value;
        public int TotalPlayers => _totalPlayers.Value;

        /// <summary>True only while the lobby/hub countdown is live (phase Lobby). Summary is idle.</summary>
        public bool IsCountingDown =>
            TournamentController.Instance != null
            && TournamentController.Instance.Phase == TournamentPhase.Lobby;

        /// <summary>
        /// Whole seconds left before the round starts (ceil, never negative). Shows
        /// <see cref="autoStartSeconds"/> before the server has armed the countdown.
        /// </summary>
        public int SecondsRemaining
        {
            get
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || _deadline.Value <= 0d) return Mathf.CeilToInt(autoStartSeconds);
                double remaining = _deadline.Value - nm.ServerTime.Time;
                return Mathf.Max(0, Mathf.CeilToInt((float)remaining));
            }
        }

        // ── Ready (any peer) ─────────────────────────────────────────────────────

        public void ToggleLocalReady() => SetLocalReady(!_localReady);

        public void SetLocalReady(bool ready)
        {
            if (!IsSpawned) return;   // can't RPC before the scene object spawns
            if (_localReady == ready) return;
            _localReady = ready;
            SetReadyRpc(ready);
        }

        [Rpc(SendTo.Server)]
        void SetReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (ready) _ready.Add(sender);
            else _ready.Remove(sender);
            Recompute();
        }

        // ── Server countdown drive ───────────────────────────────────────────────

        void Update()
        {
            if (!IsServer) return;

            // Only the lobby/hub arms + advances; the summary screen stays idle. Gated on phase (not
            // OnNetworkSpawn) so it's robust to spawn-vs-sceneLoaded ordering - it self-arms on the
            // first frame the controller reports phase Lobby.
            var tc = TournamentController.Instance;
            if (tc == null || tc.Phase != TournamentPhase.Lobby) return;

            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            if (!_armed)
            {
                _ready.Clear();
                _deadline.Value = nm.ServerTime.Time + autoStartSeconds;
                _armed = true;
                Recompute();
            }

            if (!_started && nm.ServerTime.Time >= _deadline.Value)
            {
                _started = true;
                tc.BeginNextRound();
            }
        }

        // Server: refresh the ready tally and, once everyone's ready, pull the deadline in.
        void Recompute()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            var ids = nm.ConnectedClientsIds;
            _ready.RemoveWhere(id => !ids.Contains(id));

            _readyCount.Value = _ready.Count;
            _totalPlayers.Value = ids.Count;

            if (ids.Count > 0 && _ready.Count >= ids.Count)
            {
                double snap = nm.ServerTime.Time + allReadySeconds;
                if (snap < _deadline.Value) _deadline.Value = snap;
            }
        }
    }
}
