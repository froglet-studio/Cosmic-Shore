using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Build-time safety net for End Game Conditions: when a build starts it warns if the LIVE
    /// end-game counts (what the game uses at runtime) differ from the recorded BUILD values
    /// (Tools &gt; Cosmic Shore &gt; End Game Conditions). Lowering a count to end a mode quickly while
    /// testing is common — this catches the case where someone forgets to restore the build values
    /// before shipping. Non-blocking by design: it logs a warning, it does not fail the build.
    /// See the <c>/EndGameConditions</c> skill.
    /// </summary>
    public class EndConditionBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var cfg = Resources.Load<EndConditionOverridesSO>(EndConditionOverridesSO.ResourcePath);
            if (cfg == null || cfg.LiveMatchesBuild) return;

            Debug.LogWarning(
                "[EndConditionOverrides] Building with TEST end-game counts — Live values differ from Build values:\n" +
                cfg.DescribeDrift() +
                "Restore them via Tools > Cosmic Shore > End Game Conditions " +
                "(\"Restore build values → live\") if this is unintended.");
        }
    }
}
