using System.Collections.Generic;
using System.Linq;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Owns a list of TurnMonitor, checks for end-of-turn each frame, and
    /// fires InvokeGameTurnConditionsMet when any monitor triggers.
    ///
    /// Works in both singleplayer and multiplayer:
    ///   - Multiplayer: subscribes in OnNetworkSpawn / unsubscribes in OnNetworkDespawn
    ///   - Singleplayer: falls back to OnEnable / OnDisable (OnNetworkSpawn never fires)
    /// </summary>
    public class TurnMonitorController : NetworkBehaviour
    {
        [FormerlySerializedAs("miniGameData")] [SerializeField]
        GameDataSO gameData;

        [SerializeField]
        List<TurnMonitor> monitors;

        bool _isRunning;
        bool _subscribedViaNetwork;

        // ── Network lifecycle (multiplayer) ──────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _subscribedViaNetwork = true;
            SubscribeToEvents();
        }

        public override void OnNetworkDespawn()
        {
            if (_subscribedViaNetwork)
            {
                UnsubscribeFromEvents();
                _subscribedViaNetwork = false;
            }
            base.OnNetworkDespawn();
        }

        // ── MonoBehaviour lifecycle (singleplayer fallback) ──────────────

        void OnEnable()
        {
            // In multiplayer, OnNetworkSpawn handles subscription.
            // In singleplayer, OnNetworkSpawn never fires so we subscribe here.
            if (!_subscribedViaNetwork)
                SubscribeToEvents();
        }

        void OnDisable()
        {
            if (!_subscribedViaNetwork)
                UnsubscribeFromEvents();

            StopMonitors();
        }

        // ── Core loop ────────────────────────────────────────────────────

        void Update()
        {
            if (!_isRunning)
                return;

            if (!monitors.Any(m => m.CheckForEndOfTurn()))
                return;

            _isRunning = false;
            // The ENTIRE end-game pipeline (winner calc → score sync → reveal) runs
            // synchronously inside this one raise, and the latch above means it fires
            // exactly once — so a throwing subscriber must not go quiet. Catch, name the
            // culprit loudly, and let the downstream fail-open guards (EndGameSequencer /
            // Scoreboard) still surface an exit path.
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00CED1>[FLOW-10] [TurnMonitorController] End-of-turn condition met - raising OnMiniGameTurnEnd</color>");
            try
            {
                gameData.InvokeGameTurnConditionsMet();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[TurnMonitorController] A turn-end subscriber THREW — the stack above names the culprit. " +
                               "The game-end chain was cut short at that subscriber; later subscribers never ran.");
            }
        }

        // ── Event handlers ───────────────────────────────────────────────

        void SubscribeToEvents()
        {
            // Idempotent: in a networked scene BOTH OnEnable (single-player fallback,
            // fires before spawn) and OnNetworkSpawn call this. -= before += keeps
            // StartMonitors/StopMonitors single-subscribed so each monitor gets exactly
            // one StartMonitor/StopMonitor per turn (double Starts double-subscribed
            // the monitors' per-stats handlers).
            gameData.OnMiniGameTurnStarted.OnRaised -= StartMonitors;
            gameData.OnMiniGameTurnStarted.OnRaised += StartMonitors;
            gameData.OnMiniGameTurnEnd.OnRaised -= StopMonitors;
            gameData.OnMiniGameTurnEnd.OnRaised += StopMonitors;
        }

        void UnsubscribeFromEvents()
        {
            gameData.OnMiniGameTurnStarted.OnRaised -= StartMonitors;
            gameData.OnMiniGameTurnEnd.OnRaised -= StopMonitors;
        }

        void StartMonitors()
        {
            _isRunning = true;

            foreach (var m in monitors)
                m.StartMonitor();
        }

        void StopMonitors()
        {
            _isRunning = false;

            foreach (var m in monitors)
                m.StopMonitor();
        }
    }
}
