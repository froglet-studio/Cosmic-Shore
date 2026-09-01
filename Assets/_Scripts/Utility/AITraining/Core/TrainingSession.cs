using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// "Is an AI-training run driving this play session right now?"
    ///
    /// Deliberately its own flag rather than <c>GameDataSO.IsTraining</c>, which
    /// is a DIFFERENT question wearing the same word: that one means the LEGACY
    /// single-player *training game* (the campaign practice modes) and is set to
    /// true by <c>Arcade.LaunchTrainingGame</c> for ordinary arcade launches. A
    /// deployment gate reading it would have stood down in exactly the sessions
    /// the player opened to fly against the trained AI.
    ///
    /// Static because the two readers live on opposite sides of the session — the
    /// launcher sets it as play mode begins, the deployment service reads it as
    /// each AI vessel pairs — and neither has a reference to the other. Reset at
    /// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> because a
    /// static survives play-mode exit in the editor and a leaked <c>true</c> would
    /// silently disable deployment for the rest of the editor session.
    /// </summary>
    public static class TrainingSession
    {
        public static bool IsActive { get; private set; }

        public static void Begin() => IsActive = true;
        public static void End() => IsActive = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => IsActive = false;
    }
}
