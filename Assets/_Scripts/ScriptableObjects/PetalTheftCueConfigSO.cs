using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for the PETAL THEFT CUE (Docs/ElementalAbilitySystem/PETAL_THEFT_CUE.md): how a scored
    /// hit is drawn in the world so a muted replay can say who hit whom. The victim's hull flashes in
    /// the danger colour, its petals tear off as lit faceted shards in its domain colour and stream to
    /// the attacker, the attacker's hull pulses in its own colour as they land, and a silenced hull
    /// carries danger shards for as long as the silence lasts.
    ///
    /// <para>Every size is a share of the hull's own measured radius, so one asset reads on a 7-unit
    /// Urchin and a 130-unit Shark alike. Every time is GAME time, so a slow-motion replay slows the
    /// cue with the play.</para>
    ///
    /// Place the asset at <c>Resources/PetalTheftCueConfig</c>. With no asset the defaults below apply
    /// and a warning names the missing asset once.
    /// </summary>
    [CreateAssetMenu(fileName = "PetalTheftCueConfig",
                     menuName = "ScriptableObjects/Vessel/Petal Theft Cue Config")]
    public class PetalTheftCueConfigSO : ScriptableObject
    {
        [Header("Switch")]
        [Tooltip("Draw the cue at all. Off draws nothing; the hit still scores, takes and silences.")]
        [SerializeField] bool drawCue = true;

        [Header("Shards (the petals, torn off and carried to the attacker)")]
        // The shards wear the VICTIM's domain ship material (the facing two-tone every hull's domain band
        // wears, painted from the worn colour set by ThemeManager), so they read as pieces of that hull.
        // A silenced hull's shards wear its domain's danger block. Neither is authored here.
        [Tooltip("Shards per petal the hit is worth (ten points to the petal on four elements: a bullet " +
                 "is 0.4 petal, a missile shockwave 4, a direct missile 12).")]
        [Min(0f)]
        [SerializeField] float shardsPerPetal = 1.5f;

        [Tooltip("Fewest shards one hit draws, so a single bullet still shows a petal leaving.")]
        [Min(0)]
        [SerializeField] int minShardsPerHit = 1;

        [Tooltip("Most shards one hit draws.")]
        [Min(1)]
        [SerializeField] int maxShardsPerHit = 18;

        [Tooltip("Most shards alive at once across the whole arena. A new shard past this recycles the oldest.")]
        [Min(1)]
        [SerializeField] int maxLiveShards = 160;

        [Tooltip("Shard length as a share of the victim hull's radius.")]
        [Min(0f)]
        [SerializeField] float shardSizeOfHull = 0.16f;

        [Tooltip("Shortest shard, in world units, so a small hull's petals still read from a wide camera.")]
        [Min(0f)]
        [SerializeField] float shardMinSize = 1.1f;

        [Tooltip("Longest shard, in world units.")]
        [Min(0f)]
        [SerializeField] float shardMaxSize = 7f;

        [Tooltip("Seconds between one shard leaving the hull and the next, so a burst tears off rather than pops.")]
        [Min(0f)]
        [SerializeField] float shardStagger = 0.025f;

        [Header("Tear-off (the shards leaving the victim)")]
        [Tooltip("Seconds the shards fly free off the victim's hull before they turn for the attacker.")]
        [Min(0f)]
        [SerializeField] float burstSeconds = 0.3f;

        [Tooltip("Launch speed off the hull, in hull radii per second.")]
        [Min(0f)]
        [SerializeField] float burstSpeedOfHull = 4f;

        [Tooltip("Slowest launch, in world units per second.")]
        [Min(0f)]
        [SerializeField] float burstMinSpeed = 14f;

        [Tooltip("How quickly the launch bleeds off while the shards hang (per second).")]
        [Min(0f)]
        [SerializeField] float burstDrag = 5f;

        [Tooltip("Tumble while they hang, in degrees per second.")]
        [SerializeField] float burstSpinDegrees = 540f;

        [Header("Stream (the shards carried to the attacker)")]
        [Tooltip("Cruise speed the stream is timed against, in world units per second: travel time is the " +
                 "distance at this speed, clamped below.")]
        [Min(1f)]
        [SerializeField] float streamCruiseSpeed = 240f;

        [Tooltip("Shortest time a shard takes to reach the attacker, in seconds, so a point-blank hit still shows the flow.")]
        [Min(0.05f)]
        [SerializeField] float streamMinSeconds = 0.55f;

        [Tooltip("Longest time a shard takes to reach the attacker, in seconds, so a sniper's theft lands while it still matters.")]
        [Min(0.05f)]
        [SerializeField] float streamMaxSeconds = 1.4f;

        [Tooltip("How hard a shard steers onto its course to the attacker (per second). Higher is a tighter line.")]
        [Min(0f)]
        [SerializeField] float streamSteer = 7f;

        [Tooltip("How much a fast shard stretches along its flight, per world unit per second of speed.")]
        [Min(0f)]
        [SerializeField] float stretchPerSpeed = 0.012f;

        [Tooltip("The most a shard stretches, as a multiple of its length.")]
        [Min(1f)]
        [SerializeField] float stretchMax = 3.5f;

        [Tooltip("A shard is taken in when it is this many attacker hull radii from the attacker's centre.")]
        [Min(0f)]
        [SerializeField] float arriveOfHull = 0.7f;

        [Tooltip("Seconds a shard takes to sink into the attacker's hull once it arrives (or to fade if the attacker is gone).")]
        [Min(0.01f)]
        [SerializeField] float absorbSeconds = 0.14f;

        [Header("Hull light (the victim's flash, the attacker's pulse)")]
        [Tooltip("Light the hulls at all. A real point light: it lights the hull and the mass around it.")]
        [SerializeField] bool hullLights = true;

        [Tooltip("Peak intensity of the victim's flash, in the colour set's danger colour, for a hit worth four petals or more.")]
        [Min(0f)]
        [SerializeField] float victimFlashIntensity = 9f;

        [Tooltip("Peak intensity of the attacker's pulse, in its own domain colour, once every shard of a hit has landed.")]
        [Min(0f)]
        [SerializeField] float attackerPulseIntensity = 7f;

        [Tooltip("The weakest flash a hit draws, as a share of the peak, so a bullet still flickers.")]
        [Range(0f, 1f)]
        [SerializeField] float flashFloor = 0.3f;

        [Tooltip("Seconds a flash takes to fall to a third.")]
        [Min(0.01f)]
        [SerializeField] float flashDecaySeconds = 0.3f;

        [Tooltip("Light range as hull radii.")]
        [Min(0f)]
        [SerializeField] float flashRangeOfHull = 4.5f;

        [Header("Silence (danger shards held on the silenced hull)")]
        [Tooltip("Show a silenced pilot's silence on their hull, for every viewer.")]
        [SerializeField] bool silenceShards = true;

        [Tooltip("Danger shards on a hull carrying the longest silence. Fewer show as the silence drains.")]
        [Min(0)]
        [SerializeField] int silenceShardCount = 6;

        [Tooltip("Orbit radius as hull radii.")]
        [Min(0f)]
        [SerializeField] float silenceOrbitOfHull = 1.2f;

        [Tooltip("Danger shard length as a share of the hull's radius.")]
        [Min(0f)]
        [SerializeField] float silenceShardOfHull = 0.14f;

        [Tooltip("Orbit speed, in degrees per second.")]
        [SerializeField] float silenceOrbitDegrees = 70f;

        [Header("Audio")]
        [Tooltip("FMOD event played at the victim when a hit tears its petals off. Leave empty for silence.")]
        [SerializeField] EventReference tearOffEvent;

        [Tooltip("FMOD event played at the attacker when a hit's petals land. Leave empty for silence.")]
        [SerializeField] EventReference landEvent;

        public bool DrawCue => drawCue;
        public float ShardsPerPetal => shardsPerPetal;
        public int MinShardsPerHit => minShardsPerHit;
        public int MaxShardsPerHit => Mathf.Max(minShardsPerHit, maxShardsPerHit);
        public int MaxLiveShards => maxLiveShards;
        public float ShardSizeOfHull => shardSizeOfHull;
        public float ShardMinSize => shardMinSize;
        public float ShardMaxSize => Mathf.Max(shardMinSize, shardMaxSize);
        public float ShardStagger => shardStagger;
        public float BurstSeconds => burstSeconds;
        public float BurstSpeedOfHull => burstSpeedOfHull;
        public float BurstMinSpeed => burstMinSpeed;
        public float BurstDrag => burstDrag;
        public float BurstSpinDegrees => burstSpinDegrees;
        public float StreamCruiseSpeed => streamCruiseSpeed;
        public float StreamMinSeconds => streamMinSeconds;
        public float StreamMaxSeconds => Mathf.Max(streamMinSeconds, streamMaxSeconds);
        public float StreamSteer => streamSteer;
        public float StretchPerSpeed => stretchPerSpeed;
        public float StretchMax => stretchMax;
        public float ArriveOfHull => arriveOfHull;
        public float AbsorbSeconds => absorbSeconds;
        public bool HullLights => hullLights;
        public float VictimFlashIntensity => victimFlashIntensity;
        public float AttackerPulseIntensity => attackerPulseIntensity;
        public float FlashFloor => flashFloor;
        public float FlashDecaySeconds => flashDecaySeconds;
        public float FlashRangeOfHull => flashRangeOfHull;
        public bool SilenceShards => silenceShards;
        public int SilenceShardCount => silenceShardCount;
        public float SilenceOrbitOfHull => silenceOrbitOfHull;
        public float SilenceShardOfHull => silenceShardOfHull;
        public float SilenceOrbitDegrees => silenceOrbitDegrees;
        public EventReference TearOffEvent => tearOffEvent;
        public EventReference LandEvent => landEvent;
    }
}
