using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Before a build, when <see cref="EndConditionOverridesSO.autoRestoreBuildValuesBeforeBuild"/>
    /// is on, copies the Build baseline onto the Live end-game counts and saves the asset - so a
    /// test configuration is never shipped. No warning, no block: it just restores. Turn the toggle
    /// off in Tools &gt; Cosmic Shore &gt; End Game Conditions to build with the current Live values.
    /// See the <c>/EndGameConditions</c> skill.
    /// </summary>
    public class EndConditionBuildRestore : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var cfg = Resources.Load<EndConditionOverridesSO>(EndConditionOverridesSO.ResourcePath);
            if (cfg == null || !cfg.autoRestoreBuildValuesBeforeBuild || cfg.LiveMatchesBuild) return;

            cfg.ApplyBuildValues();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[EndConditionOverrides] Auto-restored Live end-game counts to the Build baseline for this build " +
                $"(SkimRace {cfg.hexRaceCrystalCount}, Crystal Capture {cfg.crystalCaptureCrystalCount}, " +
                $"Joust {cfg.joustCount}, Maelstrom {cfg.maelstromWinTarget}).");
        }
    }
}
