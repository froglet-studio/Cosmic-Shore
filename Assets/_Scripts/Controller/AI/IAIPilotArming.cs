namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A mode controller that gives its AI pilots mode steering (a race line, a strike plan) and can give it to ONE
    /// pilot after the start: a human's hull handed to the AI mid-match (<see cref="PilotControl"/>, the Third Eye's
    /// "AI flies" toggle). A mode without it leaves such a hull on the platform autopilot's own seeking.
    /// </summary>
    public interface IAIPilotArming
    {
        /// <summary>Install this mode's AI steering on <paramref name="player"/>'s current hull. Server only.
        /// False when this machine cannot arm it (not the server).</summary>
        bool ArmAIPilot(IPlayer player);
    }
}
