using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Self-contained "the player finished a game" recorder for quest gates.
    ///
    /// The quest runner lives in Menu_Main while <c>GameDataSO.OnMiniGameEnd</c> is raised in
    /// the GAME scene — by the time the menu reloads and a WaitForGamePlayed gate resumes, the
    /// event is long gone. This static recorder subscribes once per play session (it needs no
    /// scene object, so it can never be missing like a scene service) and stamps the highest
    /// intensity finished per mode into PlayerPrefs. Gates consult <see cref="HasPlayed"/> on
    /// resume; the quest editor's progress reset clears the records.
    /// </summary>
    public static class QuestPlayRecorder
    {
        static string Key(GameModes mode) => $"QUEST_PLAYED_{mode}";

        static GameDataSO[] _subscribed = System.Array.Empty<GameDataSO>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            // Idempotent (-= then +=) so disabled domain reloads can't double-subscribe.
            foreach (var gameData in _subscribed)
                if (gameData != null && gameData.OnMiniGameEnd != null)
                    gameData.OnMiniGameEnd.OnRaised -= HandleGameEnd;

            _subscribed = Resources.FindObjectsOfTypeAll<GameDataSO>();
            foreach (var gameData in _subscribed)
            {
                if (gameData == null || gameData.OnMiniGameEnd == null) continue;
                gameData.OnMiniGameEnd.OnRaised -= HandleGameEnd;
                gameData.OnMiniGameEnd.OnRaised += HandleGameEnd;
            }
        }

        static void HandleGameEnd()
        {
            foreach (var gameData in _subscribed)
            {
                if (gameData == null) continue;

                var mode = gameData.GameMode;
                if (mode == GameModes.Random) continue;

                int intensity = gameData.SelectedIntensity != null ? Mathf.Max(1, gameData.SelectedIntensity.Value) : 1;
                int best = PlayerPrefs.GetInt(Key(mode), 0);
                if (intensity > best)
                {
                    PlayerPrefs.SetInt(Key(mode), intensity);
                    PlayerPrefs.Save();
                }

                Debug.Log($"[Quest] Play recorded: {mode} @ intensity {intensity} (best {Mathf.Max(best, intensity)}).");
                return; // all GameDataSO assets mirror the same session — one record is enough
            }
        }

        /// <summary>True if a game of this mode was finished at ≥ the given intensity (0/1 = any).</summary>
        public static bool HasPlayed(GameModes mode, int minIntensity) =>
            PlayerPrefs.GetInt(Key(mode), 0) >= Mathf.Max(1, minIntensity);

        /// <summary>Clear every recorded play (quest progress reset).</summary>
        public static void ResetAll()
        {
            foreach (GameModes mode in System.Enum.GetValues(typeof(GameModes)))
                PlayerPrefs.DeleteKey(Key(mode));
            PlayerPrefs.Save();
        }
    }
}
