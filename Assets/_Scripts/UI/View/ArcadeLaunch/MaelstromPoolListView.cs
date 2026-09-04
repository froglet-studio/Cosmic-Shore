using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// "Which games can this run draw?" — the Maelstrom launch panel's answer, redrawn every time
    /// the intensity changes.
    ///
    /// <para>The ladder lives in the data (<see cref="MaelstromDataSO.IntensityTiers"/>), not
    /// here: this view asks the asset what intensity N unlocks and draws the whole roster with the
    /// rest greyed, so raising the intensity visibly fills the list in.</para>
    /// </summary>
    public class MaelstromPoolListView : MonoBehaviour
    {
        [Header("Rows")]
        [SerializeField, Tooltip("Container the rows are built under. Put a Vertical Layout Group " +
                                 "on it; this component writes no rects.")]
        RectTransform rowContainer;

        [SerializeField, Tooltip("Row prefab, one per mode in the roster.")]
        MaelstromPoolEntry rowPrefab;

        [Header("Source")]
        [SerializeField, Tooltip("The tournament asset holding the roster and the intensity " +
                                 "ladder. Required - the list has nothing to draw without it.")]
        MaelstromDataSO tournamentData;

        [Header("Header")]
        [SerializeField, Tooltip("Optional count line. {0} = games in the pool, {1} = roster size.")]
        TMP_Text summaryText;

        [SerializeField, Tooltip("Copy for the count line.")]
        string summaryFormat = "{0} of {1} games";

        readonly List<MaelstromPoolEntry> _rows = new();

        /// <summary>How many modes the current intensity can draw. 0 until <see cref="Show"/>.</summary>
        public int UnlockedCount { get; private set; }

        /// <summary>Draw the roster for an intensity, marking what it unlocks.</summary>
        public void Show(int intensity)
        {
            if (!tournamentData || tournamentData.GameQueue == null)
            {
                CSDebug.LogWarning("[ArcadeLaunch] MaelstromPoolListView has no MaelstromDataSO - " +
                                   "the pool list cannot be drawn.", this);
                Clear();
                return;
            }

            var unlocked = tournamentData.GamesForIntensity(intensity);
            UnlockedCount = unlocked.Count;

            int used = 0;
            foreach (var game in tournamentData.GameQueue)
            {
                if (!game) continue;

                var row = RowAt(used);
                if (!row) break;

                row.Bind(game, tournamentData.UnlockIntensityOf(game), unlocked.Contains(game));
                used++;
            }

            for (int i = used; i < _rows.Count; i++)
                if (_rows[i]) _rows[i].gameObject.SetActive(false);

            if (summaryText)
                summaryText.text = string.Format(summaryFormat, UnlockedCount, used);

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ArcadeLaunch] Maelstrom pool at intensity {intensity}: {UnlockedCount}/{used} modes.");
        }

        /// <summary>Take every row down.</summary>
        public void Clear()
        {
            UnlockedCount = 0;
            foreach (var row in _rows)
                if (row) row.gameObject.SetActive(false);
            if (summaryText) summaryText.text = string.Empty;
        }

        MaelstromPoolEntry RowAt(int index)
        {
            while (_rows.Count <= index)
            {
                if (!rowPrefab || !rowContainer)
                {
                    CSDebug.LogWarning("[ArcadeLaunch] MaelstromPoolListView needs both a rowPrefab " +
                                       "and a rowContainer to draw anything.", this);
                    return null;
                }
                _rows.Add(Instantiate(rowPrefab, rowContainer));
            }
            return _rows[index];
        }
    }
}
