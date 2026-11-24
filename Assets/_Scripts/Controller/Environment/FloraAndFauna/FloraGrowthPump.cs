using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runs every plant's growth inside ONE time budget per frame, so a garden's growth can no longer pile
    /// into a single frame.
    ///
    /// <para>Each plant used to grow from its own clock (<c>Flora.GrowCoroutine</c>) or its own Update
    /// (<c>PhyllotacticFlora</c>). The clocks are staggered, but a long frame lets many clocks expire
    /// together, they all grow in the next frame, that frame is longer still, and more clocks expire in
    /// it: Sirocco had 42 frames over 250 ms in 40 seconds, spent growing and cloning limbs (Profiler
    /// capture of a built player, 2026-11-22). Here a plant only ASKS to grow; the pump serves the asks in
    /// the order they came until the frame's budget is spent, and the rest wait for the next frame.</para>
    ///
    /// <para>With room in the budget nothing changes: a tick asked for in the coroutine phase runs in this
    /// same frame's LateUpdate, and the plant's own cadence is untouched. Past the budget a garden grows
    /// later, never differently: a plant that asks again while its last ask is still waiting is not queued
    /// twice. At least one plant is served every frame.</para>
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class FloraGrowthPump : MonoBehaviour
    {
        /// <summary>Main-thread milliseconds a frame may spend on flora growth. The work of the plant that
        /// crosses the line is finished, so a frame can run over by one plant's tick.</summary>
        public static float BudgetMsPerFrame = 3f;

        static FloraGrowthPump s_instance;
        static Queue<Flora> s_ticks = new(256);
        static List<Flora> s_drains = new(256);
        static int s_drainCursor;

        /// <summary>Diagnostic: ticks and drain steps served, and frames that ended with work still waiting.</summary>
        public static int Served { get; private set; }
        public static int FramesOverBudget { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_ticks = new Queue<Flora>(256);
            s_drains = new List<Flora>(256);
            s_drainCursor = 0;
            Served = FramesOverBudget = 0;
        }

        static bool Ensure()
        {
            if (s_instance) return true;
            if (!Application.isPlaying) return false;
            var go = new GameObject(nameof(FloraGrowthPump)) { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<FloraGrowthPump>();
            return true;
        }

        /// <summary>A plant's grow tick is due. It runs this frame if the budget has room, else in a later
        /// one, in the order asked. Outside play mode it runs at once.</summary>
        public static void RequestTick(Flora plant)
        {
            if (!Ensure()) { plant.RunGrowTick(); return; }
            if (plant.TryMarkGrowTickQueued()) s_ticks.Enqueue(plant);
        }

        /// <summary>A plant has growth orders waiting to be carried out (<c>Flora.DrainQueuedGrowth</c>). It
        /// is asked for one step a frame, in turn with every other such plant, until it has none left.
        /// False outside play mode: the caller then drains itself.</summary>
        public static bool RequestDrain(Flora plant)
        {
            if (!Ensure()) return false;
            if (plant.TryMarkGrowDrainQueued()) s_drains.Add(plant);
            return true;
        }

        void LateUpdate()
        {
            if (s_ticks.Count == 0 && s_drains.Count == 0) return;

            long start = Stopwatch.GetTimestamp();
            long limit = (long)(BudgetMsPerFrame * 0.001 * Stopwatch.Frequency);
            bool servedOne = false;

            // Ticks and drains take the first turn on alternate frames, so neither can starve the other.
            bool ticksFirst = (Time.frameCount & 1) == 0;
            for (int pass = 0; pass < 2; pass++)
            {
                if ((pass == 0) == ticksFirst) ServeTicks(start, limit, ref servedOne);
                else ServeDrains(start, limit, ref servedOne);
            }

            if (s_ticks.Count > 0) FramesOverBudget++;   // a drain list is never empty while a plant is paused, so only ticks count
        }

        static bool HasRoom(long start, long limit, bool servedOne) =>
            !servedOne || Stopwatch.GetTimestamp() - start < limit;

        static void ServeTicks(long start, long limit, ref bool servedOne)
        {
            while (s_ticks.Count > 0 && HasRoom(start, limit, servedOne))
            {
                var plant = s_ticks.Dequeue();
                if (!plant) continue;   // destroyed while it waited
                plant.RunGrowTick();
                servedOne = true;
                Served++;
            }
        }

        static void ServeDrains(long start, long limit, ref bool servedOne)
        {
            // One step per plant per frame, starting where the last frame stopped, so the plants late in
            // the list are not always the ones left out. Swap-remove keeps the walk allocation-free.
            int visits = s_drains.Count;
            for (int v = 0; v < visits && s_drains.Count > 0 && HasRoom(start, limit, servedOne); v++)
            {
                if (s_drainCursor >= s_drains.Count) s_drainCursor = 0;
                var plant = s_drains[s_drainCursor];
                bool worked = false;
                bool more = plant && plant.DrainQueuedGrowth(out worked);
                if (worked) { servedOne = true; Served++; }
                if (more)
                {
                    s_drainCursor++;
                    continue;
                }
                if (plant) plant.ClearGrowDrainQueued();
                int last = s_drains.Count - 1;
                s_drains[s_drainCursor] = s_drains[last];
                s_drains.RemoveAt(last);
            }
        }
    }
}
