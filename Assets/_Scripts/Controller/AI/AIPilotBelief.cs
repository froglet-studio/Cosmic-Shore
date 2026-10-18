using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The lobby AI difficulty for a mode flown on the PLATFORM autopilot (<see cref="AIPilot"/>): the
    /// Skim Race's two deliberate mistakes (<see cref="SkimRaceHandicap"/>, numbers from
    /// <see cref="SkimRaceDifficultySO"/>) applied to whatever point the autopilot wants to reach - a
    /// mode's external target (an Astro League strike), a chased opponent (Broadside), or the crystal its
    /// own seeking picked (Brood Rush). Installed by
    /// <c>ServerPlayerVesselInitializerWithAI.ConfigureAIPilotForMode</c> on the modes
    /// <c>AIDifficultyRules.UsesPilotBelief</c> names; Hard installs none.
    ///
    /// <para><b>It changes what the pilot BELIEVES, never what it does with a belief.</b> AIPilot steers,
    /// breaks orbits, boosts and fires exactly as it always does, on the point this class hands back, so
    /// Hard (no belief) is the shipped pilot byte for byte.</para>
    /// <list type="bullet">
    /// <item><b>Late notice.</b> A target is perceived when it is NOTICED, and noticed again only
    /// <see cref="SkimRaceHandicapLevel.ReactionSeconds"/> later (x 0.5-1.5 per look). A new target (a
    /// jump of more than <see cref="NewTargetDistance"/>, the Skim Race's own rule) is not seen until
    /// the next look - the pilot keeps flying for the old one - and a moving target (a ball, a rival) is
    /// flown to where it was last seen. The first target is seen at once.</item>
    /// <item><b>Misjudged target.</b> With <see cref="SkimRaceHandicapLevel.MistakeChance"/> per decision -
    /// each new target, and every <see cref="DecisionSeconds"/> on a target that only moves - the pilot
    /// believes it sits <see cref="MissDistance"/> off to one side of its line of sight and flies there.
    /// Once it reaches or passes that point, or <see cref="SkimRaceHandicap.MistakeMaxSeconds"/> runs
    /// out, it sees the truth and turns for it.</item>
    /// </list>
    ///
    /// <para>Pure C#, seeded by the caller with <see cref="System.Random"/> (never
    /// <c>UnityEngine.Random</c>, whose global state the generators seed). No allocation per call.</para>
    /// </summary>
    public sealed class AIPilotBelief
    {
        /// <summary>A target that moved this far between two samples is a NEW target (the Skim Race
        /// driver's rule, <see cref="SkimRaceHandicap"/>).</summary>
        public const float NewTargetDistance = 40f;

        /// <summary>On a target that only moves, a fresh misjudgment is rolled this often.</summary>
        public const float DecisionSeconds = 3f;

        /// <summary>How far off its line of sight a misjudged target is believed to be.</summary>
        public const float MissDistance = 30f;

        /// <summary>Closer than this to the believed point, the pilot sees the real target.</summary>
        public const float SeeTruthDistance = 12f;

        readonly SkimRaceHandicapLevel _level;
        readonly System.Random _rng;

        bool _tracking;
        Vector3 _lastTruth;
        Vector3 _seen;
        float _nextLook;
        float _nextDecision;
        bool _pendingNewTarget;

        bool _misjudged;
        Vector3 _missOffset;
        float _misjudgedUntil;

        public AIPilotBelief(SkimRaceHandicapLevel level, int seed)
        {
            _level = level;
            _rng = new System.Random(seed);
        }

        public SkimRaceHandicapLevel Level => _level;

        /// <summary>Targets misjudged since the last <see cref="Reset"/> (diagnostics).</summary>
        public int Mistakes { get; private set; }

        /// <summary>True while a new target has not been noticed yet (diagnostics).</summary>
        public bool Unnoticed => _pendingNewTarget;

        /// <summary>True while the target is misjudged (diagnostics).</summary>
        public bool Misjudging => _misjudged;

        /// <summary>Forget the current target and the count.</summary>
        public void Reset()
        {
            _tracking = false;
            _pendingNewTarget = false;
            _misjudged = false;
            Mistakes = 0;
        }

        /// <summary>
        /// Where the pilot believes <paramref name="truth"/> is, at time <paramref name="now"/>, flying from
        /// <paramref name="self"/> along <paramref name="forward"/>.
        /// </summary>
        public Vector3 Believe(Vector3 truth, Vector3 self, Vector3 forward, float now)
        {
            if (_level.IsNone) return truth;

            if (!_tracking)
            {
                _tracking = true;
                _lastTruth = truth;
                Look(truth, now);
                Decide(truth, self, now);
            }

            if ((truth - _lastTruth).sqrMagnitude > NewTargetDistance * NewTargetDistance)
                _pendingNewTarget = true;
            _lastTruth = truth;

            if (now >= _nextLook)
            {
                Look(truth, now);
                if (_pendingNewTarget)
                {
                    _pendingNewTarget = false;
                    Decide(truth, self, now);
                }
            }

            if (!_misjudged && !_pendingNewTarget && now >= _nextDecision)
                Decide(truth, self, now);

            if (_misjudged)
            {
                Vector3 believed = _seen + _missOffset;
                Vector3 toBelieved = believed - self;
                bool arrived = toBelieved.sqrMagnitude < SeeTruthDistance * SeeTruthDistance;
                bool passed = Vector3.Dot(toBelieved, forward) < 0f;
                if (arrived || passed || now >= _misjudgedUntil) _misjudged = false;
                else return believed;
            }
            return _seen;
        }

        void Look(Vector3 truth, float now)
        {
            _seen = truth;
            float spread = Mathf.Lerp(SkimRaceHandicap.ReactionSpreadMin, SkimRaceHandicap.ReactionSpreadMax,
                                      (float)_rng.NextDouble());
            _nextLook = now + Mathf.Max(0f, _level.ReactionSeconds) * spread;
        }

        void Decide(Vector3 truth, Vector3 self, float now)
        {
            _nextDecision = now + DecisionSeconds;
            _misjudged = false;
            if (_level.MistakeChance <= 0f || _rng.NextDouble() >= _level.MistakeChance) return;

            Vector3 sight = truth - self;
            if (sight.sqrMagnitude < 1e-4f) return;
            _missOffset = SideOffset(sight, (float)(_rng.NextDouble() * Mathf.PI * 2.0), MissDistance);
            _misjudged = true;
            _misjudgedUntil = now + SkimRaceHandicap.MistakeMaxSeconds;
            Mistakes++;
        }

        /// <summary>A vector of length <paramref name="distance"/> perpendicular to <paramref name="sight"/>,
        /// at <paramref name="angle"/> radians round it.</summary>
        public static Vector3 SideOffset(Vector3 sight, float angle, float distance)
        {
            Vector3 axis = sight.normalized;
            Vector3 helper = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 u = Vector3.Cross(axis, helper).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            return (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * distance;
        }
    }
}
