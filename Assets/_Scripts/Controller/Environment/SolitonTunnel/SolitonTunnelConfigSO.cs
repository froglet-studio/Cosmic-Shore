using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A SOLITON TUNNEL, as an environmental fundamental (Yash, 2026-10-29): every number that makes a tunnel (or a Soliton
    /// Network of them) look, take shape, live, and be ridden, with no vessel in it. A maker (the Meerkat's
    /// <see cref="MeerkatSolitonExecutor"/>), a <see cref="SolitonTunnelPlacer"/> in a scene, or any other spawner points at
    /// one of these. Edited inline, in play mode too, from <c>FrogletTools/Environment/Environmental Fundamentals</c>.
    ///
    /// <para><b>The ride</b> is a slip-and-slide: a ball in a tube. Gravity pulls the rider out to the inner wall (radially,
    /// whichever way the wall is), so it mostly slides pressed to the wall and now and then bounces; nothing turns its nose to
    /// the tube; its momentum carries it, and it leaves along its own velocity (a slingshot). RT gives a small push along the
    /// tube (<see cref="ThrustOn"/>, <see cref="Thrust"/>). There is no left-stick steering inside.</para>
    ///
    /// <para><b>The look</b> is the jelly lens (<c>SolitonTunnel.shader</c>, <see cref="SolitonTunnelLensPass"/>) plus the
    /// additive spindle bridge (<see cref="SolitonTunnelView"/>), tinted by the maker's domain colour (or
    /// <see cref="OverrideTint"/>). Field reference: <c>Docs/Shaders/WORMHOLE_SHADERS.md</c> and <c>Docs/WORMHOLES.md</c>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SolitonTunnelConfig", menuName = "ScriptableObjects/Environment/Soliton Tunnel Config")]
    public sealed class SolitonTunnelConfigSO : ScriptableObject
    {
        [Header("Look")]
        [Tooltip("The comfort master: scales every bend, lens, breath and pulse, and caps their speeds.")]
        [SerializeField] SolitonTunnelComfort comfort = SolitonTunnelComfort.Default;
        [Tooltip("The jelly lens: how the tube bends what you see through its wall, breathes, and catches the light.")]
        [SerializeField] SolitonTunnelLook look = SolitonTunnelLook.TunnelDefault;
        [Tooltip("How the tube looks from inside while you ride it (calmer).")]
        [SerializeField] SolitonTunnelRideView ride = SolitonTunnelRideView.Default;

        [Header("Look: Spindle Bridge")]
        [Tooltip("Draw the additive spindle bridge (rings of glowing spindles and ribs) over a formed tube.")]
        [SerializeField] bool bridgeOn = true;
        [Tooltip("The bridge's brightness on a formed tube (0 = off; the Meerkat's aim reticle has its own).")]
        [SerializeField, Range(0f, 1f)] float bridgeBrightness = 0.35f;
        [Tooltip("...rings along the tube")]
        [SerializeField, Range(2, 60)] int bridgeRings = 36;
        [Tooltip("...spindles per ring")]
        [SerializeField, Range(3, 32)] int bridgeSpindlesPerRing = 21;
        [Tooltip("...ribs running along it")]
        [SerializeField, Range(0, 12)] int bridgeRibs = 5;
        [Tooltip("...spindle thickness (u)")]
        [SerializeField, Range(0.05f, 4f)] float bridgeSpindleThickness = 0.25f;
        [Tooltip("...shimmer (a slow glow running along it)")]
        [SerializeField, Range(0f, 1f)] float bridgeShimmer = 0.5f;
        [Tooltip("...toward white (0 = the tint's own colour)")]
        [SerializeField, Range(0f, 1f)] float bridgeWhite = 0.3f;
        [Tooltip("A Soliton Network draws the bridge only on this many of its channels nearest the camera (0 = none).")]
        [SerializeField, Range(0, 16)] int bridgeMaxChannels = 4;

        [Header("Look: Domain Tint")]
        [Tooltip("Tint the tube with its maker's domain colour (a placed tube with no maker uses Override Tint).")]
        [SerializeField] bool tintFromMaker = true;
        [Tooltip("The tint when Tint From Maker is off, or the tube has no maker.")]
        [SerializeField] Color overrideTint = new(0.31f, 0.85f, 1f, 1f);
        [Tooltip("How strongly the tint colours the jelly and the bridge (0 = clear, untinted).")]
        [SerializeField, Range(0f, 1f)] float tintStrength = 0.35f;

        [Header("Shape: Single Tunnel")]
        [Tooltip("Bend radius (u)")]
        [SerializeField, Range(5f, 200f)] float bendRadius = 10f;
        [Tooltip("Oval: shortest axis (x the round's)")]
        [SerializeField, Range(0.05f, 1f)] float ovalShortest = 0.2f;
        [Tooltip("Oval: longest axis (x the round's)")]
        [SerializeField, Range(1f, 3f)] float ovalLongest = 1.1f;
        [Tooltip("Oval: max sharpness")]
        [SerializeField, Range(1f, 20f)] float ovalMaxSharpness = 8f;
        [Tooltip("Throat diameter (u)")]
        [SerializeField, Range(2f, 60f)] float throatDiameter = 7.1f;
        [Tooltip("Mouth diameter (u)")]
        [SerializeField, Range(4f, 120f)] float mouthDiameter = 35.6f;

        [Header("Shape: Soliton Network")]
        [Tooltip("Branch points (min)")]
        [SerializeField, Range(1, 40)] int networkBranchPointsMin = 6;
        [Tooltip("Branch points (max)")]
        [SerializeField, Range(1, 40)] int networkBranchPointsMax = 12;
        [Tooltip("Openings (min)")]
        [SerializeField, Range(1, 120)] int networkOpeningsMin = 16;
        [Tooltip("Openings (max)")]
        [SerializeField, Range(1, 120)] int networkOpeningsMax = 50;
        [Tooltip("Radius (min, u)")]
        [SerializeField, Range(20f, 2000f)] float networkRadiusMin = 100f;
        [Tooltip("Radius (max, u)")]
        [SerializeField, Range(20f, 2000f)] float networkRadiusMax = 531f;
        [Tooltip("Spread (u)")]
        [SerializeField, Range(10f, 1000f)] float networkSpread = 273f;
        [Tooltip("Randomness")]
        [SerializeField, Range(0f, 1f)] float networkRandomness = 0.45f;
        [Tooltip("Channel diameter (u)")]
        [SerializeField, Range(2f, 100f)] float networkDiameter = 10.5f;
        [Tooltip("Channel mouth diameter (u)")]
        [SerializeField, Range(2f, 150f)] float networkMouthDiameter = 13.9f;
        [Tooltip("Tip narrowing (1 = none)")]
        [SerializeField, Range(0.05f, 1f)] float networkTip = 0.309f;
        [Tooltip("Gnarl (how much a channel wanders)")]
        [SerializeField, Range(0f, 1f)] float networkGnarl = 0.779f;

        [Header("Life")]
        [Tooltip("A single tunnel forms over (s)")]
        [SerializeField, Range(0.05f, 3f)] float formSeconds = 0.4f;
        [Tooltip("Stays open (s) once its rider is out")]
        [SerializeField, Range(0f, 60f)] float staySeconds = 10f;
        [Tooltip("Collapses over (s)")]
        [SerializeField, Range(0.05f, 5f)] float collapseSeconds = 0.8f;
        [Tooltip("A Soliton Network digs out over (s)")]
        [SerializeField, Range(0.05f, 5f)] float networkFormSeconds = 0.8f;

        [Header("Ride: Slip and Slide")]
        [Tooltip("Gravity out to the inner wall (u/s²): the rider is pressed to whichever wall is nearest, a ball in a tube.")]
        [SerializeField, Range(0f, 400f)] float wallGravity = 60f;
        [Tooltip("Bounce off the wall (0 = sticks to it, 1 = bounces back fully).")]
        [SerializeField, Range(0f, 1f)] float bounce = 0.45f;
        [Tooltip("Friction while sliding on the wall (per second; small = slippery).")]
        [SerializeField, Range(0f, 2f)] float wallFriction = 0.05f;
        [Tooltip("RT pushes the rider along the tube (a little agency; off = pure slide).")]
        [SerializeField] bool thrustOn = true;
        [Tooltip("...by (u/s²) at full RT")]
        [SerializeField, Range(0f, 200f)] float thrust = 25f;
        [Tooltip("Never slower than this along the tube (u/s), so a rider never stalls inside.")]
        [SerializeField, Range(0f, 200f)] float minSpeed = 30f;
        [Tooltip("Top speed inside (u/s). Carried prisms and crystals move at this too.")]
        [SerializeField, Range(5f, 400f)] float maxSpeed = 119f;
        [Tooltip("Out: speed x this, along the rider's own velocity (the slingshot).")]
        [SerializeField, Range(0.5f, 3f)] float exitMultiplier = 1.2f;
        [Tooltip("The speed-tunnel effect while riding (x)")]
        [SerializeField, Range(0f, 10f)] float rideSpeedTunnelScale = 5f;

        public SolitonTunnelComfort Comfort => comfort;
        public SolitonTunnelLook Look => look;
        public SolitonTunnelRideView Ride => ride;

        public bool BridgeOn => bridgeOn && bridgeBrightness > 0f;
        public float BridgeBrightness => bridgeBrightness;
        public int BridgeRings => bridgeRings;
        public int BridgeSpindlesPerRing => bridgeSpindlesPerRing;
        public int BridgeRibs => bridgeRibs;
        public float BridgeSpindleThickness => bridgeSpindleThickness;
        public float BridgeShimmer => bridgeShimmer;
        public float BridgeWhite => bridgeWhite;
        public int BridgeMaxChannels => bridgeMaxChannels;

        public bool TintFromMaker => tintFromMaker;
        public Color OverrideTint => overrideTint;
        public float TintStrength => tintStrength;

        public float BendRadius => bendRadius;
        public float ThroatDiameter => throatDiameter;
        public float MouthDiameter => mouthDiameter;
        public float NetworkDiameter => networkDiameter;
        public float NetworkMouthDiameter => networkMouthDiameter;
        public float NetworkTip => networkTip;
        public float NetworkGnarl => networkGnarl;

        public float FormSeconds => formSeconds;
        public float StaySeconds => staySeconds;
        public float CollapseSeconds => collapseSeconds;
        public float NetworkFormSeconds => networkFormSeconds;

        public float WallGravity => wallGravity;
        public float Bounce => bounce;
        public float WallFriction => wallFriction;
        public bool ThrustOn => thrustOn;
        public float Thrust => thrustOn ? thrust : 0f;
        public float MinSpeed => minSpeed;
        public float MaxSpeed => maxSpeed;
        public float ExitMultiplier => exitMultiplier;
        public float RideSpeedTunnelScale => rideSpeedTunnelScale;

        /// <summary>The tint a tube made by a maker of <paramref name="makerColour"/> wears (rgb), at <see cref="TintStrength"/>
        /// (a). No maker (<paramref name="hasMaker"/> false) or <see cref="TintFromMaker"/> off: <see cref="OverrideTint"/>.</summary>
        public Color Tint(Color makerColour, bool hasMaker)
        {
            var c = tintFromMaker && hasMaker ? makerColour : overrideTint;
            c.a = Mathf.Clamp01(tintStrength);
            return c;
        }

        /// <summary>A single tunnel's tube shape (the reticle's ranges, <paramref name="shaftMin"/> to
        /// <paramref name="shaftMax"/> and <paramref name="bendMaxDegrees"/>, come from whoever aims it; a placed tunnel has its
        /// own). <paramref name="area"/> &gt; 0 sizes the tube by surface area instead of throat and mouth.</summary>
        public SolitonTunnelShapeMath.ShapeSettings Shape(float shaftMin, float shaftMax, float bendMaxDegrees, float area = 0f) => new()
        {
            ShaftMin = shaftMin, ShaftMax = shaftMax, BendRadius = bendRadius, BendMaxDegrees = bendMaxDegrees,
            ThroatRadius = 0.5f * throatDiameter, MouthRadius = 0.5f * mouthDiameter,
            OvalShortest = ovalShortest, OvalLongest = ovalLongest, OvalMaxSharpness = ovalMaxSharpness,
            Area = area,
        };

        /// <summary>A Soliton Network's layout (<see cref="SolitonTunnelMath.BuildLayout"/>).</summary>
        public SolitonTunnelMath.LayoutSettings NetworkLayout() => new()
        {
            BranchPointsMin = networkBranchPointsMin, BranchPointsMax = networkBranchPointsMax, OpeningsMin = networkOpeningsMin,
            OpeningsMax = networkOpeningsMax, RadiusMin = networkRadiusMin, RadiusMax = networkRadiusMax, Spread = networkSpread,
            Randomness = networkRandomness,
        };

        /// <summary>A network channel's size (<see cref="SolitonTunnelMath.Build"/>).</summary>
        public SolitonTunnelMath.ChannelSettings NetworkChannel() => new()
        {
            Diameter = networkDiameter, MouthDiameter = networkMouthDiameter, Gnarl = networkGnarl, Tip = networkTip,
        };

        /// <summary>The slide inside (<see cref="SolitonTunnelMath.Step"/>). <paramref name="rush"/> (Garrett's kit, 1 = none)
        /// scales the speeds and the push; <paramref name="tip"/> is the tube's tip narrowing (1 for a single tunnel).</summary>
        public SolitonTunnelMath.FlightSettings Flight(float rush = 1f, float tip = 1f) => new()
        {
            Gravity = wallGravity, Bounce = bounce, Friction = wallFriction, Thrust = Thrust * rush, MinSpeed = minSpeed * rush,
            MaxSpeed = maxSpeed * rush, Tip = tip, ExitMultiplier = exitMultiplier,
        };

        /// <summary>The network's flight (its own tip).</summary>
        public SolitonTunnelMath.FlightSettings NetworkFlight(float rush = 1f) => Flight(rush, networkTip);

        /// <summary>For the Environmental Fundamentals window: anything edited inline marks the asset dirty itself; this is
        /// the hook to clamp pairs after an edit.</summary>
        void OnValidate()
        {
            networkBranchPointsMax = Mathf.Max(networkBranchPointsMin, networkBranchPointsMax);
            networkOpeningsMax = Mathf.Max(networkOpeningsMin, networkOpeningsMax);
            networkRadiusMax = Mathf.Max(networkRadiusMin, networkRadiusMax);
            mouthDiameter = Mathf.Max(throatDiameter, mouthDiameter);
            maxSpeed = Mathf.Max(minSpeed, maxSpeed);
        }
    }
}
