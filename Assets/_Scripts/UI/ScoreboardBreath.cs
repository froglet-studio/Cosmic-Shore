using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The breath between rounds on the scoreboard (P5, Loop Atlas loop <c>breath</c>): ONE offered next step beside
    /// Play again - the next rung after a win, a crossing to the arena game the round trained for, or today's Tide job
    /// (<see cref="BreathRules"/>). A copy of the Play again button made in code, the way the voyage's pause action copies
    /// Resume, so no prefab carries a second button to keep in step. Host and solo only: a guest's buttons are votes.
    /// Words and destinations are authored on <c>Assets/Resources/BreathConfig.asset</c>.
    /// </summary>
    public sealed class ScoreboardBreath : MonoBehaviour
    {
        /// <summary>Today's Tide job, when one is waiting: its name and how to start it. Set by the Tide (era.tide); null
        /// when no job service runs.</summary>
        public static System.Func<(bool Waiting, string Name, System.Action Start)> DailyJob;

        /// <summary>Results seen this sitting (the app's run): the breath crosses only after a few rounds of one card.</summary>
        public static int RoundsThisSitting { get; private set; }

        GameObject _button;
        TMP_Text _label;
        System.Action _go;

        /// <summary>The breath under <paramref name="playAgain"/>, made once and reused.</summary>
        public static ScoreboardBreath Ensure(GameObject playAgain)
        {
            if (!playAgain || !playAgain.transform.parent) return null;
            var host = playAgain.transform.parent;
            var existing = host.GetComponentInChildren<ScoreboardBreath>(true);
            if (existing) return existing;

            var copy = Instantiate(playAgain, host);
            copy.name = "BreathButton";
            copy.transform.SetSiblingIndex(playAgain.transform.GetSiblingIndex() + 1);
            // The copy is an offer, not a vote: the rematch faces stay under Play again.
            foreach (var roster in copy.GetComponentsInChildren<RematchVoteRoster>(true)) Destroy(roster);
            var breath = copy.AddComponent<ScoreboardBreath>();
            breath._button = copy;
            breath._label = copy.GetComponentInChildren<TMP_Text>(true);
            if (copy.TryGetComponent(out Button button))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(breath.Press);
            }
            copy.SetActive(false);
            return breath;
        }

        /// <summary>Choose and show the offer for the result on screen; hide it when there is none.</summary>
        public void Show(GameDataSO gameData, bool won)
        {
            RoundsThisSitting++;
            _go = null;
            var config = BreathConfigSO.Load();
            if (!config || gameData == null) { Hide(); return; }

            var card = config.Card(gameData.GameMode);
            int intensity = gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1;
            var progression = GameModeProgressionService.Instance;
            int highest = card ? card.MaxIntensity : intensity;
            if (progression) highest = Mathf.Min(highest, progression.GetMaxUnlockedIntensity(gameData.GameMode));

            var crossing = CrossingFor(config, gameData);
            bool crossingOpen = crossing && (!progression || progression.IsGameModeUnlocked(crossing.Mode));
            var job = DailyJob?.Invoke() ?? default;

            var step = BreathRules.Choose(won, intensity, highest, crossingOpen, RoundsThisSitting,
                config.RoundsBeforeCrossing, job.Waiting && job.Start != null);
            string line = null;
            switch (step)
            {
                case BreathStep.NextRung when card:
                    int next = intensity + 1;
                    line = TemplateText.Fill(config.NextRungLine, "intensity", next.ToString());
                    _go = () => Launch(gameData, card, next);
                    break;
                case BreathStep.Crossing:
                    int rung = Mathf.Clamp(SO_ArcadeGame.ArenaIntensity, crossing.MinIntensity, crossing.MaxIntensity);
                    line = TemplateText.Fill(config.CrossingLine, "game", crossing.DisplayName);
                    _go = () => Launch(gameData, crossing, rung);
                    break;
                case BreathStep.DailyJob:
                    line = TemplateText.Fill(config.DailyJobLine, "job", job.Name);
                    _go = job.Start;
                    break;
            }

            if (string.IsNullOrEmpty(line) || _go == null) { Hide(); return; }
            if (_label) _label.text = line;
            if (_button) _button.SetActive(true);
        }

        public void Hide()
        {
            _go = null;
            if (_button) _button.SetActive(false);
        }

        void Press()
        {
            var go = _go;
            Hide();
            go?.Invoke();
        }

        /// <summary>The arena card this round's element trains for, unless the round already is that card.</summary>
        static SO_ArcadeGame CrossingFor(BreathConfigSO config, GameDataSO gameData)
        {
            var metric = gameData.ScoringRule != null ? gameData.ScoringRule.Metric : (ScoringMetric?)null;
            if (!ModeGenre.TryElementsFor(gameData.GameMode, metric, out var primary, out _)) return null;
            var arena = BreathRules.ArenaFor(primary);
            if (arena == GameModes.Random || arena == gameData.GameMode || gameData.IsArenaMatch) return null;
            var card = config.Card(arena);
            return card && card.ArenaRules ? card : null;
        }

        /// <summary>Launch <paramref name="card"/> at <paramref name="intensity"/> from the game scene, the way the
        /// Maelstrom launches its next game: intensity first, then the card's identity, then the load.</summary>
        static void Launch(GameDataSO gameData, SO_ArcadeGame card, int intensity)
        {
            if (gameData.SelectedIntensity != null) gameData.SelectedIntensity.Value = intensity;
            gameData.SyncFromArcadeGame(card);
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Breath] {card.DisplayName} at intensity {intensity}.");
            gameData.InvokeGameLaunch();
        }
    }
}
