using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.AI
{
    /// <summary>One pilot the Third Eye can watch: a live vessel in the running game.</summary>
    public sealed class ThirdEyePilot
    {
        public VesselStatus Status;
        public Transform Hull;
        public string Name;
        public string HullClass;
        public Domains Domain;
        public bool HasDomain;
        public bool IsLocalHuman;
        public bool IsAI;
        public SkimRacePilot SkimPilot;
        public AIPilot Autopilot;
        /// <summary>The pilot's Player, for the You / AI toggle (<see cref="PilotControl"/>). Null for a hull with no player.</summary>
        public Player Player;

        /// <summary>The AI is flying this hull right now: an AI seat, or a human's hull the AI is holding.</summary>
        /// Your own hull asks <see cref="PilotControl.IsHeldByAI"/>, the toggle's own answer: a Squirrel handed to the Skim
        /// Race pilot has its platform autopilot switched OFF, so reading the autopilot alone showed "You" while the AI flew.
        public bool AIFlying => IsLocalHuman ? PilotControl.IsHeldByAI(Player) : IsAI;

        public int Id => Status ? Status.GetInstanceID() : 0;
        public bool Alive => Status && Hull && Hull.gameObject.activeInHierarchy;

        public string Label
        {
            get
            {
                string who = IsLocalHuman ? "You" : IsAI ? "AI" : "Pilot";
                string domain = HasDomain ? $" {Domain}" : "";
                return $"{who}{domain}: {Name} ({HullClass})";
            }
        }
    }

    /// <summary>
    /// One pilot's recent path: a ring buffer of (position, game time) the Third Eye draws as its trajectory.
    /// Fixed capacity, so sampling allocates nothing after the first point.
    /// </summary>
    public sealed class ThirdEyeTrail
    {
        readonly Vector3[] _points;
        readonly float[] _times;
        int _start;

        public ThirdEyeTrail(int capacity)
        {
            _points = new Vector3[Mathf.Max(2, capacity)];
            _times = new float[_points.Length];
        }

        public int Count { get; private set; }

        public void Add(Vector3 point, float time)
        {
            int at = (_start + Count) % _points.Length;
            _points[at] = point;
            _times[at] = time;
            if (Count < _points.Length) Count++;
            else _start = (_start + 1) % _points.Length;
        }

        /// <summary>Point <paramref name="i"/>, oldest first.</summary>
        public void Get(int i, out Vector3 point, out float time)
        {
            int at = (_start + i) % _points.Length;
            point = _points[at];
            time = _times[at];
        }
    }

    /// <summary>What an AI is doing this frame, read from its pilot's public diagnostics.</summary>
    public struct ThirdEyeThinking
    {
        public bool Has;
        public string Pilot;
        public string State;
        public Color Color;
        public Vector3 Aim;
        public bool HasTarget;
        public Vector3 Target;
        public string Detail;
        /// <summary>The skim readout (Skim Race pilot only): shell gap, line offset, boost, mass in view.</summary>
        public string Skim;
    }

    /// <summary>
    /// Finds the pilots of the running game and reads what their AI is thinking. A READER: it never
    /// adds a component or writes game state. In particular it never touches
    /// <see cref="VesselStatus.AIPilot"/>, whose getter ADDS an <see cref="AIPilot"/> to a hull that has
    /// none, which would put an autopilot on a human's ship.
    /// </summary>
    public static class ThirdEyePilots
    {
        static readonly List<SkimRacePilot> s_skim = new();

        /// <summary>Refill <paramref name="into"/> with every live vessel. Call on a timer, never per repaint.</summary>
        public static void Scan(List<ThirdEyePilot> into)
        {
            into.Clear();
            s_skim.Clear();
            s_skim.AddRange(Object.FindObjectsByType<SkimRacePilot>(FindObjectsSortMode.None));

            foreach (var status in Object.FindObjectsByType<VesselStatus>(FindObjectsSortMode.None))
            {
                if (!status || !status.isActiveAndEnabled) continue;

                Transform hull = status.TryGetComponent(out IVessel vessel) && vessel is Object v && v
                    ? vessel.Transform
                    : status.transform;
                if (!hull) continue;

                var pilot = new ThirdEyePilot
                {
                    Status = status,
                    Hull = hull,
                    HullClass = status.VesselType.ToString(),
                    Name = status.gameObject.name,
                };

                IPlayer player = status.Player is Object p && p ? status.Player : null;
                if (player != null)
                {
                    pilot.Name = string.IsNullOrEmpty(player.Name) ? pilot.Name : player.Name;
                    pilot.Domain = player.Domain;
                    pilot.HasDomain = true;
                    pilot.IsAI = player.IsInitializedAsAI;
                    pilot.IsLocalHuman = player.IsLocalPilot && !player.IsInitializedAsAI;
                    pilot.Player = player as Player;
                }

                if (status.TryGetComponent(out AIPilot autopilot))
                {
                    pilot.Autopilot = autopilot;
                    pilot.IsAI |= autopilot.AutoPilotEnabled;
                }

                foreach (var skim in s_skim)
                {
                    if (!skim || !(skim.Vessel is Object sv && sv)) continue;
                    if (!ReferenceEquals(skim.Vessel.VesselStatus, status)) continue;
                    pilot.SkimPilot = skim;
                    // A pilot that stands down on a human's hull (a taken-over seat) does not make it an AI's.
                    if (pilot.Player == null || pilot.IsAI || skim.FlyingForHuman) pilot.IsAI = true;
                    break;
                }

                into.Add(pilot);
            }

            into.Sort((a, b) =>
            {
                int rank = Rank(a).CompareTo(Rank(b));
                return rank != 0 ? rank : string.CompareOrdinal(a.Name, b.Name);
            });
            s_skim.Clear();
        }

        // You first, then the other humans, then the AI.
        static int Rank(ThirdEyePilot p) => p.IsLocalHuman ? 0 : p.IsAI ? 2 : 1;

        /// <summary>
        /// The pilot's aim this frame. The Skim Race pilot reports its aim point, its state and its
        /// difficulty mistakes (the colours the Squirrel studio uses: amber = not noticed yet,
        /// ruby = misjudged); any other hull on the platform autopilot reports the point it wants to reach,
        /// and - where the mode says (<see cref="AIPilot.Objective"/>, every gate race) - the ring it is after, the lap,
        /// the phase ("Approach ring 3", "Riding the rail", a difficulty mistake) and its boost policy's decision.
        /// </summary>
        /// <summary>
        /// The skim readout: how far the hull is from its own course's contact shell (what a skim
        /// touches and a strike hits), how far off its skim line, its boost, the non-course mass the
        /// mass guard is steering round, and the course's skim lift. A reader: one projection per
        /// repaint, nothing written.
        /// </summary>
        static string SkimDetail(SkimRacePilot skim, in SkimRaceObservation o)
        {
            var course = skim.Course;
            var driver = skim.Driver;
            if (course == null || driver == null) return "";
            int hint = -1;
            course.Project(o.Position, ref hint, out _, out _);
            float shell = course.ShellClearance(o.Position, hint, 6, out _);
            string gap = float.IsInfinity(shell) ? "n/a" : $"{shell:F1} u";
            string off = driver.LastTrackError >= 0f ? $"{driver.LastTrackError:F1} u" : "n/a";
            return $"skim: shell gap {gap} (skims under {skim.Config.PlannerSkimReach:F1}, strikes at 0), " +
                   $"off line {off}, boost x{o.BoostMultiplier:F2}, mass in view {driver.Obstacles.Count}, " +
                   $"lift {course.SkimLift:F1} u, direct {(driver.AllowDirectFlight ? (driver.DirectFlight ? "ON" : "armed") : "off")}";
        }

        public static ThirdEyeThinking Read(ThirdEyePilot pilot)
        {
            var t = new ThirdEyeThinking();
            if (pilot == null || !pilot.Alive) return t;

            var skim = pilot.SkimPilot;
            if (skim && skim.isActiveAndEnabled && skim.RaceActive && skim.Driver != null)
            {
                var d = skim.Driver.LastDiagnostics;
                var handicap = skim.Driver.Handicap;
                var o = skim.LastObservation;

                t.Has = true;
                t.Pilot = "Skim Race pilot";
                t.Aim = d.AimPoint;
                t.HasTarget = o.HasTarget;
                t.Target = o.TargetPosition;

                if (handicap != null && handicap.Misjudging)
                {
                    t.State = "Misjudged crystal";
                    t.Color = FrogletEditorPalette.Ruby;
                }
                else if (handicap != null && handicap.Unnoticed)
                {
                    t.State = "Not noticed yet";
                    t.Color = FrogletEditorPalette.Gold;
                }
                else if (d.Mode == SkimRaceDriver.Mode.Recovering)
                {
                    t.State = "Recovering";
                    t.Color = FrogletEditorPalette.Violet;
                }
                else
                {
                    t.State = d.Mode.ToString();
                    t.Color = FrogletEditorPalette.Lime;
                }

                string level = handicap == null || handicap.Level.IsNone
                    ? "no handicap (Hard)"
                    : $"reaction {handicap.Level.ReactionSeconds:F2} s, mistake chance {handicap.Level.MistakeChance:P1}, mistakes {handicap.Mistakes}";
                string recovery = string.IsNullOrEmpty(d.LastRecoveryReason) ? "" : $" (last: {d.LastRecoveryReason})";
                t.Detail = $"heading error {d.HeadingErrorDegrees:F0} deg, command error {d.CommandErrorDegrees:F0} deg, " +
                           $"recoveries {d.Recoveries}{recovery}" +
                           (d.CrystalPull ? ", crystal pull" : "") + (d.Unreachable ? ", UNREACHABLE" : "") +
                           $" | {level}";
                t.Skim = SkimDetail(skim, o);
                return t;
            }

            var ai = pilot.Autopilot;
            if (ai && ai.AutoPilotEnabled)
            {
                var o = ai.Objective;
                t.Has = true;
                t.Pilot = "Platform autopilot";
                t.Aim = ai.TargetPosition;
                t.HasTarget = o.Has;
                t.Target = o.Objective;
                t.State = ai.IsBreakingOrbit ? "Breaking orbit" : o.Has ? PhaseText(o, pilot) : "Seeking";
                t.Color = ai.IsBreakingOrbit ? FrogletEditorPalette.Violet
                    : o.Has && o.Mistake ? FrogletEditorPalette.Ruby
                    : FrogletEditorPalette.Azure;
                string lap = o.Has && o.Lap > 0 ? $"lap {o.Lap}, " : "";
                string objective = o.Has ? $"{Vector3.Distance(pilot.Hull.position, o.Objective):F0} u to it, " : "";
                string boost = ai.BoostStatus;
                t.Detail = $"{lap}{objective}steering at a point {Vector3.Distance(pilot.Hull.position, ai.TargetPosition):F0} u away" +
                           (boost != null ? $" | boost {boost}" : "");
            }
            return t;
        }

        /// <summary>The mode's report in words. Editor-side, so the game never formats a string per frame.</summary>
        static string PhaseText(in AIPilot.ObjectiveReport o, ThirdEyePilot pilot)
        {
            string ring = o.Ring >= 0 ? $" ring {o.Ring + 1}" : "";
            return o.Phase switch
            {
                AIPilot.ObjectivePhase.Approach => "Approach" + ring,
                AIPilot.ObjectivePhase.Through => "Through" + ring,
                AIPilot.ObjectivePhase.CrystalDetour => "Crystal on the way to" + ring,
                AIPilot.ObjectivePhase.NotNoticed => "Not noticed yet:" + ring,
                AIPilot.ObjectivePhase.Misjudged => "Misjudged" + ring,
                AIPilot.ObjectivePhase.Override => pilot.Status && pilot.Status.IsAttached ? "Riding the rail" : "Heading for its own rail",
                AIPilot.ObjectivePhase.Finished => "Finished",
                _ => "Seeking",
            };
        }

        const string GameColorSetPath = "Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset";
        static SO_ColorSet s_gameColors;

        /// <summary>
        /// A domain's colour as the GAME shows it (the live <c>OriginalColorSetSO</c>'s signal colour, the
        /// one the studios read through <c>studio-domains.js</c>), so a label matches the pilot's trail.
        /// Game Ruby is magenta, not the editor palette's red. Falls back to the editor palette only if the
        /// asset is missing.
        /// </summary>
        public static Color DomainColor(ThirdEyePilot pilot)
        {
            if (pilot == null || !pilot.HasDomain) return FrogletEditorPalette.Muted;
            if (!s_gameColors) s_gameColors = AssetDatabase.LoadAssetAtPath<SO_ColorSet>(GameColorSetPath);
            if (s_gameColors) return s_gameColors.GetDomainSignalColor(pilot.Domain);
            return pilot.Domain switch
            {
                Domains.Jade => FrogletEditorPalette.Jade,
                Domains.Ruby => FrogletEditorPalette.Magenta,
                Domains.Gold => FrogletEditorPalette.Gold,
                _ => FrogletEditorPalette.Azure,
            };
        }
    }
}
