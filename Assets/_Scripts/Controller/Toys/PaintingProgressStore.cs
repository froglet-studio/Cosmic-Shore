using System;
using System.Collections.Generic;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Local persistence for fly-by-numbers painting progress, so a many-hour painting survives
    /// app restarts. Follows the FavoriteSystem pattern: a small JSON file via
    /// <see cref="DataAccessor"/> in persistentDataPath, loaded lazily, saved on every change.
    ///
    /// Progress is stroke-granular and strictly ordered (strokes are flown in author order), so an
    /// entry is just "how many strokes are done" plus the stroke count it was saved against - if a
    /// painting's stroke list changes across builds, its progress resets rather than mis-mapping.
    /// Note the painted prisms themselves live only as long as the scene; after a restart the
    /// completed strokes render as "done" ghost lines and the credit is kept.
    /// </summary>
    public static class PaintingProgressStore
    {
        [Serializable]
        public class Entry
        {
            public int strokesCompleted;
            public int totalStrokes;
            public int timesCompleted;
        }

        [Serializable]
        class SaveData
        {
            public Dictionary<string, Entry> paintings = new();
        }

        const string SaveFileName = "painting_progress.data";

        static SaveData _data;

        static SaveData Data
        {
            get
            {
                if (_data == null)
                {
                    _data = DataAccessor.Load<SaveData>(SaveFileName) ?? new SaveData();
                    _data.paintings ??= new Dictionary<string, Entry>();
                }
                return _data;
            }
        }

        /// <summary>Completed stroke count for a painting; 0 when unknown or the stroke list changed.</summary>
        public static int GetStrokesCompleted(string paintingId, int totalStrokes)
        {
            if (string.IsNullOrEmpty(paintingId)) return 0;
            if (!Data.paintings.TryGetValue(paintingId, out var e) || e == null) return 0;
            if (e.totalStrokes != totalStrokes) return 0; // painting changed across builds - start over
            return Math.Clamp(e.strokesCompleted, 0, totalStrokes);
        }

        public static int GetTimesCompleted(string paintingId)
        {
            if (string.IsNullOrEmpty(paintingId)) return 0;
            return Data.paintings.TryGetValue(paintingId, out var e) && e != null ? e.timesCompleted : 0;
        }

        public static void SetStrokesCompleted(string paintingId, int strokesCompleted, int totalStrokes)
        {
            if (string.IsNullOrEmpty(paintingId)) return;
            var e = GetOrCreate(paintingId);
            e.strokesCompleted = Math.Clamp(strokesCompleted, 0, totalStrokes);
            e.totalStrokes = totalStrokes;
            Save();
        }

        /// <summary>Record a finished masterpiece (keeps the 100% display until the player repaints).</summary>
        public static void MarkCompleted(string paintingId, int totalStrokes)
        {
            if (string.IsNullOrEmpty(paintingId)) return;
            var e = GetOrCreate(paintingId);
            e.strokesCompleted = totalStrokes;
            e.totalStrokes = totalStrokes;
            e.timesCompleted++;
            Save();
        }

        /// <summary>Reset a painting to unpainted (used when the player chooses to repaint).</summary>
        public static void ResetProgress(string paintingId)
        {
            if (string.IsNullOrEmpty(paintingId)) return;
            if (!Data.paintings.TryGetValue(paintingId, out var e) || e == null) return;
            e.strokesCompleted = 0;
            Save();
        }

        static Entry GetOrCreate(string paintingId)
        {
            if (!Data.paintings.TryGetValue(paintingId, out var e) || e == null)
            {
                e = new Entry();
                Data.paintings[paintingId] = e;
            }
            return e;
        }

        static void Save() => DataAccessor.Save(SaveFileName, Data);
    }
}
