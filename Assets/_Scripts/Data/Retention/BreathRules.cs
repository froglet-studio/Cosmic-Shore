namespace CosmicShore.Data
{
    /// <summary>What the breath between rounds offers (P5, Loop Atlas loop <c>breath</c>).</summary>
    public enum BreathStep
    {
        /// <summary>Nothing beyond Play again: the same rung is the next step.</summary>
        None = 0,
        /// <summary>The next rung of this card's ladder, after a win, when it is open.</summary>
        NextRung = 1,
        /// <summary>A crossing to the place the round trained for (an arcade Time game's arena race, say).</summary>
        Crossing = 2,
        /// <summary>Today's job of the Tide is waiting and fits the sitting (era.tide).</summary>
        DailyJob = 3,
    }

    /// <summary>
    /// The breath's choice, pure so it is tested without a scene (BreathRulesTests). After every result the scoreboard
    /// offers ONE next step beside Play again (Garrett's P5: "rematch, next rung, or a crossing to the place the round
    /// trained for"), so a sitting crosses places instead of ending at the menu. The rules:
    /// <list type="number">
    /// <item>A win with a higher rung open offers that rung (the ladder's pull: "3 to 5 rounds a run").</item>
    /// <item>Otherwise, after enough rounds this sitting, a crossing to the place this round trained for.</item>
    /// <item>Otherwise, today's Tide job when one is waiting.</item>
    /// <item>Otherwise nothing: Play again already is "the same rung after a loss".</item>
    /// </list>
    /// </summary>
    public static class BreathRules
    {
        public static BreathStep Choose(bool won, int intensity, int highestOpen, bool crossingOpen, int roundsThisSitting,
                                        int roundsBeforeCrossing, bool dailyJobWaiting)
        {
            if (won && intensity < highestOpen) return BreathStep.NextRung;
            if (crossingOpen && roundsThisSitting >= roundsBeforeCrossing) return BreathStep.Crossing;
            if (dailyJobWaiting) return BreathStep.DailyJob;
            return BreathStep.None;
        }

        /// <summary>The arena game an element's arcade games train for (the elemental loop's three arena games: Regatta for
        /// Time, Broadside for Charge, Brood Rush for Mass and Space together; CLAUDE.md, the elemental vessel loop).</summary>
        public static GameModes ArenaFor(Element element) => element switch
        {
            Element.Time => GameModes.Regatta,
            Element.Charge => GameModes.Broadside,
            Element.Mass => GameModes.BroodRush,
            Element.Space => GameModes.BroodRush,
            _ => GameModes.Random,
        };
    }
}
