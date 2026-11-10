using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Regatta's course POOL: the circuits one Regatta level shuffles between (Garrett, 2026-11-06: one Regatta level,
    /// no intensities, several balanced course variants shuffled). The host draws one entry per match
    /// (<see cref="Draw"/>), every machine lays the same one, and intensity no longer picks the course.
    ///
    /// <para><b>An entry is a cell config.</b> Each entry names a Regatta <see cref="CellConfigDataSO"/>, whose
    /// <c>EnvironmentPrefab</c> is the <see cref="SpawnableRegattaRails"/> variant that lays the circuit (its circuit
    /// row, its seed, and every per-arena setting the course search adds later) and whose phase thresholds ride that
    /// circuit's measured rail volume. So the course data stays where the course harness and the parity gates read it
    /// (<c>RegattaCourse</c>, the prefab), the pool only chooses among circuits, and a new searched course is a new
    /// prefab + cell config + entry, all authored by <c>Tools/Build/author_regatta_assets.py</c>.</para>
    ///
    /// <para><b>How the draw travels.</b> The host draws the entry index in the controller's spawn
    /// (<c>MultiplayerMiniGameControllerBase.DrawCourseVariant</c>), stores it in <c>GameDataSO.CourseVariant</c> and sends it in
    /// the game-config sync RPC; the scene's Cell (<c>CellTypeChoiceOptions.CourseVariantWise</c>) takes
    /// <c>CellConfigs[CourseVariant]</c>, so the entries' ORDER must be the Cell's list order (the generator checks
    /// it). The rails and the rings are then re-derived on every machine from the same prefab through the same pure
    /// call, as before.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RegattaCoursePool", menuName = "ScriptableObjects/Game Modes/Regatta Course Pool")]
    public class RegattaCoursePoolSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("A short name for this circuit, for people reading the pool, a replay or a flight record.")]
            public string Label;

            [Tooltip("The Regatta cell config that lays this circuit. Its EnvironmentPrefab is the SpawnableRegattaRails " +
                     "variant carrying the circuit; the scene Cell's CellConfigs list must hold the same configs in the " +
                     "same order as this pool.")]
            public CellConfigDataSO CellConfig;

            [Tooltip("Off = never drawn. How a circuit is taken out of the shuffle without deleting it.")]
            public bool Enabled = true;

            [Tooltip("Why this circuit is in the pool (the search that found it, its measured spread). Notes only.")]
            [TextArea(1, 4)] public string Notes;

            /// <summary>The rails prefab this entry lays, or null when the config authors none.</summary>
            public SpawnableRegattaRails Rails => CellConfig ? CellConfig.EnvironmentPrefab as SpawnableRegattaRails : null;
        }

        [Tooltip("The circuits the shuffle deals from, in the scene Cell's CellConfigs order.")]
        [SerializeField] List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;
        public int Count => entries?.Count ?? 0;

        [NonSerialized] CourseShuffleBag _bag;
        [NonSerialized] readonly List<bool> _enabledScratch = new();

        /// <summary>
        /// The entry this match lays, 0-based. A pinned variant (<see cref="CourseVariantOverride"/>: a replay, a
        /// benchmark) wins; otherwise the shuffle bag deals the next enabled entry. 0 when no entry is enabled, so a
        /// misauthored pool still lays a course (the first) rather than none. Host only.
        /// </summary>
        public int Draw() => Draw(new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue)));

        /// <summary><see cref="Draw()"/> with the caller's random source (tests).</summary>
        public int Draw(System.Random rng)
        {
            int n = Count;
            if (n == 0) return 0;

            int pinned = CourseVariantOverride.Pick(n);
            if (pinned >= 0) return pinned;

            _enabledScratch.Clear();
            for (int i = 0; i < n; i++) _enabledScratch.Add(entries[i] != null && entries[i].Enabled && entries[i].CellConfig);
            _bag ??= new CourseShuffleBag();
            int dealt = _bag.Next(_enabledScratch, rng);
            return dealt < 0 ? 0 : dealt;
        }

        /// <summary>The entry's label, or its index when unlabelled / out of range (logs and records).</summary>
        public string LabelOf(int index) =>
            index >= 0 && index < Count && entries[index] != null && !string.IsNullOrEmpty(entries[index].Label)
                ? entries[index].Label
                : $"entry {index}";

        /// <summary>
        /// Why this pool cannot drive a Cell whose <c>CellConfigs</c> are <paramref name="cellConfigs"/>, in one
        /// sentence, or null when it can: the same configs in the same order, each laying Regatta rails.
        /// </summary>
        public string MismatchWith(IReadOnlyList<CellConfigDataSO> cellConfigs)
        {
            int n = Count;
            if (n == 0) return "the course pool has no entries";
            if (cellConfigs == null || cellConfigs.Count != n)
                return $"the course pool has {n} entries but the Cell lists {cellConfigs?.Count ?? 0} configs";
            for (int i = 0; i < n; i++)
            {
                var e = entries[i];
                if (e == null || !e.CellConfig) return $"pool entry {i} names no cell config";
                if (cellConfigs[i] != e.CellConfig)
                    return $"pool entry {i} is '{e.CellConfig.name}' but the Cell's config {i} is " +
                           $"'{(cellConfigs[i] ? cellConfigs[i].name : "none")}'";
                if (!e.Rails) return $"pool entry {i} ('{e.CellConfig.name}') lays no SpawnableRegattaRails";
            }
            return null;
        }
    }
}
