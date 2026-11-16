using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One blast LOOK (BLAST_LOOK.md): how a blast is drawn, in three beats, for every blast whose
    /// <see cref="AOEExplosion"/> names this asset. The look is all here, never per prefab, so a blast
    /// family shares one look and a retune is one asset.
    ///
    /// <para><b>Release.</b> A white-hot faceted CORE flashes at the bomb and collapses as it cools;
    /// a faceted SHOCK SHELL races out to the blast's radius on the blast's own curve (it replaces
    /// the ripple sphere's photons, not its sweep); a short point LIGHT goes off at the core.</para>
    ///
    /// <para><b>Aftermath.</b> SHARDS of the casing (chunky, faceted, of every size) and EMBERS (small,
    /// fast, stretched along their flight while they are fast) are flung out, slowed by drag so they
    /// HANG, tumbling, cooling from white-hot to the shooter's colour to the dark body, and shrink away.
    /// They outlive the blast by seconds: that is what a slow-motion shot lingers on.</para>
    ///
    /// <para>Every distance is a SHARE of the blast's radius (half its MaxScale), so a tap-sized
    /// blast and a full squeeze are the same picture at two sizes. Every time is game time
    /// (<c>Time.deltaTime</c>), so slow motion slows it. Visual only: nothing reads it.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "BlastLook", menuName = "ScriptableObjects/Effects/Blast Look")]
    public class BlastLookSO : ScriptableObject
    {
        [Header("Materials")]
        [SerializeField, Tooltip("CosmicShore/BlastFacet with Heat From Vertex 0: the core.")]
        Material coreMaterial;
        [SerializeField, Tooltip("CosmicShore/BlastFacet with Heat From Vertex 1: the shards and embers (one mesh per blast, each shard's heat in its uv0.x).")]
        Material shardMaterial;
        [SerializeField, Tooltip("CosmicShore/BlastShell: the shock front.")]
        Material shellMaterial;

        [Header("Colour")]
        [SerializeField, Tooltip("The dark body as a share of the shooter's colour (an edge-on facet of a cooled shard). The fleet's two-tone: about a fifth of the signal.")]
        [Range(0f, 1f)] float bodyShare = 0.16f;
        [SerializeField, Tooltip("How far the white-hot colour leans from the shooter's colour toward white. 1 is pure white; lower keeps a hint of whose blast it is even in the flash.")]
        [Range(0f, 1f)] float hotWhiteness = 0.78f;

        [Header("Core (release)")]
        [SerializeField, Tooltip("Core radius at its widest, as a share of the blast radius.")]
        float coreRadiusShare = 0.075f;
        [SerializeField, Tooltip("Smallest and largest core radius, world units: a tap blast's core still reads, a full one never fills the chase camera.")]
        Vector2 coreRadiusClamp = new(1.5f, 8f);
        [SerializeField, Tooltip("Seconds the core takes to flash out to full size.")]
        float coreGrowSeconds = 0.05f;
        [SerializeField, Tooltip("Seconds it holds white-hot at full size.")]
        float coreHoldSeconds = 0.1f;
        [SerializeField, Tooltip("Seconds it then takes to cool and collapse to nothing.")]
        float coreFadeSeconds = 0.65f;
        [SerializeField, Tooltip("HDR glow of the white-hot core (the material's Heat Intensity, per blast).")]
        float coreHeatIntensity = 9f;

        [Header("Shock shell (release)")]
        [SerializeField, Tooltip("Draw the faceted shock shell IN PLACE of the blast's ripple sphere (the sweep, the trigger and the damage are untouched: only its photons are held).")]
        bool replaceRippleSphere = true;
        [SerializeField, Tooltip("Seconds the front stays hot (white) before it is the shooter's colour.")]
        float shellHeatSeconds = 0.18f;
        [SerializeField, Tooltip("The front's opacity at the release; it fades as (1 - growth)^Fade Power.")]
        [Range(0f, 1f)] float shellOpacity = 0.9f;
        [SerializeField, Tooltip("How fast the front fades as it grows (1 linear with growth, higher fades sooner).")]
        float shellFadePower = 1.4f;
        [SerializeField, Tooltip("HDR intensity of the front's rim.")]
        float shellIntensity = 3.2f;

        [Header("Light (release)")]
        [SerializeField, Tooltip("A real point light at the core for the flash: lights whatever in the world takes light.")]
        bool pointLight = true;
        [SerializeField, Tooltip("The light's range as a share of the blast radius.")]
        float lightRangeShare = 1.1f;
        [SerializeField, Tooltip("The light's intensity at the release.")]
        float lightIntensity = 14f;
        [SerializeField, Tooltip("Seconds for the light to fall to a third (it decays exponentially; off after three of these).")]
        float lightSeconds = 0.22f;

        [Header("Casing shards (aftermath)")]
        [SerializeField, Tooltip("Shards per blast.")]
        int shardCount = 34;
        [SerializeField, Tooltip("Shard length range as a share of the blast radius (most are small: sizes are drawn on a power curve).")]
        Vector2 shardSizeShare = new(0.008f, 0.04f);
        [SerializeField, Tooltip("Shard length clamp, world units.")]
        Vector2 shardSizeClamp = new(0.35f, 4.5f);
        [SerializeField, Tooltip("How far the farthest shard drifts, as a share of the blast radius (drag stops it there).")]
        Vector2 shardReachShare = new(0.12f, 0.6f);
        [SerializeField, Tooltip("Drag, per second: velocity falls by e every 1/drag seconds. Higher = the shards stop sooner and HANG.")]
        float shardDrag = 2.6f;
        [SerializeField, Tooltip("Shard life range, seconds.")]
        Vector2 shardLife = new(2.2f, 3.6f);
        [SerializeField, Tooltip("Shard cooling time range, seconds (heat falls by e over this).")]
        Vector2 shardCool = new(0.35f, 0.9f);
        [SerializeField, Tooltip("Largest tumble, radians per second.")]
        float shardSpin = 7f;
        [SerializeField, Tooltip("Tumble drag, per second (lower than the flight's, so a hanging shard still turns).")]
        float shardSpinDrag = 0.6f;

        [Header("Embers (aftermath)")]
        [SerializeField, Tooltip("Embers per blast.")]
        int emberCount = 80;
        [SerializeField, Tooltip("Ember size range as a share of the blast radius.")]
        Vector2 emberSizeShare = new(0.0015f, 0.005f);
        [SerializeField, Tooltip("Ember size clamp, world units.")]
        Vector2 emberSizeClamp = new(0.12f, 0.55f);
        [SerializeField, Tooltip("How far the farthest ember flies, as a share of the blast radius.")]
        Vector2 emberReachShare = new(0.3f, 0.95f);
        [SerializeField, Tooltip("Ember drag, per second.")]
        float emberDrag = 3.4f;
        [SerializeField, Tooltip("Ember life range, seconds.")]
        Vector2 emberLife = new(1.1f, 2.4f);
        [SerializeField, Tooltip("Ember cooling time range, seconds.")]
        Vector2 emberCool = new(0.2f, 0.5f);
        [SerializeField, Tooltip("Seconds of flight an ember is stretched over: its length is its size plus speed x this, so a fast ember is a streak and a slowed one a speck.")]
        float emberStreakSeconds = 0.025f;

        [Header("Both")]
        [SerializeField, Tooltip("Share of its life over which a shard or ember shrinks away at the end (opaque: it shrinks, never fades).")]
        [Range(0.05f, 1f)] float shrinkShare = 0.35f;
        [SerializeField, Tooltip("HDR glow of a white-hot shard (the shard material's Heat Intensity, per blast).")]
        float shardHeatIntensity = 6f;

        public Material CoreMaterial => coreMaterial;
        public Material ShardMaterial => shardMaterial;
        public Material ShellMaterial => shellMaterial;
        public float BodyShare => bodyShare;
        public float HotWhiteness => hotWhiteness;
        public float CoreRadiusShare => coreRadiusShare;
        public Vector2 CoreRadiusClamp => coreRadiusClamp;
        public float CoreGrowSeconds => coreGrowSeconds;
        public float CoreHoldSeconds => coreHoldSeconds;
        public float CoreFadeSeconds => coreFadeSeconds;
        public float CoreHeatIntensity => coreHeatIntensity;
        public bool ReplaceRippleSphere => replaceRippleSphere;
        public float ShellHeatSeconds => shellHeatSeconds;
        public float ShellOpacity => shellOpacity;
        public float ShellFadePower => shellFadePower;
        public float ShellIntensity => shellIntensity;
        public bool PointLight => pointLight;
        public float LightRangeShare => lightRangeShare;
        public float LightIntensity => lightIntensity;
        public float LightSeconds => lightSeconds;
        public int ShardCount => Mathf.Max(0, shardCount);
        public int EmberCount => Mathf.Max(0, emberCount);
        public float ShrinkShare => shrinkShare;
        public float ShardHeatIntensity => shardHeatIntensity;
        public float EmberStreakSeconds => emberStreakSeconds;

        /// <summary>The two kinds of debris, as one table the system reads.</summary>
        public BlastDebrisSpec Shards => new(shardSizeShare, shardSizeClamp, shardReachShare, shardDrag, shardLife,
            shardCool, shardSpin, shardSpinDrag, false);

        public BlastDebrisSpec Embers => new(emberSizeShare, emberSizeClamp, emberReachShare, emberDrag, emberLife,
            emberCool, 0f, 0f, true);

        /// <summary>The longest any part of this look lives after its release, seconds.</summary>
        public float LongestLife(float blastSeconds) => Mathf.Max(
            Mathf.Max(shardLife.y, emberLife.y),
            Mathf.Max(coreGrowSeconds + coreHoldSeconds + coreFadeSeconds, Mathf.Max(blastSeconds, lightSeconds * 3f)));
    }

    /// <summary>One kind of blast debris, read off <see cref="BlastLookSO"/>.</summary>
    public readonly struct BlastDebrisSpec
    {
        public readonly Vector2 SizeShare, SizeClamp, ReachShare, Life, Cool;
        public readonly float Drag, Spin, SpinDrag;
        public readonly bool Streaks;

        public BlastDebrisSpec(Vector2 sizeShare, Vector2 sizeClamp, Vector2 reachShare, float drag, Vector2 life,
            Vector2 cool, float spin, float spinDrag, bool streaks)
        {
            SizeShare = sizeShare;
            SizeClamp = sizeClamp;
            ReachShare = reachShare;
            Drag = Mathf.Max(0.01f, drag);
            Life = life;
            Cool = cool;
            Spin = spin;
            SpinDrag = spinDrag;
            Streaks = streaks;
        }
    }
}
