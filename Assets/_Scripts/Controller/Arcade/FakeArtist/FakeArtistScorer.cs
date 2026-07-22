using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Pure vote-tally scoring for a Fake Artist round (server-side; unit-tested).
    /// Players are addressed by roster index (0..playerCount-1).
    ///
    /// Rules (all values config-driven via <see cref="Config"/>):
    /// - Every eligible voter (everyone but the fake artist) answered two questions:
    ///   the subject and the accused. Correct subject: +CorrectSubjectPoints.
    ///   Correct accusation: +CorrectImposterPoints. Unanswered (-1) scores nothing.
    /// - Any player accused by AT LEAST ONE voter takes GuessedPenalty once -
    ///   imposter or not ("a negative point if they are guessed").
    /// - The fake artist earns ImposterReward every round, caught or not (a caught
    ///   imposter still nets ImposterReward + GuessedPenalty).
    /// </summary>
    public static class FakeArtistScorer
    {
        public readonly struct Config
        {
            public readonly int CorrectSubjectPoints;
            public readonly int CorrectImposterPoints;
            public readonly int GuessedPenalty;
            public readonly int ImposterReward;

            public Config(int correctSubjectPoints, int correctImposterPoints, int guessedPenalty, int imposterReward)
            {
                CorrectSubjectPoints = correctSubjectPoints;
                CorrectImposterPoints = correctImposterPoints;
                GuessedPenalty = guessedPenalty;
                ImposterReward = imposterReward;
            }
        }

        /// <summary>One voter's answers; -1 means no answer for that question.</summary>
        public readonly struct Answers
        {
            public readonly int SubjectChoice;   // index into the round's subject options
            public readonly int AccusedIndex;    // roster index of the accused player

            public Answers(int subjectChoice, int accusedIndex)
            {
                SubjectChoice = subjectChoice;
                AccusedIndex = accusedIndex;
            }
        }

        /// <summary>
        /// Tallies one round. <paramref name="answers"/> is keyed by voter roster index;
        /// the fake artist's entry (if present) is ignored. Returns per-player point
        /// deltas indexed by roster index.
        /// </summary>
        public static int[] ScoreRound(
            int playerCount,
            int imposterIndex,
            int correctSubjectChoice,
            IReadOnlyDictionary<int, Answers> answers,
            Config config)
        {
            var deltas = new int[playerCount];
            if (playerCount <= 0) return deltas;

            var accused = new bool[playerCount];

            if (answers != null)
            {
                foreach (var kvp in answers)
                {
                    int voter = kvp.Key;
                    if (voter < 0 || voter >= playerCount) continue;
                    if (voter == imposterIndex) continue; // the fake artist doesn't vote

                    var a = kvp.Value;
                    if (a.SubjectChoice >= 0 && a.SubjectChoice == correctSubjectChoice)
                        deltas[voter] += config.CorrectSubjectPoints;

                    if (a.AccusedIndex >= 0 && a.AccusedIndex < playerCount && a.AccusedIndex != voter)
                    {
                        accused[a.AccusedIndex] = true;
                        if (a.AccusedIndex == imposterIndex)
                            deltas[voter] += config.CorrectImposterPoints;
                    }
                }
            }

            for (int i = 0; i < playerCount; i++)
            {
                if (accused[i])
                    deltas[i] += config.GuessedPenalty;
            }

            if (imposterIndex >= 0 && imposterIndex < playerCount)
                deltas[imposterIndex] += config.ImposterReward;

            return deltas;
        }
    }
}
