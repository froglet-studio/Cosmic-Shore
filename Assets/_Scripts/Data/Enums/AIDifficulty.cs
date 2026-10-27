namespace CosmicShore.Data
{
    // Always assign static numeric values; Unity serialization drift on enum reordering
    // breaks saved records and replicated ints silently.
    //
    // How well the AI pilots fly, chosen by the HOST on the launch panel. It is deliberately
    // NOT intensity: intensity is the MAP (track length, crystal count, laps), difficulty is the
    // OPPONENT - so a Legendary AI can race intensity 1 and an Easy one intensity 4.
    //
    // Legendary is the pilot with no deliberate mistakes (the shipped AI byte for byte); Hard, Medium
    // and Easy fly the same pilot with more and more human-shaped mistakes (SkimRaceDifficultySO).
    // Until 2026-10-26 Hard was the mistake-free pilot, so a flight recorded before then with a
    // "Hard" rival raced what is now Legendary.
    //
    // 0 is not a member on purpose. It is what an unwritten field reads as (a save made before
    // difficulty existed, a default struct, a lobby value nobody set), and every reader turns it
    // into the default through AIDifficultyRules.Resolve - never a switch on the raw value.
    public enum AIDifficulty
    {
        Easy = 1,
        Medium = 2,
        Hard = 3,
        Legendary = 4,
    }

    /// <summary>
    /// The few rules every reader of <see cref="AIDifficulty"/> shares, so the launch panel, the
    /// lobby sync, the saved launch preference and the AI that flies all agree on them.
    /// </summary>
    public static class AIDifficultyRules
    {
        /// <summary>What a card opens on the first time, and what any unwritten value means.</summary>
        public const AIDifficulty Default = AIDifficulty.Medium;

        /// <summary>
        /// <paramref name="value"/> when it is a real difficulty, otherwise <see cref="Default"/>.
        /// Used on every value that arrives from outside this build's own code - a save file, the
        /// replicated lobby - because both can carry a 0 or a number a future build added.
        /// </summary>
        public static AIDifficulty Resolve(AIDifficulty value) =>
            value is AIDifficulty.Easy or AIDifficulty.Medium or AIDifficulty.Hard or AIDifficulty.Legendary
                ? value : Default;

        /// <summary>The same rule for the int the lobby replicates.</summary>
        public static AIDifficulty Resolve(int value) => Resolve((AIDifficulty)value);

        /// <summary>
        /// Whether the launch panel offers the picker for <paramref name="mode"/>: every game mode
        /// (Yash, 2026-10-26: "Every game mode should have easy, medium, hard AI. That's a generic
        /// rule."). Every AI reads it through one of three handicaps, all from
        /// <c>SkimRaceDifficultySO</c>: the Skim Race pilot's (<c>SkimRaceHandicap</c>), a gate race's
        /// (<c>GateRaceHandicap</c>, every <c>GateRaceController</c> mode), or the platform autopilot's
        /// belief (<c>AIPilotBelief</c>, every other mode). Only <see cref="GameModes.Random"/>, which is
        /// not a mode a card can name, is left out.
        /// </summary>
        public static bool IsOfferedFor(GameModes mode) => mode != GameModes.Random;

        /// <summary>
        /// Whether <paramref name="mode"/>'s AI is given the PLATFORM autopilot's belief
        /// (<c>AIPilotBelief</c>, installed by <c>ServerPlayerVesselInitializerWithAI
        /// .ConfigureAIPilotForMode</c>) when it spawns: every offered mode but the Skim Race, whose
        /// pilot stands the autopilot down. A gate race replaces the belief with its own ring handicap
        /// when it arms its racers (<c>GateRaceController.ArmRacer</c>), so no mistake is made twice;
        /// the modes listed here are the gate races known to the rules, which never take the belief at all.
        /// </summary>
        public static bool UsesPilotBelief(GameModes mode) =>
            IsOfferedFor(mode) && !HasOwnHandicap(mode);

        /// <summary>The modes whose AI carries a handicap of its own (the Skim Race pilot, the Stoat's and
        /// Regatta's gate races). Every other <c>GateRaceController</c> mode drops the belief at arm time.</summary>
        public static bool HasOwnHandicap(GameModes mode) =>
            mode == GameModes.SkimRace || mode == GameModes.Slingshot || mode == GameModes.Warpline
            || mode == GameModes.Regatta || mode == GameModes.BurrowRun;

        /// <summary>The easiest and the hardest difficulty, in the order the row draws them.</summary>
        public const AIDifficulty Easiest = AIDifficulty.Easy;
        public const AIDifficulty Hardest = AIDifficulty.Legendary;

        /// <summary>One step easier or harder, clamped to Easy..Legendary (the gamepad's left/right).</summary>
        public static AIDifficulty Step(AIDifficulty from, int direction)
        {
            int next = (int)Resolve(from) + (direction < 0 ? -1 : direction > 0 ? 1 : 0);
            if (next < (int)Easiest) next = (int)Easiest;
            if (next > (int)Hardest) next = (int)Hardest;
            return (AIDifficulty)next;
        }
    }
}
