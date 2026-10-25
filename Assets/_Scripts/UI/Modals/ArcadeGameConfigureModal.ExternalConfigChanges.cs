using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    // Part of ArcadeGameConfigureModal (Docs/REFACTOR_BACKLOG.md M13): this region moved here verbatim from ArcadeGameConfigureModal.cs.
    public partial class ArcadeGameConfigureModal
    {
        #region Config change handlers (intensity, locked, external)

        void HandleLockedIntensitySelected(int intensity)
        {
            OnLockedIntensityClicked?.Invoke(intensity);

            if (_selectedGame == null) return;

            // The refusal: the Denied sting, the node shakes once (a no-op on a button without a
            // KitShake), and the toast says what earns the level.
            if (audioSystem) audioSystem.PlayMenuAudio(MenuAudioCategory.Denied);
            var row = ActiveIntensityButtons;
            if (intensity >= 1 && intensity <= row.Count && row[intensity - 1])
                KitShake.Play(row[intensity - 1].gameObject);

            ToastNotificationAPI.Show(LockedIntensityGoal(_selectedGame, intensity), ToastKind.Goal);
        }

        /// <summary>
        /// What earns <paramref name="level"/> on <paramref name="game"/> - one answer for the node's
        /// goal line and the refusal toast, so the two can never disagree. Levels 3 and 4 carry their
        /// quest's own copy; a level the quest graph is holding back (the first-flight funnel) says so;
        /// anything else is the plain "play the level below" rule the defaults spell out.
        /// </summary>
        string LockedIntensityGoal(SO_ArcadeGame game, int level)
        {
            if (game && QuestArcadeConstraints.IsIntensityBlocked(game.Mode, level))
                return "Not open on this mission yet.";

            var service = GameModeProgressionService.Instance;
            var quest = service != null && game ? service.GetQuestForMode(game.Mode) : null;
            string authored = quest == null ? null
                : level == 3 ? quest.Intensity3GoalDescription
                : level == 4 ? quest.Intensity4GoalDescription
                : null;
            return string.IsNullOrEmpty(authored) ? $"Play Intensity {level - 1} to unlock" : authored;
        }

        #endregion
    }
}
