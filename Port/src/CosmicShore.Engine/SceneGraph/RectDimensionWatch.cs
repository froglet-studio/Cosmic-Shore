using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// <c>OnRectTransformDimensionsChange</c> (original contract): sent to the behaviours on a
    /// RectTransform whose rect changed size or shape. Rects here are computed on read, with no
    /// setter to hook, so the change is found by comparison instead: every enabled behaviour that
    /// declares the message is watched, and the canvas update polls their rects before and after
    /// the layout solve (<see cref="UI.LayoutRebuilder.FlushQueuedRebuilds"/>). A change made in
    /// Update (a script resizing a panel, a parent stretching with the screen) is reported before
    /// layout, so a root layout group re-lays out that same frame; a change the layout made (a
    /// group resizing its children) is reported after it, the way the original reports it during.
    /// A rect seen for the first time is recorded, not reported: OnEnable covers the first state.
    /// </summary>
    internal static class RectDimensionWatch
    {
        static readonly List<MonoBehaviour> s_Watched = new();
        static readonly Dictionary<MonoBehaviour, int> s_Index = new(ReferenceEqualityComparer.Instance);
        static readonly List<MonoBehaviour> s_Fire = new();
        static int s_Poll;

        internal static int Count => s_Watched.Count;

        public static void Add(MonoBehaviour mb)
        {
            if (s_Index.ContainsKey(mb)) return;
            s_Index[mb] = s_Watched.Count;
            s_Watched.Add(mb);
        }

        public static void Remove(MonoBehaviour mb)
        {
            if (!s_Index.Remove(mb, out int i)) return;
            int last = s_Watched.Count - 1;
            if (i != last)
            {
                var moved = s_Watched[last];
                s_Watched[i] = moved;
                s_Index[moved] = i;
            }
            s_Watched.RemoveAt(last);
        }

        internal static void Reset()
        {
            s_Watched.Clear();
            s_Index.Clear();
        }

        /// <summary>Reports every watched rect that changed since the last poll; returns how many behaviours were told.</summary>
        public static int Poll()
        {
            if (s_Watched.Count == 0) return 0;
            s_Poll++;
            s_Fire.Clear();
            for (int i = 0; i < s_Watched.Count; i++)
            {
                var mb = s_Watched[i];
                if (mb.destroyedFlag || mb.gameObject is null || mb.transform is not RectTransform rt) continue;
                if (rt.DimensionPoll != s_Poll)
                {
                    rt.DimensionPoll = s_Poll;
                    var r = rt.rect;
                    rt.DimensionChanged = rt.DimensionSeen && !Same(r, rt.DimensionRect);
                    rt.DimensionRect = r;
                    rt.DimensionSeen = true;
                }
                if (rt.DimensionChanged) s_Fire.Add(mb);
            }
            if (s_Fire.Count == 0) return 0;
            // A handler may resize more rects or force a canvas update (a nested poll): fire from a copy.
            var fire = s_Fire.ToArray();
            s_Fire.Clear();
            int fired = 0;
            foreach (var mb in fire)
            {
                if (mb.destroyedFlag || !mb.enabledRun || mb.hooks?.RectTransformDimensionsChange is not { } hook) continue;
                mb.SendMessageHook(hook);
                fired++;
            }
            return fired;
        }

        static bool Same(Rect a, Rect b) =>
            Mathf.Abs(a.x - b.x) < 1e-3f && Mathf.Abs(a.y - b.y) < 1e-3f &&
            Mathf.Abs(a.width - b.width) < 1e-3f && Mathf.Abs(a.height - b.height) < 1e-3f;
    }
}
