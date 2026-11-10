using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// The motion and look of a <see cref="GhostPilotFlock"/>: a render-only, footage-tuned ambient wander flock
    /// (Docs/GENOME_GHOST_PILOTS.md). Each field maps one parameter of the genome lab's agent ops
    /// (<c>ag.spawn</c>, <c>ag.boids</c>'s turn, <c>ag.wander</c>, <c>ag.attract</c>, <c>ag.bound</c>, <c>ag.trail</c>,
    /// <c>ag.body</c>); the two shipped presets are the lab's tuned genomes, decoded value for value.
    /// Not fauna: nothing here has mass, a collider or a heart.
    /// </summary>
    [CreateAssetMenu(fileName = "GhostPilotFlockProfile",
        menuName = "ScriptableObjects/Environment/Ghost Pilot Flock Profile")]
    public class GhostPilotFlockProfileSO : ScriptableObject
    {
        [Header("Provenance")]
        [Tooltip("Which lab genome these numbers came from (file and node ids). Documentation only.")]
        [TextArea(2, 4)]
        [SerializeField] string sourceGenome = "";

        [Header("Population (lab ag.spawn)")]
        [Tooltip("How many pilots. The tuned genomes fly 16-24; at these counts a pilot averages ~0.1 neighbours, " +
                 "which is why the step needs no neighbour search.")]
        [Min(1)]
        [SerializeField] int count = 16;

        [Tooltip("Radius of the volume pilots start in, in local units (lab ag.spawn radius).")]
        [Min(0f)]
        [SerializeField] float spawnRadius = 65f;

        [Tooltip("Shape pilots start in (lab ag.spawn shape): ball, shell, ring/disk or line.")]
        [SerializeField] GhostPilotSpawnShape spawnShape = GhostPilotSpawnShape.Ball;

        [Tooltip("Random seed. The same seed always gives the same flock.")]
        [SerializeField] int seed = 7;

        [Tooltip("Warm-up simulated (motion only, no trails, nothing drawn) before the flock blooms in, in seconds " +
                 "(lab out.warmup). Lets pilots settle into their bound so the bloom shows a flock in motion.")]
        [Min(0f)]
        [SerializeField] float warmupSeconds = 8f;

        [Header("Cruise and turning (lab ag.spawn speed, ag.boids turn)")]
        [Tooltip("Cruise speed in local units per second. Speed relaxes toward this; wander along the heading " +
                 "still speeds pilots up or slows them down (the lab's rule).")]
        [Min(0f)]
        [SerializeField] float cruiseSpeed = 30f;

        [Tooltip("Turn cap in radians per second: the most a pilot's heading can turn. 0 = uncapped.")]
        [Min(0f)]
        [SerializeField] float turnCap = 1.34f;

        [Tooltip("How fast speed relaxes back to cruise, per second (the lab's fixed 2.0).")]
        [Min(0f)]
        [SerializeField] float speedRelax = 2f;

        [Header("Wander (lab ag.wander)")]
        [Tooltip("Base wander frequency in Hz. Each pilot steers by a sum of six sines at multiples of this.")]
        [Min(0f)]
        [SerializeField] float wanderFrequency = 0.3f;

        [Tooltip("Wander amplitude (lab amp). Acceleration = amp x wave x cruise speed x 0.3.")]
        [Min(0f)]
        [SerializeField] float wanderAmplitude = 6.1f;

        [Header("Soft bound (lab ag.bound)")]
        [Tooltip("Radius of the soft sphere pilots are pulled back into, in local units around the flock's centre.")]
        [Min(0f)]
        [SerializeField] float boundRadius = 146f;

        [Tooltip("Spring stiffness of the bound, per second squared, applied to the distance outside the radius.")]
        [Min(0f)]
        [SerializeField] float boundStiffness = 0.49f;

        [Header("Attract and orbit (lab ag.attract, optional)")]
        [Tooltip("Pull pilots toward a sphere around a centre, with an optional swirl. Neither tuned genome uses it.")]
        [SerializeField] bool attractEnabled;

        [Tooltip("Centre of the attracting sphere, in local units.")]
        [SerializeField] Vector3 attractCentre = Vector3.zero;

        [Tooltip("Spring strength toward the sphere's surface, per second squared.")]
        [Min(0f)]
        [SerializeField] float attractStrength = 0.1f;

        [Tooltip("Radius of the attracting sphere, in local units.")]
        [Min(0f)]
        [SerializeField] float attractRadius = 100f;

        [Tooltip("Swirl around the world up axis (lab orbit, -2..2). Acceleration = orbit x tangent x speed x 2.")]
        [Range(-2f, 2f)]
        [SerializeField] float attractOrbit;

        [Header("Team colours (lab ag.spawn hues)")]
        [Tooltip("Number of colour teams (1-4). Team k's hue is hue0 + k x hue step.")]
        [Range(1, 4)]
        [SerializeField] int teams = 3;

        [Tooltip("Hue of team 0 (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] float teamHue0 = 0.64f;

        [Tooltip("Hue step between teams (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] float teamHueStep = 0.4f;

        [Tooltip("Team colour saturation (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] float teamSaturation = 0.93f;

        [Tooltip("Team colour value; above 1 is HDR and goes to emission.")]
        [Min(0f)]
        [SerializeField] float teamValue = 1.6f;

        [Header("Trail (lab ag.trail)")]
        [Tooltip("Seconds between trail prisms dropped behind each pilot.")]
        [Min(0.01f)]
        [SerializeField] float trailInterval = 0.058f;

        [Tooltip("Seconds a trail prism lives. It grows in, tapers, and shrinks to nothing at the end (never pops).")]
        [Min(0.1f)]
        [SerializeField] float trailLifetime = 9.77f;

        [Tooltip("Trail prism size in local units: x width, y height, z length along the heading.")]
        [SerializeField] Vector3 trailSize = new(0.54f, 0.37f, 2.2f);

        [Tooltip("How the trail cross-section tapers with age: scale = (1 - age/life)^power. 0.5 matches the lab's " +
                 "sqrt alpha fade.")]
        [Min(0f)]
        [SerializeField] float trailTaperPower = 0.5f;

        [Tooltip("Trail emission strength (lab emit).")]
        [Min(0f)]
        [SerializeField] float trailEmission = 0.67f;

        [Tooltip("Team mix (team colour x 0.6 + trail colour x 0.4) or the trail colour only.")]
        [SerializeField] GhostPilotColorMode trailColorMode = GhostPilotColorMode.TeamMix;

        [Tooltip("Trail colour hue (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] float trailHue = 0.05f;

        [Tooltip("Trail colour saturation (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] float trailSaturation;

        [Tooltip("Trail colour value; above 1 is HDR.")]
        [Min(0f)]
        [SerializeField] float trailValue = 1f;

        [Header("Body (lab ag.body)")]
        [Tooltip("Pilot body (a faceted dart) size in local units: x span, y height, z length.")]
        [SerializeField] Vector3 bodySize = new(0.8f, 0.4f, 1.08f);

        [Tooltip("Body emission strength (lab emit).")]
        [Min(0f)]
        [SerializeField] float bodyEmission = 0.58f;

        [Tooltip("Team mix (team colour x 0.5 + body colour x 0.5) or the body colour only.")]
        [SerializeField] GhostPilotColorMode bodyColorMode = GhostPilotColorMode.TeamMix;

        [Tooltip("Body colour hue (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] float bodyHue = 0.06f;

        [Tooltip("Body colour saturation (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] float bodySaturation = 0.03f;

        [Tooltip("Body colour value; above 1 is HDR.")]
        [Min(0f)]
        [SerializeField] float bodyValue = 2.88f;

        public string SourceGenome => sourceGenome;
        public int Count => count;
        public float SpawnRadius => spawnRadius;
        public GhostPilotSpawnShape SpawnShape => spawnShape;
        public int Seed => seed;
        public float WarmupSeconds => warmupSeconds;
        public float CruiseSpeed => cruiseSpeed;
        public float TurnCap => turnCap;
        public float SpeedRelax => speedRelax;
        public float WanderFrequency => wanderFrequency;
        public float WanderAmplitude => wanderAmplitude;
        public float BoundRadius => boundRadius;
        public float BoundStiffness => boundStiffness;
        public bool AttractEnabled => attractEnabled;
        public Vector3 AttractCentre => attractCentre;
        public float AttractStrength => attractStrength;
        public float AttractRadius => attractRadius;
        public float AttractOrbit => attractOrbit;
        public int Teams => teams;
        public float TrailInterval => trailInterval;
        public float TrailLifetime => trailLifetime;
        public Vector3 TrailSize => trailSize;
        public float TrailTaperPower => trailTaperPower;
        public float TrailEmission => trailEmission;
        public Vector3 BodySize => bodySize;
        public float BodyEmission => bodyEmission;

        /// <summary>Trail prisms one pilot can have alive at once: its ring buffer's length.</summary>
        public int TrailCapacity => Mathf.Max(1, Mathf.CeilToInt(trailLifetime / Mathf.Max(0.01f, trailInterval)) + 2);

        /// <summary>Team <paramref name="k"/>'s colour, the lab's way: HSV at full value, then times value (may be HDR).</summary>
        public Color TeamColor(int k) => Hsv(teamHue0 + k * teamHueStep, teamSaturation, teamValue);

        /// <summary>Team <paramref name="k"/>'s trail colour (lab ag.trail colormode).</summary>
        public Color TrailColor(int k) => trailColorMode == GhostPilotColorMode.TeamMix
            ? TeamColor(k) * 0.6f + Hsv(trailHue, trailSaturation, trailValue) * 0.4f
            : Hsv(trailHue, trailSaturation, trailValue);

        /// <summary>Team <paramref name="k"/>'s body colour (lab ag.body colormode).</summary>
        public Color BodyColor(int k) => bodyColorMode == GhostPilotColorMode.TeamMix
            ? TeamColor(k) * 0.5f + Hsv(bodyHue, bodySaturation, bodyValue) * 0.5f
            : Hsv(bodyHue, bodySaturation, bodyValue);

        static Color Hsv(float h, float s, float v)
        {
            Color c = Color.HSVToRGB(Mathf.Repeat(h, 1f), Mathf.Clamp01(s), 1f);
            return new Color(c.r * v, c.g * v, c.b * v, 1f);
        }
    }
}
