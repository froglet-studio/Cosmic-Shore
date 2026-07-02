using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until a game mode's unlocked intensity reaches a tier — the quest gate the
    /// progression system already evaluates ("reach Intensity 4 to unlock the next mode").
    /// Listens to <see cref="GameModeProgressionService.OnIntensityUnlocked"/> and also
    /// checks the current state up-front, so an already-met gate passes straight through.
    /// </summary>
    public class QuestWaitForIntensityNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Waits until the mode's unlocked intensity reaches the target tier (the progression service's own gate — tier 4 completes a mode's quest). Passes immediately if already met.";
        public override string EditorSummary => $"{mode} ≥ tier {intensityTier}";

        [Tooltip("The game mode whose intensity gate this node waits on.")]
        public GameModes mode = GameModes.MultiplayerCrystalCapture;

        [Tooltip("The intensity tier that satisfies this gate (4 = the tier that completes the mode's quest).")]
        [Range(2, 4)] public int intensityTier = 4;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            var svc = GameModeProgressionService.Instance;
            if (svc == null)
            {
                Debug.LogError("[Quest] WaitForIntensityNode: GameModeProgressionService not alive — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            if (svc.GetMaxUnlockedIntensity(mode) >= intensityTier)
            {
                advance(QuestPorts.Next);
                yield break;
            }

            void OnUnlocked(GameModes unlockedMode, int tier)
            {
                if (unlockedMode == mode && tier >= intensityTier)
                    advance(QuestPorts.Next);
            }

            svc.OnIntensityUnlocked += OnUnlocked;
            ctx.AddCleanup(() => svc.OnIntensityUnlocked -= OnUnlocked);
        }
    }
}
