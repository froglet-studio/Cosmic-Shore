namespace CosmicShore.Data
{
    /// <summary>
    /// What KIND of game a mode is, said in elements.
    ///
    /// <para>A card's genre badge is a promise about what you will be doing: a <b>race</b> is
    /// TIME, <b>making or taking mass</b> is MASS, <b>destroying or reducing mass</b> is SPACE,
    /// and <b>working other vessels or the wildlife over</b> is CHARGE. Four categories, one or
    /// two petals, read at a glance off a grid of twenty-odd cards.</para>
    ///
    /// <para><b>It is keyed on the MODE, with the METRIC as the fallback.</b> A mode's genre is
    /// usually predictable from <see cref="ScoringMetric"/> - a mode scored on prisms destroyed
    /// IS a destruction mode - but it is not the SAME fact, and the two places they part company
    /// are the reason this table exists. Tollway and Scarab Scramble are both scored on
    /// <see cref="ScoringMetric.Goals"/> and are not the same kind of game: one is a ball race,
    /// the other is acquisition - a toll raises a monument, and the arena is built out of the
    /// scoring. Scurry is scored on <see cref="ScoringMetric.Crystals"/>, which reads as a race
    /// and is a gather. So the explicit rows below win, and any mode with no row falls through to
    /// <see cref="TryElementForMetric"/>, which is what keeps a new mode from drawing nothing
    /// merely because nobody has been asked about it yet.</para>
    ///
    /// <para><b>A genre can be TWO elements.</b> Brood Rush contests the nucleus by laying claim
    /// mass inside it and tearing the other side's out - MASS and SPACE - and a card that could
    /// only say one of those would be advertising half the mode. Two is the ceiling: a badge that
    /// needs three elements is a mode whose genre nobody can state, which is a design question
    /// rather than a UI one.</para>
    ///
    /// <para><b>It is a RULE, so it lives in code rather than in an authored table.</b> An
    /// authored row can disagree with the behaviour under it; this cannot. The same argument
    /// <c>ToyDefinitionSO.Category</c> is abstract-and-in-code for.</para>
    ///
    /// <para><b>An unclassified mode draws NO petal</b>, which is the honest state and is what
    /// tells you a newly-added metric has not been given a genre yet. <c>ModeGenreTests</c> makes
    /// that loud rather than silent: it sweeps every <see cref="ScoringMetric"/> member and fails
    /// on any that does not classify, and pins every shipped card's answer.</para>
    ///
    /// <para>Pure and Unity-free, so it compiles into the extracted <c>CosmicShore.Data</c> leaf
    /// assembly beside the enums it relates.</para>
    /// </summary>
    public static class ModeGenre
    {
        /// <summary>
        /// The one or two elements standing for the kind of game a mode is. The mode's own row
        /// wins; a mode with no row falls back to what its <paramref name="metric"/> implies.
        /// Returns false - and leaves both elements <see cref="Element.None"/> - when neither
        /// answers, so the caller draws nothing rather than the wrong thing. A null
        /// <paramref name="metric"/> means "this mode has no metric to read" (Maelstrom, which
        /// draws OTHER modes), and is not the same as the enum's zero.
        /// <paramref name="secondary"/> is <see cref="Element.None"/> for the usual single-genre
        /// card.
        /// </summary>
        public static bool TryElementsFor(GameModes mode, ScoringMetric? metric,
                                          out Element primary, out Element secondary)
        {
            secondary = Element.None;

            switch (mode)
            {
                // ACQUISITION, not a race. Both of these are scored on a metric that reads as a
                // race (Crystals, Goals) and neither is one: Scurry is a gather, and a Tollway
                // toll is paid in mass - every ring that pays raises a 255-prism monument on the
                // spot, so the arena ends the match built out of the scoring.
                case GameModes.Scurry:
                case GameModes.Tollway:
                    primary = Element.Mass;
                    return true;

                // BOTH. The nucleus claim is won by laying your own mass inside it and taken by
                // tearing the other side's out, so the mode is a making game and a destroying
                // game at once and the card says both.
                case GameModes.BroodRush:
                    primary = Element.Mass;
                    secondary = Element.Space;
                    return true;
            }

            if (metric.HasValue) return TryElementForMetric(metric.Value, out primary);

            primary = Element.None;
            return false;
        }

        /// <summary>
        /// The fallback: the element standing for the kind of game a METRIC describes, for a mode
        /// with no row of its own. Returns false for a metric with no genre yet.
        /// </summary>
        public static bool TryElementForMetric(ScoringMetric metric, out Element element)
        {
            switch (metric)
            {
                // RACE - get there, thread them, put them through, before anyone else does.
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
