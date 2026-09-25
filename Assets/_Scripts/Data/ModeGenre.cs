namespace CosmicShore.Data
{
    /// <summary>
    /// What KIND of game a mode is, said in one element.
    ///
    /// <para>A card's genre badge is a promise about what you will be doing: a <b>race</b> is
    /// TIME, <b>making mass</b> is MASS, <b>destroying or reducing mass</b> is SPACE, and
    /// <b>working other vessels or the wildlife over</b> is CHARGE. Four categories, one petal,
    /// read at a glance off a grid of twenty-odd cards.</para>
    ///
    /// <para><b>It is keyed on the METRIC, never on the game mode</b> - the same rule
    /// <c>ObjectiveIconSetSO</c> is built on, and for the
    /// same reason. <see cref="ScoringMetric"/> is already the platform's single answer to
    /// "what is this mode scored on", and a mode's genre is not a separate fact from that: a
    /// mode scored on prisms destroyed IS a destruction mode. So a new mode that picks an
    /// existing metric gets its badge for free, and a per-mode override would re-open exactly
    /// the divergence <see cref="ScoringMetric"/> exists to close - a card could then advertise
    /// a genre its own end condition contradicts.</para>
    ///
    /// <para><b>It is a RULE, so it lives in code rather than in an authored table.</b> An
    /// authored row can disagree with the behaviour under it; this cannot. The same argument
    /// <c>ToyDefinitionSO.Category</c> is abstract-and-in-code for.</para>
    ///
    /// <para><b>An unclassified metric draws NO petal</b>, which is the honest state and is what
    /// tells you a newly-added metric has not been given a genre yet.
    /// <c>ModeGenreTests</c> makes that loud rather than silent: it sweeps every
    /// <see cref="ScoringMetric"/> member and fails on any that does not classify.</para>
    ///
    /// <para>Pure and Unity-free, so it compiles into the extracted <c>CosmicShore.Data</c> leaf
    /// assembly beside the two enums it relates.</para>
    /// </summary>
    public static class ModeGenre
    {
        /// <summary>
        /// The element standing for the kind of game a metric describes. Returns false for a
        /// metric with no genre yet - the caller draws nothing rather than the wrong thing.
        /// </summary>
        public static bool TryElementFor(ScoringMetric metric, out Element element)
        {
            switch (metric)
            {
                // RACE - get there, collect them, thread them, put them through, before anyone
                // else does. Crystal counts, goals and threaded gates are all a race to N.
                case ScoringMetric.Crystals:
                case ScoringMetric.OmniCrystals:
                case ScoringMetric.ElementalCrystals:
                case ScoringMetric.Goals:
                case ScoringMetric.SwitchesThreaded:
                    element = Element.Time;
                    return true;

                // MAKING MASS - the score is mass you have put on the board and still hold.
                // PrismsStolen belongs here rather than with destruction because nothing is
                // removed from the arena to score it: mass changes hands and your holdings grow.
                case ScoringMetric.PrismsRemaining:
                case ScoringMetric.PrismsStolen:
                    element = Element.Mass;
                    return true;

                // DESTRUCTION - the score is mass taken off the board, whether it is a prism, a
                // volume of one, or a creature's body.
                case ScoringMetric.PrismsDestroyed:
                case ScoringMetric.VolumeDestroyed:
                case ScoringMetric.LifeformsKilled:
                    element = Element.Space;
                    return true;

                // WORKING PILOTS OVER - the score comes off another vessel: a joust, a bullet, a
                // rocket, a debuff. The one family whose subject is a pilot rather than mass.
                case ScoringMetric.Jousts:
                case ScoringMetric.CombatPoints:
                    element = Element.Charge;
                    return true;

                default:
                    element = Element.None;
                    return false;
            }
        }
    }
}
