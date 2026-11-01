#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Dev tool: compiled only into the editor and development builds (as PilotControl, which it drives).
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
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
        /// <summary>
        /// Refill <paramref name="into"/> with every live vessel. Call on a timer, never per repaint.
        /// Reads <see cref="VesselStatus.Live"/> and each hull's own <see cref="SkimRacePilot"/> (the pilot is
        /// installed on the vessel's GameObject, <c>SkimRaceAIDeployment.TryInstall</c>), never
        /// <c>FindObjectsByType</c>: that walks every loaded object, and the scan's two walks were the
        /// ~85-130 ms editor freeze every 0.5 s in Yash's 10-26 captures whenever this window was open
        /// (shown or docked behind a tab), the same walk the 10-17 Stoat slowdown measured.
        /// </summary>
        public static void Scan(List<ThirdEyePilot> into)
        {
            into.Clear();
            foreach (var status in VesselStatus.Live)
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

                if (hull.TryGetComponent(out SkimRacePilot skim) && skim.Vessel is Object sv && sv
                    && ReferenceEquals(skim.Vessel.VesselStatus, status))
                {
                    pilot.SkimPilot = skim;
                    // A pilot that stands down on a human's hull (a taken-over seat) does not make it an AI's.
                    if (pilot.Player == null || pilot.IsAI || skim.FlyingForHuman) pilot.IsAI = true;
                }

                into.Add(pilot);
            }

            into.Sort((a, b) =>
            {
                int rank = Rank(a).CompareTo(Rank(b));
                return rank != 0 ? rank : string.CompareOrdinal(a.Name, b.Name);
            });
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

        /// <param name="text">False skips every string (state, detail, skim): the Game-view aim lines need only the
        /// points and the colour, so drawing them every frame formats nothing.</param>
        public static ThirdEyeThinking Read(ThirdEyePilot pilot, bool text = true)
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
                    t.Color = ThirdEyeColors.Ruby;
                }
                else if (handicap != null && handicap.Unnoticed)
                {
                    t.State = "Not noticed yet";
                    t.Color = ThirdEyeColors.Gold;
                }
                else if (d.Mode == SkimRaceDriver.Mode.Recovering)
                {
                    t.State = "Recovering";
                    t.Color = ThirdEyeColors.Violet;
                }
                else
                {
                    t.State = d.Mode.ToString();
                    t.Color = ThirdEyeColors.Lime;
                }

                string level = handicap == null || handicap.Level.IsNone
                    ? "no handicap (Hard)"
                    : $"reaction {handicap.Level.ReactionSeconds:F2} s, mistake chance {handicap.Level.MistakeChance:P1}, mistakes {handicap.Mistakes}";
                string recovery = string.IsNullOrEmpty(d.LastRecoveryReason) ? "" : $" (last: {d.LastRecoveryReason})";
                if (!text) return t;
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
                if (text) t.State = ai.IsBreakingOrbit ? "Breaking orbit" : o.Has ? PhaseText(o, pilot) : "Seeking";
                t.Color = ai.IsBreakingOrbit ? ThirdEyeColors.Violet
                    : o.Has && o.Mistake ? ThirdEyeColors.Ruby
                    : ThirdEyeColors.Azure;
                if (!text)
                {
                    var b = ai.Belief;
                    if (b != null && b.Misjudging) t.Color = ThirdEyeColors.Ruby;
                    else if (b != null && b.Unnoticed) t.Color = ThirdEyeColors.Gold;
                    return t;
                }
                string lap = o.Has && o.Lap > 0 ? $"lap {o.Lap}, " : "";
                string objective = o.Has ? $"{Vector3.Distance(pilot.Hull.position, o.Objective):F0} u to it, " : "";
                string boost = ai.BoostStatus;
                t.Detail = $"{lap}{objective}steering at a point {Vector3.Distance(pilot.Hull.position, ai.TargetPosition):F0} u away" +
                           (boost != null ? $" | boost {boost}" : "");

                // The lobby difficulty's mistakes (AIPilotBelief), in the studio colours.
                var belief = ai.Belief;
                if (belief != null)
                {
                    if (belief.Misjudging)
                    {
                        t.State = "Misjudged target";
                        t.Color = ThirdEyeColors.Ruby;
                    }
                    else if (belief.Unnoticed)
                    {
                        t.State = "Not noticed yet";
                        t.Color = ThirdEyeColors.Gold;
                    }
                    t.Detail += $" | reaction {belief.Level.ReactionSeconds:F2} s, mistake chance " +
                                $"{belief.Level.MistakeChance:P1}, mistakes {belief.Mistakes}";
                }
            }
            return t;
        }

        /// <summary>The mode's report in words. Only formatted while the Third Eye panel shows it, never by the game.</summary>
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

        /// <summary>
        /// A domain's colour as the GAME shows it (the live theme's signal colour, <see cref="PrismLit.DomainTint"/>),
        /// so a label matches the pilot's trail. Falls back to fixed colours when no theme is loaded (a tool scene).
        /// </summary>
        public static Color DomainColor(ThirdEyePilot pilot)
        {
            if (pilot == null || !pilot.HasDomain) return ThirdEyeColors.Muted;
            if (PrismLit.ColorSet != null) return PrismLit.ColorSet.GetDomainSignalColor(pilot.Domain);
            return pilot.Domain switch
            {
                Domains.Jade => ThirdEyeColors.Jade,
                Domains.Ruby => ThirdEyeColors.Magenta,
                Domains.Gold => ThirdEyeColors.Gold,
                _ => ThirdEyeColors.Azure,
            };
        }
    }

    /// <summary>The Third Eye's accent colours (the Froglet editor palette's values, usable in a player build).</summary>
    public static class ThirdEyeColors
    {
        public static readonly Color Jade = new(0.16f, 0.78f, 0.56f);
        public static readonly Color Ruby = new(0.93f, 0.27f, 0.38f);
        public static readonly Color Gold = new(0.98f, 0.75f, 0.18f);
        public static readonly Color Azure = new(0.24f, 0.60f, 0.95f);
        public static readonly Color Violet = new(0.62f, 0.42f, 0.94f);
        public static readonly Color Cyan = new(0.16f, 0.78f, 0.82f);
        public static readonly Color Magenta = new(0.90f, 0.36f, 0.71f);
        public static readonly Color Lime = new(0.62f, 0.83f, 0.24f);
        public static readonly Color Muted = new(0.55f, 0.57f, 0.62f);
    }
}
#endif
