using System.Collections;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Applies (or clears) <see cref="QuestArcadeConstraints"/> — the quest-graph funnel over
    /// the arcade UI: only one game card clickable, one intensity selectable, and player/domain
    /// steppers defaulted for the best first-game experience. The constraints are static so they
    /// survive the Menu → game → Menu scene round-trip; author an explicit Clear node once the
    /// funnel is over. Advances immediately.
    /// </summary>
    public class QuestSetArcadeConstraintsNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gameplay;
        public override string TypeTooltip =>
            "Funnels the arcade UI: locks every game card except Allowed Mode, disables every intensity except Forced Intensity, and defaults the configure modal's player count (max) and domain count. Survives scene reloads — pair with a Clear node when the funnel ends.";
        public override string EditorSummary => clearConstraints
            ? "Clear all constraints"
            : $"Only {allowedMode}"
              + (forcedIntensity > 0 ? $" @ intensity {forcedIntensity}" : string.Empty)
              + (forcedPlayerCount > 0 ? $" · {forcedPlayerCount} players" : string.Empty)
              + (forcedDomainCount > 0 ? $" · {forcedDomainCount} domains" : string.Empty);

        [Tooltip("Clear every constraint instead of applying (end of the funnel).")]
        public bool clearConstraints;

        [Tooltip("The one game card left clickable. Random = no card restriction.")]
        public GameModes allowedMode = GameModes.MultiplayerCrystalCapture;

        [Tooltip("The only selectable intensity in the configure modal (0 = no restriction).")]
        [Range(0, 4)] public int forcedIntensity = 1;

        [Tooltip("Exact player count the configure modal defaults to (0 = no override).")]
        [Range(0, 12)] public int forcedPlayerCount = 2;

        [Tooltip("Default the configure modal's domain (team) count (0 = no override).")]
        [Range(0, 3)] public int forcedDomainCount = 2;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            // Apply/Clear raise QuestArcadeConstraints.OnChanged — the arcade card grid
            // subscribes and refreshes its lock state immediately, even if already open.
            if (clearConstraints)
                QuestArcadeConstraints.Clear();
            else
                QuestArcadeConstraints.Apply(allowedMode, forcedIntensity, forcedPlayerCount, forcedDomainCount);

            Debug.Log($"[Quest] Arcade constraints {(clearConstraints ? "cleared" : $"applied: {EditorSummary}")}.");
            advance(QuestPorts.Next);
            yield break;
        }
    }
}
