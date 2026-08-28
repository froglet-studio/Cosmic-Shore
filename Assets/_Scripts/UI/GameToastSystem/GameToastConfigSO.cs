using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// One entry in a <see cref="GameToastConfigSO"/>: the copy + presentation + (optional)
    /// idle-hint behaviour for a single <see cref="GameToastSituation"/>.
    /// </summary>
    [System.Serializable]
    public class GameToastDefinition
    {
        [Tooltip("The situation this toast responds to.")]
        public GameToastSituation situation;

        [Tooltip("Display template. Placeholders per situation:\n" +
                 "PlayerJoined/PlayerReady/PlayerDisconnected: {0}=player name\n" +
                 "Joust: {0}=scorer name, {1}=scorer points, {2}=target name, {3}=target points\n" +
                 "Overtake: {0}=overtaker, {1}=overtaken\n" +
                 "NewRaceLeader: {0}=leader name\n" +
                 "ComebackActivated: {0}=player name\n" +
                 "BroodWaveScored: {0}=domain name, {1}=brood sum, {2}=wave target\n" +
                 "Rich text (<b>, <i>, <color>) is supported.")]
        [TextArea]
        public string messageTemplate;

        [Header("Presentation")]
        [Tooltip("Tint the whole line with the primary subject's domain UI color. " +
                 "Ignored when Use Domain Colored Names is on (the line stays white and " +
                 "only the names carry domain colors).")]
        public bool tintWithDomainColor = true;

        [Tooltip("Wrap name arguments in <color>+<b> tags using each subject's domain color " +
                 "(two-tone lines like the joust toast).")]
        public bool useDomainColoredNames;

        [Range(0f, 1f)]
        [Tooltip("Alpha multiplier for this line (e.g. dim disconnect notices).")]
        public float alpha = 1f;

        [Header("Idle Hint")]
        [Tooltip("When on, this entry is a HINT: it is never raised by gameplay, instead it " +
                 "shows automatically after Idle Seconds pass during an active turn without " +
                 "the Reset On situation firing.")]
        public bool isIdleHint;

        [Tooltip("The situation whose activity resets this hint's idle timer (e.g. the Joust " +
                 "hint resets whenever a Joust toast fires).")]
        public GameToastSituation resetOnSituation = GameToastSituation.None;

        [Min(1f)]
        [Tooltip("Seconds of inactivity (since turn start or since the last Reset On " +
                 "situation) before the hint shows.")]
        public float idleSeconds = 60f;

        [Tooltip("Re-show the hint after every further Idle Seconds of inactivity, instead " +
                 "of only once per turn.")]
        public bool repeatWhileIdle = true;
    }

    /// <summary>
    /// A game mode's set of in-game toasts: which situations it announces and with what copy.
    /// Situations NOT authored here (and not in the library's shared config) never show -
    /// that is how a mode opts out (Scurry ships an empty list). Resolved through
    /// <see cref="GameToastLibrarySO"/> by the current <see cref="GameModes"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameToastConfig",
        menuName = "ScriptableObjects/UI/Game Toast Config")]
    public class GameToastConfigSO : ScriptableObject
    {
        [Tooltip("The game mode this set of toasts belongs to (ignored on the shared config).")]
        public GameModes gameMode;

        [Tooltip("The situations this mode announces, with their copy and presentation.")]
        public List<GameToastDefinition> toasts = new();

        public bool TryGetDefinition(GameToastSituation situation, out GameToastDefinition definition)
        {
            for (int i = 0, count = toasts.Count; i < count; i++)
            {
                var entry = toasts[i];
                if (entry != null && entry.situation == situation && !string.IsNullOrEmpty(entry.messageTemplate))
                {
                    definition = entry;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        /// <summary>Appends this config's idle-hint entries to <paramref name="results"/>.</summary>
        public void CollectIdleHints(List<GameToastDefinition> results)
        {
            for (int i = 0, count = toasts.Count; i < count; i++)
            {
                var entry = toasts[i];
                if (entry != null && entry.isIdleHint && !string.IsNullOrEmpty(entry.messageTemplate))
                    results.Add(entry);
            }
        }
    }
}
