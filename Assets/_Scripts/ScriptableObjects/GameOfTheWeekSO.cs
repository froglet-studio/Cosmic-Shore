using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Names this week's Game of the Week: the arcade mode a brand-new player is walked into for
    /// their first Lesson (Docs/ModePreview/TRAINING_PLAN.md §5).
    ///
    /// <para><b>A minimal source, on purpose.</b> The rotation itself is a separate product
    /// thread; the training plan only needs ONE <see cref="GameModes"/> value, so this is the
    /// smallest thing that gives one and can be replaced without touching anything that reads it
    /// (everything goes through <see cref="Current"/>). The rotation is an authored, ordered list
    /// stepped once per UTC week - reading order IS the schedule, which is what a person editing
    /// it expects - and an empty list falls back to <see cref="fallback"/>.</para>
    ///
    /// <para><b>Derived, never stored</b>, like the weekly challenge (Docs/WEEKLY_CHALLENGE.md):
    /// the week is computed from the UTC date, so it resolves offline and identically on every
    /// machine. Weeks start on the UTC MONDAY, and the count is integer days from a fixed Monday,
    /// so no calendar or culture is ever involved.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "GameOfTheWeek", menuName = "ScriptableObjects/Arcade/Game Of The Week")]
    public sealed class GameOfTheWeekSO : ScriptableObject
    {
        public const string ResourcePath = "GameOfTheWeek";

        public static GameOfTheWeekSO Load() => Resources.Load<GameOfTheWeekSO>(ResourcePath);

        [Tooltip("The modes in rotation, one per UTC week, in this order. Each must have a card on the " +
                 "arcade roster and a flyable preview, or the first-login walk-in has nowhere to go.")]
        [SerializeField] List<GameModes> rotation = new();

        [Tooltip("Used when the rotation is empty.")]
        [SerializeField] GameModes fallback = GameModes.SkimRace;

        /// <summary>A Monday: the week every rotation index counts from.</summary>
        static readonly DateTime Epoch = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public IReadOnlyList<GameModes> Rotation => rotation;

        /// <summary>This week's mode.</summary>
        public GameModes Current() => For(DateTime.UtcNow);

        /// <summary>The mode for the UTC week containing <paramref name="utc"/>.</summary>
        public GameModes For(DateTime utc)
        {
            if (rotation == null || rotation.Count == 0) return fallback;
            return rotation[WeekIndex(utc, rotation.Count)];
        }

        /// <summary>Whole UTC weeks since <see cref="Epoch"/>, wrapped into the rotation.</summary>
        public static int WeekIndex(DateTime utc, int count)
        {
            if (count <= 0) return 0;
            long days = (long)Math.Floor((utc.ToUniversalTime() - Epoch).TotalDays);
            long week = days >= 0 ? days / 7 : (days - 6) / 7;   // floor, so a pre-epoch date is not off by one
            long index = week % count;
            return (int)(index < 0 ? index + count : index);
        }
    }
}
