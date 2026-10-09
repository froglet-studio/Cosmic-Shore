using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One difficulty's deliberate mistakes (<see cref="SkimRaceHandicap"/>). The default - both zero -
    /// is no handicap at all, which is what Hard flies.
    /// </summary>
    [System.Serializable]
    public struct SkimRaceHandicapLevel
    {
        /// <summary>Seconds between a new crystal appearing and the pilot noticing it (varies per
        /// crystal, half to one and a half times this).</summary>
        public float ReactionSeconds;

        /// <summary>Chance per crystal that the pilot misjudges its pass, flies over it and has to
        /// turn back for it. 0..1.</summary>
        public float MistakeChance;

        public SkimRaceHandicapLevel(float reactionSeconds, float mistakeChance)
        {
            ReactionSeconds = reactionSeconds;
            MistakeChance = mistakeChance;
        }

        /// <summary>True when this level makes no mistakes - the pilot then carries no handicap.</summary>
        public bool IsNone => ReactionSeconds <= 0f && MistakeChance <= 0f;
    }

    /// <summary>
    /// The deliberate, human-shaped mistakes an Easy or Medium Skim Race AI makes - the lobby's AI
    /// difficulty (<c>AIDifficulty</c>), which is independent of intensity: the same mistakes on every
    /// track, so the hardest AI can race intensity 1 and an easy one intensity 4.
    ///
    /// <para><b>It changes what the driver BELIEVES, never what it does with a belief.</b> The
    /// <see cref="SkimRaceDriver"/> decides exactly as it always does, on a view of the world this
    /// class has edited - so a handicapped pilot flies with the same steering, guards and recovery as
    /// the unhandicapped one, and Hard (no handicap) is the shipped pilot byte for byte. Two kinds of
    /// mistake, both per CRYSTAL rather than per frame, because a person's mistakes are decisions made
    /// a little late or a little wrong, not a shaking hand:</para>
    /// <list type="bullet">
    /// <item><b>Slow reaction.</b> For a moment after a new crystal appears the pilot has not noticed
    /// it: the view carries no target, so it keeps flying the racing line and turns in late. The
    /// moment varies per crystal (<see cref="ReactionSpreadMin"/>..<see cref="ReactionSpreadMax"/>
    /// times <see cref="SkimRaceHandicapLevel.ReactionSeconds"/>).</item>
    /// <item><b>Misjudged crystal.</b> With <see cref="SkimRaceHandicapLevel.MistakeChance"/> per
    /// crystal the pilot believes the crystal sits further off the ribbon than it does - on the
    /// crystal's own side, away from the ribbon, so the mistake can never steer it INTO the track - and
    /// passes over it. Once it is past (or has reached the point it believed in, or
    /// <see cref="MistakeMaxSeconds"/> ran out) the view shows the real crystal, now behind it, and the
    /// driver turns back for it the way it does for any missed crystal.</item>
    /// </list>
    ///
    /// <para>Mistakes are random every race (<see cref="System.Random"/>, seeded by the caller - never
    /// <c>UnityEngine.Random</c>, whose global state the track generator seeds). Pure C#: the offline
    /// simulator runs this exact class (<c>Tools/Build/skimrace_sim_harness</c>), which is where each
    /// difficulty's numbers were tuned (<c>Docs/SKIM_RACE_AI.md</c> §10).</para>
    /// </summary>
    public sealed class SkimRaceHandicap
    {
        /// <summary>A target that moved this far is a new crystal - the driver's own rule
        /// (<c>SkimRaceDriver.UpdateProgress</c>), so both agree on what "new" means.</summary>
        const float NewTargetDistanceSqr = 40f * 40f;

        /// <summary>Per-crystal spread of the reaction time, as a factor of the level's value.</summary>
        public const float ReactionSpreadMin = 0.5f;
        public const float ReactionSpreadMax = 1.5f;

        /// <summary>A misjudged pass aims this many capture radii (plus <see cref="MissMargin"/>) off
        /// the crystal - far enough that the hull passes outside its pickup.</summary>
        const float MissRadii = 2f;
        const float MissMargin = 4f;
        const float FallbackCaptureRadius = 10f;

        /// <summary>A misjudgment never outlasts this, so the pilot always turns back for the crystal.</summary>
        public const float MistakeMaxSeconds = 6f;

        readonly SkimRaceHandicapLevel _level;
        readonly System.Random _rng;

        bool _tracking;
        int _targetId;
        Vector3 _targetPos;
        float _noticeAt;
        bool _misjudged;
        float _misjudgedUntil;
        Vector3 _missOffset;

        public SkimRaceHandicap(SkimRaceHandicapLevel level, int seed)
        {
            _level = level;
            _rng = new System.Random(seed);
        }

        public SkimRaceHandicapLevel Level => _level;

        /// <summary>Crystals misjudged this race (diagnostics).</summary>
        public int Mistakes { get; private set; }

        /// <summary>True while the current crystal has not been noticed yet (diagnostics).</summary>
        public bool Unnoticed { get; private set; }

        /// <summary>True while the current crystal's pass is misjudged (diagnostics).</summary>
        public bool Misjudging => _misjudged;

        /// <summary>Forget the current crystal and the race's count (a new race).</summary>
        public void Reset()
        {
            _tracking = false;
            _misjudged = false;
            Unnoticed = false;
            Mistakes = 0;
        }

        /// <summary>
        /// The observation as this pilot believes it: <paramref name="o"/> unchanged except for the
        /// target, which is hidden while the crystal is unnoticed and displaced while it is misjudged.
        /// </summary>
        public SkimRaceObservation View(in SkimRaceObservation o, SkimRaceCourse course)
        {
            var v = o;
            Unnoticed = false;
            if (!o.HasTarget) return v;

            if (!_tracking || o.TargetId != _targetId || (o.TargetPosition - _targetPos).sqrMagnitude > NewTargetDistanceSqr)
                BeginTarget(o, course);
            _targetPos = o.TargetPosition;

            if (o.RaceTime < _noticeAt)
            {
                Unnoticed = true;
                v.HasTarget = false;
                return v;
            }

            if (_misjudged)
            {
                Vector3 believed = o.TargetPosition + _missOffset;
                float reach = Mathf.Max(4f, o.TargetRadius);
                bool passed = o.HasCourse && o.TargetAheadOnCourse > o.CourseLength * 0.5f;
                bool arrived = (believed - o.Position).sqrMagnitude < reach * reach;
                if (passed || arrived || o.RaceTime >= _misjudgedUntil) _misjudged = false;
                else SetTarget(ref v, believed);
            }
            return v;
        }

        void BeginTarget(in SkimRaceObservation o, SkimRaceCourse course)
        {
            _tracking = true;
            _targetId = o.TargetId;
            _targetPos = o.TargetPosition;

            float spread = Mathf.Lerp(ReactionSpreadMin, ReactionSpreadMax, (float)_rng.NextDouble());
            _noticeAt = o.RaceTime + Mathf.Max(0f, _level.ReactionSeconds) * spread;

            _misjudged = false;
            if (_level.MistakeChance <= 0f || course == null || !o.HasCourse) return;
            if (_rng.NextDouble() >= _level.MistakeChance) return;

            // Over the crystal, on its own side of the ribbon: the believed crystal is further from the
            // ribbon than the real one, so the misjudged line can never be steered into the track.
            float s = course.Wrap(o.CourseProgress + o.TargetAheadOnCourse);
            Vector3 onRibbon = course.Sample(s, out _, out Vector3 normal);
            float side = Vector3.Dot(o.TargetPosition - onRibbon, normal) >= 0f ? 1f : -1f;
            float radius = o.TargetRadius > 0f ? o.TargetRadius : FallbackCaptureRadius;
            _missOffset = normal * (side * (radius * MissRadii + MissMargin));
            _misjudged = true;
            _misjudgedUntil = _noticeAt + MistakeMaxSeconds;
            Mistakes++;
        }

        static void SetTarget(ref SkimRaceObservation v, Vector3 target)
        {
            v.TargetPosition = target;
            v.ToTarget = target - v.Position;
            v.TargetDistance = v.ToTarget.magnitude;
            Vector3 dir = v.TargetDistance > 1e-3f ? v.ToTarget / v.TargetDistance : v.Forward;
            v.TargetLocalDirection = Quaternion.Inverse(v.Rotation) * dir;
            v.TargetAlignment = Vector3.Dot(v.Forward, dir);
        }
    }
}
