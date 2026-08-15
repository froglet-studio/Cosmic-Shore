using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Gives every vessel the Squirrel's two-layer jet FX (see <c>Docs/VESSEL_JET_FX.md</c>).
    ///
    /// The two layers are deliberately tuned for two different VIEWERS, which is why both are
    /// needed and why neither substitutes for the other:
    ///
    ///  - BEACON RIBBON — long and wide, streaming behind the hull. It is what lets OTHER
    ///    players find the vessel across a cell, where the hull itself is a few pixels.
    ///  - ENGINE PLUMES — short and bright, one per engine mount. It is feedback for the
    ///    PILOT, who sees their own engines from the chase camera and reads throttle and
    ///    attitude off them.
    ///
    /// Both layers are <see cref="TrailRenderer"/>-bearing, which is the whole point: that is
    /// what makes <see cref="VesselTrailCustomization"/> repaint BOTH on every domain change
    /// through the one existing <c>ShipHelper.SetShipProperties</c> → <c>IVessel.SetTrailColors</c>
    /// path. A jet layer that did not carry a TrailRenderer would be a jet that lies about
    /// whose vessel it is.
    ///
    /// MOUNTS ARE RESOLVED BY NAME, exactly as <see cref="VesselAnimation.ResolvePart"/>
    /// already resolves animated parts. That is the only mechanism that works uniformly across
    /// the fleet: some vessels expose their engines as real GameObjects (Dolphin
    /// "Engine Left.1", Grizzly "Ship_Wedge_Jet_UL", Urchin "JetTopLeft", Rhino "engine left")
    /// and some only as FBX BONES (Sparrow tails, Serpent "EngineBone"). Hand-authoring onto
    /// bones is not reliably possible — the vessel FBX metas ship an EMPTY
    /// <c>internalIDToNameTable</c>, so a bone has no stable name→fileID mapping to author
    /// against — while at runtime every bone is just a named Transform.
    ///
    /// COST: instantiation only, at Initialize. Plumes are parented to their mount and aligned
    /// ONCE, so a swinging engine bone carries its plume with it (engine gimbal, free) and
    /// there is no per-frame CPU in this component at all — no Update, no LateUpdate.
    /// </summary>
    public class VesselJetFX : MonoBehaviour
    {
        const string ConfigResourcePath = "VesselJetFXConfig";
        // Deliberately free of any mount token ("jet"/"engine"/...): these objects live
        // under the vessel and would otherwise be re-resolved as engine mounts.
        const string SpawnedRootName = "VesselFX (spawned)";

        [Header("Config")]
        [Tooltip("Fleet-wide jet FX tuning. Left empty, loads Resources/VesselJetFXConfig.")]
        [SerializeField] VesselJetFXConfigSO config;

        [Header("Layers")]
        [Tooltip("Whether this vessel WANTS engine plumes. A layer the vessel already AUTHORS " +
                 "is never doubled regardless of this flag — see the authored-FX detection in " +
                 "Initialize. Turn OFF only to deliberately deny a vessel the layer.")]
        [SerializeField] bool spawnEnginePlumes = true;

        [Tooltip("Whether this vessel WANTS the long beacon ribbon. A vessel that already " +
                 "authors a trail (Squirrel, Dolphin and Sparrow each ship a TrailEmpty) keeps " +
                 "its authored one and is not given a second.")]
        [SerializeField] bool spawnBeaconRibbon = true;

        [Header("Mounts")]
        [Tooltip("Explicit engine mounts. Authored references always win over name resolution — " +
                 "use this when a model's naming does not describe its engines, or to override " +
                 "which subset of a many-engine model actually fires.")]
        [SerializeField] List<Transform> mountOverrides = new();

        readonly List<TrailRenderer> _spawnedTrails = new();
        Transform _spawnedRoot;
        bool _initialized;

        /// <summary>Trails this component created, for the auditor and for tests.</summary>
        public IReadOnlyList<TrailRenderer> SpawnedTrails => _spawnedTrails;

        public VesselJetFXConfigSO Config
        {
            get
            {
                if (config == null) config = Resources.Load<VesselJetFXConfigSO>(ConfigResourcePath);
                return config;
            }
        }

        /// <summary>
        /// Called from <c>VesselController.Initialize</c> BEFORE
        /// <c>ShipHelper.SetShipProperties</c>, so the trails this spawns exist in time to be
        /// caught by the FIRST domain paint rather than staying prefab-coloured until the
        /// player happens to change domain.
        /// </summary>
        public void Initialize(IVesselStatus vesselStatus)
        {
            if (_initialized) return;
            _initialized = true;

            var cfg = Config;
            if (cfg == null)
            {
                CSDebug.LogWarning(
                    $"[VesselJetFX] No VesselJetFXConfigSO on {name} and none at " +
                    $"Resources/{ConfigResourcePath}. This vessel will have no jet FX. " +
                    "Create one via Assets > Create > ScriptableObjects > Vessel > Jet FX Config.");
                return;
            }
            if (!cfg.IsSane)
            {
                CSDebug.LogError($"[VesselJetFX] {cfg.name} is out of range (see IsSane). Skipping jet FX on {name}.");
                return;
            }

            float hullRadius = PrismOcclusionCorridor.MeasureCircumscribedRadius(transform);
            if (hullRadius <= Mathf.Epsilon)
            {
                CSDebug.LogWarning($"[VesselJetFX] Could not measure a hull radius for {name}; jet FX skipped.");
                return;
            }

            // AUTHORED FX WINS, ALWAYS. Three vessels hand-author part of this law already —
            // the Squirrel authors both layers (it is the reference the rest of the fleet is
            // being brought up to) and the Dolphin and Sparrow each author a beacon ribbon.
            // Detecting that here, rather than trusting a per-prefab bool, is what makes the
            // component safe to add anywhere: VesselStatus.JetFX can GetOrAdd one at runtime
            // with default field values, and a vessel that already has its FX still cannot end
            // up with two beacons or a doubled stack on every engine.
            var preexistingTrails = GetComponentsInChildren<TrailRenderer>(includeInactive: true);

            _spawnedRoot = new GameObject(SpawnedRootName).transform;
            _spawnedRoot.SetParent(transform, worldPositionStays: false);

            // A vessel's beacon IS its long trail. If it already draws one, it has a beacon.
            bool beaconAuthored = preexistingTrails.Length > 0;
            bool plumesAuthored = AnyTrailUnderMounts(preexistingTrails, cfg);

            if (spawnBeaconRibbon && !beaconAuthored) SpawnBeacons(cfg, hullRadius);
            if (spawnEnginePlumes && !plumesAuthored)
                SpawnPlumes(cfg, hullRadius, ResolveMounts(cfg, hullRadius));

            // The trails must be tinted for the domain the vessel ALREADY has. VesselController
            // paints right after this, but ChangePlayer and a mid-life re-init both re-enter
            // through paths that may not, so ask the tint component to pick up what we added.
            GetComponentInChildren<VesselTrailCustomization>(includeInactive: true)?.Refresh();
        }

        /// <summary>
        /// Places the beacon ribbons the way the Squirrel authors them: a symmetric PAIR, offset
        /// laterally, starting at (or behind) the pilot's own camera.
        ///
        /// Both parts of that are deliberate and were the Squirrel's design, not decoration:
        /// - the ribbons are OFF the centreline (Squirrel: +/-4) so nothing hangs down the middle
        ///   of the pilot's view;
        /// - the depth is measured against THIS VESSEL'S CAMERA, not its hull, so the ribbon
        ///   starts behind the camera and cannot obstruct the pilot. That reference matters
        ///   enormously across this fleet: camera follow distance runs from 17 on the Squirrel to
        ///   250 on the Serpent, so a hull-relative offset tuned on one is in the other's face.
        /// The ribbon is for OTHER players; the pilot's engine feedback is the plume layer.
        /// </summary>
        void SpawnBeacons(VesselJetFXConfigSO cfg, float hullRadius)
        {
            if (cfg.BeaconRibbonPrefab == null) return;

            // Implicit bool rather than `?.` — a UnityEngine.Object's null is overloaded, and a
            // destroyed component would slip past a reference-null check (CLAUDE.md).
            var cameraCustomizer = GetComponent<VesselCameraCustomizer>();
            float cameraDistance = VesselJetFXConfigSO.ResolveCameraDistance(
                cameraCustomizer ? cameraCustomizer.Settings : null);

            float depth = cameraDistance > Mathf.Epsilon
                ? cameraDistance * cfg.BeaconDepthPerCameraDistance
                : hullRadius * cfg.BeaconFallbackDepthPerHullRadius;

            float lateral = hullRadius * cfg.BeaconLateralPerHullRadius;

            for (int i = 0; i < cfg.BeaconCount; i++)
            {
                var beacon = Instantiate(cfg.BeaconRibbonPrefab, _spawnedRoot);
                beacon.name = $"BeaconRibbon_{i}";
                beacon.transform.localPosition = new Vector3(
                    VesselJetFXConfigSO.BeaconLateralOffset(i, cfg.BeaconCount, lateral), 0f, -depth);
                beacon.transform.localRotation = Quaternion.identity;
                CollectTrails(beacon);
            }
        }

        /// <summary>
        /// True when an ALREADY-EXISTING trail hangs somewhere under an engine-named ancestor —
        /// i.e. this vessel authors its own engine plumes (the Squirrel parents a jet.prefab
        /// under each of its four engine bones). Checked by walking UP from each trail using the
        /// LOOSE name test rather than down from the filtered mount list, because the exclusion
        /// tokens that correctly stop us spawning on a cowling would otherwise hide an authored
        /// plume that hangs off one — and a missed detection doubles the Squirrel's jets.
        /// </summary>
        bool AnyTrailUnderMounts(TrailRenderer[] trails, VesselJetFXConfigSO cfg)
        {
            if (trails == null) return false;
            foreach (var trail in trails)
            {
                if (trail == null) continue;
                for (var p = trail.transform; p != null && p != transform; p = p.parent)
                    if (cfg.IsMountNameLoose(p.name)) return true;
            }
            return false;
        }

        void SpawnPlumes(VesselJetFXConfigSO cfg, float hullRadius, List<Transform> mounts)
        {
            if (cfg.EnginePlumePrefab == null) return;

            for (int i = 0; i < mounts.Count; i++)
            {
                var mount = mounts[i];
                if (mount == null) continue;

                var plume = Instantiate(cfg.EnginePlumePrefab, mount);
                plume.name = $"Plume_{i}";
                plume.transform.localPosition = Vector3.zero;

                // Align to the VESSEL, not to the mount: an engine bone's rest orientation is
                // authored for the art (the Dolphin's cases rest at 26-169 degrees), so
                // inheriting it would fire plumes sideways through the hull. Setting world
                // rotation once means a bone that later swings carries its plume along — the
                // gimbal reads as intentional and costs nothing per frame.
                plume.transform.rotation = transform.rotation;

                ApplyPlumeScale(plume.transform, mount, cfg, hullRadius);
                CollectTrails(plume);
            }
        }

        /// <summary>
        /// Sizes a plume in WORLD units and then divides out the mount's own lossy scale.
        /// That division is load-bearing: engine mounts across the fleet carry wildly different
        /// inherited scales — the Dolphin's "Engine Left.1" sits at 0.01 and the Urchin's
        /// "JetTopLeft" at 1.75 — so a plume given a raw local scale would be 100x too small on
        /// one vessel and nearly 2x too big on another.
        /// </summary>
        void ApplyPlumeScale(Transform plume, Transform mount, VesselJetFXConfigSO cfg, float hullRadius)
        {
            float width = MountWidth(mount) * cfg.PlumeScalePerMountSize;
            if (width <= Mathf.Epsilon)
                width = hullRadius * cfg.PlumeScalePerHullRadius;

            var world = new Vector3(width, width, width * cfg.PlumeLengthAspect);
            var lossy = mount.lossyScale;
            plume.localScale = new Vector3(
                world.x / SafeScale(lossy.x),
                world.y / SafeScale(lossy.y),
                world.z / SafeScale(lossy.z));
        }

        static float SafeScale(float v) => Mathf.Abs(v) < 1e-6f ? 1e-6f : Mathf.Abs(v);

        /// <summary>World-space cross-section of a mount's own nozzle mesh, or 0 for a bare bone.</summary>
        static float MountWidth(Transform mount)
        {
            if (!mount.TryGetComponent<Renderer>(out var renderer)) return 0f;
            if (!renderer.enabled) return 0f;
            var size = renderer.bounds.size;
            return Mathf.Max(size.x, size.y);
        }

        /// <summary>
        /// Authored overrides win; otherwise every descendant whose name reads as an engine.
        /// Results are sorted by hierarchy path so that separate peers building the same vessel
        /// pick the same subset when the cap bites — jets are local FX, but a vessel that grows
        /// a different number of them per machine is a bug waiting to be blamed on something else.
        /// </summary>
        List<Transform> ResolveMounts(VesselJetFXConfigSO cfg, float hullRadius)
        {
            var mounts = new List<Transform>();

            if (mountOverrides is { Count: > 0 })
            {
                foreach (var t in mountOverrides)
                    if (t != null) mounts.Add(t);
                if (mounts.Count > 0) return Trim(mounts, cfg);
            }

            var bones = CollectBones();
            foreach (var child in GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (child == transform) continue;
                if (!cfg.IsMountName(child.name)) continue;
                // Never hang a plume off the skimmer field, the HUD canvas, or FX we just made.
                if (child.GetComponentInParent<Skimmer>(true) != null) continue;
                if (child.GetComponentInParent<Canvas>(true) != null) continue;
                if (_spawnedRoot != null && child.IsChildOf(_spawnedRoot)) continue;
                if (!IsPlausibleMount(child, bones)) continue;
                mounts.Add(child);
            }

            if (mounts.Count == 0)
            {
                // The model exposes no engine geometry at all (the Manta family — Manta,
                // Falcon, Shrike and Termite all share Manta_shapekey_rigged.fbx, which has
                // only chassis and wing bones). Derive a symmetric pair at the rear of the hull
                // so the vessel still reads as powered. Flagged for art review in the doc.
                return Trim(DeriveRearMounts(cfg, hullRadius), cfg);
            }

            mounts.Sort((a, b) => string.CompareOrdinal(HierarchyPath(a), HierarchyPath(b)));
            return Trim(mounts, cfg);
        }

        /// <summary>
        /// A mount must be a real place on the SHIP: either it draws a nozzle of its own, or it
        /// is a rig bone. Name matching alone is not enough — an ability executor can be called
        /// "ExhaustBarrage" (the Sparrow's translation-mode toggle) and would otherwise collect
        /// a plume at the vessel origin, firing an engine out of the cockpit. Renderer OR bone
        /// is the honest test, because the fleet legitimately mounts on both: the Dolphin's
        /// "Engine Left.1" is a mesh, the Serpent's "EngineBone" is a bone with no geometry.
        /// </summary>
        static bool IsPlausibleMount(Transform t, HashSet<Transform> bones)
        {
            if (bones.Contains(t)) return true;
            if (!t.TryGetComponent<Renderer>(out var renderer)) return false;
            // An inactive FX object still reports renderer.enabled — check the GameObject too,
            // or the Rhino's disabled LeftJetParticle/RightJetParticle read as live nozzles.
            return renderer.enabled && t.gameObject.activeInHierarchy;
        }

        /// <summary>Every bone referenced by any skinned mesh on this vessel.</summary>
        HashSet<Transform> CollectBones()
        {
            var bones = new HashSet<Transform>();
            foreach (var skinned in GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
            {
                if (skinned.bones == null) continue;
                foreach (var bone in skinned.bones)
                    if (bone != null) bones.Add(bone);
            }
            return bones;
        }

        static List<Transform> Trim(List<Transform> mounts, VesselJetFXConfigSO cfg)
        {
            if (mounts.Count > cfg.MaxEnginePlumes)
                mounts.RemoveRange(cfg.MaxEnginePlumes, mounts.Count - cfg.MaxEnginePlumes);
            return mounts;
        }

        /// <summary>
        /// Plumes for a model with no engine geometry. Placed OUT TO THE SIDES at the rear, not
        /// on the centreline: the plume layer exists to read as engines from the pilot's chase
        /// camera, and on every vessel that HAS jets they emerge from the hull's flanks. A
        /// centreline pair would read as one exhaust and lose the vessel's sense of width.
        /// This is a stand-in for art, not art direction — see Docs/VESSEL_JET_FX.md §7.
        /// </summary>
        List<Transform> DeriveRearMounts(VesselJetFXConfigSO cfg, float hullRadius)
        {
            var derived = new List<Transform>();
            int count = cfg.DerivedMountCount;
            if (count <= 0) return derived;

            float spread = hullRadius * cfg.DerivedMountSpreadPerHullRadius;
            float back = hullRadius * cfg.DerivedMountDepthPerHullRadius;

            for (int i = 0; i < count; i++)
            {
                var mount = new GameObject($"DerivedMount_{i}").transform;
                mount.SetParent(_spawnedRoot, worldPositionStays: false);
                mount.localPosition = new Vector3(
                    VesselJetFXConfigSO.BeaconLateralOffset(i, count, spread), 0f, back);
                mount.localRotation = Quaternion.identity;
                derived.Add(mount);
            }
            return derived;
        }

        void CollectTrails(GameObject spawned)
        {
            foreach (var trail in spawned.GetComponentsInChildren<TrailRenderer>(includeInactive: true))
                _spawnedTrails.Add(trail);
        }

        static string HierarchyPath(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent)
                sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
