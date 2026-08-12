using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of the camera↔vessel prism occlusion corridor: prisms sitting between
    /// the player's camera and the player's vessel dissolve so the ship is never hidden
    /// (Docs/PRISM_ANIMATION.md §4.7).
    ///
    /// The corridor is a BARE CONE, and it is SHIP-SIZED: a point at the lens, widening to
    /// the circle that circumscribes the bound vessel's hull, ending flat at the vessel's
    /// plane with no cap at either end. Both radii are measured from the vessel itself at
    /// bind time, so nothing about the size is authored per vessel.
    ///
    /// It publishes exactly TWO global shader uniforms once per frame and does nothing
    /// else. There is no per-prism work of any kind — no trigger volumes, no material
    /// swaps, no per-instance overrides, no tracking dictionary. The corridor test runs
    /// per fragment in <c>PrismOcclusionCorridor.hlsl</c>, wired into BlockGraph.
    ///
    /// This is the sibling of <see cref="PrismClock"/> and it earns its per-frame write
    /// the same way: occlusion is camera-relative LIVE data (PRISM_ANIMATION.md §1,
    /// "animation vs. live gameplay data"), so it can never be a per-prism stamp — but a
    /// single O(1) global uniform write is explicitly the law's allowed shape, because it
    /// is not per-prism.
    ///
    /// History: the retired <c>ClearPrisms</c> component did the opposite of all of this.
    /// It grew a physics capsule per vessel, swapped each entered prism's sharedMaterial
    /// to the team transparent material on OnTriggerEnter, and wrote a MaterialPropertyBlock
    /// per tracked prism per physics tick. It also could not work: prisms draw through
    /// companion entities (instanced rendering is ON), so a GameObject MaterialPropertyBlock
    /// never reaches the batch — and its capsule sat on layer TrailBlockOcclusion while
    /// prisms sit on Default, which the collision matrix does not pair, so the triggers
    /// never fired either.
    ///
    /// Only ONE target is ever published — the local player's vessel. The camera end of
    /// the corridor is read on the GPU (<c>_WorldSpaceCameraPos</c>), so it is always
    /// exactly the camera that is rendering and never needs to be resolved or published.
    ///
    /// PLATFORM LAW (see Docs/PRISM_ANIMATION.md §4.7). The corridor is not a feature a
    /// vessel or a game mode may choose. It is bound in <c>VesselController.Initialize</c> —
    /// the one method every vessel must call to become a player's vessel, on every spawn
    /// path (single-player, multiplayer, menu autopilot, runtime swap) — so there is
    /// nothing per-vessel and nothing per-scene to wire, and therefore nothing to forget.
    /// The shader half is in the prism graphs themselves, so a new prism, a new vessel or a
    /// new minigame inherits it by construction. The only sanctioned hold is
    /// <see cref="SetSuppressed"/>, used by exactly one caller.
    /// </summary>
    public static class PrismOcclusionCorridor
    {
        static readonly int TargetId = Shader.PropertyToID("_PrismOcclusionTarget");
        static readonly int ParamsId = Shader.PropertyToID("_PrismOcclusionParams");

        const string ConfigResourcePath = "PrismOcclusionConfig";

        static Transform _target;
        static float _targetRadius;
        static bool _suppressed;
        static PrismOcclusionConfigSO _config;
        static bool _configResolved;
        static bool _publishedActive;

        /// <summary>The vessel the corridor currently opens onto, or null when it is off.</summary>
        public static Transform Target => _target;

        /// <summary>True while the corridor is publishing a live cone.</summary>
        public static bool IsActive => _publishedActive;

        /// <summary>
        /// The bound vessel's circumscribing radius in world units — the sphere that encloses
        /// its hull, measured once at bind. This is the corridor cone's radius AT THE VESSEL
        /// (times the config's outer scale); it tapers to zero at the camera, so the corridor
        /// is ship-sized rather than world-sized.
        /// </summary>
        public static float TargetRadius => _targetRadius;

        /// <summary>
        /// Tuning (radius / feather / core alpha). Falls back to the SO's own defaults when
        /// no <c>Resources/PrismOcclusionConfig</c> asset exists, so the feature works with
        /// no authoring.
        /// </summary>
        public static PrismOcclusionConfigSO Config
        {
            get
            {
                if (!_configResolved)
                {
                    _config = Resources.Load<PrismOcclusionConfigSO>(ConfigResourcePath);
                    if (_config == null)
                        _config = ScriptableObject.CreateInstance<PrismOcclusionConfigSO>();
                    _configResolved = true;
                }
                return _config;
            }
        }

        /// <summary>
        /// Point the corridor at the local pilot's vessel. The ONLY caller is
        /// <c>VesselController.Initialize</c> under <c>IPlayer.IsLocalPilot</c> — deliberately
        /// the universal vessel entry point rather than any camera, mode, or per-vessel
        /// component, so the corridor cannot be omitted by authoring. Do not add call sites
        /// that give a mode a way to point it somewhere else.
        /// </summary>
        public static void SetTarget(Transform target)
        {
            _target = target;
            _targetRadius = target != null ? MeasureCircumscribedRadius(target) : 0f;
        }

        /// <summary>
        /// The radius of the sphere that circumscribes a vessel's HULL, in world units,
        /// measured about the vessel's own origin.
        ///
        /// Rotation-invariant by construction: a rigid rotation about the vessel origin
        /// preserves every corner's distance from that origin, so this returns the same
        /// number whatever attitude the ship happens to be in when it spawns. (Taking
        /// <c>Renderer.bounds</c> — a world AABB — instead would swing by tens of percent
        /// with attitude and make the corridor a different size on every spawn.)
        ///
        /// Hull only, and that exclusion is load-bearing:
        /// - MeshFilter + SkinnedMeshRenderer only, so particle/trail/UI renderers (which are
        ///   <c>Renderer</c>s but carry no mesh here) cannot inflate it.
        /// - DISABLED renderers are skipped: an invisible mesh is not hull, and vessels carry
        ///   hidden placeholder geometry (rig-swap leftovers, dummy volumes) that would
        ///   silently size the corridor to something the player cannot see.
        /// - Anything under a <see cref="Skimmer"/> is skipped, INCLUDING INACTIVE ONES.
        ///   A skimmer is a FIELD volume, deliberately far larger than the ship — the shared
        ///   Skimmer prefab is scaled 15× around a 0.5 sphere, and its child
        ///   <c>ForcefieldCrackleOverlay</c> therefore reaches 12.99 units (0.5·√3·15) — so
        ///   counting it would peg a vessel's corridor to the skimmer instead of the hull.
        ///   The <c>includeInactive: true</c> argument is load-bearing and easy to lose:
        ///   <c>GetComponentInParent&lt;T&gt;()</c> defaults to skipping components on
        ///   inactive GameObjects, and a PREFAB ASSET reports <c>activeInHierarchy == false</c>
        ///   for every object in it (it is in no scene) — so the default overload finds
        ///   nothing at all when this runs over assets, and the skimmer silently becomes the
        ///   "hull" for most of the fleet. It also matters in play: a vessel whose skimmer
        ///   object is deactivated would otherwise have its field volume measured as hull.
        /// - A SKINNED mesh measures its <c>localBounds</c> in ROOT-BONE space — the culling
        ///   bounds the renderer actually draws with — never <c>sharedMesh.bounds</c>. The
        ///   bind-pose mesh bounds live in MESH space, and on a rig whose scale is carried by
        ///   the ARMATURE (the Sparrow's armature scales 0.2, with vertex data authored 5×
        ///   larger to match) transforming them by the renderer node alone overstates the
        ///   hull by the full armature factor — a ~5× oversized corridor that made the
        ///   Sparrow's dither read as wrong while the (compact-mesh) Squirrel looked right.
        ///   localBounds + rootBone matches what renders BY CONSTRUCTION: if culling is
        ///   right on screen, this number is right.
        /// </summary>
        public static float MeasureCircumscribedRadius(Transform vessel)
        {
            if (vessel == null) return 0f;

            Vector3 origin = vessel.position;
            float maxSqr = 0f;

            foreach (var filter in vessel.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                if (!filter.TryGetComponent<MeshRenderer>(out var renderer)) continue; // collider-only mesh
                if (!renderer.enabled) continue;
                if (filter.GetComponentInParent<Skimmer>(true) != null) continue;
                Accumulate(filter.sharedMesh.bounds, filter.transform, origin, ref maxSqr);
            }

            foreach (var skinned in vessel.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skinned.sharedMesh == null) continue;
                if (!skinned.enabled) continue;
                if (skinned.GetComponentInParent<Skimmer>(true) != null) continue;
                var space = skinned.rootBone != null ? skinned.rootBone : skinned.transform;
                Accumulate(skinned.localBounds, space, origin, ref maxSqr);
            }

            return Mathf.Sqrt(maxSqr);
        }

        /// <summary>Farthest of a local AABB's 8 corners from <paramref name="origin"/>, in world space.</summary>
        static void Accumulate(Bounds localBounds, Transform space, Vector3 origin, ref float maxSqr)
        {
            Vector3 c = localBounds.center, e = localBounds.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    c.x + ((i & 1) == 0 ? -e.x : e.x),
                    c.y + ((i & 2) == 0 ? -e.y : e.y),
                    c.z + ((i & 4) == 0 ? -e.z : e.z));
                float sqr = (space.TransformPoint(corner) - origin).sqrMagnitude;
                if (sqr > maxSqr) maxSqr = sqr;
            }
        }

        /// <summary>
        /// Temporarily hold the corridor closed WITHOUT unbinding the vessel. The one
        /// sanctioned caller is <c>CameraManager</c>'s manual replay camera: a broadcast
        /// vantage is not looking at the local ship, so a camera→ship capsule would cut a
        /// hole through unrelated mass. Symmetric — <c>RestoreGameplayCamera</c> lifts it —
        /// and it is a HOLD, not an opt-out: the vessel binding survives it, so nothing has
        /// to remember to re-point the corridor afterwards.
        /// </summary>
        public static void SetSuppressed(bool suppressed) => _suppressed = suppressed;

        /// <summary>True while <see cref="SetSuppressed"/> is holding the corridor closed.</summary>
        public static bool IsSuppressed => _suppressed;

        /// <summary>
        /// Turn the corridor off, but only if <paramref name="target"/> is still the one in
        /// force — so a late teardown from an old vessel cannot cancel a newer one.
        /// </summary>
        public static void ClearTarget(Transform target)
        {
            if (_target == target)
            {
                _target = null;
                _targetRadius = 0f;
            }
        }

        /// <summary>Unconditional off (scene teardown, returning to a vessel-less camera).</summary>
        public static void ClearTarget()
        {
            _target = null;
            _targetRadius = 0f;
        }

        /// <summary>Drop the cached config so the next frame re-reads the asset (editor tooling).</summary>
        public static void InvalidateConfig()
        {
            _config = null;
            _configResolved = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallPublisher()
        {
            _target = null;
            _targetRadius = 0f;
            _suppressed = false;
            // Shader globals survive play-mode exit in the editor, so a stale corridor from
            // the previous session would fade prisms around a vessel that no longer exists.
            // Publish the off state before anything renders.
            PublishOff();

            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from play-mode-exit
            // cleanup), the same pattern PrismClock's publisher uses.
            var go = new GameObject("[PrismOcclusionCorridor]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Publisher>();
        }

        static void PublishOff()
        {
            Shader.SetGlobalVector(TargetId, Vector4.zero);
            Shader.SetGlobalVector(ParamsId, Vector4.zero); // x <= 0 is the shader's "off" sentinel
            _publishedActive = false;
        }

        static void Publish()
        {
            var config = Config;
            if (_suppressed || _target == null || !_target.gameObject.activeInHierarchy || !config.Enabled)
            {
                if (_publishedActive)
                    PublishOff();
                return;
            }

            // The corridor is sized ENTIRELY by the hull measurement now, so a bad measurement
            // is not cosmetic: too small and the ship stays hidden, too large and the world
            // dissolves in front of the player. Both degrade to a sane corridor and scream —
            // never to a silently-absent one, which is the platform law failing quietly.
            float radius = _targetRadius;
            if (radius <= 0f)
            {
                radius = config.FallbackVesselRadius;
                PrismOcclusionDiagnostics.WarnUnmeasurableVessel(_target, radius);
            }
            else
            {
                float clamped = Mathf.Clamp(radius, config.MinVesselRadius, config.MaxVesselRadius);
                if (clamped != radius)
                {
                    PrismOcclusionDiagnostics.WarnImplausibleVesselRadius(_target, radius, clamped);
                    radius = clamped;
                }
            }

            var packed = config.PackParams(radius);
            if (packed.x <= 0f)
            {
                if (_publishedActive)
                    PublishOff();
                return;
            }

            Vector3 p = _target.position;
            Shader.SetGlobalVector(TargetId, new Vector4(p.x, p.y, p.z, 0f));
            Shader.SetGlobalVector(ParamsId, packed);
            _publishedActive = true;
        }

        /// <summary>
        /// LateUpdate so the corridor is published after the vessel has moved and after
        /// Cinemachine has posed the camera for this frame.
        /// </summary>
        sealed class Publisher : MonoBehaviour
        {
            void LateUpdate() => Publish();
            void OnDisable() => PublishOff();
        }
    }
}
