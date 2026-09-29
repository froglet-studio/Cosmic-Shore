using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Payload raised on the game toast channel. Carries the SITUATION plus raw arguments
    /// (player names etc.) - never display text. The <c>GameToastController</c> resolves the
    /// situation against the current mode's <c>GameToastConfigSO</c> and formats the final
    /// message, so all copy lives in config assets instead of call sites.
    ///
    /// Argument contract per situation (indexes into <see cref="Args"/>):
    ///   PlayerJoined / PlayerReady / PlayerDisconnected: [0]=player name
    ///   Joust:            [0]=scorer name, [1]=target name (points are appended at display time)
    ///   Overtake:         [0]=overtaker name, [1]=overtaken name
    ///   NewRaceLeader:    [0]=leader name
    ///   ComebackActivated:[0]=player name
    ///   BroodWaveScored:  [0]=domain name, [1]=brood sum, [2]=wave target
    /// </summary>
    [System.Serializable]
    public struct GameToastData
    {
        public GameToastSituation Situation;

        /// <summary>Domain of the primary subject (colors the line / first name).</summary>
        public Domains PrimaryDomain;

        /// <summary>Domain of the secondary subject (e.g. the jousted/overtaken player).</summary>
        public Domains SecondaryDomain;

        public string[] Args;

        public GameToastData(GameToastSituation situation, Domains primaryDomain,
            Domains secondaryDomain, params string[] args)
        {
            Situation = situation;
            PrimaryDomain = primaryDomain;
            SecondaryDomain = secondaryDomain;
            Args = args ?? System.Array.Empty<string>();
        }
    }
}
