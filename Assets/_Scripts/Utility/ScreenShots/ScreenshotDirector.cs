using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Press a button in flight, get a UI-free photograph of your vessel from a camera angle drawn
    /// at random from a library of capture concepts (<see cref="ScreenshotDirectorConfigSO"/>).
    ///
    /// <para><b>It renders its own camera into a RenderTexture, never the screen.</b> That is what
    /// makes the shot UI-free without hiding anything: a screen-space canvas is composited straight
    /// to the display and never reaches a RenderTexture at all, so the HUD is excluded by
    /// construction rather than by a toggle that can be left in the wrong state. Only world-space
    /// UI needs a culling mask, and it gets one. Rendering to a texture is also what allows a
    /// capture to be larger than the window and posed somewhere the player's camera is not.</para>
    ///
    /// <para><b>Zero wiring.</b> It installs itself after scene load like
    /// <c>DisplayGraphicsSettings</c> does, so it exists in every scene with no prefab to place and
    /// nothing to remember. It does no work until the gesture fires.</para>
    ///
    /// <para><b>It never touches the gameplay camera.</b> The speed tunnel and the vessel vision
    /// band are bound to that camera and stay bound; this one is a second, disabled camera stepped
    /// by hand. The single platform law it does hold is the prism occlusion corridor — see
    /// <see cref="HoldOcclusionCorridor"/>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ScreenshotDirector : SingletonPersistent<ScreenshotDirector>
    {
        ScreenshotDirectorConfigSO _config;
        Camera _camera;
        System.Random _rng;
        bool _capturing;
        readonly List<Transform> _vesselScratch = new();

        ScreenshotDirectorConfigSO Config => _config != null ? _config : _config = ScreenshotDirectorConfigSO.Resolve();

        /// <summary>
        /// Stand the director up once per launch, with nothing in any scene referencing it. Guards
        /// on a live <see cref="Instance"/> so an inspector-placed one wins, exactly as
        /// <c>DisplayGraphicsSettings.EnsureExists</c> does.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (Instance == null)
                new GameObject("[ScreenshotDirector]").AddComponent<ScreenshotDirector>();
        }

        public override void Awake()
        {
            base.Awake();
            if (Instance != this) return; // duplicate destroyed by base
            _rng = new System.Random();
        }

        void Update()
        {
            if (!_capturing && ScreenshotGesture.RequestedThisFrame())
                CaptureAsync().Forget();
        }

        /// <summary>
        /// Take one photograph now, whatever the gesture is bound to. Public so a toy, a mode, or a
        /// console command can ask for one.
        /// </summary>
        public void Capture()
        {
            if (!_capturing) CaptureAsync().Forget();
        }

        async UniTaskVoid CaptureAsync()
        {
            _capturing = true;
            bool heldCorridor = false;
            bool heldVisionBand = false;

            try
            {
                var config = Config;

                if (!TryResolveSubject(out Transform subject, out float hullRadius, out IVesselStatus status))
                {
                    CSDebug.LogWarning("[Screenshot] Nothing to photograph - no local vessel is bound. " +
                                       "Captures are taken of the ship you are flying.");
                    return;
                }

                // A pair is the rarer, better moment, so it is checked FIRST and taken most of
                // the time it exists. It falls back rather than failing: no pair in the band, no
                // usable Pair concept, or the roll going the other way all land on the solo shot.
                // Declared up front rather than inline in the `&&` chain: `out` variables
                // introduced inside a short-circuiting condition are not definitely assigned at a
                // later use site, so the inline form does not compile.
                Transform pairA = null;
                Transform pairB = null;
                float pairRadius = 0f;

                bool wantPair =
                    config.HasConcepts(ScreenshotFramingKind.Pair) &&
                    _rng.NextDouble() < config.pairChance &&
                    TryResolvePair(config, subject, out pairA, out pairB, out pairRadius);

                var kind = wantPair ? ScreenshotFramingKind.Pair : ScreenshotFramingKind.Solo;
                var concept = config.PickConcept(_rng, kind)
                              ?? config.PickConcept(_rng, ScreenshotFramingKind.Solo);
                if (concept == null)
                {
                    CSDebug.LogWarning("[Screenshot] No usable capture concept - every concept in " +
                                       "ScreenshotDirectorConfig has zero weight or no reachable distance.");
                    return;
                }
                wantPair = concept.framing == ScreenshotFramingKind.Pair;

                heldCorridor = HoldOcclusionCorridor(config);

                // One frame, so the corridor's driver re-publishes its globals with the hold applied
                // BEFORE the capture camera renders. Skipping this photographs the frame the hold
                // had not reached yet, which looks exactly like the hold not working.
                await UniTask.DelayFrame(1);
                if (this == null || subject == null) return;

                Vector3 course = status != null && status.Course.sqrMagnitude > 1e-6f
                    ? status.Course
                    : subject.forward;
                float speed = status?.Speed ?? 0f;

                ScreenshotShot shot;
                if (wantPair && pairA != null && pairB != null)
                {
                    // The pair's shared heading is what the vantage angle is measured from. Read
                    // from each hull's own facing rather than the local ship's course, because
                    // either or both may be a vessel this machine only sees replicated.
                    Vector3 flow = pairA.forward + pairB.forward;
                    if (flow.sqrMagnitude < 1e-6f) flow = course;

                    float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
                    float distanceFloor = Mathf.Max(ScreenshotFraming.MinimumDistance, pairRadius * 1.6f);

                    shot = PickClearestShot(
                        config,
                        () => ScreenshotFraming.SolvePair(
                            concept, pairA.position, pairB.position, flow, pairRadius, _rng,
                            minimumDistance: distanceFloor, aspect: aspect),
                        candidate =>
                            CountOccluders(candidate.Position, pairA.position, pairRadius) +
                            CountOccluders(candidate.Position, pairB.position, pairRadius));
                }
                else
                {
                    Vector3 subjectPosition = subject.position;
                    Vector3 nose = subject.forward;
                    float distanceFloor = Mathf.Max(ScreenshotFraming.MinimumDistance, hullRadius * 1.6f);

                    shot = PickClearestShot(
                        config,
                        () => ScreenshotFraming.Solve(
                            concept, subjectPosition, nose, course, speed, _rng,
                            minimumDistance: distanceFloor),
                        candidate => CountOccluders(candidate.Position, subjectPosition, hullRadius));
                }

                // Mark the ships for the capture frame: the vessel vision band, rescaled so the
                // mark arrives halfway through this concept's own zoom range and is a solid
                // domain-coloured silhouette at its furthest. Held across Render() and released in
                // the outer finally, identity-guarded exactly like the corridor's hold above.
                if (config.TryResolveVisionBand(concept, out float markStart, out float markSolid))
                    heldVisionBand = VesselVisionShading.BeginCapturePass(markStart, markSolid);

                byte[] png = Render(config, shot);
                if (png == null) return;

                Write(config, concept, png);
            }
            catch (Exception ex)
            {
                // A screenshot must never be able to take the game down with it.
                CSDebug.LogError($"[Screenshot] Capture failed: {ex.Message}");
            }
            finally
            {
                // Identity-guarded: only lift a hold this capture placed, so a replay camera's
                // own hold survives a photograph taken during it.
                if (heldCorridor) PrismOcclusionCorridor.SetSuppressed(false);
                if (heldVisionBand) VesselVisionShading.EndCapturePass();
                _capturing = false;
            }
        }

        // ───────────────────────── a clear line of sight ─────────────────────────

        /// <summary>
        /// Roll <paramref name="solve"/> a few times and keep the vantage with the least prism mass
        /// standing between the lens and the subject.
        ///
        /// <para>It re-rolls the VANTAGE and never the CONCEPT, which is the whole design: the shot
        /// stays an over-the-shoulder or a static tracking cam, and only the azimuth, elevation,
        /// distance and lens within that concept move. A search that could change concepts would
        /// quietly collapse the library onto whichever shot type happens to look at open space, and
        /// the point of the library is variety.</para>
        ///
        /// <para>Deliberately a PREFERENCE and not a rule — "generally, but not always". It keeps
        /// the best of a handful of samples rather than searching until it finds a clear one, so a
        /// capture taken deep inside a forest still comes out (framed from wherever the mass was
        /// thinnest) instead of failing or teleporting the camera somewhere the concept never
        /// described. At <c>clearShotSamples = 1</c> it is exactly the old single roll.</para>
        ///
        /// <para>The early accept matters more than the sample count: in open space the first
        /// candidate scores zero and the remaining solves never run, so the common case costs one
        /// cone count.</para>
        /// </summary>
        static ScreenshotShot PickClearestShot(
            ScreenshotDirectorConfigSO config,
            Func<ScreenshotShot> solve,
            Func<ScreenshotShot, int> scoreOccluders)
        {
            int samples = Mathf.Max(1, config.clearShotSamples);
            int acceptAt = Mathf.Max(0, config.clearShotAcceptOccluders);

            ScreenshotShot best = solve();
            if (samples == 1) return best;

            int bestScore = scoreOccluders(best);
            if (bestScore <= acceptAt) return best;

            for (int i = 1; i < samples; i++)
            {
                var candidate = solve();
                int score = scoreOccluders(candidate);
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
                if (bestScore <= acceptAt) break;
            }

            return best;
        }

        /// <summary>
        /// How many live prisms stand between <paramref name="cameraPosition"/> and the subject —
        /// the cone from the lens to the subject's circumscribing sphere, which is the same volume
        /// <c>PrismOcclusionCorridor</c> clears for the pilot and therefore the same geometry
        /// "obscured" means.
        ///
        /// <para>The cone stops ONE HULL RADIUS SHORT of the subject on purpose. A ship threading a
        /// canyon is surrounded by mass and that is a photograph worth having — what ruins the shot
        /// is a prism between the lens and the hull, so counting to the hull's near surface asks
        /// exactly that and lets the ship sit in and among the blocks as freely as it likes.</para>
        ///
        /// <para>Returns 0 when no spatial index exists (a scene with no prism mass at all), so the
        /// search degrades to the first roll rather than to an error. Never
        /// <c>Physics.OverlapSphere</c>: the index is the canonical store of prism mass and physics
        /// is structurally blind to prisms for the first 0.6 s of their life anyway (CLAUDE.md).</para>
        /// </summary>
        static int CountOccluders(Vector3 cameraPosition, Vector3 subjectPosition, float subjectRadius)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable || subjectRadius <= 0f) return 0;

            Vector3 toSubject = subjectPosition - cameraPosition;
            float distance = toSubject.magnitude;
            float reach = distance - subjectRadius;
            if (reach <= 0.01f) return 0;   // camera inside the hull's own sphere; nothing can be between

            Vector3 basePoint = cameraPosition + toSubject * (reach / distance);
            return index.CountInCone(cameraPosition, basePoint, subjectRadius);
        }

        // ───────────────────────── the subject ─────────────────────────

        /// <summary>
        /// The local pilot's hull, its measured radius, and its flight state.
        ///
        /// <para>Read from <see cref="PrismOcclusionCorridor"/> rather than from
        /// <c>GameDataSO.LocalPlayer</c> for a specific reason: this object is created at runtime
        /// by <see cref="EnsureExists"/>, so Reflex never injects it and an <c>[Inject]</c> field
        /// here would be permanently null (CLAUDE.md ▸ DI). The corridor already maintains exactly
        /// the handle wanted — the LOCAL PILOT's vessel transform and its circumscribing radius,
        /// bound on every spawn path including a mid-match vessel swap, because a platform law
        /// depends on it being right.</para>
        /// </summary>
        static bool TryResolveSubject(out Transform subject, out float hullRadius, out IVesselStatus status)
        {
            subject = PrismOcclusionCorridor.Target;
            hullRadius = PrismOcclusionCorridor.TargetRadius;
            status = null;

            if (subject == null || !subject.gameObject.activeInHierarchy)
            {
                subject = null;
                return false;
            }

            status = subject.GetComponent<IVesselStatus>() ?? subject.GetComponentInParent<IVesselStatus>();
            return true;
        }

        /// <summary>
        /// The best two vessels to photograph together, or none.
        ///
        /// <para>Candidates come from <see cref="VesselVisionShading.CollectStampedVessels"/> —
        /// the vision band's own roster, which every vessel joins through
        /// <c>VesselHelper.SetShipProperties</c> on every spawn, swap and replicated domain change.
        /// That is the same argument this class already makes for reading the occlusion corridor:
        /// a platform law maintains the handle because the law depends on it being right, so
        /// reading it is free and cannot drift from what is on screen. It also excludes the toy
        /// matrices' mini hulls by construction, since those are stamped through a different door.</para>
        ///
        /// <para>Pairs containing the LOCAL ship win ties, because the photograph is nominally of
        /// your own flight; among equals the closest pair wins, since that is the tighter moment.
        /// Two vessels at the same position are rejected by the band's floor rather than by a
        /// special case.</para>
        /// </summary>
        bool TryResolvePair(
            ScreenshotDirectorConfigSO config, Transform local,
            out Transform a, out Transform b, out float subjectRadius)
        {
            a = b = null;
            subjectRadius = 0f;

            VesselVisionShading.CollectStampedVessels(_vesselScratch);
            if (_vesselScratch.Count < 2) return false;

            config.ResolvePairBand(out float min, out float max);
            float minSqr = min * min;
            float maxSqr = max * max;

            float bestGapSqr = float.MaxValue;
            bool bestHasLocal = false;

            for (int i = 0; i < _vesselScratch.Count; i++)
            for (int j = i + 1; j < _vesselScratch.Count; j++)
            {
                Transform first = _vesselScratch[i];
                Transform second = _vesselScratch[j];

                float gapSqr = (second.position - first.position).sqrMagnitude;
                if (gapSqr < minSqr || gapSqr > maxSqr) continue;

                bool hasLocal = ReferenceEquals(first, local) || ReferenceEquals(second, local);

                // Local beats non-local outright; within a tier, closer wins.
                if (bestHasLocal && !hasLocal) continue;
                if (hasLocal == bestHasLocal && gapSqr >= bestGapSqr) continue;

                a = first;
                b = second;
                bestGapSqr = gapSqr;
                bestHasLocal = hasLocal;
            }

            if (a == null) return false;

            // Measured per hull rather than assumed: the fleet spans a wide size range, and this
            // is the corridor's own measurement, so a new vessel needs nothing authored.
            subjectRadius = Mathf.Max(
                PrismOcclusionCorridor.MeasureCircumscribedRadius(a),
                PrismOcclusionCorridor.MeasureCircumscribedRadius(b));
            return true;
        }

        /// <summary>
        /// Close the prism occlusion corridor for the capture frame.
        ///
        /// <para>This is the same narrow, symmetric hold <c>CameraManager.BeginManualReplayCamera</c>
        /// takes, for the same reason: the corridor dissolves mass along the line from the CAMERA to
        /// the local ship so the pilot can always see their own hull, and a camera posed somewhere
        /// the pilot is not would cut that hole through unrelated mass — in a photograph, straight
        /// through the trail the shot exists to show. It is a hold and not an opt-out: the vessel
        /// binding stays, the lift in <c>finally</c> is unconditional, and it lasts two frames.</para>
        ///
        /// <para>Returns whether THIS capture placed the hold, so a hold somebody else is already
        /// holding is never lifted by us.</para>
        /// </summary>
        static bool HoldOcclusionCorridor(ScreenshotDirectorConfigSO config)
        {
            if (!config.holdOcclusionCorridor || PrismOcclusionCorridor.IsSuppressed) return false;
            PrismOcclusionCorridor.SetSuppressed(true);
            return true;
        }

        // ───────────────────────── the render ─────────────────────────

        byte[] Render(ScreenshotDirectorConfigSO config, ScreenshotShot shot)
        {
            var cam = EnsureCamera(config);
            if (cam == null) return null;

            int height = Mathf.Clamp(config.captureHeight, 480, 4320);
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            int width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 480, 8192);

            // `GetTemporary`'s antiAliasing parameter DEFAULTS TO 1, so omitting it renders the
            // capture with no MSAA at all while the game beside it runs the URP asset's 4x — the
            // shot comes back with stair-stepped prism edges and reads as a lower-quality image
            // than the screen it was taken from. `QualitySettings.antiAliasing` is the value URP
            // syncs FROM its own asset, so this asks for exactly what the game is running.
            int msaa = config.matchGameQuality ? ResolveMsaaSamples() : 1;

            // sRGB read/write, because the project renders in linear and the PNG has to come out
            // looking like the screen rather than washed out.
            RenderTexture target = RenderTexture.GetTemporary(
                width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, msaa);
            // An MSAA surface is resolved by BLITTING it, never by reading it: `ReadPixels` off a
            // multisampled target is undefined on several backends (it returns one sample, or
            // nothing). One extra full-frame copy, on a key the player pressed.
            RenderTexture resolve = msaa > 1
                ? RenderTexture.GetTemporary(
                    width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                : null;
            Texture2D readback = null;
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                cam.transform.SetPositionAndRotation(shot.Position, shot.Rotation);
                cam.fieldOfView = shot.FieldOfView;
                // Far enough to keep the whole arena in the shot at any concept distance; the near
                // plane scales with the shot so a long-range capture does not lose precision.
                cam.nearClipPlane = Mathf.Clamp(shot.Distance * 0.005f, 0.05f, 1f);
                cam.farClipPlane = Mathf.Max(20000f, shot.Distance * 20f);
                // Assigning a target texture re-derives the aspect from it, so the explicit aspect
                // goes AFTER — otherwise the rounding in `width` quietly re-frames the shot.
                cam.targetTexture = target;
                cam.aspect = aspect;

                cam.Render();

                if (resolve != null) Graphics.Blit(target, resolve);

                RenderTexture.active = resolve != null ? resolve : target;
                readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply(false);

                // PNG, so the file is LOSSLESS — a capture is a source image somebody may crop,
                // colour or scale later, and a lossy encode would bake this moment's artefacts in.
                return readback.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                if (resolve != null) RenderTexture.ReleaseTemporary(resolve);
                if (readback != null) Destroy(readback);
            }
        }

        /// <summary>
        /// The MSAA sample count the game is actually running, snapped to a count a RenderTexture
        /// will accept. `QualitySettings.antiAliasing` reports 0 for "off" and otherwise 2/4/8;
        /// a RenderTexture wants 1/2/4/8, so 0 and 1 are the same request and anything else is
        /// rounded DOWN to a legal count rather than refused (an illegal count throws).
        /// </summary>
        static int ResolveMsaaSamples()
        {
            int samples = QualitySettings.antiAliasing;
            if (samples >= 8) return 8;
            if (samples >= 4) return 4;
            if (samples >= 2) return 2;
            return 1;
        }

        Camera EnsureCamera(ScreenshotDirectorConfigSO config)
        {
            if (_camera == null)
            {
                var go = new GameObject("[ScreenshotCamera]");
                go.transform.SetParent(transform, false);
                _camera = go.AddComponent<Camera>();
                // Stepped by hand in Render(). An ENABLED camera would render every frame of every
                // scene for a texture nobody is looking at.
                _camera.enabled = false;
            }

            var source = Camera.main;
            if (source != null)
            {
                _camera.clearFlags = source.clearFlags;
                _camera.backgroundColor = source.backgroundColor;
                _camera.cullingMask = source.cullingMask & ~config.excludedLayers.value;
                _camera.allowHDR = source.allowHDR;
                _camera.allowMSAA = source.allowMSAA;
            }
            else
            {
                _camera.clearFlags = CameraClearFlags.Skybox;
                _camera.cullingMask = ~config.excludedLayers.value;
            }

            AdoptUrpSettings(_camera, source, config.matchGameQuality);
            return _camera;
        }

        /// <summary>
        /// A bare <c>AddComponent&lt;Camera&gt;</c> comes up with URP's DEFAULTS, not the project's
        /// — so an un-adopted capture camera photographs a flat, bloom-free version of a world the
        /// game shows lit, which reads as the feature being broken rather than as a different
        /// camera. (<c>ConnectingArenaPreview.AdoptUrpSettings</c> records the same finding, from
        /// the same trap.)
        /// </summary>
        static void AdoptUrpSettings(Camera target, Camera source, bool matchGameQuality)
        {
            var to = target.GetUniversalAdditionalCameraData();
            if (to == null) return;

            if (source == null || !source.TryGetComponent(out UniversalAdditionalCameraData from))
            {
                to.renderPostProcessing = matchGameQuality;
                return;
            }

            to.volumeLayerMask = from.volumeLayerMask;
            to.renderPostProcessing = matchGameQuality && from.renderPostProcessing;
            to.antialiasing = matchGameQuality ? from.antialiasing : AntialiasingMode.None;
            to.antialiasingQuality = from.antialiasingQuality;
            to.renderShadows = matchGameQuality && from.renderShadows;
        }

        // ───────────────────────── the file ─────────────────────────

        /// <summary>
        /// Writes the PNG. Synchronous on purpose: the expensive half of a capture is
        /// <c>EncodeToPNG</c>, which is main-thread-only, so an async WRITE would move a few
        /// milliseconds off a frame that has already spent a couple of hundred encoding. (It is
        /// also not available: this project builds against the .NET Framework 4.8 profile, which
        /// has no <c>File.WriteAllBytesAsync</c>.) The hitch is one frame, on a key the player
        /// pressed deliberately.
        /// </summary>
        static void Write(ScreenshotDirectorConfigSO config, ScreenshotConcept concept, byte[] png)
        {
            string folder = config.ResolveOutputFolder();
            string file = config.BuildFileName(concept.name, DateTime.Now);
            string path;

            try
            {
                Directory.CreateDirectory(folder);
                path = Path.Combine(folder, file);
            }
            catch (Exception ex)
            {
                // A folder the OS will not give us is the one failure worth a second attempt
                // somewhere that always works, rather than losing the capture.
                CSDebug.LogWarning($"[Screenshot] Could not use '{folder}' ({ex.Message}); " +
                                   "falling back to the persistent data path.");
                folder = Path.Combine(Application.persistentDataPath, "Screenshots");
                Directory.CreateDirectory(folder);
                path = Path.Combine(folder, file);
            }

            File.WriteAllBytes(path, png);
            CSDebug.Log($"[Screenshot] {concept.name} → {path}");
        }
    }
}
