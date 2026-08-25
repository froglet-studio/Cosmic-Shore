using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of the VESSEL VISION BAND: every vessel is progressively re-shaded into a
    /// flat, cel-banded silhouette in its own DOMAIN colour as a function of its distance from
    /// the camera drawing it — nothing up close, full mark across the middle of an arena, gone
    /// again at extreme range, both edges graded (Docs/VESSEL_VISION.md).
    ///
    /// PLATFORM LAW. This is not a feature a vessel, a scene or a game mode may choose. A pilot
    /// being able to find another pilot is a property of the game, not of the ship they happen to
    /// be flying, so — like the prism occlusion corridor and the speed tunnel — it is built so
    /// that there is nothing to author and therefore nothing to forget:
    ///
    ///   1. The shading lives in <c>VesselGraph.shadergraph</c> itself, which every hull surface
    ///      of every vessel in the fleet is painted with (Body, Domain and Window roles alike —
    ///      see <c>VesselCustomization</c>). A new vessel inherits the law by being painted.
    ///   2. The per-vessel datum — the domain colour — is stamped from
    ///      <c>VesselHelper.SetShipProperties</c>, the ONE method every vessel's domain flows
    ///      through on every path: first spawn, runtime vessel swap, and every replicated
    ///      <c>Player.NetDomain</c> change. There is no component to add and no scene to wire.
    ///   3. <see cref="VesselVisionDiagnostics"/> screams once, by name, for a vessel that could
    ///      not be stamped, because the failure mode is otherwise SILENT — an unmarked ship just
    ///      looks like a ship.
    ///   4. <c>VesselVisionLawTests</c> and FrogletTools > Vessels > Validate Vessel Vision Band
    ///      fail on a graph that has come unwired, on a hull material outside the wired shader,
    ///      and on a config authored into a state that does nothing.
    ///
    /// WHAT IT COSTS. Four <c>Shader.SetGlobalVector</c> calls per frame and nothing else that
    /// scales — no per-vessel per-frame write, no material clone, no extra draw call, no depth
    /// read, no second pass. The distance test is per fragment in
    /// <c>VesselVisionShading.hlsl</c>, which is where it belongs: distance-to-camera is
    /// per-CAMERA live data, so a CPU implementation would have to pick one camera and be wrong
    /// in the scene view, in a replay view and in any future split screen.
    ///
    /// THERE IS DELIBERATELY NO SUPPRESSION HOLD, and the absence is the point. The corridor and
    /// the speed tunnel each carry a <c>SetSuppressed</c> for the manual replay camera, because
    /// both are effects for the pilot at the controls. This one is the opposite: a broadcast
    /// vantage parked away from the fight is exactly when telling three domains apart matters
    /// most, so the replay camera gets the marks too — including on the local pilot's own ship,
    /// which is simply another distant vessel from there.
    ///
    /// WHY THE STAMP HEALS ITSELF. The tint rides a <c>MaterialPropertyBlock</c>, and a vessel's
    /// renderers are written by several other systems (the Echo Sight highlight, the Serpent's
    /// cloak, the Rhino's sword FX). Those compose correctly — every one of them does a
    /// get-modify-set round trip, which preserves foreign properties — but a
    /// <c>SetPropertyBlock(null)</c> RESTORE clears the whole block, tint included, and a vision
    /// aid that a sibling effect can silently switch off for the rest of the match is not a law.
    /// So the publisher re-stamps ONE vessel per frame, round-robin. With a full lobby that is a
    /// complete sweep every twelve frames for a twelfth of the cost, it needs no cooperation from
    /// any other system, and it covers the next MPB writer as well as the current ones.
    /// </summary>
    public static class VesselVisionShading
    {
        static readonly int BandId = Shader.PropertyToID("_VesselVisionBand");
        static readonly int ShapeId = Shader.PropertyToID("_VesselVisionShape");
        static readonly int RimId = Shader.PropertyToID("_VesselVisionRim");
        static readonly int BreakupId = Shader.PropertyToID("_VesselVisionBreakup");

        /// <summary>
        /// The per-vessel datum: rgb is the domain's signal colour, and ALPHA IS A MARKER rather
        /// than an opacity — alpha 0 means "no domain published for this object" and the shader
        /// leaves the surface alone. That is what keeps the law to vessels even though
        /// VesselGraph is also worn by a projectile material.
        /// </summary>
        public static readonly int TintId = Shader.PropertyToID("_VesselVisionTint");

        /// <summary>The ShaderGraph the law is wired into. Shared with the diagnostics and the validator.</summary>
        public const string WiredShaderName = "VesselGraph";

        const string ConfigResourcePath = "VesselVisionShadingConfig";

        static VesselVisionShadingConfigSO _config;
        static bool _configResolved;
        static bool _publishedActive;

        static readonly List<Entry> _entries = new();
        static MaterialPropertyBlock _block;
        static int _healCursor;

        /// <summary>True while the law is publishing a live band.</summary>
        public static bool IsActive => _publishedActive;

        /// <summary>Number of vessels currently carrying a stamp (diagnostics / tests).</summary>
        public static int StampedVesselCount => _entries.Count;

        /// <summary>
        /// Tuning. Falls back to the SO's own defaults when no
        /// <c>Resources/VesselVisionShadingConfig</c> asset exists, so the law works with no
        /// authoring at all.
        /// </summary>
        public static VesselVisionShadingConfigSO Config
        {
            get
            {
                if (!_configResolved)
                {
                    _config = Resources.Load<VesselVisionShadingConfigSO>(ConfigResourcePath);
                    if (_config == null)
                        _config = ScriptableObject.CreateInstance<VesselVisionShadingConfigSO>();
                    _configResolved = true;
                }
                return _config;
            }
        }

        /// <summary>Drop the cached config so the next frame re-reads the asset (editor tooling).</summary>
        public static void InvalidateConfig()
        {
            _config = null;
            _configResolved = false;
        }

        /// <summary>
        /// Publish <paramref name="domainSignalColor"/> onto every renderer of
        /// <paramref name="vessel"/> that can wear it, and remember the vessel so the stamp can
        /// be re-asserted.
        ///
        /// The ONE caller is <c>VesselHelper.SetShipProperties</c> — deliberately the universal
        /// domain entry point rather than a per-vessel component, so the mark cannot be omitted
        /// by authoring and cannot go stale when a pilot changes domain. Idempotent: re-stamping
        /// the same colour costs a get-modify-set round trip per renderer and changes nothing.
        /// </summary>
        public static void Stamp(Transform vessel, Color domainSignalColor)
        {
            if (vessel == null) return;

            // Alpha is the marker the shader gates on; an authored translucent domain colour must
            // not be able to switch a pilot's mark off.
            domainSignalColor.a = 1f;

            var entry = Resolve(vessel);
            entry.Tint = domainSignalColor;
            CollectTargets(vessel, entry.Targets);
            Apply(entry);

            if (entry.Targets.Count == 0)
                VesselVisionDiagnostics.WarnUnmarkableVessel(vessel);
        }

        /// <summary>
        /// Forget every stamp. Scene teardown and editor play-mode entry — the roster holds
        /// Transforms, so keeping it across a scene load would leave the heal walking corpses
        /// until it happened to visit them.
        /// </summary>
        public static void ClearAll()
        {
            _entries.Clear();
            _healCursor = 0;
        }

        // ---------------- Internals ----------------

        sealed class Entry
        {
            public Transform Vessel;
            public Color Tint = Color.white;
            public readonly List<Target> Targets = new();
        }

        /// <summary>One renderer sub-mesh whose material can wear the mark.</summary>
        readonly struct Target
        {
            public readonly Renderer Renderer;
            public readonly int MaterialIndex;
            public Target(Renderer renderer, int materialIndex)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
            }
        }

        static Entry Resolve(Transform vessel)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (ReferenceEquals(_entries[i].Vessel, vessel))
                    return _entries[i];

            var entry = new Entry { Vessel = vessel };
            _entries.Add(entry);
            return entry;
        }

        static void CollectTargets(Transform vessel, List<Target> into)
        {
            into.Clear();

            // Inactive renderers included: a vessel's rig-swap leftovers and its hidden variants
            // are re-enabled by animation and hull morphs, and a renderer that switched on after
            // the stamp would otherwise be the one unmarked patch on the ship.
            var renderers = vessel.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (!renderer) continue;

                // sharedMaterials, never .materials — reading the live instances the vessel is
                // already drawing with, without cloning a single one of them.
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    var material = materials[m];
                    if (!material) continue;

                    // The property gate IS the filter: a skimmer's crackle overlay, a jet's
                    // particle material and a trail viewer are all children of the vessel and
                    // none of them is on the wired graph, so none of them can wear the mark and
                    // none of them needs to be named here.
                    if (!material.HasColor(TintId)) continue;

                    into.Add(new Target(renderer, m));
                }
            }
        }

        static void Apply(Entry entry)
        {
            _block ??= new MaterialPropertyBlock();

            for (int i = 0; i < entry.Targets.Count; i++)
            {
                var target = entry.Targets[i];
                if (!target.Renderer) continue;

                // Get-modify-set, never a bare Set: a vessel's renderers carry other systems'
                // overrides (the Echo Sight hull tint, the cloak's alpha) and clobbering the block
                // would be the very defect the heal exists to repair.
                target.Renderer.GetPropertyBlock(_block, target.MaterialIndex);
                _block.SetColor(TintId, entry.Tint);
                target.Renderer.SetPropertyBlock(_block, target.MaterialIndex);
            }
        }

        /// <summary>
        /// Re-assert ONE vessel's stamp and prune anything that has died. Round-robin rather than
        /// a full sweep: the whole roster is refreshed within <c>_entries.Count</c> frames, which
        /// is at most a fifth of a second for a full lobby, and the per-frame cost stays flat as
        /// the lobby grows.
        /// </summary>
        static void Heal()
        {
            if (_entries.Count == 0) return;

            if (_healCursor >= _entries.Count) _healCursor = 0;
            var entry = _entries[_healCursor];

            if (entry.Vessel == null)
            {
                _entries.RemoveAt(_healCursor);
                return;
            }

            // A renderer list can go stale under a rig swap or a hull morph that enables new
            // geometry; re-collecting on the heal tick means the law repairs that too, at the
            // same amortised cost.
            if (entry.Targets.Count == 0) CollectTargets(entry.Vessel, entry.Targets);
            Apply(entry);

            _healCursor++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallPublisher()
        {
            ClearAll();
            InvalidateConfig();

            // Shader globals survive play-mode exit in the editor, so a stale band from the last
            // session would mark ships before anything had a chance to publish. Off first.
            PublishOff();

            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from play-mode-exit
            // cleanup), the same pattern PrismOcclusionCorridor's publisher uses.
            var go = new GameObject("[VesselVisionShading]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Publisher>();
        }

        static void PublishOff()
        {
            Shader.SetGlobalVector(BandId, Vector4.zero);   // w <= 0 is the shader's "off" sentinel
            Shader.SetGlobalVector(ShapeId, Vector4.zero);
            Shader.SetGlobalVector(RimId, Vector4.zero);
            Shader.SetGlobalVector(BreakupId, Vector4.zero);
            _publishedActive = false;
        }

        static void Publish()
        {
            var config = Config;
            if (!config.Enabled)
            {
                if (_publishedActive) PublishOff();
                return;
            }

            // Re-published every frame rather than once at startup so an edit to the asset is live
            // in play mode, and so a scene load or a camera stack change can never leave the band
            // holding a value nothing owns. Four writes; it does not scale with anything.
            Shader.SetGlobalVector(BandId, config.PackBand());
            Shader.SetGlobalVector(ShapeId, config.PackShape());
            Shader.SetGlobalVector(RimId, config.PackRim());
            Shader.SetGlobalVector(BreakupId, config.PackBreakup());
            _publishedActive = true;
        }

        /// <summary>
        /// LateUpdate so the band is published after every vessel has moved for this frame and
        /// after the cameras have been posed.
        /// </summary>
        sealed class Publisher : MonoBehaviour
        {
            void LateUpdate()
            {
                Publish();
                Heal();
            }

            void OnDisable() => PublishOff();
        }
    }
}
