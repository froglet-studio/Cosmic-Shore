using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One thing the racing pilot flies at: a point, the radius the hull has to pass
    /// within, and an id that changes whenever it is a NEW thing to fly at.</summary>
    public struct SkimRaceTarget
    {
        public Vector3 Position;
        public float Radius;
        public int Id;
    }

    /// <summary>
    /// WHAT a <see cref="SkimRacePilot"/> races for - the only part of the pilot that knows which
    /// mode it is in. The driver itself only ever sees a course polyline, a target point with a
    /// radius, and a progress count; this answers those three from the mode's own live state.
    ///
    /// <list type="bullet">
    /// <item><b>Skim Race</b> (<see cref="CrystalTrackObjective"/>): the course is the waypoint
    /// track's ribbon, the target is this domain's crystal, progress is crystals.</item>
    /// <item><b>Regatta</b> (<see cref="RegattaRingObjective"/>): the course is this domain's rail,
    /// the target is the pilot's next ring, progress is gates threaded.</item>
    /// </list>
    ///
    /// One instance per pilot: an objective may hold per-pilot state (the crystal it is locked
    /// on to, a cached scene lookup). Read-only by contract, like the pilot: it observes the
    /// game and never writes it.
    /// </summary>
    public abstract class SkimRaceObjective
    {
        /// <summary>The objective for this match's mode, or null when no racing objective
        /// exists for it (the pilot is then not installed).</summary>
        public static SkimRaceObjective For(GameDataSO gameData)
        {
            if (gameData == null) return null;
            return gameData.GameMode switch
            {
                GameModes.SkimRace => new CrystalTrackObjective(gameData),
                GameModes.Regatta => new RegattaRingObjective(gameData),
                _ => null,
            };
        }

        protected readonly GameDataSO GameData;

        protected SkimRaceObjective(GameDataSO gameData) => GameData = gameData;

        /// <summary>The racing line, from what the game actually laid. False until it exists
        /// (tracks lay over several frames); the pilot retries.</summary>
        public abstract bool TryBuildCourse(IVesselStatus status, out SkimRaceCourse course);

        /// <summary>What to fly at right now. False is a normal transient state (between a
        /// pickup and its respawn, or the race is over); the pilot follows the course.</summary>
        public abstract bool TryGetTarget(IVesselStatus status, Vector3 position, out SkimRaceTarget target);

        /// <summary>This pilot's OWN count - a rise is "I just took one".</summary>
        public abstract int OwnProgress(IVesselStatus status);

        /// <summary>The count the race is scored on for this pilot (Skim Race: the domain's
        /// shared crystals).</summary>
        public abstract int SharedProgress(IVesselStatus status);

        /// <summary>How many are left before the race ends for this pilot.</summary>
        public abstract int Remaining(IVesselStatus status);

        /// <summary>Forget per-race state (a new race on the same pilot).</summary>
        public virtual void Reset() { }

        /// <summary>Whether a race played by hand in the editor records itself
        /// (<see cref="SkimRaceRaceRecorder"/>, which reads Skim Race's crystals).</summary>
        public virtual bool RecordsManualRaces => false;
    }

    /// <summary>Skim Race: the domain's crystal along the waypoint track. Exactly the behaviour
    /// the pilot had before the objective was split out.</summary>
    public sealed class CrystalTrackObjective : SkimRaceObjective
    {
        Crystal _target;
        Crystal _radiusOf;
        float _radius;

        public CrystalTrackObjective(GameDataSO gameData) : base(gameData) { }

        /// <summary>Skim Race team play (<see cref="SkimRaceTeamPlan"/>): the crystal the team plan gives
        /// this pilot this frame, or null for the nearest-crystal rule. The pilot sets it before each
        /// <see cref="TryGetTarget"/>; a lone AI on its team never has one.</summary>
        public Crystal Planned { get; set; }

        public override bool RecordsManualRaces => true;

        public override void Reset() { _target = null; Planned = null; }

        public override bool TryBuildCourse(IVesselStatus status, out SkimRaceCourse course) =>
            SkimRaceCourseSource.TryBuildFromScene(out course);

        public override bool TryGetTarget(IVesselStatus status, Vector3 position, out SkimRaceTarget target)
        {
            // The authoritative active crystal for this domain.
            _target = Planned != null ? Planned : SkimRaceTargetTracker.Select(status.Domain, position, _target);
            target = default;
            if (_target == null) return false;
            target.Position = _target.transform.position;
            target.Radius = CaptureRadius(_target);
            target.Id = _target.GetInstanceID();
            return true;
        }

        public override int OwnProgress(IVesselStatus status)
        {
            var stats = status.Player?.RoundStats;
            return stats != null ? stats.CrystalsCollected : 0;
        }

        public override int SharedProgress(IVesselStatus status)
        {
            int sum = 0;
            var list = GameData.RoundStatsList;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].Domain == status.Domain) sum += list[i].CrystalsCollected;
            return sum;
        }

        public override int Remaining(IVesselStatus status) =>
            Mathf.Max(0, GameData.CrystalTargetCount - SharedProgress(status));

        /// <summary>World radius of the crystal's pickup sphere — what the hull has to touch.</summary>
        float CaptureRadius(Crystal c)
        {
            if (c == _radiusOf) return _radius;
            _radiusOf = c;
            _radius = 0f;
            if (c != null && c.TryGetComponent(out SphereCollider sc))
            {
                var s = sc.transform.lossyScale;
                _radius = sc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            }
            return _radius;
        }
    }
}
