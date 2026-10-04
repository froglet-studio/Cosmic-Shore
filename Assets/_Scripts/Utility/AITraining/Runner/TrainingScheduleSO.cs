using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Overnight queue. Each slot is one scenario with its own episode cap
    /// and wall-clock budget. Either cap advances the queue. The cursor that
    /// says which slot is live lives on <see cref="TrainingSessionStateSO"/>,
    /// so a halt resumes that slot instead of the first one.
    ///
    /// A missing schedule reference on the control asset means a single
    /// scenario. A missing int on an older slot reads as 0, which is "no
    /// episode cap" and "no extra retries", never "stop the night".
    /// </summary>
    [CreateAssetMenu(
        fileName = "TrainingSchedule",
        menuName = "ScriptableObjects/AI Training/Schedule",
        order = 204)]
    public class TrainingScheduleSO : ScriptableObject
    {
        [Serializable]
        public class Slot
        {
            public TrainingScenarioSO Scenario;

            [Tooltip("Completed evaluations in this slot before the queue advances. " +
                     "0 or less means this slot has no episode cap.")]
            public int TargetEpisodes;

            [Tooltip("Wall-clock hours in this slot before the queue advances. " +
                     "0 or less means this slot has no clock cap. The clock is " +
                     "unscaled, so a faster simulation does not finish the budget early.")]
            public float WallClockHours = 4f;

            [Tooltip("Failures tolerated on this slot, including the one that just failed. " +
                     "2 retries once, then the queue skips the slot. 0 is treated as 1: " +
                     "skip after the failure, and keep the rest of the night.")]
            public int MaxAttempts = 2;
        }

        public List<Slot> Slots = new();

        public int Count => Slots != null ? Slots.Count : 0;

        public Slot Get(int index)
        {
            if (Slots == null || index < 0 || index >= Slots.Count) return null;
            return Slots[index];
        }
    }

    /// <summary>
    /// Pure queue decisions. The runner and the launcher both call this, and
    /// the edit-mode tests call it with no scene. It does not load a scene,
    /// write a genome, or touch an archive.
    /// </summary>
    public static class TrainingSchedule
    {
        public enum Action
        {
            Continue = 0,
            RetrySame = 1,
            Advance = 2,
            Finished = 3,
        }

        public readonly struct Decision
        {
            public readonly Action Action;
            public readonly int Index;
            public readonly string Reason;

            public Decision(Action action, int index, string reason)
            {
                Action = action;
                Index = index;
                Reason = reason ?? "";
            }
        }

        public static double WallSeconds(TrainingScheduleSO.Slot slot)
        {
            if (slot == null || slot.WallClockHours <= 0f) return 0d;
            return slot.WallClockHours * 3600d;
        }

        public static Decision AfterEpisode(
            TrainingScheduleSO schedule, int index, int slotEpisodes, double elapsedSeconds)
        {
            if (schedule == null || schedule.Count == 0)
                return new Decision(Action.Continue, 0, "no schedule");
            if (index >= schedule.Count)
                return new Decision(Action.Finished, index, "queue already finished");
            if (index < 0) index = 0;

            var slot = schedule.Get(index);
            if (slot == null || slot.Scenario == null)
                return Move(index, schedule.Count, "missing scenario");

            bool episodes = slot.TargetEpisodes > 0 && slotEpisodes >= slot.TargetEpisodes;
            bool clock = WallSeconds(slot) > 0d && elapsedSeconds >= WallSeconds(slot);
            if (!episodes && !clock)
                return new Decision(Action.Continue, index, "slot still open");

            string why = episodes && clock
                ? "target episodes and wall-clock budget"
                : episodes ? "target episodes" : "wall-clock budget";
            return Move(index, schedule.Count, why);
        }

        /// <summary>
        /// A thrown or wedged slot. Attempts below the cap retry the same
        /// scenario. At the cap the slot is skipped and the next one starts.
        /// The last slot finishing this way ends the queue. It does not
        /// throw, and it does not clear any other slot.
        /// </summary>
        public static Decision AfterFailure(
            TrainingScheduleSO schedule, int index, int attempts)
        {
            if (schedule == null || schedule.Count == 0)
                return new Decision(Action.Finished, 0, "no schedule");
            if (index < 0) index = 0;
            if (index >= schedule.Count)
                return new Decision(Action.Finished, index, "queue already finished");

            var slot = schedule.Get(index);
            if (slot == null || slot.Scenario == null)
                return Move(index, schedule.Count, "missing scenario");

            int max = slot.MaxAttempts <= 0 ? 1 : slot.MaxAttempts;
            int nextAttempts = attempts + 1;
            if (nextAttempts < max)
                return new Decision(Action.RetrySame, index, "retry after failure");
            return Move(index, schedule.Count, "skipped after failure");
        }

        public static void Apply(TrainingSessionStateSO state, Decision decision)
        {
            if (state == null) return;
            switch (decision.Action)
            {
                case Action.RetrySame:
                    state.ScheduleSlotAttempts++;
                    break;
                case Action.Advance:
                case Action.Finished:
                    state.ScheduleIndex = decision.Index;
                    state.ScheduleSlotEpisodes = 0;
                    state.ScheduleSlotElapsedSeconds = 0d;
                    state.ScheduleSlotAttempts = 0;
                    break;
            }
        }

        static Decision Move(int index, int count, string reason)
        {
            int next = index + 1;
            if (next >= count)
                return new Decision(Action.Finished, next, reason);
            return new Decision(Action.Advance, next, reason);
        }
    }
}
