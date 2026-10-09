using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
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
                    pilot.IsAI = true;
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
        /// ruby = misjudged); any other hull on the platform autopilot reports the point it wants to reach.
        /// </summary>
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
                return t;
            }

            var ai = pilot.Autopilot;
            if (ai && ai.AutoPilotEnabled)
            {
                t.Has = true;
                t.Pilot = "Platform autopilot";
                t.Aim = ai.TargetPosition;
                t.State = ai.IsBreakingOrbit ? "Breaking orbit" : "Seeking";
                t.Color = ai.IsBreakingOrbit ? FrogletEditorPalette.Violet : FrogletEditorPalette.Azure;
                t.Detail = $"target {Vector3.Distance(pilot.Hull.position, ai.TargetPosition):F0} u away";
            }
            return t;
        }

        /// <summary>A domain's colour from the palette (Blue is the neutral sentinel).</summary>
        public static Color DomainColor(ThirdEyePilot pilot)
        {
            if (pilot == null || !pilot.HasDomain) return FrogletEditorPalette.Muted;
            return pilot.Domain switch
            {
                Domains.Jade => FrogletEditorPalette.Jade,
                Domains.Ruby => FrogletEditorPalette.Ruby,
                Domains.Gold => FrogletEditorPalette.Gold,
                _ => FrogletEditorPalette.Azure,
            };
        }
    }
}
