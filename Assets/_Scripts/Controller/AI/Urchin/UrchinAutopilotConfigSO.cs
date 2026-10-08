using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every tunable <see cref="UrchinAutopilotDriver"/> reads, in one asset.
    ///
    /// <para>The shipped instance lives at <c>Resources/UrchinAutopilotConfig</c> so every mode
    /// that drives an AI Urchin loads the same values with no scene wiring
    /// (<see cref="LoadDefault"/>), the <c>SkimRaceAIConfigSO</c> pattern. A missing asset falls
    /// back to these initializers rather than to nothing, so a clean checkout still flies.</para>
    ///
    /// <para>Nothing here grants the AI anything a human Urchin does not have: every field shapes
    /// WHEN the autopilot presses one of the vessel's own four controls, never what the press
    /// does. The ability numbers (ammo cost, ghost time, track length, cooldown) are read off the
    /// ability SOs themselves.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "UrchinAutopilotConfig", menuName = "ScriptableObjects/AI/Urchin Autopilot Config")]
    public class UrchinAutopilotConfigSO : ScriptableObject
    {
        public const string ResourcePath = "UrchinAutopilotConfig";

        [Header("Riding")]
        [Tooltip("How far down its own rail a riding AI aims. Far enough that the range to the aim " +
                 "point falls every frame (which keeps AIPilot's OrbitDetector reset) and the " +
                 "bearing stays on the rail's tangent (which keeps LookingAtCrystal, and with it " +
                 "the prefab's ram throttle, engaged for the whole grind).")]
        [Min(10f)] public float RailLeadDistance = 260f;

        [Tooltip("Seconds between rail assessments - how often a riding AI re-asks whether the rail " +
                 "it is on still goes where it is going. The walk is a few hundred prism reads.")]
        [Min(0.05f)] public float AssessIntervalSeconds = 0.25f;

        [Tooltip("Arc length (world units) the assessment walks each way along the rail. Covers a " +
                 "whole Skein segment (750 u) with room to spare.")]
        [Min(50f)] public float ScanArc = 1600f;

        [Tooltip("Fraction of the objective's own capture radius (a ring's mouth) inside which a " +
                 "rail counts as THREADING it. Under 1 so a rail that grazes the rim is not read as " +
                 "a rail that goes through.")]
        [Range(0.1f, 1f)] public float CaptureFraction = 0.8f;

        [Tooltip("A rail whose closest approach to the objective is under this fraction of the " +
                 "current range is still worth riding. The grind runs at several times cruise, so " +
                 "a rail that gets you most of the way is faster than the straight line in the air.")]
        [Range(0.1f, 0.95f)] public float ClosingFraction = 0.7f;

        [Tooltip("An open rail end inside this arc is left by LAUNCHING rather than by slipping: " +
                 "the launch is free and carries 1.2x the grind speed.")]
        [Min(0f)] public float LaunchPreferArc = 160f;

        [Tooltip("While crawling HOSTILE mass with no ammo to convert it, a rail is only worth " +
                 "staying on if it threads the objective within this arc (the crawl is ~20 u/s).")]
        [Min(0f)] public float DryCrawlArc = 80f;

        [Tooltip("Reversal aim distance, as a multiple of the vessel's live minimum turn radius. " +
                 "The aim point is set off to one side as well as behind, so the swing has a " +
                 "turning direction instead of a vanishing cross product.")]
        [Min(1f)] public float ReverseLeadTurnRadii = 2.2f;

        [Header("Stall escape")]
        [Tooltip("Speed (u/s) under which a riding AI counts as PARKED. Deliberately below the " +
                 "~20 u/s hostile crawl, which is a raid in progress, not a stall.")]
        [Min(0.5f)] public float ParkedSpeed = 6f;

        [Tooltip("Seconds parked before the AI Slips off the rail.")]
        [Min(0.5f)] public float ParkedSeconds = 5f;

        [Header("Slip")]
        [Tooltip("Minimum seconds between two Slips, so a rail that physically lies between the AI " +
                 "and its objective cannot catch it in a slip / re-latch loop faster than this.")]
        [Min(0.25f)] public float MinSlipIntervalSeconds = 1.5f;

        [Header("Chain spikes")]
        [Tooltip("Seconds between spike taps. Each tap converts the prisms ahead and restores " +
                 "grind speed on a hostile rail.")]
        [Min(0.1f)] public float SpikeIntervalSeconds = 1.5f;

        [Tooltip("Minimum spike ammo (0-1 of the meter) before the AI spends a tap, on top of the " +
                 "tap's own authored cost. Riding recharges it, so this is what keeps an AI from " +
                 "arriving somewhere it needs a volley with an empty meter.")]
        [Range(0f, 1f)] public float SpikeMinAmmo = 0.2f;

        [Header("Track Projector")]
        [Tooltip("Project a track only while the nose is within this many degrees of the objective " +
                 "- the track is laid along the NOSE, so this is how straight it points at it.")]
        [Range(1f, 45f)] public float TrackAimDegrees = 8f;

        [Tooltip("Project a track only when the objective is at least this many TRACK LENGTHS away. " +
                 "The launch off a projected track carries 360 u/s that bleeds off over seconds, so " +
                 "a short shot overshoots whatever it was aimed at.")]
        [Min(1f)] public float TrackMinRangeInLengths = 3f;

        [Tooltip("When the objective has a flow axis (a ring), project only when the flight line is " +
                 "this well aligned with it (|cos|), so the launch flies THROUGH the mouth.")]
        [Range(0f, 1f)] public float TrackAxisAlignment = 0.8f;

        public static UrchinAutopilotConfigSO LoadDefault()
        {
            var cfg = Resources.Load<UrchinAutopilotConfigSO>(ResourcePath);
            return cfg != null ? cfg : CreateInstance<UrchinAutopilotConfigSO>();
        }
    }
}
