using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One recorded prism of a painting: PAINTING-LOCAL pose (independent of where the toybox
    /// anchors the monument), scale, domain, and the prism type it was laid as. Compact fields -
    /// thousands of these serialize per painting.
    /// </summary>
    [Serializable]
    public class PaintingPrismRecord
    {
        public float px, py, pz;      // local position
        public float qx, qy, qz, qw;  // local rotation
        public float sx, sy, sz;      // target scale
        public int domain;            // Domains
        public int prismType;         // PrismType

        public Vector3 Position => new(px, py, pz);
        public Quaternion Rotation => new(qx, qy, qz, qw);
        public Vector3 Scale => new(sx, sy, sz);

        public static PaintingPrismRecord From(Vector3 localPos, Quaternion localRot, Vector3 scale,
            Domains domain, PrismType prismType) => new()
        {
            px = localPos.x, py = localPos.y, pz = localPos.z,
            qx = localRot.x, qy = localRot.y, qz = localRot.z, qw = localRot.w,
            sx = scale.x, sy = scale.y, sz = scale.z,
            domain = (int)domain,
            prismType = (int)prismType,
        };
    }

    /// <summary>
    /// Local persistence of the PRISMS a painting is made of, stroke by stroke - the drawing
    /// state itself, not just the stroke counter. This is what lets a half-finished monument
    /// physically regrow when the player returns from another painting, another game mode, or
    /// another session, and what the share exporter reconstructs in the web viewer.
    ///
    /// Same blessed pattern as the progress store (<see cref="Utility.DataAccessor"/> JSON in
    /// persistentDataPath), one file per painting so a Taj Mahal's few-hundred-KB doesn't ride
    /// along with every small save. Prisms buffer in memory during a stroke and are committed
    /// only when the stroke completes - an abandoned stroke is re-flown fresh, so its partial
    /// prisms never persist.
    /// </summary>
    public static class PaintingPrismStore
    {
        [Serializable]
        class PaintingPrisms
        {
            public List<List<PaintingPrismRecord>> strokes = new(); // index = stroke order
        }

        static readonly Dictionary<string, PaintingPrisms> Cache = new();
        static readonly Dictionary<string, List<PaintingPrismRecord>> PendingStroke = new();

        // Keyed by stable painting ids: an uncommitted stroke from a play session that exited
        // mid-stroke must not be committed to disk by the NEXT session's CommitStroke.
        // (Cache stays — it is disk-truthed.)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => PendingStroke.Clear();

        static string FileName(string paintingId) => $"painting_prisms_{paintingId}.data";

        static PaintingPrisms Load(string paintingId)
        {
            if (Cache.TryGetValue(paintingId, out var data) && data != null) return data;
            data = DataAccessor.Load<PaintingPrisms>(FileName(paintingId)) ?? new PaintingPrisms();
            data.strokes ??= new List<List<PaintingPrismRecord>>();
            Cache[paintingId] = data;
            return data;
        }

        /// <summary>Buffer a prism laid during the in-progress stroke (not yet persisted).</summary>
        public static void RecordPrism(string paintingId, PaintingPrismRecord record)
        {
            if (string.IsNullOrEmpty(paintingId) || record == null) return;
            if (!PendingStroke.TryGetValue(paintingId, out var pending) || pending == null)
                PendingStroke[paintingId] = pending = new List<PaintingPrismRecord>();
            pending.Add(record);
        }

        /// <summary>Drop the in-progress buffer (stroke restarted / run torn down mid-stroke).</summary>
        public static void DiscardPending(string paintingId)
        {
            if (!string.IsNullOrEmpty(paintingId)) PendingStroke.Remove(paintingId);
        }

        /// <summary>Persist the buffered prisms as stroke <paramref name="strokeIndex"/>'s content.</summary>
        public static void CommitStroke(string paintingId, int strokeIndex)
        {
            if (string.IsNullOrEmpty(paintingId) || strokeIndex < 0) return;
            var data = Load(paintingId);
            while (data.strokes.Count <= strokeIndex)
                data.strokes.Add(new List<PaintingPrismRecord>());
            PendingStroke.TryGetValue(paintingId, out var pending);
            data.strokes[strokeIndex] = pending ?? new List<PaintingPrismRecord>();
            PendingStroke.Remove(paintingId);
            DataAccessor.Save(FileName(paintingId), data);
        }

        /// <summary>Every persisted prism of strokes [0, strokeCount) in stroke order.</summary>
        public static List<PaintingPrismRecord> GetPrisms(string paintingId, int strokeCount)
        {
            var result = new List<PaintingPrismRecord>();
            if (string.IsNullOrEmpty(paintingId)) return result;
            var data = Load(paintingId);
            int n = Mathf.Min(strokeCount, data.strokes.Count);
            for (int i = 0; i < n; i++)
                if (data.strokes[i] != null)
                    result.AddRange(data.strokes[i]);
            return result;
        }

        public static bool HasPrisms(string paintingId)
        {
            if (string.IsNullOrEmpty(paintingId)) return false;
            var data = Load(paintingId);
            foreach (var stroke in data.strokes)
                if (stroke is { Count: > 0 }) return true;
            return false;
        }

        /// <summary>Wipe a painting's prisms (the player chose to repaint from scratch).</summary>
        public static void Clear(string paintingId)
        {
            if (string.IsNullOrEmpty(paintingId)) return;
            PendingStroke.Remove(paintingId);
            Cache[paintingId] = new PaintingPrisms();
            DataAccessor.Flush(FileName(paintingId));
        }
    }
}
