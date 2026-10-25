using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>How the explosion's wedges are cut into a destruction's fragments. Explicit values: serialized.</summary>
    public enum DestructionCut
    {
        Sierpinski = 0,
        SilverGnomon = 1,
    }

    /// <summary>
    /// The DETAIL LEVELS of the prism's two fundamental interactions, game-wide (Docs/PRISM_ANIMATION.md
    /// §4.12): how a prism is DESTROYED and how it is STOLEN, drawn on high-resolution meshes
    /// (<c>PrismCollisionBurst</c>, <c>PrismCollisionBurst.hlsl</c>) when the viewer is close enough to see the
    /// detail and the budget allows, and as the ordinary effect otherwise. <c>PrismInteractionLod</c> decides.
    ///
    /// <list type="bullet">
    /// <item><b>Destruction</b> is one cut of the explosion's wedges scaled by recursion depth — the silver gnomon cut
    /// (<c>PrismSilverGnomonTiling</c>, round 7) or the Sierpinski cut (<c>PrismSierpinskiTiling</c>, round 4):
    /// level 0 IS the ordinary explosion (its four wedges a face — far away, off screen, over budget), and each
    /// finer level cuts the wedges deeper into a range of fragment sizes, each level the next with its finest
    /// fragments merged into their parents. Every fragment bursts out on its own direction (its face's radial,
    /// lifted, leaning along the hit), tumbles about cross(velocity, N) at the explosion's own capped rate, and shrinks
    /// away at its own end, the smallest first — a continuous noise keeps neighbours alike and never in step (round 9:
    /// the squint against the explosion is retired; the range of sizes lets the small ones go unseen far away).</item>
    /// <item><b>Stealing</b> is the GRADE (the 0.8 s colour lerp) far away and a SHED close up — a ROLL (the game's
    /// one shed since round 7; bloom, unravel and flake stay as weights at 0): each face's skin in the old colours
    /// rolls up away from the camera and round the prism until the prism hides it — at one of three mesh
    /// resolutions (12, 18, 24 subdivisions).</item>
    /// <item><b>Suction</b> (round 15) rides the destruction's ladder: the same cut at the same depth lines, every
    /// fragment drawn into the eater instead of bursting out; level 0 is the cut's depth 0 or the SuctionGraph.</item>
    /// </list>
    ///
    /// Both ladders are graded by DISTANCE (the prism's projected diameter over the screen height, so one number
    /// means the same on every camera and resolution) and by AMOUNT (how full the budget already is: crowded, one
    /// level coarser; packed, two). A level that does not fit the triangle budget steps down until one does.
    /// Lives at <c>Resources/PrismInteractionLodConfig</c>; absent, every interaction draws its ordinary effect.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismInteractionLodConfig",
        menuName = "ScriptableObjects/Prisms/Prism Interaction LOD Config")]
    public class PrismInteractionLodConfigSO : ScriptableObject
    {
        public const string ResourcePath = "PrismInteractionLodConfig";

        [Header("The carrier and the budget")]
        [Tooltip("The PrismCollisionBurst.shader material. Its look dials (fresnel, inside colour, dissolve noise) " +
                 "are authored on it; the shape below is written onto a runtime copy.")]
        [SerializeField] Material burstMaterial;

        [Tooltip("Detail-level entities alive at once, destruction and sheds together. Past it, every interaction " +
                 "draws its ordinary effect — never nothing.")]
        [SerializeField] int maxLiveBursts = 48;

        [Tooltip("Detail-level TRIANGLES alive at once. A level that would exceed it steps down to a cheaper one, " +
                 "and finally to the ordinary effect.")]
        [SerializeField] int maxLiveTriangles = 80_000;

        [Tooltip("AMOUNT: when the budget (entities or triangles, whichever is fuller) is at least this full, every " +
                 "new interaction is drawn one level coarser.")]
        [Range(0f, 1f)] [SerializeField] float crowdedFill = 0.5f;

        [Tooltip("AMOUNT: at least this full, two levels coarser.")]
        [Range(0f, 1f)] [SerializeField] float packedFill = 0.8f;

        [Header("Destruction — the explosion (level 0), then the cut of its wedges, finer as the prism nears")]
        [Tooltip("Off: every destruction is the ordinary explosion.")]
        [SerializeField] bool destructionEnabled = true;

        [Tooltip("Round 11 (the designer: unify the explosion with the levels): level 0 — every death the finer " +
                 "levels do not take, far away, off screen, plain or over their budget — is the FRAGMENT burst on the " +
                 "cut's depth 0, the explosion's own 24 wedges, so one law and one set of dials move every destruction. " +
                 "Off: level 0 is the explosion's own graph (ExplodingBlockMaterial), as before.")]
        [SerializeField] bool explosionAsFragments = true;

        [Tooltip("Level-0 bursts alive at once (24 triangles each, the explosion's own count; kept OUT of the detail " +
                 "budget above, so a field-wide wipe never crowds the close-up levels). Past it a death falls back to " +
                 "the explosion's graph — never nothing.")]
        [SerializeField] int maxLiveExplosions = 4096;

        [Tooltip("How the explosion's wedges are cut. SilverGnomon (the designer, 2026-10-15): silver triangles and " +
                 "their gnomons, a range of sizes in the silver ratio. Sierpinski: corners split at their midpoints, " +
                 "middles kept whole (round 4).")]
        [SerializeField] DestructionCut destructionCut = DestructionCut.SilverGnomon;

        [Tooltip("One line per detail level, from level 1: the prism's projected diameter, as a fraction of the " +
                 "screen height, at and above which a death is cut that finely. Below the first line, level 0: the " +
                 "ordinary explosion. Ascending; the array's length is the number of levels (at most 5).")]
        [SerializeField] float[] depthMinScreen = { 0.05f, 0.315f, 0.52f, 0.6f };

        [Tooltip("The silver gnomon cut's recursion depth at each level, from level 1 (24 x 8, 21, 55, 144 fragments " +
                 "at depths 4, 6, 8, 10). The Sierpinski cut's depth IS the level.")]
        [SerializeField] int[] silverDepths = { 4, 6, 8, 10 };

        [Header("Destruction — how each fragment flies (round 9: \"now I just want it to look awesome\")")]
        [Tooltip("How far each fragment bursts out, in prism radii, at the full ease (the ease's time constant is " +
                 "openSeconds).")]
        [Range(0f, 4f)] [SerializeField] float fragmentBurst = 0.36f;

        [Tooltip("And then how far it drifts on, in prism radii per second, so the cloud keeps opening while it fades.")]
        [Range(0f, 2f)] [SerializeField] float fragmentDrift = 0.216f;

        [Tooltip("Its direction is its face's own radial (from the face centre to the fragment) plus the face normal " +
                 "x this: 0 skims along the face, higher lifts the burst off it.")]
        [Range(0f, 3f)] [SerializeField] float fragmentLift = 2f;

        [Tooltip("Plus the hit's direction x this (x the speed boost): the burst leans the way the prism was struck.")]
        [Range(0f, 2f)] [SerializeField] float fragmentAim = 0.95f;

        [Tooltip("Round 10: a harder hit bursts WIDER and leans more along it, as the explosion's own spread grows with " +
                 "its speed. Distance and aim x (1 + this x (flight speed / fragmentSpeedRef - 1)). 0: every hit alike.")]
        [Range(0f, 2f)] [SerializeField] float fragmentSpeedGain = 1.65f;

        [Tooltip("The flight speed (u/s) of the gentlest hit, where the boost is 1: the explosion's slowest debris " +
                 "(PrismExplosion.prefab minSpeed).")]
        [Range(1f, 100f)] [SerializeField] float fragmentSpeedRef = 10f;

        [Tooltip("Per size level (each level about 0.71x the size) the smaller fragments fly this much farther: " +
                 "distance x (1 + this x level).")]
        [Range(0f, 1f)] [SerializeField] float fragmentSizeSpeed = 0.8f;

        [Tooltip("Per size level the smaller fragments spin this much faster: the explosion's angle x " +
                 "min(1 + this x level, fragmentSpinCap).")]
        [Range(0f, 1f)] [SerializeField] float fragmentSizeSpin = 0.25f;

        [Tooltip("The most any fragment's spin rate is multiplied over the explosion's, so nothing flickers.")]
        [Range(1f, 3f)] [SerializeField] float fragmentSpinCap = 1.6f;

        [Tooltip("The size level counted as the smallest (the cut's finest, about 4 at silver depth 10).")]
        [Range(1f, 6f)] [SerializeField] float fragmentLevelRef = 4f;

        [Tooltip("The share of the explosion's life at which the SMALLEST fragments are gone: they leave first, so far " +
                 "away they simply go unseen first.")]
        [Range(0.1f, 0.95f)] [SerializeField] float fragmentEndSmall = 0.45f;

        [Tooltip("The share of the life at which the LARGEST are gone. At least fragmentEndSmall.")]
        [Range(0.1f, 0.95f)] [SerializeField] float fragmentEndBig = 0.92f;

        [Tooltip("The share of its own life over which a fragment shrinks away to its centre (no dissolve).")]
        [Range(0.05f, 1f)] [SerializeField] float fragmentShrink = 0.35f;

        [Tooltip("How much each fragment's two spin angles wander: 1 + this x a gradient noise over its position. " +
                 "Continuous, so neighbours turn nearly alike, never in perfect step. 0: in perfect step.")]
        [FormerlySerializedAs("wedgeSpinNoise")]
        [Range(0f, 0.5f)] [SerializeField] float fragmentSpinNoise = 0.15f;

        [Tooltip("The noise's frequency, in cycles per prism width (object space): higher, neighbours differ more.")]
        [FormerlySerializedAs("wedgeNoiseFrequency")]
        [Range(0.5f, 8f)] [SerializeField] float fragmentNoiseFrequency = 3f;

        [Tooltip("How much each fragment's TRAVEL wanders: its distance x (1 + this x the noise), and its direction " +
                 "by this across and along its face. 0: a face's fragments leave on a perfect fan.")]
        [FormerlySerializedAs("wedgeSlideNoise")]
        [Range(0f, 0.5f)] [SerializeField] float fragmentTravelNoise = 0.48f;

        [Tooltip("Round 12: how much one death differs from the next, so two prisms struck alike never play the same " +
                 "animation. Hashed once per death (never per fragment), so a single explosion stays one smooth " +
                 "structure: at 1 its spread and opening time vary by 50%, its spin rate by 60%, its tumble plane " +
                 "turns up to 72 degrees, its lean tilts off the hit and the cloud is drawn out along a random axis " +
                 "by up to 60%. 0: every death alike.")]
        [Range(0f, 1f)] [SerializeField] float fragmentVariety = 0.5f;

        [Header("Suction — the destruction's cut and ladder, every fragment drawn into the eater (round 15)")]
        [Tooltip("Garrett, 2026-10-25: \"use the silver gnomen triangles that are used on the explosion shader on the " +
                 "suction shader. Use the same LOD system as well.\" On: a consumed prism comes apart along the " +
                 "destruction's cut at the depth its size on screen and the budget allow (the depth lines above), and " +
                 "every fragment is drawn into the eater. Off: every suction is the SuctionGraph's implosion.")]
        [SerializeField] bool suctionEnabled = true;

        [Tooltip("Level 0 — every suction the finer levels do not take, far away, off screen or over their budget — " +
                 "is the same kind on the cut's depth 0 (the explosion's 24 wedges), as explosionAsFragments does for " +
                 "the destruction. Off: level 0 is the SuctionGraph's implosion.")]
        [SerializeField] bool suctionAsFragments = true;

        [Tooltip("Level-0 suctions alive at once (24 triangles each; kept OUT of the detail budget). Past it a " +
                 "suction falls back to the SuctionGraph's implosion — never nothing.")]
        [SerializeField] int maxLiveSuctions = 1024;

        [Tooltip("The share of the implosion's life the fragments' STARTS are spread over: 0, all leave together; " +
                 "higher, one after another (the SuctionGraph consumes face by face). The rest of the life is each " +
                 "fragment's own trip, all of them in by 95% of it.")]
        [Range(0f, 0.85f)] [SerializeField] float suctionStagger = 0.5f;

        [Tooltip("Each trip's progress is (its own time)^this: it creeps off the face, then rushes in — the crystal " +
                 "capture's suction is never linear either. 1: linear.")]
        [Range(1f, 4f)] [SerializeField] float suctionAcceleration = 2.2f;

        [Tooltip("How far each fragment first rises off its face on the way in, in prism radii (the curve's control " +
                 "point), so the pieces visibly part before they are drawn in.")]
        [Range(0f, 2f)] [SerializeField] float suctionLift = 0.35f;

        [Tooltip("Turns each fragment winds about the line to the eater on the way in: the drain's vortex. Its hand " +
                 "and amount vary per suction with fragmentVariety.")]
        [Range(0f, 2f)] [SerializeField] float suctionSwirl = 0.35f;

        [Tooltip("Turns each fragment tumbles on the way in, the smaller a little faster up to fragmentSpinCap.")]
        [Range(0f, 3f)] [SerializeField] float suctionSpin = 0.75f;

        [Tooltip("The share of its own trip over which a fragment shrinks into the eater: it is gone the moment it " +
                 "arrives.")]
        [Range(0.05f, 1f)] [SerializeField] float suctionShrink = 0.45f;

        [Tooltip("Which go first: 0, the side facing the eater (the SuctionGraph's face order); 1, the smallest " +
                 "(as a destruction's smallest leave first).")]
        [Range(0f, 1f)] [SerializeField] float suctionSizeOrder = 0.35f;

        [Tooltip("How much a gradient noise over the prism moves each fragment's turn, so neighbours leave nearly " +
                 "together, never in step. 0: a perfect wave.")]
        [Range(0f, 0.3f)] [SerializeField] float suctionOrderNoise = 0.1f;

        [Header("Stealing — the grade, or a shed at one of three resolutions")]
        [Tooltip("Off: every steal is the ordinary grade.")]
        [SerializeField] bool shedEnabled = true;

        [Tooltip("At and above this projected diameter a steal SHEDS (at shedSubdivision) instead of grading.")]
        [Range(0f, 1f)] [SerializeField] float shedMinScreen = 0.08f;

        [Tooltip("At and above this, the shed is drawn at shedFineSubdivision.")]
        [Range(0f, 1f)] [SerializeField] float shedFineScreen = 0.24f;

        [Tooltip("At and above this, the shed is drawn at shedFinestSubdivision.")]
        [Range(0f, 1f)] [SerializeField] float shedFinestScreen = 0.28f;

        [Tooltip("Mesh subdivision of the coarsest shed. Keep the three a multiple of twice bloomRings and of " +
                 "bloomSegments (and of ribbonsPerFace and twice shardsPerFaceEdge for the other sheds), so every " +
                 "piece falls on whole triangles.")]
        [SerializeField] int shedSubdivision = 12;

        [Tooltip("Mesh subdivision of the finer shed (12 x subdivision^2 triangles).")]
        [SerializeField] int shedFineSubdivision = 18;

        [Tooltip("Mesh subdivision of the finest shed (24 = 6,912 triangles).")]
        [SerializeField] int shedFinestSubdivision = 24;

        [Tooltip("Relative weight of the ROLL shed (the designer, 2026-10-15): each face's skin rolls up into a tight " +
                 "roll, AWAY from the camera, over the edge and round until its own prism hides it — no dissolve. " +
                 "The game sheds as a roll only: the other weights ship at 0.")]
        [Min(0f)] [SerializeField] float rollWeight = 1f;

        [Tooltip("The roll's innermost turn, in prism radii (never less than about a mesh cell, so a turn stays round).")]
        [Range(0.01f, 0.3f)] [SerializeField] float rollRadius = 0.05f;

        [Tooltip("The gap between the roll's turns, in prism radii (never less than a cell chord's sag, so turns " +
                 "never cross): larger, a looser, fatter roll.")]
        [Range(0.001f, 0.1f)] [SerializeField] float rollGap = 0.01f;

        [Tooltip("Relative weight of the BLOOM shed (petals curling open off each face); 0 removes it. Round 5's " +
                 "shed; ships at 0 since the roll.")]
        [Min(0f)] [SerializeField] float bloomWeight;

        [Tooltip("Relative weight of the UNRAVEL shed (ribbons peeling off along each face); 0 removes it.")]
        [Min(0f)] [SerializeField] float unravelWeight;

        [Tooltip("Relative weight of the FLAKE shed (scales tipping off in a wave down the prism); 0 removes it.")]
        [Min(0f)] [SerializeField] float flakeWeight;

        [Tooltip("The bloom's CUTS across a quarter face: rings from the cube edge inward. Each ring is its own " +
                 "piece, hinged on its own outer edge, so more rings make shorter pieces that curl tighter and stay " +
                 "closer to the surface — less in the way of a vessel skimming the prism.")]
        [Range(1, 6)] [SerializeField] int bloomRings = 3;

        [Tooltip("The bloom's cuts ALONG each cube edge: segments per quarter face.")]
        [Range(1, 6)] [SerializeField] int bloomSegments = 3;

        [Tooltip("How differently each bloom piece peels: 0 every piece alike, 1 each with its own start, curl " +
                 "and lift. With the per-steal seed, no two steals peel the same way.")]
        [Range(0f, 1f)] [SerializeField] float peelNoise = 0.6f;

        [Tooltip("How far the shed skin sits outside the prism, in prism radii, so the two never share a depth.")]
        [Range(0f, 0.2f)] [SerializeField] float shedInflate = 0.02f;

        [Header("Shape (fine scale only — the envelope is the ordinary effect's)")]
        [Tooltip("How far a piece leaves, in prism radii. Capped at 1 so a destruction stays the explosion's size " +
                 "squinted at.")]
        [SerializeField] float spread = 0.55f;

        [Tooltip("Time constant of the opening (1 - e^(-t/tau)), seconds: short is a crack, long a slow bloom.")]
        [SerializeField] float openSeconds = 0.22f;

        [Tooltip("Shard tumble, radians per unit of explosion speed per second — the explosion's wedges spin with " +
                 "its speed the same way.")]
        [SerializeField] float spinPerSpeed = 0.3f;

        [Tooltip("A bloom petal tip's final curl, in turns (capped 0.8).")]
        [SerializeField] float bloomTurns = 0.7f;

        [Tooltip("An unravelling ribbon end's final curl, in turns (capped 0.85).")]
        [SerializeField] float unravelTurns = 0.8f;

        [Tooltip("Ribbons per face for the unravel.")]
        [SerializeField] int ribbonsPerFace = 6;

        [Tooltip("Shatter seeds per face edge (n x n shards a face); the flake's scales are a grid twice as fine.")]
        [SerializeField] int shardsPerFaceEdge = 3;

        [Tooltip("How ragged the dissolve is inside a piece (0: each piece goes whole). Glitter shards and flake " +
                 "scales always go whole.")]
        [SerializeField] float dissolveGrain = 0.35f;

        public Material BurstMaterial => burstMaterial;
        public int MaxLiveBursts => Mathf.Max(0, maxLiveBursts);
        public int MaxLiveTriangles => Mathf.Max(0, maxLiveTriangles);
        public bool ExplosionAsFragments => explosionAsFragments;
        public int MaxLiveExplosions => Mathf.Max(0, maxLiveExplosions);
        public float CrowdedFill => Mathf.Clamp01(crowdedFill);
        public float PackedFill => Mathf.Clamp01(packedFill);
        public bool DestructionEnabled => destructionEnabled;
        public bool SuctionEnabled => suctionEnabled;
        public bool SuctionAsFragments => suctionAsFragments;
        public int MaxLiveSuctions => Mathf.Max(0, maxLiveSuctions);
        public float SuctionStagger => Mathf.Clamp(suctionStagger, 0f, 0.85f);
        public float SuctionAcceleration => Mathf.Clamp(suctionAcceleration, 1f, 4f);
        public float SuctionLift => Mathf.Clamp(suctionLift, 0f, 2f);
        public float SuctionSwirl => Mathf.Clamp(suctionSwirl, 0f, 2f);
        public float SuctionSpin => Mathf.Clamp(suctionSpin, 0f, 3f);
        public float SuctionShrink => Mathf.Clamp(suctionShrink, 0.05f, 1f);
        public float SuctionSizeOrder => Mathf.Clamp01(suctionSizeOrder);
        public float SuctionOrderNoise => Mathf.Clamp(suctionOrderNoise, 0f, 0.3f);

        /// <summary>The deepest cut a death can be drawn at (the number of depth lines, capped).</summary>
        public int MaxDepth => depthMinScreen == null ? 0
            : Mathf.Min(depthMinScreen.Length, destructionCut == DestructionCut.SilverGnomon
                ? (silverDepths == null ? 0 : silverDepths.Length) : PrismSierpinskiTiling.MaxDepth);

        /// <summary>
        /// The recursion depth of the cut a death at detail <paramref name="level"/> (1 – <see cref="MaxDepth"/>) is
        /// drawn at: the level itself on the Sierpinski cut, <c>silverDepths[level - 1]</c> on the silver gnomon cut.
        /// </summary>
        public int CutDepthOf(int level) => level <= 0 ? 0
            : destructionCut == DestructionCut.SilverGnomon
                ? Mathf.Clamp(silverDepths[Mathf.Min(level, silverDepths.Length) - 1], 0, PrismSilverGnomonTiling.MaxDepth)
                : Mathf.Min(level, PrismSierpinskiTiling.MaxDepth);

        /// <summary>Triangles one death at detail <paramref name="level"/> draws.</summary>
        public int TrianglesOfLevel(int level) =>
            destructionCut == DestructionCut.SilverGnomon
                ? PrismSilverGnomonTiling.TriangleCount(CutDepthOf(level))
                : PrismSierpinskiTiling.TriangleCount(CutDepthOf(level));

        /// <summary>The screen-size line of depth <paramref name="depth"/> (1 – <see cref="MaxDepth"/>); depth 0 has none.</summary>
        public float DepthLine(int depth) =>
            depth >= 1 && depth <= MaxDepth ? Mathf.Clamp01(depthMinScreen[depth - 1]) : depth <= 0 ? 0f : float.PositiveInfinity;
        public bool ShedEnabled => shedEnabled;
        public float ShedMinScreen => Mathf.Clamp01(shedMinScreen);
        public float ShedFineScreen => Mathf.Clamp01(shedFineScreen);
        public float ShedFinestScreen => Mathf.Clamp01(shedFinestScreen);
        public int ShedSubdivision => Mathf.Clamp(shedSubdivision, 2, 32);
        public int ShedFineSubdivision => Mathf.Clamp(shedFineSubdivision, 2, 32);
        public int ShedFinestSubdivision => Mathf.Clamp(shedFinestSubdivision, 2, 32);
        public float BloomWeight => Mathf.Max(0f, bloomWeight);
        public float RollWeight => Mathf.Max(0f, rollWeight);
        public float RollRadius => Mathf.Clamp(rollRadius, 0.01f, 0.3f);
        public float RollGap => Mathf.Clamp(rollGap, 0.001f, 0.1f);
        public DestructionCut Cut => destructionCut;
        public float FragmentSpinNoise => Mathf.Clamp(fragmentSpinNoise, 0f, 0.5f);
        public float FragmentNoiseFrequency => Mathf.Clamp(fragmentNoiseFrequency, 0.5f, 8f);
        public float FragmentTravelNoise => Mathf.Clamp(fragmentTravelNoise, 0f, 0.5f);
        public float FragmentVariety => Mathf.Clamp01(fragmentVariety);
        public float FragmentBurst => Mathf.Clamp(fragmentBurst, 0f, 4f);
        public float FragmentDrift => Mathf.Clamp(fragmentDrift, 0f, 2f);
        public float FragmentLift => Mathf.Clamp(fragmentLift, 0f, 3f);
        public float FragmentAim => Mathf.Clamp(fragmentAim, 0f, 2f);
        public float FragmentSpeedGain => Mathf.Clamp(fragmentSpeedGain, 0f, 2f);
        public float FragmentSpeedRef => Mathf.Clamp(fragmentSpeedRef, 1f, 100f);
        public float FragmentSizeSpeed => Mathf.Clamp01(fragmentSizeSpeed);
        public float FragmentSizeSpin => Mathf.Clamp01(fragmentSizeSpin);
        public float FragmentSpinCap => Mathf.Clamp(fragmentSpinCap, 1f, 3f);
        public float FragmentLevelRef => Mathf.Clamp(fragmentLevelRef, 1f, 6f);
        public float FragmentEndSmall => Mathf.Clamp(fragmentEndSmall, 0.1f, 0.95f);
        public float FragmentEndBig => Mathf.Clamp(fragmentEndBig, 0.1f, 0.95f);
        public float FragmentShrink => Mathf.Clamp(fragmentShrink, 0.05f, 1f);
        public float UnravelWeight => Mathf.Max(0f, unravelWeight);
        public float FlakeWeight => Mathf.Max(0f, flakeWeight);
        public int BloomRings => Mathf.Clamp(bloomRings, 1, 6);
        public int BloomSegments => Mathf.Clamp(bloomSegments, 1, 6);
        public float PeelNoise => Mathf.Clamp01(peelNoise);
        public float ShedInflate => Mathf.Clamp(shedInflate, 0f, 0.2f);
        public float Spread => Mathf.Clamp01(spread);
        public float OpenSeconds => Mathf.Max(0.01f, openSeconds);
        public float SpinPerSpeed => Mathf.Max(0f, spinPerSpeed);
        public float BloomTurns => Mathf.Clamp(bloomTurns, 0f, 0.8f);
        public float UnravelTurns => Mathf.Clamp(unravelTurns, 0f, 0.85f);
        public int RibbonsPerFace => Mathf.Clamp(ribbonsPerFace, 1, 12);
        public int ShardsPerFaceEdge => Mathf.Clamp(shardsPerFaceEdge, 1, 6);
        public float DissolveGrain => Mathf.Clamp01(dissolveGrain);

        /// <summary>The screen-size lines of the steal ladder, coarse to fine (shed, fine, finest).</summary>
        public float StealLine(int level) => level switch
        {
            1 => ShedMinScreen,
            2 => ShedFineScreen,
            3 => ShedFinestScreen,
            _ => 0f,
        };

        /// <summary>The mesh subdivision of steal level 1–3.</summary>
        public int ShedSubdivisionOf(int level) => level >= 3 ? ShedFinestSubdivision : level == 2 ? ShedFineSubdivision : ShedSubdivision;

        /// <summary>The most triangles one entity of any level can draw.</summary>
        public int LargestEntityTriangles =>
            Mathf.Max(TrianglesOfLevel(MaxDepth), 12 * ShedFinestSubdivision * ShedFinestSubdivision);

        /// <summary>
        /// Whether the bloom's rings and segments fall on grid lines of a shed mesh of <paramref name="subdivision"/>:
        /// a ring boundary sits at face coordinate (1 ± j / rings) / 2, a segment boundary at s / segments.
        /// </summary>
        public bool BloomCutsFit(int subdivision) =>
            subdivision % (2 * BloomRings) == 0 && subdivision % BloomSegments == 0;

        bool SilverDepthsAscend()
        {
            if (destructionCut != DestructionCut.SilverGnomon) return true;
            if (silverDepths == null || silverDepths.Length < MaxDepth) return false;
            for (int l = 2; l <= MaxDepth; l++)
                if (CutDepthOf(l) <= CutDepthOf(l - 1)) return false;
            return true;
        }

        bool DepthLinesAscend()
        {
            for (int d = 2; d <= MaxDepth; d++)
                if (DepthLine(d) < DepthLine(d - 1)) return false;
            return true;
        }

        /// <summary>
        /// The one predicate the tests and the runtime share: each ladder is ordered, the crowding steps are ordered,
        /// the small fragments leave no later than the large, a shed has a kind to pick, every bloom piece falls on whole triangles of every shed mesh, and the budget
        /// is bounded and holds at least one entity of every level.
        /// </summary>
        public bool IsSane =>
            DepthLinesAscend() &&
            ShedMinScreen <= ShedFineScreen && ShedFineScreen <= ShedFinestScreen &&
            ShedSubdivision <= ShedFineSubdivision && ShedFineSubdivision <= ShedFinestSubdivision &&
            CrowdedFill <= PackedFill &&
            (!ShedEnabled || BloomWeight + UnravelWeight + FlakeWeight + RollWeight > 0f) &&
            SilverDepthsAscend() &&
            FragmentEndSmall <= FragmentEndBig &&
            (BloomWeight <= 0f || (BloomCutsFit(ShedSubdivision) && BloomCutsFit(ShedFineSubdivision) &&
                                   BloomCutsFit(ShedFinestSubdivision))) &&
            MaxLiveTriangles <= 200_000 && LargestEntityTriangles <= MaxLiveTriangles;

        static PrismInteractionLodConfigSO s_instance;
        static bool s_resolved;

        /// <summary><c>Resources/PrismInteractionLodConfig</c>, resolved once and cached; null when absent.</summary>
        public static PrismInteractionLodConfigSO Instance
        {
            get
            {
                if (!s_resolved)
                {
                    s_instance = Resources.Load<PrismInteractionLodConfigSO>(ResourcePath);
                    s_resolved = true;
                }
                return s_instance;
            }
        }

        /// <summary>Editor tooling: forget the cached asset so the next ask re-reads it.</summary>
        public static void InvalidateInstance()
        {
            s_instance = null;
            s_resolved = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => InvalidateInstance();
    }
}
