namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Keeps the AI seats' track-planner re-plans (<see cref="SkimRaceDriver"/>'s tracking MPC, ~4 ms
    /// per seat on editor Mono) out of each other's frames. Each seat re-plans at
    /// <see cref="SkimRaceAIConfigSO.TrackMpcHz"/> on its own clock. Once two seats land in the same
    /// frame they stay there, because both then schedule their next re-plan from the same frame time.
    /// Measured 2026-10-07: they shared a frame about half the time, and the spike frame paid both,
    /// 8.1 ms (<c>Docs/SKIM_RACE_AI.md</c> §8.0h).
    ///
    /// <para>One re-plan claims a frame. A seat that finds its frame taken flies its previous plan
    /// for ONE more frame; the driver never makes it wait twice. That one-frame shift is enough: the
    /// seat then schedules from its own frame and stays out of the other's. The re-plan rate and
    /// the average cost are unchanged; only the peak frame halves.</para>
    ///
    /// <para>Shared by every seat in one process (<see cref="SkimRacePilot"/> owns the game's). The
    /// owner marks each frame with <see cref="BeginFrame"/>. Any seat may call it, and only a new
    /// frame number resets the claim.</para>
    /// </summary>
    public sealed class SkimRaceReplanGate
    {
        int _frame = int.MinValue;
        bool _claimed;

        /// <summary>Marks the frame in progress. Idempotent within a frame.</summary>
        public void BeginFrame(int frame)
        {
            if (frame == _frame) return;
            _frame = frame;
            _claimed = false;
        }

        /// <summary>Claims this frame's re-plan. False when another seat already holds it.</summary>
        public bool TryClaim()
        {
            if (_claimed) return false;
            _claimed = true;
            return true;
        }
    }
}
