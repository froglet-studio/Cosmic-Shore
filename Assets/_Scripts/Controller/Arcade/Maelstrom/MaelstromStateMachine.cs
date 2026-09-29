using System;
using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Coarse phase of a tournament session.
    /// </summary>
    public enum MaelstromPhase
    {
        /// <summary>No tournament running.</summary>
        Idle = 0,

        /// <summary>
        /// Maelstrom scene shown as the intro lobby (before the first game) OR the between-round hub
        /// (after a game, when the shuffle isn't decided) - both use the active layout with the
        /// ready-up. The summary is its own phase (<see cref="Summary"/>).
        /// </summary>
        Lobby = 1,

        /// <summary>A minigame in the lineup is loaded / being played.</summary>
        InGame = 2,

        /// <summary>The final game has finished; its scoreboard is up (Continue → Summary).</summary>
        Complete = 3,

        /// <summary>The Maelstrom results scene is up after the last game (Play Again / Main Menu).</summary>
        Summary = 4,
    }

    /// <summary>
    /// Tiny table-driven phase tracker for a tournament session, modelled on
    /// <see cref="CosmicShore.Core.ApplicationStateMachine"/> but local to the tournament.
    /// Owned by <see cref="MaelstromController"/>; invalid transitions are ignored (logged
    /// by the controller). Each peer runs its own instance and they stay in lock-step because
    /// they are driven by the same deterministic signals (scene loads + synced results).
    /// </summary>
    public class MaelstromStateMachine
    {
        static readonly Dictionary<MaelstromPhase, HashSet<MaelstromPhase>> Valid = new()
        {
            [MaelstromPhase.Idle]     = new HashSet<MaelstromPhase> { MaelstromPhase.Lobby },
            // Lobby → Complete: the shuffle can be found decided at a Maelstrom (hub) load even if the
            // deciding game's end did not land the InGame → Complete transition (see EnterSummary). This
            // lets the authoritative IsShuffleComplete check route to the summary from the hub phase too.
            [MaelstromPhase.Lobby]    = new HashSet<MaelstromPhase> { MaelstromPhase.InGame, MaelstromPhase.Idle, MaelstromPhase.Complete },
            [MaelstromPhase.InGame]   = new HashSet<MaelstromPhase> { MaelstromPhase.InGame, MaelstromPhase.Complete, MaelstromPhase.Idle, MaelstromPhase.Lobby },
            [MaelstromPhase.Complete] = new HashSet<MaelstromPhase> { MaelstromPhase.Summary, MaelstromPhase.Idle },
            [MaelstromPhase.Summary]  = new HashSet<MaelstromPhase> { MaelstromPhase.InGame, MaelstromPhase.Idle },
        };

        public MaelstromPhase Current { get; private set; } = MaelstromPhase.Idle;

        /// <summary>Raised after a successful transition with the new phase.</summary>
        public event Action<MaelstromPhase> OnPhaseChanged;

        /// <summary>
        /// Attempts a transition. Returns true if it was valid and applied. A no-op
        /// (already in <paramref name="next"/>) returns false without raising.
        /// </summary>
        public bool TransitionTo(MaelstromPhase next)
        {
            if (next == Current) return false;
            if (!Valid.TryGetValue(Current, out var allowed) || !allowed.Contains(next))
                return false;

            Current = next;
            OnPhaseChanged?.Invoke(next);
            return true;
        }

        /// <summary>Force back to <see cref="MaelstromPhase.Idle"/> (exit / abort).</summary>
        public void ResetToIdle()
        {
            if (Current == MaelstromPhase.Idle) return;
            Current = MaelstromPhase.Idle;
            OnPhaseChanged?.Invoke(MaelstromPhase.Idle);
        }
    }
}
