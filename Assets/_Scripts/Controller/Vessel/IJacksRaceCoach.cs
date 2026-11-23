namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What a race tells an AI Jacks between cycles (<see cref="JacksExecutor.RaceCoach"/>). Asked when the hull is flying,
    /// idle, and its seat swap is ready: answer a cycle to herd it now, or false to keep flying (the mode's aim steers the
    /// flight, for instance to roll the board onto the next rings).
    /// </summary>
    public interface IJacksRaceCoach
    {
        bool TryNextCycle(JacksExecutor jacks, out JacksRacePlan plan);
    }
}
