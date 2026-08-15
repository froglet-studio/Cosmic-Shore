using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// THE single tuning source for the fleet-wide vessel jet FX law (see
    /// <c>Docs/VESSEL_JET_FX.md</c>). Per CLAUDE.md config separation, every knob that
    /// decides how a vessel's jets look lives here — never on a vessel prefab — so the
    /// fleet cannot drift apart one prefab at a time.
    ///
    /// The law is TWO cooperating layers, both domain-tinted through the same
    /// <see cref="CosmicShore.Gameplay.VesselTrailCustomization"/> pass:
    ///
    ///  1. BEACON RIBBON — one long, wide <c>TrailRenderer</c> streaming behind the hull.
    ///     Tuned so OTHER players can find the vessel: it is the vessel's signature at
    ///     ranges where the hull is a few pixels.
    ///  2. ENGINE PLUMES — one short, bright FX per engine mount. Tuned as feedback for the
    ///     PILOT, who sees their own engines from the chase camera.
    ///
    /// The Squirrel authored both by hand and is the reference the rest of the fleet is
    /// measured against; the numbers below reproduce its proportions on any hull.
    /// </summary>
    [CreateAssetMenu(fileName = "VesselJetFXConfig", menuName = "ScriptableObjects/Vessel/Jet FX Config")]
    public class VesselJetFXConfigSO : ScriptableObject
    {
        [Header("Prefabs")]
        [Tooltip("The per-engine plume FX. Leave empty to disable the plume layer fleet-wide. " +
                 "Reference implementation: _Prefabs/Spacevessels/Components/Jet/jet.prefab.")]
        [SerializeField] GameObject enginePlumePrefab;

        [Tooltip("The long ribbon that makes a vessel findable at distance. Leave empty to " +
                 "disable the beacon layer fleet-wide. Reference implementation: " +
                 "_Prefabs/Spacevessels/Components/TrailEmpty.prefab.")]
        [SerializeField] GameObject beaconRibbonPrefab;

        [Header("Engine mount resolution (by name, case-insensitive)")]
        [Tooltip("A transform whose name CONTAINS any of these is a candidate engine mount. " +
                 "Matches the naming every vessel model already uses: Dolphin 'Engine Left.1', " +
                 "Urchin 'JetTopLeft', Grizzly 'Ship_Wedge_Jet_UL', Rhino 'engine left'.")]
        [SerializeField] string[] mountNameTokens = { "jet", "engine", "thruster", "exhaust" };

        [Tooltip("A candidate whose name contains any of these is REJECTED even if it matched " +
                 "above. Two families: the housings and shrouds that WRAP a nozzle rather than " +
                 "being one ('Engine case Left.1' on the Dolphin, 'ShroudTopLeft' on the " +
                 "Urchin, 'bbone_FrontEngineTrim.L' on the Squirrel), and existing FX objects " +
                 "that merely have engine-ish names ('JetFX', 'JetTest', 'LeftJetParticle' on " +
                 "the Rhino) — hanging a plume on one of those stacks FX on FX.\n\n" +
                 "NOTE: these are SUBSTRING tests, so a token can swallow a longer word that " +
                 "contains it. 'rig' was tried here for the Serpent's EngineRig and silently " +
                 "deleted every RIGHT-side engine in the fleet — 'right' contains 'rig' — " +
                 "leaving every vessel firing only its port engines. Always check a new token " +
                 "against the resolved mount list (FrogletTools > Vessels > Audit Vessel Jet FX) " +
                 "before adding it.")]
        [SerializeField] string[] mountExcludeTokens =
            { "case", "shroud", "hold", "trim", "frame", "gun", "fx", "particle", "test" };

        [Header("Sizing")]
        [Tooltip("Plume world size as a multiple of the MOUNT's own renderer bounds, used " +
                 "whenever the mount has a visible nozzle mesh. A jet should be about as wide " +
                 "as the engine it comes out of, so mount-derived sizing beats a global constant.")]
        [SerializeField] float plumeScalePerMountSize = 1.0f;

        [Tooltip("Fallback plume world size, as a fraction of the vessel's circumscribed hull " +
                 "radius. Used when the mount is a bare BONE with no renderer (Sparrow tails, " +
                 "Serpent EngineBone, the derived Manta mounts).")]
        [SerializeField] float plumeScalePerHullRadius = 0.11f;

        [Tooltip("Plume length as a multiple of its width. The reference jet.prefab is a wide, " +
                 "shallow flare rather than a long cone.")]
        [SerializeField] float plumeLengthAspect = 0.22f;

        [Header("Beacon placement")]
        [Tooltip("How many beacon ribbons, spread symmetrically across the hull. The Squirrel " +
                 "authors a PAIR — never a single ribbon on the centreline, which would hang " +
                 "straight down the middle of the pilot's view.")]
        [SerializeField] int beaconCount = 2;

        [Tooltip("Lateral offset of each ribbon from the centreline, as a fraction of the " +
                 "circumscribed hull radius. The Squirrel authors +/-4 world units.")]
        [SerializeField] float beaconLateralPerHullRadius = 0.45f;

        [Tooltip("How far behind the hull the ribbons START, as a fraction of THIS VESSEL'S OWN " +
                 "camera follow distance. The beacon is for other players, so it must begin at " +
                 "or behind the pilot's camera and never obstruct their view.\n\n" +
                 "This is measured against the CAMERA, not the hull, because the fleet's camera " +
                 "distances span 17 (Squirrel) to 250 (Serpent) — a hull-relative offset that " +
                 "reads fine on the Squirrel would put the Serpent's ribbon directly in its " +
                 "pilot's face.\n\n" +
                 "1.0 places the ribbon head exactly at the camera plane. The Squirrel's " +
                 "authored pair measures 12 / 17 = 0.71, which is slightly in FRONT of its " +
                 "camera — close enough not to obstruct because of the lateral offset above, " +
                 "and it lets the pilot glimpse their own ribbon. Pull this toward 0.71 if the " +
                 "beacon reads as invisible to its own pilot.")]
        [SerializeField] float beaconDepthPerCameraDistance = 1.0f;

        [Tooltip("Fallback ribbon depth as a fraction of hull radius, used only when a vessel " +
                 "has no CameraSettingsSO to measure a camera distance from.")]
        [SerializeField] float beaconFallbackDepthPerHullRadius = 1.6f;

        [Header("Budget")]
        [Tooltip("Hard cap on engine plumes per vessel. Each plume instantiates the plume " +
                 "prefab's full particle stack, so this is the fleet's per-vessel FX budget. " +
                 "The Dolphin has 6 engine mounts, the widest in the fleet.")]
        [SerializeField] int maxEnginePlumes = 6;

        [Header("Fallback mounts (models with no jet geometry)")]
        [Tooltip("How many plumes to derive when a model exposes NO engine mount at all. The " +
                 "Manta family (Manta/Falcon/Shrike/Termite) all share Manta_shapekey_rigged.fbx, " +
                 "which has only chassis and wing bones; the Sparrow's model has guns and tails " +
                 "but no engines.")]
        [SerializeField] int derivedMountCount = 2;

        [Tooltip("Lateral offset of each derived mount, as a fraction of hull radius. Plumes " +
                 "belong OUT TO THE SIDES where a vessel's engines would be, not stacked on the " +
                 "centreline — that is what makes them read as engines from the cockpit.")]
        [SerializeField] float derivedMountSpreadPerHullRadius = 0.5f;

        [Tooltip("How far back derived mounts sit, as a fraction of hull radius. Negative is " +
                 "behind: plumes come out of the BACK of the ship.")]
        [SerializeField] float derivedMountDepthPerHullRadius = -0.6f;

        public GameObject EnginePlumePrefab => enginePlumePrefab;
        public GameObject BeaconRibbonPrefab => beaconRibbonPrefab;
        public string[] MountNameTokens => mountNameTokens;
        public string[] MountExcludeTokens => mountExcludeTokens;
        public float PlumeScalePerMountSize => plumeScalePerMountSize;
        public float PlumeScalePerHullRadius => plumeScalePerHullRadius;
        public float PlumeLengthAspect => plumeLengthAspect;
        public int BeaconCount => beaconCount;
        public float BeaconLateralPerHullRadius => beaconLateralPerHullRadius;
        public float BeaconDepthPerCameraDistance => beaconDepthPerCameraDistance;
        public float BeaconFallbackDepthPerHullRadius => beaconFallbackDepthPerHullRadius;
        public int MaxEnginePlumes => maxEnginePlumes;
        public int DerivedMountCount => derivedMountCount;
        public float DerivedMountSpreadPerHullRadius => derivedMountSpreadPerHullRadius;
        public float DerivedMountDepthPerHullRadius => derivedMountDepthPerHullRadius;

        /// <summary>
        /// How far behind the vessel its own camera sits, in world units — the quantity the
        /// beacon is placed against. Uses the FULL offset magnitude so a camera lifted above the
        /// hull (the Sparrow's is at y +10) still counts its height, and the CLOSEST distance a
        /// dynamic camera can reach, because that is the worst case for obstructing the pilot.
        /// Returns 0 when there is nothing to measure, which is the caller's cue to fall back to
        /// a hull-relative offset.
        /// </summary>
        public static float ResolveCameraDistance(CameraSettingsSO settings)
        {
            if (settings == null) return 0f;
            if (settings.mode.HasFlag(CameraMode.DynamicCamera) && settings.dynamicMinDistance > 0f)
                return settings.dynamicMinDistance;
            return settings.followOffset.magnitude;
        }

        /// <summary>
        /// Lateral offsets for <see cref="BeaconCount"/> ribbons, spread symmetrically about the
        /// centreline. Even counts straddle it (a pair lands at -spread and +spread); an odd
        /// count puts one ribbon on it. Pure, so the placement is testable without a vessel.
        /// </summary>
        public static float BeaconLateralOffset(int index, int count, float spread)
        {
            if (count <= 1) return 0f;
            float t = count == 2 ? (index == 0 ? -1f : 1f)
                                 : Mathf.Lerp(-1f, 1f, index / (float)(count - 1));
            return t * spread;
        }

        /// <summary>
        /// Name test shared by the runtime resolver and the editor auditor, so the audit and
        /// the game can never disagree about what counts as an engine mount.
        /// </summary>
        public bool IsMountName(string name) =>
            IsMountNameLoose(name) && !HasToken(name, mountExcludeTokens);

        /// <summary>
        /// The SAME token match WITHOUT the exclusion list — "does this name mention an engine
        /// at all". Used only to detect FX a vessel already authors, where the two error
        /// directions are not symmetric: a false positive merely declines to add a layer the
        /// vessel probably has, while a false negative DOUBLES it. The Squirrel is the case
        /// that forces this — its authored jets hang off bones named
        /// <c>bbone_BackEngineFrame.L</c> and <c>bbone_FrontEngineTrim.L</c>, both of which the
        /// exclusion list (correctly, for spawning) rejects as cowlings.
        /// </summary>
        public bool IsMountNameLoose(string name) => HasToken(name, mountNameTokens);

        static bool HasToken(string name, string[] tokens)
        {
            if (string.IsNullOrEmpty(name) || tokens == null) return false;
            foreach (var token in tokens)
            {
                if (string.IsNullOrEmpty(token)) continue;
                if (name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>Guards against an asset edited into a state that would spawn nothing or spam.</summary>
        public bool IsSane =>
            maxEnginePlumes > 0 && maxEnginePlumes <= 32 &&
            plumeScalePerMountSize > 0f &&
            plumeScalePerHullRadius > 0f &&
            plumeLengthAspect > 0f &&
            derivedMountCount >= 0 && derivedMountCount <= 8 &&
            beaconCount > 0 && beaconCount <= 8 &&
            beaconDepthPerCameraDistance > 0f &&
            beaconFallbackDepthPerHullRadius > 0f &&
            mountNameTokens is { Length: > 0 };

        void OnValidate()
        {
            maxEnginePlumes = Mathf.Clamp(maxEnginePlumes, 1, 32);
            beaconCount = Mathf.Clamp(beaconCount, 1, 8);
            beaconDepthPerCameraDistance = Mathf.Max(0.0001f, beaconDepthPerCameraDistance);
            beaconFallbackDepthPerHullRadius = Mathf.Max(0.0001f, beaconFallbackDepthPerHullRadius);
            derivedMountCount = Mathf.Clamp(derivedMountCount, 0, 8);
            plumeScalePerMountSize = Mathf.Max(0.0001f, plumeScalePerMountSize);
            plumeScalePerHullRadius = Mathf.Max(0.0001f, plumeScalePerHullRadius);
            plumeLengthAspect = Mathf.Max(0.0001f, plumeLengthAspect);
        }
    }
}
