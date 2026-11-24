namespace CosmicShore.Data
{
    /// <summary>The rule behind <see cref="PilotMood"/>: a pilot's faction against a voyage type's lists.</summary>
    public static class VoyageMood
    {
        /// <summary>
        /// The mood of a pilot of <paramref name="faction"/> on a voyage of type <paramref name="info"/>: Refuses when the
        /// type's Refuse list names the faction, else Keen, else Reluctant, else Steady. None when there is no type (a
        /// voyage with no giver, the Crystal Run) or the pilot has no faction.
        /// </summary>
        public static PilotMood Of(VoyageTypeInfo info, FactionType faction)
        {
            if (info == null || faction == FactionType.None) return PilotMood.None;
            if (info.Refuse != null && info.Refuse.Contains(faction)) return PilotMood.Refuses;
            if (info.Keen != null && info.Keen.Contains(faction)) return PilotMood.Keen;
            if (info.Reluctant != null && info.Reluctant.Contains(faction)) return PilotMood.Reluctant;
            return PilotMood.Steady;
        }
    }
}
