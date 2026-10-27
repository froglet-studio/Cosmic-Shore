using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The prototyped courses of the time races cut from Slingshot's circuit - the Stoat's <b>Slingshot</b>, the Meerkat's
    /// <b>Warpline</b> and <b>Burrow Run</b> (Yash, 2026-10-27: "create prototypes with various configuration options that I
    /// can click and see and also save ... inside Unity directly"). Authored ONLY through
    /// <c>FrogletTools &gt; Game Modes &gt; Time Race Prototyper</c> (<c>Docs/TIME_RACE_PROTOTYPER.md</c>), loaded from
    /// <c>Resources/TimeRaceCourseConfig</c>.
    ///
    /// <para><b>Off is the shipped race.</b> A mode with no entry, or whose entry has <c>useThisCourse</c> off, flies
    /// exactly <see cref="SlingshotCourse.ForIntensity"/> and its end-condition target, as before this asset existed. On,
    /// its laps, rings a lap and each intensity's circuit numbers replace the shipped ones. The server builds the course and
    /// broadcasts it (<see cref="GateRaceController"/>), so only the host's copy decides a match.</para>
    ///
    /// <para><b>Presets</b> are named snapshots of a mode's course (laps, rings and the four rungs) that the window saves,
    /// loads and deletes; only the live values race.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "TimeRaceCourseConfig",
        menuName = "ScriptableObjects/" + nameof(TimeRaceCourseConfigSO))]
    public class TimeRaceCourseConfigSO : ScriptableObject
    {
        /// <summary>Resources path the course sources load this from (Assets/Resources/TimeRaceCourseConfig.asset).</summary>
        public const string ResourcePath = "TimeRaceCourseConfig";

        /// <summary>The races this asset can reshape: every course cut by <see cref="SlingshotCourse.ForIntensity"/>.</summary>
        public static readonly GameModes[] Modes = { GameModes.Slingshot, GameModes.Warpline, GameModes.BurrowRun };

        [Tooltip("One entry per prototyped race. A race with no entry, or with Use This Course off, flies its shipped course.")]
        [SerializeField] List<TimeRaceCourse> courses = new();

        public IReadOnlyList<TimeRaceCourse> Courses => courses;

        static TimeRaceCourseConfigSO _instance;
        static bool _loaded;

        /// <summary>The asset in Resources, or null when none was saved (every race then flies its shipped course).</summary>
        public static TimeRaceCourseConfigSO Instance
        {
            get
            {
                if (!_loaded)
                {
                    _instance = Resources.Load<TimeRaceCourseConfigSO>(ResourcePath);
                    _loaded = true;
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _loaded = false;
        }

        /// <summary>The entry for <paramref name="mode"/>, or null.</summary>
        public TimeRaceCourse Find(GameModes mode)
        {
            for (int i = 0; i < courses.Count; i++)
                if (courses[i] != null && courses[i].Mode == mode) return courses[i];
            return null;
        }

        /// <summary>The entry for <paramref name="mode"/>, made from the shipped race when there is none (the window's).</summary>
        public TimeRaceCourse GetOrAdd(GameModes mode, int shippedLaps, int shippedRingsPerLap)
        {
            var found = Find(mode);
            if (found != null) return found;
            var made = TimeRaceCourse.Shipped(mode, shippedLaps, shippedRingsPerLap);
            courses.Add(made);
            return made;
        }

        /// <summary>The entry that races for <paramref name="mode"/> now: on, and with all four rungs.</summary>
        static TimeRaceCourse Active(GameModes mode)
        {
            var cfg = Instance;
            var entry = cfg ? cfg.Find(mode) : null;
            return entry is { UseThisCourse: true } && entry.HasRungs ? entry : null;
        }

        /// <summary>
        /// The circuit <paramref name="mode"/> races at <paramref name="intensity"/>: Slingshot's shipped cut, with the
        /// prototyped rung written over it when <paramref name="useConfig"/> and the mode's entry is on.
        /// </summary>
        public static HeadlongCircuitSettings Circuit(GameModes mode, int intensity, bool useConfig = true)
        {
            var settings = SlingshotCourse.ForIntensity(intensity);
            var entry = useConfig ? Active(mode) : null;
            return entry != null ? entry.Rung(intensity).ApplyTo(settings) : settings;
        }

        /// <summary>Laps of a race: the prototyped count when the mode's entry is on, else <paramref name="shipped"/>.</summary>
        public static int LapsFor(GameModes mode, int shipped)
        {
            var entry = Active(mode);
            return Mathf.Max(1, entry != null ? entry.Laps : shipped);
        }

        /// <summary>The race length in gate threadings: laps x rings when the mode's entry is on, else <paramref name="shipped"/>.</summary>
        public static int GateTargetFor(GameModes mode, int shipped)
        {
            var entry = Active(mode);
            return entry != null ? entry.Laps * entry.RingsPerLap : shipped;
        }
    }

    /// <summary>One race's prototyped course: laps, rings a lap, and one circuit per intensity, plus its saved presets.</summary>
    [Serializable]
    public class TimeRaceCourse
    {
        [Tooltip("The race this course is for.")]
        [SerializeField] GameModes mode;

        [Tooltip("On: the race flies the laps, rings and circuits below. Off: it flies its shipped course.")]
        [SerializeField] bool useThisCourse;

        [Tooltip("Laps of the ring set that make one race.")]
        [SerializeField, Min(1)] int laps = 2;

        [Tooltip("Rings in one lap. The race is laps x rings threadings long.")]
        [SerializeField, Range(3, 16)] int ringsPerLap = 8;

        [Tooltip("The circuit at intensities 1 to 4.")]
        [SerializeField] TimeRaceCourseRung[] rungs = new TimeRaceCourseRung[4];

        [Tooltip("Named snapshots saved from the Time Race Prototyper. Only the live values above race.")]
        [SerializeField] List<TimeRaceCoursePreset> presets = new();

        [Tooltip("The preset last saved or loaded, for the window's label.")]
        [SerializeField] string activePreset = string.Empty;

        public GameModes Mode => mode;
        public bool UseThisCourse { get => useThisCourse; set => useThisCourse = value; }
        public int Laps => laps;
        public int RingsPerLap => ringsPerLap;
        public IReadOnlyList<TimeRaceCoursePreset> Presets => presets;
        public string ActivePreset => activePreset;
        public bool HasRungs => rungs is { Length: 4 } && Array.TrueForAll(rungs, r => r != null);

        /// <summary>The rung at <paramref name="intensity"/> (1..4).</summary>
        public TimeRaceCourseRung Rung(int intensity) => rungs[Mathf.Clamp(intensity, 1, 4) - 1];

        /// <summary>A course that is the shipped race, number for number, and off.</summary>
        public static TimeRaceCourse Shipped(GameModes mode, int laps, int ringsPerLap)
        {
            var c = new TimeRaceCourse { mode = mode };
            c.ResetToShipped(laps, ringsPerLap);
            return c;
        }

        /// <summary>Every number back to the shipped race (the toggle and the presets are kept).</summary>
        public void ResetToShipped(int shippedLaps, int shippedRingsPerLap)
        {
            laps = Mathf.Max(1, shippedLaps);
            ringsPerLap = Mathf.Clamp(shippedRingsPerLap, 3, 16);
            rungs = new TimeRaceCourseRung[4];
            for (int i = 0; i < 4; i++)
                rungs[i] = TimeRaceCourseRung.From(SlingshotCourse.ForIntensity(i + 1));
        }

        /// <summary>One rung back to the shipped circuit.</summary>
        public void ResetRungToShipped(int intensity) =>
            rungs[Mathf.Clamp(intensity, 1, 4) - 1] = TimeRaceCourseRung.From(SlingshotCourse.ForIntensity(intensity));

        /// <summary>Save the live values as <paramref name="name"/>, replacing a preset of that name.</summary>
        public void SavePreset(string name, string note)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            presets.RemoveAll(p => p != null && p.Name == name);
            presets.Add(new TimeRaceCoursePreset(name, note, laps, ringsPerLap, rungs, DateTime.Now.ToString("yyyy-MM-dd HH:mm")));
            activePreset = name;
        }

        /// <summary>Make the preset named <paramref name="name"/> the live values. False when there is none.</summary>
        public bool LoadPreset(string name)
        {
            var p = presets.Find(x => x != null && x.Name == name);
            if (p == null || !p.HasRungs) return false;
            laps = p.Laps;
            ringsPerLap = p.RingsPerLap;
            rungs = p.CopyRungs();
            activePreset = name;
            return true;
        }

        /// <summary>Remove the preset named <paramref name="name"/>.</summary>
        public void DeletePreset(string name)
        {
            presets.RemoveAll(p => p != null && p.Name == name);
            if (activePreset == name) activePreset = string.Empty;
        }
    }

    /// <summary>One intensity's circuit: the <see cref="HeadlongCircuitSettings"/> numbers a designer shapes a course with.</summary>
    [Serializable]
    public class TimeRaceCourseRung
    {
        [Tooltip("Radius of each ring's mouth (u). Smaller asks for a more precise line.")]
        [SerializeField, Range(6f, 120f)] float ringRadius = 48f;

        [Tooltip("Radius of the circle the circuit is laid around (u). Bigger gives longer legs. The race shell is about " +
                 "480 to 1080 u, so the circuit's swing has to fit inside it.")]
        [SerializeField, Range(500f, 1000f)] float baseRadius = 600f;

        [Tooltip("How far each ring may sit above or below the circuit's plane (u): turns that climb and dive.")]
        [SerializeField, Range(0f, 400f)] float lateralPerturbation = 80f;

        [Tooltip("How uneven the gaps between rings may become: 0 is evenly spaced, higher mixes long legs with short ones.")]
        [SerializeField, Range(0f, 6f)] float angularSpread = 1.2f;

        [Tooltip("How far a ring may be pushed in or out of the circle to make a corner, as a share of the base radius.")]
        [SerializeField, Range(0f, 0.8f)] float radialSwing = 0.42f;

        [Tooltip("How far each ring's mouth is turned off the line through it (degrees). More = rings you must line up on.")]
        [SerializeField, Range(0f, 80f)] float axisJitterDegrees = 20f;

        [Tooltip("The most a ring may face away from its corner's bisector (degrees). Must cover half the hardest turn.")]
        [SerializeField, Range(10f, 90f)] float maxPresentDegrees = 50f;

        [Tooltip("No corner tighter than this radius (u): the safety floor. The solver relaxes any corner below it.")]
        [SerializeField, Range(40f, 600f)] float cornerFloorRadius = 358f;

        [Tooltip("The turn each corner of the lap is built to (degrees), dealt round the lap so the sharpest are spread " +
                 "apart. The turns of a closed lap average 360 / rings, so a hairpin is paid for with near-straights.")]
        [SerializeField] float[] cornerProfile = { 90f, 72f, 58f, 48f, 38f, 28f, 16f, 10f };

        public float RingRadius => ringRadius;
        public float BaseRadius => baseRadius;
        public IReadOnlyList<float> CornerProfile => cornerProfile;

        public static TimeRaceCourseRung From(HeadlongCircuitSettings s) => new()
        {
            ringRadius = s.RingRadius,
            baseRadius = s.BaseRadius,
            lateralPerturbation = s.LateralPerturbation,
            angularSpread = s.AngularSpread,
            radialSwing = s.RadialSwing,
            axisJitterDegrees = s.AxisJitterDegrees,
            maxPresentDegrees = s.MaxPresentDegrees,
            cornerFloorRadius = s.CornerFloorRadius,
            cornerProfile = s.CornerProfile != null ? (float[])s.CornerProfile.Clone() : Array.Empty<float>(),
        };

        /// <summary><paramref name="s"/> with this rung's numbers written over it (the shell and gate count are the caller's).</summary>
        public HeadlongCircuitSettings ApplyTo(HeadlongCircuitSettings s)
        {
            s.RingRadius = ringRadius;
            s.BaseRadius = baseRadius;
            s.LateralPerturbation = lateralPerturbation;
            s.AngularSpread = angularSpread;
            s.RadialSwing = radialSwing;
            s.AxisJitterDegrees = axisJitterDegrees;
            s.MaxPresentDegrees = maxPresentDegrees;
            s.CornerFloorRadius = cornerFloorRadius;
            s.CornerRadiusFactor = 0f;   // the floor above is absolute, in the race hull's own units
            if (cornerProfile is { Length: > 0 }) s.CornerProfile = (float[])cornerProfile.Clone();
            return s;
        }

        public TimeRaceCourseRung Clone()
        {
            var c = (TimeRaceCourseRung)MemberwiseClone();
            c.cornerProfile = cornerProfile != null ? (float[])cornerProfile.Clone() : Array.Empty<float>();
            return c;
        }
    }

    /// <summary>A named snapshot of a <see cref="TimeRaceCourse"/>'s live values.</summary>
    [Serializable]
    public class TimeRaceCoursePreset
    {
        [SerializeField] string name;
        [SerializeField] string note;
        [SerializeField] string savedAt;
        [SerializeField] int laps;
        [SerializeField] int ringsPerLap;
        [SerializeField] TimeRaceCourseRung[] rungs;

        public TimeRaceCoursePreset(string name, string note, int laps, int ringsPerLap, TimeRaceCourseRung[] from, string savedAt)
        {
            this.name = name;
            this.note = note ?? string.Empty;
            this.savedAt = savedAt;
            this.laps = laps;
            this.ringsPerLap = ringsPerLap;
            rungs = new TimeRaceCourseRung[4];
            for (int i = 0; i < 4; i++) rungs[i] = from != null && i < from.Length && from[i] != null ? from[i].Clone() : null;
        }

        public string Name => name;
        public string Note => note;
        public string SavedAt => savedAt;
        public int Laps => laps;
        public int RingsPerLap => ringsPerLap;
        public bool HasRungs => rungs is { Length: 4 } && Array.TrueForAll(rungs, r => r != null);

        public TimeRaceCourseRung[] CopyRungs()
        {
            var copy = new TimeRaceCourseRung[4];
            for (int i = 0; i < 4; i++) copy[i] = rungs[i].Clone();
            return copy;
        }
    }
}
