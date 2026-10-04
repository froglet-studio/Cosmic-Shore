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
    /// THE LOCAL PILOT'S OWN VESSEL IS EXCLUDED EXPLICITLY, and the reason it is explicit is worth
    /// keeping. The law was first built with NO "is this me" test, on the reasoning that a pilot's
    /// own hull rides 10-40 units from its camera — an order of magnitude inside the near floor —
    /// so the exclusion would simply FALL OUT of "close things do not need help". **The shipped
    /// fleet falsifies that.** Camera distance is <c>|CameraSettingsSO.followOffset|</c>, and it
    /// spans 6.7 (Urchin) to 250 (Serpent), with the Rhino's adaptive zoom reaching 200 — so a
    /// Serpent pilot sat at half strength inside the rising grade and marked their own ship. The
    /// premise was read off the SO's DEFAULTS and never off the assets.
    ///
    /// The alternative fix — raising the near floor above the widest camera in the fleet — was
    /// rejected on a collateral cost: the toybox's vessel matrices bloom at 360 units and depend
    /// on being just inside the band, so a floor at 400 would silently un-mark every station.
    /// Binding an explicit local vessel is exact, immune to a camera being re-authored, and rides
    /// the SAME two call sites the corridor and the speed tunnel already use.
    ///
    /// The cost, stated plainly: a broadcast or replay camera no longer marks the local pilot's
    /// ship. That was previously documented as a virtue of the distance-only design; it is the
    /// price of the design being correct.
    ///
    /// THERE IS STILL NO SUPPRESSION HOLD. The corridor and the speed tunnel each carry a
    /// <c>SetSuppressed</c> for the manual replay camera, because both are effects for the pilot
    /// at the controls. This one is the opposite: a broadcast vantage parked away from the fight
    /// is exactly when telling three domains apart matters most, so every OTHER ship stays marked.
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
        static readonly List<Target> _scratchTargets = new();
        static Transform _localVessel;
        static MaterialPropertyBlock _block;
        static int _healCursor;
        static bool _capturePass;

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
        /// Name the vessel the local pilot is flying, so it is never marked on this machine.
        ///
        /// The ONLY callers are <c>VesselController.Initialize</c> and
        /// <c>VesselController.ChangePlayer</c>, both under <c>IPlayer.IsLocalPilot</c> — the same
        /// two sites the prism occlusion corridor and the speed tunnel bind at, for the same
        /// reason: <c>Initialize</c> is the one method every vessel must call on every spawn path,
        /// and <c>ChangePlayer</c> hands a LIVE vessel to a different player (the Cellular Duel
        /// ownership swap) without ever reaching <c>Initialize</c>. Binding here is what makes it
        /// impossible to author a vessel or a mode in which the exclusion is missing.
        ///
        /// Re-applies immediately rather than waiting for the heal cursor, so an ownership swap
        /// takes effect on the frame it happens instead of up to a dozen frames later.
        /// </summary>
        public static void SetLocalVessel(Transform vessel)
        {
            if (_localVessel == vessel) return;
            var previous = _localVessel;
            _localVessel = vessel;
            ReapplyFor(previous);
            ReapplyFor(vessel);
        }

        /// <summary>
        /// Release the local-pilot binding, but only if <paramref name="vessel"/> is still the one
        /// in force — so a losing vessel's teardown cannot cancel the winning vessel's bind,
        /// whatever order the two arrive in. Same identity guard the sibling laws use.
        /// </summary>
        public static void ClearLocalVessel(Transform vessel)
        {
            if (_localVessel != vessel) return;
            _localVessel = null;
            ReapplyFor(vessel);
        }

        /// <summary>The vessel currently excluded as the local pilot's, or null.</summary>
        public static Transform LocalVessel => _localVessel;

        /// <summary>True while a capture pass is holding the band open (tests / diagnostics).</summary>
        public static bool IsCapturePassActive => _capturePass;

        /// <summary>
        /// Open the band for ONE hand-stepped render: mark every vessel further than
        /// <paramref name="markThresholdDistance"/> from the lens and nothing nearer, and include
        /// the local pilot's own hull — both undone by <see cref="EndCapturePass"/>.
        ///
        /// <para><b>It is a THRESHOLD, not a band.</b> The law's four control points exist because
        /// a mark that pops on reads as a new object appearing, which is continuity of existence
        /// applied to visibility — and a photograph is ONE frame, so there is nothing for it to pop
        /// against. Dropping the graded edges is therefore free here and is the whole simplification:
        /// beyond the threshold a hull is the solid domain-coloured silhouette, inside it the hull
        /// renders as itself, and there is no in-between for a shot to land in. An earlier version
        /// rescaled the law's whole three-beat arc onto each concept's own zoom range; it was
        /// correct and it made "when is a ship marked?" a question about which concept was rolled.</para>
        ///
        /// <para>The break-up closes at the same threshold for the same reason: over distance the
        /// band opens its centre into cells and closes them again by
        /// <c>breakupEndDistance</c>, and leaving that authored 900 in place would have every
        /// photograph come back an outline with a dithered middle.</para>
        ///
        /// <para>This is NOT the suppression hold this law deliberately does not have, and the
        /// difference is the whole justification: a suppression hold would let a camera switch the
        /// aid OFF, which is what makes an aid authorable-away. This only ever marks MORE, for one
        /// render, on a camera that is not anybody's eye — the screenshot director's, which renders
        /// into a RenderTexture by calling <c>Camera.Render()</c> by hand
        /// (<c>Docs/SCREENSHOT_DIRECTOR.md</c>). Nothing a player is looking through can reach it.</para>
        ///
        /// <para>The local exclusion has to lift or the feature is empty: the subject of a
        /// photograph is usually the local pilot's own ship, which is precisely the hull
        /// <see cref="EffectiveTint"/> paints with the transparent sentinel. What the exclusion is
        /// FOR — a pilot not wanting their own cockpit view cluttered by a mark on their own hull —
        /// simply does not apply to a photograph of that hull.</para>
        ///
        /// <para>Safe against a missed release by construction: the publisher re-writes all four
        /// globals every <c>LateUpdate</c>, so an override that escaped its <c>finally</c> would
        /// last at most one frame — but the release is unconditional anyway, because the STAMP
        /// half does not self-heal for a frame or more (the heal is round-robin).</para>
        ///
        /// <para>Returns false, changing nothing, when the law is authored off — a caller must
        /// still call <see cref="EndCapturePass"/> only if it got true, on the identity-guard
        /// principle the corridor's hold uses.</para>
        /// </summary>
        public static bool BeginCapturePass(float markThresholdDistance)
        {
            var config = Config;
            if (!config.Enabled || _capturePass) return false;

            float start = Mathf.Max(0f, markThresholdDistance);
            float solid = start + MinCaptureEdgeWidth;

            // The FAR edges are deliberately NOT moved. They exist so a pilot is not reading
            // coloured dots across half an arena, which is a thing a cockpit does and a photograph
            // never does. Raised only where the threshold would have overtaken them, since an
            // inverted band is the one shape the shader cannot render sanely.
            float farFullEnd = Mathf.Max(config.FarFullEnd, solid);
            float farFadeEnd = Mathf.Max(config.FarFadeEnd, farFullEnd + MinCaptureEdgeWidth);

            Shader.SetGlobalVector(BandId, new Vector4(start, solid, farFullEnd, farFadeEnd));

            if (config.BreakupActive)
                Shader.SetGlobalVector(BreakupId, new Vector4(
                    config.BreakupCells, config.BreakupReach, config.BreakupStrength, solid));

            _capturePass = true;
            ReapplyFor(_localVessel);
            return true;
        }

        /// <summary>
        /// Close a <see cref="BeginCapturePass"/>: restore the authored band and re-exclude the
        /// local pilot's hull, immediately rather than on the next publish, so a second render in
        /// the same frame cannot inherit the photograph's band.
        /// </summary>
        public static void EndCapturePass()
        {
            if (!_capturePass) return;
            _capturePass = false;
            ReapplyFor(_localVessel);
            Publish();
        }

        /// <summary>Smallest gap that keeps a capture band's edges ordered for the shader.</summary>
        const float MinCaptureEdgeWidth = 0.01f;

        /// <summary>
        /// Fill <paramref name="into"/> with every live vessel currently carrying a stamp.
        ///
        /// <para>A pure INDEX READ — it spawns, moves, tints and removes nothing, and holding the
        /// list changes no behaviour of the law. It exists because this roster is already the one
        /// correct answer to "which vessels are in the arena right now": every vessel joins it
        /// through <c>VesselHelper.SetShipProperties</c>, the single method a vessel's domain flows
        /// through on every path (spawn, vessel swap, every replicated <c>NetDomain</c> change), so
        /// it covers local and remote, human and AI, with nothing per-mode to wire — and a caller
        /// reading it cannot drift from what is actually on screen. Compare
        /// <c>Object.FindObjectsByType</c>, which would also sweep up prefab-built props.</para>
        ///
        /// <para>Display-only models are deliberately absent, because <see cref="StampDisplayModel"/>
        /// never joins the roster — so a toy matrix's mini hulls can never be mistaken for pilots.
        /// Destroyed and deactivated vessels are skipped here rather than pruned, since pruning is
        /// the heal pass's business and a read must not mutate.</para>
        /// </summary>
        public static void CollectStampedVessels(List<Transform> into)
        {
            if (into == null) return;
            into.Clear();

            for (int i = 0; i < _entries.Count; i++)
            {
                var vessel = _entries[i].Vessel;
                if (vessel == null || !vessel.gameObject.activeInHierarchy) continue;
                into.Add(vessel);
            }
        }

        static void ReapplyFor(Transform vessel)
        {
            if (vessel == null) return;
            for (int i = 0; i < _entries.Count; i++)
                if (ReferenceEquals(_entries[i].Vessel, vessel))
                {
                    Apply(_entries[i]);
                    return;
                }
        }

        /// <summary>
        /// What a vessel is actually stamped with: its domain colour, or a TRANSPARENT tint for the
        /// local pilot's own ship. Alpha 0 is the shader's "not a vessel" sentinel, so the exclusion
        /// costs nothing on the GPU — the fragment takes the early-out every unstamped object takes.
        /// </summary>
        static Color EffectiveTint(Entry entry) =>
            !_capturePass && ReferenceEquals(entry.Vessel, _localVessel) ? Color.clear : entry.Tint;

        /// <summary>
        /// Mark a DISPLAY-ONLY model — a mini hull in a toy matrix, built from a ship prefab asset
        /// and never instantiated as a vessel — so the vision band shades it like the real thing.
        ///
        /// <para>Deliberately NOT <see cref="Stamp"/>, and the distinction is the law's, not a
        /// convenience. <see cref="Stamp"/> takes ownership of a VESSEL's per-renderer channel: it
        /// joins the heal roster, because a real vessel's renderers are also written by the Echo
        /// Sight, the Serpent's cloak and the Rhino's sword FX, and one of them clearing its block
        /// would otherwise switch a pilot's mark off for the rest of the match. A display model has
        /// none of that — nothing else writes it, it has no domain that can change under it, and it
        /// lives for one pass of a matrix — so joining the roster would only put a prop in the
        /// queue ahead of real ships. The single-owner rule the gates enforce is about VESSELS, and
        /// this is not one.</para>
        ///
        /// <para>The model's renderers must already wear the vessel's own materials for this to
        /// reach anything; a model painted with a flat preview material has no
        /// <c>_VesselVisionTint</c> to write and this is a silent no-op by construction — which is
        /// correct, since a flat glyph is already showing its domain in the only way it can.</para>
        /// </summary>
        public static void StampDisplayModel(Transform model, Color domainSignalColor)
        {
            if (model == null) return;
            domainSignalColor.a = 1f;

            CollectTargets(model, _scratchTargets);
            WriteTint(_scratchTargets, domainSignalColor);
            _scratchTargets.Clear();
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
            _localVessel = null;
            _capturePass = false;
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

        static void Apply(Entry entry) => WriteTint(entry.Targets, EffectiveTint(entry));

        static void WriteTint(List<Target> targets, Color tint)
        {
            _block ??= new MaterialPropertyBlock();

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (!target.Renderer) continue;

                // Get-modify-set, never a bare Set: a vessel's renderers carry other systems'
                // overrides (the Echo Sight hull tint, the cloak's alpha) and clobbering the block
                // would be the very defect the heal exists to repair.
                target.Renderer.GetPropertyBlock(_block, target.MaterialIndex);
                _block.SetColor(TintId, tint);
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
