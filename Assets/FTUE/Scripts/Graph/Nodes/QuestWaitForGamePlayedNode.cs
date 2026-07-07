using System.Collections;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until the player finishes a game — advances on <c>GameDataSO.OnMiniGameEnd</c>.
    /// Optional filters restrict which run counts: a specific mode ("played Crystal
    /// Capture") and/or a minimum selected intensity ("at intensity 3 or higher").
    /// </summary>
    public class QuestWaitForGamePlayedNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Waits until a game finishes (GameDataSO.OnMiniGameEnd). Optional filters: only a specific mode, and/or only runs at a minimum intensity.";
        public override string EditorSummary
        {
            get
            {
                string what = filterByMode ? expectedMode.ToString() : "any game";
                return minIntensity > 0 ? $"{what} @ intensity ≥ {minIntensity}" : what;
            }
        }

        [Tooltip("Only count runs of a specific game mode.")]
        public bool filterByMode;

        [Tooltip("The mode that must have been played (used when Filter By Mode is on).")]
        public GameModes expectedMode = GameModes.MultiplayerCrystalCapture;

        [Tooltip("Minimum selected intensity for the run to count (0 = any).")]
        [Range(0, 4)] public int minIntensity;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.GameData == null || ctx.GameData.OnMiniGameEnd == null)
            {
                Debug.LogError("[Quest] WaitForGamePlayedNode: GameData.OnMiniGameEnd not available — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            // Resume path: the game END happens in the game scene, but this runner lives in
            // Menu_Main — the OnMiniGameEnd raise is long gone by the time the menu reloads and
            // the quest resumes on this node. The progression service (DontDestroyOnLoad) heard
            // it and recorded the play, so consult its per-intensity play counts first.
            if (filterByMode && GameModeProgressionService.Instance != null)
            {
                var svc = GameModeProgressionService.Instance;
                for (int intensity = Mathf.Max(1, minIntensity); intensity <= 4; intensity++)
                {
                    if (svc.GetIntensityPlayCount(expectedMode, intensity) > 0)
                    {
                        Debug.Log($"[Quest] WaitForGamePlayedNode: {expectedMode} already played @ intensity {intensity} — advancing (resume).");
                        advance(QuestPorts.Next);
                        yield break;
                    }
                }
            }

            void OnEnd()
            {
                if (filterByMode && ctx.GameData.GameMode != expectedMode)
                    return;
                if (minIntensity > 0 && ctx.GameData.SelectedIntensity != null
                    && ctx.GameData.SelectedIntensity.Value < minIntensity)
                    return;

                advance(QuestPorts.Next);
            }

            ctx.GameData.OnMiniGameEnd.OnRaised += OnEnd;
            ctx.AddCleanup(() => ctx.GameData.OnMiniGameEnd.OnRaised -= OnEnd);
        }
    }
}
