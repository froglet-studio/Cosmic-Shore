namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The local player's end-game cinematic reveal payload - what the score-reveal
    /// animation shows. Produced once by the mode's <see cref="ScoringRuleSO"/> so the
    /// reveal and the scoreboard read from the same computed deficit/result and cannot
    /// disagree (this is the read-model side of BUGS.md B2).
    /// </summary>
    public readonly struct ScoreReveal
    {
        /// <summary>"VICTORY" / "DEFEAT".</summary>
        public readonly string Header;

        /// <summary>Caption under the header, e.g. "RACE TIME", "CRYSTALS LEFT", "WON BY 3 JOUSTS".</summary>
        public readonly string Label;

        /// <summary>The number the reveal counts up/down to.</summary>
        public readonly int Value;

        /// <summary>When true, <see cref="Value"/> is seconds and is shown as MM:SS:CS.</summary>
        public readonly bool FormatAsTime;

        public ScoreReveal(string header, string label, int value, bool formatAsTime)
        {
            Header = header;
            Label = label;
            Value = value;
            FormatAsTime = formatAsTime;
        }
    }
}
