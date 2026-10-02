using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Make this account a first-timer for the microgame drill again: the Lesson keys and the
    /// seen tips are forgotten, so the next Lesson is forced and the first-login railroad
    /// (Docs/ModePreview/TRAINING_PLAN.md §10.5) has a Lesson to wait for.
    ///
    /// <para>Run it in PLAY MODE for a full reset. The store merges this machine's copy with the
    /// cloud copy, and the cloud copy is only reachable once the game has signed in; in edit mode
    /// only the local copy is cleared, which is enough while the progression backend is off.
    /// Quest progress is separate - reset that in the Quest Graph Editor.</para>
    /// </summary>
    public static class DrillProgressResetMenu
    {
        [MenuItem("FrogletTools/Quest Graph/Reset Microgame Lessons (testing)")]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 2,
            Description = "Forget the account's microgame Lesson keys and seen tips, so the Lesson is " +
                          "forced again. Run in play mode to clear the cloud copy too.")]
        public static void Reset()
        {
            DrillProgressStore.ResetForTesting();
            DrillResume.Clear();
            Debug.Log(Application.isPlaying
                ? "[Drill] Lesson keys, seen tips and resume marks cleared (local and, if loaded, cloud)."
                : "[Drill] Lesson keys and seen tips cleared on this machine. Run again in play mode to " +
                  "clear the cloud copy, which is merged back in on sign-in.");
        }
    }
}
