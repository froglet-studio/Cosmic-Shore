using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Port engine extension: render change tracking — the engine-side half of what lets a
    /// render backend keep a flat, cached table of renderer bounds instead of revisiting every
    /// renderer every frame (a grown arena carries ~230k of them, almost all static).
    ///
    /// Any write that can move a renderer's world bounds or change what it draws marks that
    /// renderer dirty: a transform write marks its whole subtree (world poses compose), a
    /// reparent does the same, and a material or mesh assignment marks its own renderer. The
    /// backend drains the set once per frame and rebuilds only those entries. A subtree marked
    /// in the current epoch is not walked again until the next drain, so a transform written
    /// many times a frame (an animated bone) costs one walk.
    ///
    /// Off unless a backend turns it on (<see cref="TrackChanges"/>): headless runs and tests
    /// pay one boolean test per write. Nothing here reads or changes gameplay state.
    /// </summary>
    public partial class Renderer
    {
        static bool s_track;
        internal static long s_changeEpoch = 1;
        static readonly List<Renderer> s_dirty = new();
        long _dirtyEpoch;

        /// <summary>Turn tracking on (render backend). Turning it on marks every live renderer dirty.</summary>
        public static bool TrackChanges
        {
            get => s_track;
            set
            {
                if (s_track == value) return;
                s_track = value;
                s_dirty.Clear();
                s_changeEpoch++;
                if (value)
                    foreach (var r in s_live) r.MarkRenderDirty();
            }
        }

        /// <summary>Move every renderer marked since the last drain into <paramref name="into"/> and open a new epoch.</summary>
        public static void DrainDirty(List<Renderer> into)
        {
            into.Clear();
            into.AddRange(s_dirty);
            s_dirty.Clear();
            s_changeEpoch++;
        }

        /// <summary>Mark this renderer's cached render state stale (no-op unless tracking).</summary>
        public void MarkRenderDirty()
        {
            if (!s_track || _dirtyEpoch == s_changeEpoch) return;
            _dirtyEpoch = s_changeEpoch;
            s_dirty.Add(this);
        }
    }

    public partial class GameObject
    {
        /// <summary>Mark every renderer on this object dirty (no-op unless tracking).</summary>
        internal void MarkRenderersDirty()
        {
            foreach (var c in _components)
                if (c is Renderer r) r.MarkRenderDirty();
        }
    }

    public partial class Transform
    {
        long _movedEpoch;
        static readonly Stack<Transform> s_markStack = new();

        /// <summary>
        /// This transform's world pose may have changed: mark the renderers of its whole subtree
        /// dirty, once per epoch (no-op unless tracking).
        /// </summary>
        internal void MarkMoved()
        {
            if (!Renderer.TrackChanges) return;
            long epoch = Renderer.s_changeEpoch;
            if (_movedEpoch == epoch) return;
            var stack = s_markStack;
            int floor = stack.Count;
            stack.Push(this);
            while (stack.Count > floor)
            {
                var t = stack.Pop();
                if (t._movedEpoch == epoch) continue;
                t._movedEpoch = epoch;
                t.gameObject?.MarkRenderersDirty();
                foreach (var child in t._children)
                    if (child._movedEpoch != epoch) stack.Push(child);
            }
        }
    }
}
