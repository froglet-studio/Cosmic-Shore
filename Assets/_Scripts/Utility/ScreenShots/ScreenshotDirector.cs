using System;
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

            try
            {
                var config = Config;

                if (!TryResolveSubject(out Transform subject, out float hullRadius, out IVesselStatus status))
                {
                    CSDebug.LogWarning("[Screenshot] Nothing to photograph - no local vessel is bound. " +
                                       "Captures are taken of the ship you are flying.");
                    return;
                }

                var concept = config.PickConcept(_rng);
                if (concept == null)
                {
                    CSDebug.LogWarning("[Screenshot] No usable capture concept - every concept in " +
                                       "ScreenshotDirectorConfig has zero weight or no reachable distance.");
                    return;
                }

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

                var shot = ScreenshotFraming.Solve(
                    concept, subject.position, subject.forward, course, speed, _rng,
                    minimumDistance: Mathf.Max(ScreenshotFraming.MinimumDistance, hullRadius * 1.6f));

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
                _capturing = false;
            }
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

            // sRGB read/write, because the project renders in linear and the PNG has to come out
            // looking like the screen rather than washed out.
            RenderTexture target = RenderTexture.GetTemporary(
                width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
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

                RenderTexture.active = target;
                readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply(false);

                return readback.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                if (readback != null) Destroy(readback);
            }
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
