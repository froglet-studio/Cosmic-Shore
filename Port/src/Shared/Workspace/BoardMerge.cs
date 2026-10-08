#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Prisma.Workspace
{
    /// <summary>
    /// Three-way merge of two boards, card by card, matched by <see cref="PrismaBoard.Item.Uid"/>.
    /// The ancestor is what this board last read or wrote (<c>PrismaBoard.Base</c>):
    /// <list type="bullet">
    /// <item>A card only the other side changed takes their version, copied into this board's own
    /// object so references the app holds stay live.</item>
    /// <item>A card only this side changed keeps ours.</item>
    /// <item>A card both sides changed takes the one with the later <c>Updated</c> (ours on a tie).</item>
    /// <item>A card new on either side is kept.</item>
    /// <item>A card one side deleted stays deleted unless the other side edited it since.</item>
    /// </list>
    /// Two writers may have handed out the same key (T-5) to different cards. The card the store
    /// already had keeps it, and ours gets the next free one. The counters end past every key on
    /// either side.
    /// </summary>
    public static class BoardMerge
    {
        public static string Json(PrismaBoard.Item item) => BoardJson.Serialize(item);

        public static Dictionary<string, string> Snapshot(PrismaBoard board)
        {
            Backfill(board);
            var map = new Dictionary<string, string>();
            foreach (var i in board.Items) map[i.Uid] = Json(i);
            return map;
        }

        /// <summary>
        /// Gives every card a Uid. A card from before Uids gets one derived from its key and creation
        /// time, so every writer that reads the same old file derives the same Uid.
        /// </summary>
        public static void Backfill(PrismaBoard board)
        {
            foreach (var i in board.Items)
                if (string.IsNullOrWhiteSpace(i.Uid))
                    i.Uid = $"legacy-{i.Id}-{i.Created.ToUniversalTime().Ticks:x}";
        }

        /// <summary>Merges <paramref name="theirs"/> (what the store holds now) into <paramref name="mine"/>.</summary>
        public static void Merge(PrismaBoard mine, PrismaBoard theirs)
        {
            Backfill(mine);
            Backfill(theirs);
            var baseMap = mine.Base;
            var theirMap = new Dictionary<string, PrismaBoard.Item>();
            foreach (var t in theirs.Items) theirMap.TryAdd(t.Uid, t);
            var myUids = new HashSet<string>(mine.Items.Select(i => i.Uid));

            var result = new List<PrismaBoard.Item>();
            foreach (var m in mine.Items)
            {
                baseMap.TryGetValue(m.Uid, out var b);
                var mj = Json(m);
                if (theirMap.TryGetValue(m.Uid, out var t))
                {
                    var tj = Json(t);
                    if (mj != tj)
                    {
                        bool mineChanged = b != mj, theirsChanged = b != tj;
                        if (!mineChanged || (theirsChanged && t.Updated > m.Updated)) CopyInto(m, t);
                    }
                    result.Add(m);
                }
                else if (b == null || b != mj) result.Add(m);   // new here, or edited here after they deleted it
                // else: they deleted it and we did not touch it since - it stays deleted
            }
            var seen = new HashSet<string>(myUids);
            foreach (var t in theirs.Items)
            {
                if (!seen.Add(t.Uid)) continue;
                if (baseMap.TryGetValue(t.Uid, out var b) && b == Json(t)) continue;   // we deleted it; they did not touch it
                result.Add(t);
            }

            // Keys: the store's cards keep theirs; a clash renumbers the card that is new on our side.
            var stored = new HashSet<string>(theirs.Items.Select(i => i.Uid));
            var taken = new HashSet<string>(result.Where(i => stored.Contains(i.Uid)).Select(i => i.Id));
            mine.Items = result;
            mine.NextBug = Math.Max(Math.Max(mine.NextBug, theirs.NextBug), MaxNumber(result, "B-") + 1);
            mine.NextTask = Math.Max(Math.Max(mine.NextTask, theirs.NextTask), MaxNumber(result, "T-") + 1);
            foreach (var i in result.Where(i => !stored.Contains(i.Uid)))
            {
                if (!taken.Add(i.Id))
                {
                    var old = i.Id;
                    i.Id = mine.NewId(i.Type);
                    i.Notes.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} renumbered from {old}: another writer took that key first");
                    taken.Add(i.Id);
                }
            }

            mine.Schema = Math.Max(mine.Schema, theirs.Schema);
            if (theirs.Extra != null)
            {
                mine.Extra ??= new();
                foreach (var kv in theirs.Extra) mine.Extra.TryAdd(kv.Key, kv.Value);
            }
        }

        /// <summary>The largest number among keys with this prefix ("B-", "T-"), or 0.</summary>
        public static int MaxNumber(IEnumerable<PrismaBoard.Item> items, string prefix)
        {
            int max = 0;
            foreach (var i in items)
                if (i.Id.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(i.Id.AsSpan(prefix.Length), out var n) && n > max) max = n;
            return max;
        }

        static readonly PropertyInfo[] Stored = typeof(PrismaBoard.Item).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<JsonIgnoreAttribute>() == null).ToArray();

        /// <summary>Copies every stored field of <paramref name="from"/> into <paramref name="to"/> (computed views like Type/State follow their stored names).</summary>
        public static void CopyInto(PrismaBoard.Item to, PrismaBoard.Item from)
        {
            foreach (var p in Stored) p.SetValue(to, p.GetValue(from));
        }
    }
}
