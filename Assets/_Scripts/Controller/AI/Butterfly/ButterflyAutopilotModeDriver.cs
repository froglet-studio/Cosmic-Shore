using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puts an AUTOPILOTED Butterfly into Mass mode or Dust mode, through the same replicated
    /// press a human's right trigger travels. Shared by the three Butterfly element games
    /// (Dustup, Tapestry, Sirocco), because every one of them is decided by the mode switch.
    ///
    /// <para><b>Why a mode needs this at all.</b> <see cref="AIPilot"/> writes a stick and a
    /// throttle and nothing else, and a Butterfly SPAWNS in Mass mode
    /// (<see cref="SpreadWingsActionExecutor.Initialize"/>). So without a driver every AI Butterfly
    /// flies the whole match with its dust switched off - which in Dustup and Sirocco is an
    /// opponent that cannot score at all, and an all-AI domain that cannot play is a defect (the
    /// Tollway rule).</para>
    ///
    /// <para><b>Replicated, never local.</b> An AI is simulated on the server only, and the mode
    /// is simulated on EVERY peer (the wake is conserved mass every peer lays, the dust a skimmer
    /// every peer observes), so a local press would switch the mode on one machine. It goes
    /// through <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/>, found
    /// by capability (<see cref="R_VesselActionHandler.TryGetBoundAction{T}"/>) so no control is
    /// named here.</para>
    ///
    /// <para><b>It reads the mode back rather than remembering what it asked for.</b> A press is
    /// a TOGGLE, so a driver that counted its own presses would drift the first time one was lost
    /// or doubled; asking <see cref="SpreadWingsActionExecutor.IsDustMode"/> on the server's own
    /// replica cannot. A press in flight is latched for <see cref="RetrySeconds"/> so a round trip
    /// is not answered with a second toggle that undoes the first.</para>
    ///
    /// <para><b>Both input styles.</b> <c>Toggle</c> (shipped) is a press-and-release per switch;
    /// <c>HoldForDust</c> holds the control for as long as Dust is wanted, and releases it for
    /// Mass. <see cref="ReleaseAll"/> lets go of anything still held - call it when the match
    /// ends or the controller despawns, because a release is an input event and nothing else
    /// would send one.</para>
    /// </summary>
    public sealed class ButterflyAutopilotModeDriver
    {
        public float RetrySeconds { get; set; } = 1f;

        readonly Dictionary<IPlayer, float> _pendingUntil = new();
        readonly Dictionary<IPlayer, SpreadWingsActionExecutor> _executors = new();
        readonly HashSet<IPlayer> _held = new();

        /// <summary>
        /// SERVER. Moves <paramref name="pilot"/>'s Butterfly toward the wanted mode. Returns true
        /// once it is there. Safe to call every frame; a pilot that is not a Butterfly, or has no
        /// bound mode switch, returns false and is left alone.
        /// </summary>
        public bool Drive(IPlayer pilot, bool wantDust)
        {
            var status = pilot?.Vessel?.VesselStatus;
            var handler = status?.ActionHandler;
            if (handler == null) return false;

            var executor = ResolveExecutor(pilot, status);
            if (executor == null) return false;
            if (!handler.TryGetBoundAction<SpreadWingsActionSO>(out var so, out var input) || so == null)
                return false;

            bool hold = so.InputStyle == SpreadWingsActionSO.ModeInputStyle.HoldForDust;

            if (executor.IsDustMode == wantDust)
            {
                _pendingUntil.Remove(pilot);
                return true;
            }

            if (_pendingUntil.TryGetValue(pilot, out float until) && Time.time < until) return false;
            _pendingUntil[pilot] = Time.time + Mathf.Max(0.1f, RetrySeconds);

            if (hold)
            {
                if (wantDust)
                {
                    handler.PerformShipControllerActionsReplicated(input);
                    _held.Add(pilot);
                }
                else
                {
                    handler.StopShipControllerActionsReplicated(input);
                    _held.Remove(pilot);
                }
            }
            else
            {
                handler.PerformShipControllerActionsReplicated(input);
                handler.StopShipControllerActionsReplicated(input);
            }
            return false;
        }

        /// <summary>True when the pilot's Butterfly is in Dust mode on this machine.</summary>
        public bool IsDust(IPlayer pilot)
        {
            var status = pilot?.Vessel?.VesselStatus;
            var executor = status != null ? ResolveExecutor(pilot, status) : null;
            return executor != null && executor.IsDustMode;
        }

        /// <summary>Lets go of every held control and forgets every pilot.</summary>
        public void ReleaseAll()
        {
            foreach (var pilot in _held)
            {
                var handler = pilot?.Vessel?.VesselStatus?.ActionHandler;
                if (handler == null) continue;
                if (handler.TryGetBoundAction<SpreadWingsActionSO>(out _, out var input))
                    handler.StopShipControllerActionsReplicated(input);
            }
            _held.Clear();
            _pendingUntil.Clear();
            _executors.Clear();
        }

        SpreadWingsActionExecutor ResolveExecutor(IPlayer pilot, IVesselStatus status)
        {
            // Cached per PILOT, and re-resolved whenever the cached one dies: a vessel swap hands
            // a pilot a different hull, and a destroyed component still passes a reference test
            // (Unity's == is what catches it).
            if (_executors.TryGetValue(pilot, out var cached) && cached != null) return cached;

            var root = status.Transform;
            var found = root != null ? root.GetComponentInChildren<SpreadWingsActionExecutor>(true) : null;
            if (found != null) _executors[pilot] = found;
            else _executors.Remove(pilot);
            return found;
        }
    }
}
