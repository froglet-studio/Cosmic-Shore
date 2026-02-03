using System;
using System.Threading;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game.Arcade
{
    public abstract class TurnMonitor : NetworkBehaviour
    {
        [Header("Timing")]
        [SerializeField] protected float _updateInterval = 1f;
        
        [FormerlySerializedAs("miniGameData")] [SerializeField]
        protected GameDataSO gameData;
        
        [Header("UI/Event")]
        [SerializeField] 
        protected ScriptableEventString onUpdateTurnMonitorDisplay;

        bool isRunning;

        CancellationTokenSource _cts;
        
        // ---- Public API ---------------------------------------------------

        /// <summary>Starts the turn monitor loop (no-op if already running).</summary>
        public virtual void StartMonitor()
        {
            if (isRunning) return;

            _cts = new CancellationTokenSource();
            isRunning = true;
            _ = RunLoopAsync(_cts.Token);
        }

        private void Update()
        {
            if (!isRunning) 
                return;
            if (!CheckForEndOfTurn()) return; // <-- REMOVE the 'return' on the next line
            OnTurnEnded();
            StopMonitor(); // Stop the async loop
            // // End-of-turn check
            // if (CheckForEndOfTurn())  // <-- REMOVE the 'return' on the next line
            // {
            //     OnTurnEnded();
            //     StopMonitor(); // Stop the async loop
            // }
            // Pause(); // exits loop on next iteration
        }

        /// <summary>Stops the monitor loop (safe to call multiple times).</summary>
        public void StopMonitor()
        {
            if (!isRunning) return;

            try { _cts?.Cancel(); }
            catch { /* ignore */ }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                isRunning = false;
            }
        }

        /// <summary>Pauses periodic updates until Resume() is called.</summary>
        public void Pause()  => isRunning = false;

        /// <summary>Resumes periodic updates if paused.</summary>
        public void Resume() => isRunning = true;

        /// <summary>
        /// Stops, clears state, and (optionally) restarts.
        /// Override ResetState() to clear subclass data.
        /// </summary>
        public void ResetMonitor(bool restart = false)
        {
            StopMonitor();
            ResetState();
            if (restart) StartMonitor();
        }

        // ---- Subclass contracts ------------------------------------------

        /// <summary>Return true when the turn should end.</summary>
        public abstract bool CheckForEndOfTurn();

        /// <summary>Called periodically according to UpdateInterval (while not paused).</summary>
        protected virtual void RestrictedUpdate() {}

        /// <summary>Called exactly once when CheckForEndOfTurn becomes true.</summary>
        protected virtual void OnTurnEnded() { }

        /// <summary>Override to reset any subclass counters/state on ResetMonitor.</summary>
        protected virtual void ResetState() { }

        // ---- Internal loop -----------------------------------------------

        async UniTaskVoid RunLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && isRunning)
                {
                    // Pause gate
                    if (!isRunning)
                    {
                        // Wait until unpaused or cancelled
                        await UniTask.WaitUntil(() => isRunning, cancellationToken: token);
                        if (token.IsCancellationRequested) break;
                    }

                    // One tick
                    RestrictedUpdate();
                    
                    // Wait for next tick (game-time)
                    if (_updateInterval > 0f)
                        await UniTask.WaitForSeconds(_updateInterval, cancellationToken: token);
                        /*await UniTask.Delay(TimeSpan.FromSeconds(_updateInterval),
                                            DelayType.DeltaTime,
                                            PlayerLoopTiming.Update,
                                            token);*/
                    else
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on StopMonitor
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
            finally
            {
                // Guarantee flags are coherent if someone cancelled externally
                if (token.IsCancellationRequested)
                    isRunning = false;
            }
        }
    }
}
