namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A race's coach for an AI Orca helm (Breach, <c>Controller/Arcade/BREACH.md</c>): whether the helm holds its burn.
    /// The shipped AI helm never burns the boost tank, which is the helm's Time card and the whole of a Time race. The burn
    /// itself is the shipped one: the coach only says when, through the same tank the helm's right trigger spends.
    /// </summary>
    public interface IOrcaRaceCoach
    {
        /// <summary>True: the AI helm holds the burn this frame (the tank decides whether it burns).</summary>
        bool HelmBurns(OrcaCrew crew);
    }
}
