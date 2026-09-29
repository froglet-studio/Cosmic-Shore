using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Everything a gate race's course is a function of, in one value. A course source reads
    /// nothing else - no scene, no injected data, no live cell - which is what lets the match
    /// controller and the arcade card's preview window build the SAME course from the same
    /// inputs.
    /// </summary>
    public readonly struct RaceCourseRequest
    {
        /// <summary>The course seed. Ignored by a mode whose course is a property of its ARENA
        /// (Skein, Regatta), which takes its seed off the arena prefab instead.</summary>
        public readonly int Seed;

        /// <summary>1..4. The same number the match reads off <c>GameDataSO.SelectedIntensity</c>.</summary>
        public readonly int Intensity;

        /// <summary>The authored race length - <see cref="RaceCourseSource.AuthoredGateTarget"/>.</summary>
        public readonly int GateCount;

        /// <summary>The shell the course is laid inside, cell-local, from
        /// <see cref="RaceCourseSource.ResolveShell"/>.</summary>
        public readonly float Inner;
        public readonly float Outer;

        /// <summary>The cell's EXPECTED config - the arena a mode like Skein hangs its rings on.
        /// Null is legal; a source that needs it reports why and returns nothing.</summary>
        public readonly CellConfigDataSO Config;

        public RaceCourseRequest(int seed, int intensity, int gateCount, float inner, float outer,
                                 CellConfigDataSO config)
        {
            Seed = seed;
            Intensity = Mathf.Clamp(intensity, 1, 4);
            GateCount = gateCount;
            Inner = inner;
            Outer = outer;
            Config = config;
        }
    }

    /// <summary>
    /// ONE definition of a gate race's course, shared by the match and by its preview.
    ///
    /// <para><b>Why this was pulled out of the controller.</b> <c>GateRaceController.BuildCourse</c>
    /// used to be the only way to ask a mode for its course, and it lived on a NetworkBehaviour
    /// that exists only inside the mode's own scene. So the arcade card's preview - which stands
    /// the mode's cell as a satellite in Menu_Main and never loads that scene - had no way to lay
    /// a single ring, and every racing card previewed as open water with nothing to race
    /// (Docs/ModePreview/TRAINING_PLAN.md §7). The generators behind it were already pure; only
    /// the recipe that fed them was trapped.</para>
    ///
    /// <para><b>Everything the course reads is on this object, and nothing else.</b> A source is
    /// constructed with its knobs and asked with a <see cref="RaceCourseRequest"/>. The match's
    /// controller builds one from its serialized fields; <see cref="For"/> builds one from the
    /// shipped defaults, which is what the preview uses. Those two agree today because every
    /// gate-race scene authors exactly the defaults, and <c>RaceCourseSourceTests</c> reads the
    /// scene files to keep it that way - a scene retune that forgot the preview would fail there
    /// rather than ship a preview course that is subtly not the match's.</para>
    ///
    /// <para><b>A pure move.</b> Every override below is the body its controller's
    /// <c>BuildCourse</c> had, verbatim, with <c>Intensity</c> read off the request instead of
    /// off injected game data - so the same seed gives the same course it always did.</para>
    /// </summary>
    public abstract class RaceCourseSource
    {
        /// <summary>The mode this is the course of.</summary>
        public abstract GameModes Mode { get; }

        /// <summary>Mode name for log lines.</summary>
        public virtual string ModeName => Mode.ToString();

        // ── The shell (shared by every mode) ─────────────────────────────

        /// <summary>Course shell, outer edge. 0.9 x the CapsuleMembrane's authored radius (1200).</summary>
        public float OuterRadius = DefaultOuterRadius;

        /// <summary>Inner edge when the cell's nucleus cannot be measured.</summary>
        public float InnerRadiusFallback = DefaultInnerRadiusFallback;

        /// <summary>How far outside the nucleus the inner shell sits.</summary>
        public float InnerRadiusNucleusFactor = DefaultInnerRadiusNucleusFactor;

        public const float DefaultOuterRadius = 1080f;
        public const float DefaultInnerRadiusFallback = 480f;
        public const float DefaultInnerRadiusNucleusFactor = 1.22f;

        /// <summary>
        /// The shell, from the cell's nucleus radius (0 when unknown). The rule the controller
        /// always used: inner = nucleus x factor, else the fallback; outer never closer than
        /// 120 units to the inner edge.
        /// </summary>
        public void ResolveShell(float nucleusRadius, out float inner, out float outer)
        {
            inner = nucleusRadius > 0f ? nucleusRadius * InnerRadiusNucleusFactor : InnerRadiusFallback;
            outer = Mathf.Max(inner + 120f, OuterRadius);
        }

        // ── Race shape ───────────────────────────────────────────────────

        /// <summary>Laps of the ring set that make one race (1 = an open chain).</summary>
        public virtual int LapsPerRace(int intensity) => 1;

        /// <summary>Rings at the front threaded once and never again (a start gate).</summary>
        public virtual int LeadInGates => 0;

        /// <summary>The authored race length at this intensity, before the course has had its say.</summary>
        public abstract int AuthoredGateTarget(int intensity);

        /// <summary>Threadings that finish a race of <paramref name="ringCount"/> rings.</summary>
        public int RaceLengthFor(int ringCount, int intensity) =>
            GateRaceController.RaceLengthFor(ringCount, LeadInGates, LapsPerRace(intensity));

        /// <summary>The ring a pilot on <paramref name="threaded"/> threadings flies next.</summary>
        public int RingIndexFor(int threaded, int ringCount, int intensity) =>
            GateRaceController.RingIndexFor(threaded, ringCount, LeadInGates, LapsPerRace(intensity));

        /// <summary>
        /// The course, CELL-LOCAL, or null with <paramref name="failure"/> saying why in one
        /// sentence. May be retried every frame by the match (a scene-built arena answers a
        /// second late), so it must never log an error itself.
        /// </summary>
        public abstract List<RaceGate> Build(in RaceCourseRequest request, out string failure);

        // ── The shipped defaults, by mode ────────────────────────────────

        /// <summary>
        /// The course source a mode ships with, or null for a mode that is not a gate race. What
        /// the preview uses - it has no scene to read knobs from.
        /// </summary>
        public static RaceCourseSource For(GameModes mode) => mode switch
        {
            GameModes.Switchback => new SwitchbackCourseSource(),
            GameModes.Headlong   => new HeadlongCourseSource(),
            GameModes.Redline    => new RedlineCourseSource(),
            GameModes.Breakwater => new BreakwaterCourseSource(),
            GameModes.Skein      => new SkeinCourseSource(),
            GameModes.Regatta    => new RegattaCourseSource(),
            GameModes.Waystation => new WaystationCourseSource(),
            _ => null,
        };

        /// <summary>Every mode <see cref="For"/> answers - the tests walk this.</summary>
        public static readonly GameModes[] GateRaceModes =
        {
            GameModes.Switchback, GameModes.Headlong, GameModes.Redline, GameModes.Breakwater,
            GameModes.Skein, GameModes.Regatta, GameModes.Waystation,
        };
    }
}
