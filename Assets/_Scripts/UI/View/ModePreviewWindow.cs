using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The arcade modal's preview window, in place of the pre-rendered video clip. It has exactly
    /// two states and <b>never changes size in either of them</b>:
    ///
    /// <list type="number">
    /// <item><b>Idle</b> — a slowly turning scale model of the arena the mode actually builds,
    /// rendered from a private stage. Cheap; this is what you see while browsing.</item>
    /// <item><b>Live</b> — the real gameplay camera, following the real vessel in the mode's own
    /// arena, rendered into this same window. The game is simply playing in there.</item>
    /// </list>
    ///
    /// <para><b>Focus, not full screen.</b> Clicking the window (or pressing Submit on it with a
    /// pad) hands input to the vessel; clicking away, or Cancel/Escape, hands it back to the UI.
    /// The window does not grow, the modal does not close, and the menu scene behind it never
    /// changes. That is the whole interaction — it is a focusable widget that happens to contain a
    /// game.</para>
    ///
    /// <para>The window owns the <see cref="RenderTexture"/> both states draw into and lends it to
    /// <see cref="ModePreviewSession"/> for the live state; it never owns the arena or the vessel.</para>
    /// </summary>
    public class ModePreviewWindow : MonoBehaviour
    {
        [Header("Surface")]
        [SerializeField, Tooltip("The RawImage inside the modal's preview frame. Both states draw here.")]
        RawImage surface;

        [SerializeField, Tooltip("Button covering the surface. Its click is what asks for focus - " +
                                 "using a Button rather than a raw pointer handler is what makes " +
                                 "the window reachable with a gamepad's Submit as well as a tap.")]
        Button focusButton;

        [SerializeField, Tooltip("Optional frame shown only while the window has focus, so it is " +
                                 "obvious where input is going.")]
        GameObject focusIndicator;

        [SerializeField, Tooltip("Optional 'tap to play' hint, hidden once the window has focus.")]
        GameObject focusHint;

        [Header("Render texture")]
        [SerializeField, Tooltip("Render texture height in pixels. Width follows the window's own " +
                                 "aspect, so the live view is never letterboxed or stretched.")]
        int renderHeight = 360;

        [SerializeField, Tooltip("Colour behind the idle scale model.")]
        Color idleBackground = new(0.02f, 0.03f, 0.06f, 1f);

        [Header("Idle stage")]
        [SerializeField, Tooltip("Layer the idle scale model lives on. The idle camera is culled to " +
                                 "this layer and nothing else, which is what keeps it from rendering " +
                                 "the menu world a second time.")]
        string previewLayerName = "ModePreview";

        [SerializeField, Tooltip("How far out the idle stage sits. Must stay well beyond every " +
                                 "gameplay camera's far clip (8000 in Menu_Main).")]
        float stageDistance = 50000f;

        [SerializeField, Tooltip("World radius the idle model is fitted into.")]
        float stageRadius = 50f;

        [SerializeField, Tooltip("Idle camera distance as a multiple of the model radius.")]
        float cameraDistance = 2.4f;

        [SerializeField, Tooltip("Degrees the idle camera looks down from.")]
        float cameraPitch = 18f;

        [SerializeField, Tooltip("Idle camera vertical field of view.")]
        float fieldOfView = 40f;

        [Inject] GameDataSO gameData;

        readonly Dictionary<CellConfigDataSO, CellMiniatureBuilder.Miniature> _cache = new();

        Transform _stage;
        Transform _modelHost;
        Camera _idleCamera;
        Light _idleLight;
        RenderTexture _renderTexture;
        RectTransform _surfaceRect;
        int _layer = -1;
        float _spinRate;
        CancellationTokenSource _buildCts;
        ToyContext _context;

        /// <summary>Raised when the player asks to play in the window (click / Submit).</summary>
        public event Action OnFocusRequested;

        /// <summary>Raised when focus is given up (click away, Cancel, Escape).</summary>
        public event Action OnFocusReleased;

        /// <summary>True while input belongs to the window rather than the UI.</summary>
        public bool HasFocus { get; private set; }

        /// <summary>True while the window is showing something.</summary>
        public bool IsShowing { get; private set; }

        /// <summary>True while the live gameplay camera is drawing here.</summary>
        public bool IsLive { get; private set; }

        /// <summary>The texture both states draw into. Null until the window is first shown.</summary>
        public RenderTexture LiveTexture => _renderTexture;

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            _surfaceRect = surface ? surface.rectTransform : null;
            if (focusButton) focusButton.onClick.AddListener(HandleFocusButton);
            ApplyFocusChrome();
        }

        void OnDestroy()
        {
            if (focusButton) focusButton.onClick.RemoveListener(HandleFocusButton);

            CancelBuild();
            ReleaseStage();
            ReleaseRenderTexture();

            foreach (var entry in _cache)
                if (entry.Value.Mesh) Destroy(entry.Value.Mesh);
            _cache.Clear();
        }

        void Update()
        {
            if (HasFocus)
            {
                if (WantsRelease()) ReleaseFocus();
                return;
            }

            if (!IsShowing || IsLive || !_modelHost) return;

            // The idle model turns in place - a thing you can watch. Unscaled: the modal is free
            // to sit over a paused menu.
            _modelHost.Rotate(Vector3.up, _spinRate * Time.unscaledDeltaTime, Space.Self);
        }

        // ── Showing ──────────────────────────────────────────────────────────

        /// <summary>
        /// Show <paramref name="definition"/>'s arena as an idle scale model. Returns false when
        /// the mode has nothing modellable — a cell whose arena is GROWN rather than laid
        /// (Rampage's seeded forest, Scarab Scramble's nucleus court) authors no
        /// <c>EnvironmentPrefab</c>, and the caller falls back to the legacy video.
        /// </summary>
        public bool ShowIdle(ModePreviewDefinitionSO definition)
        {
            CancelBuild();
            GoIdleInternal();

            if (!definition || !definition.HasDiorama) { Hide(); return false; }
            if (!EnsureRenderTexture() || !EnsureStage()) { Hide(); return false; }

            _spinRate = definition.DioramaSpinRate;
            ClearModel();

            IsShowing = true;
            SetSurfaceVisible(true);
            _idleCamera.enabled = true;
            if (_idleLight) _idleLight.enabled = true;

            _buildCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            BuildAsync(definition, _buildCts.Token).Forget();
            return true;
        }

        /// <summary>
        /// Hand the window to the live gameplay camera. The idle stage stops rendering; the
        /// surface keeps the same texture and the same size, so nothing on screen moves.
        /// </summary>
        public void GoLive()
        {
            if (!EnsureRenderTexture()) return;

            CancelBuild();
            ClearModel();

            IsShowing = true;
            IsLive = true;
            if (_idleCamera) _idleCamera.enabled = false;
            if (_idleLight) _idleLight.enabled = false;
            SetSurfaceVisible(true);
            ApplyFocusChrome();
        }

        /// <summary>Back to the idle model (the live camera has been given up elsewhere).</summary>
        public void GoIdle()
        {
            GoIdleInternal();
            if (_idleCamera && IsShowing) _idleCamera.enabled = true;
            if (_idleLight && IsShowing) _idleLight.enabled = true;
        }

        void GoIdleInternal()
        {
            if (HasFocus) ReleaseFocus();
            IsLive = false;
            ApplyFocusChrome();
        }

        /// <summary>Stop drawing entirely. Safe to call when already hidden.</summary>
        public void Hide()
        {
            CancelBuild();
            if (HasFocus) ReleaseFocus();

            IsShowing = false;
            IsLive = false;
            if (_idleCamera) _idleCamera.enabled = false;
            if (_idleLight) _idleLight.enabled = false;
            SetSurfaceVisible(false);
            ClearModel();
            ApplyFocusChrome();
        }

        void SetSurfaceVisible(bool visible)
        {
            if (!surface) return;
            surface.texture = visible ? _renderTexture : null;
            surface.enabled = visible;
            if (focusButton) focusButton.interactable = visible;
        }

        // ── Focus ────────────────────────────────────────────────────────────

        void HandleFocusButton()
        {
            if (HasFocus) return;
            OnFocusRequested?.Invoke();
        }

        /// <summary>
        /// Grant focus. Called by the session once the arena is actually flyable - the window
        /// never grants its own focus, because focus is meaningless until there is something in
        /// there to fly.
        /// </summary>
        public void GrantFocus()
        {
            if (HasFocus) return;
            HasFocus = true;
            ApplyFocusChrome();
        }

        /// <summary>Give focus back to the UI. Idempotent.</summary>
        public void ReleaseFocus()
        {
            if (!HasFocus) return;
            HasFocus = false;
            ApplyFocusChrome();
            OnFocusReleased?.Invoke();
        }

        /// <summary>
        /// Focus is given up by Cancel (gamepad East / Escape) or by a click outside the window.
        /// Read from the devices directly rather than through the EventSystem, because a focused
        /// window has navigation events switched off - that is the point of focus.
        /// </summary>
        bool WantsRelease()
        {
            var pad = Gamepad.current;
            if (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
                return true;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && _surfaceRect)
            {
                var canvas = _surfaceRect.GetComponentInParent<Canvas>();
                var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        _surfaceRect, mouse.position.ReadValue(), cam))
                    return true;
            }

            return false;
        }

        void ApplyFocusChrome()
        {
            if (focusIndicator) focusIndicator.SetActive(HasFocus);
            if (focusHint) focusHint.SetActive(IsShowing && !HasFocus);
        }

        // ── Idle model ───────────────────────────────────────────────────────

        async UniTaskVoid BuildAsync(ModePreviewDefinitionSO definition, CancellationToken ct)
        {
            // One frame, so opening the modal is never gated on a generation pass.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            var environment = ResolveMiniature(definition.PreviewCell.EnvironmentPrefab,
                                               definition.PreviewCell, definition);
            if (environment.IsValid) Attach(environment);

            if (definition.StructurePrefab &&
                definition.StructurePrefab.TryGetComponent(out SpawnableBase structure))
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

                var built = CellMiniatureBuilder.Build(structure, stageRadius,
                    definition.DioramaPointBudget, definition.DioramaSignatureCoverage);
                if (structure is CellEnvironmentSpawnableBase env) env.ReleaseGeneratedData();
                if (built.IsValid) Attach(built);
            }
        }

        CellMiniatureBuilder.Miniature ResolveMiniature(SpawnableBase prefab,
            CellConfigDataSO config, ModePreviewDefinitionSO definition)
        {
            if (_cache.TryGetValue(config, out var cached) && cached.Mesh)
                return cached;

            var built = CellMiniatureBuilder.Build(prefab, stageRadius,
                definition.DioramaPointBudget, definition.DioramaSignatureCoverage);

            if (prefab is CellEnvironmentSpawnableBase env) env.ReleaseGeneratedData();

            if (built.IsValid) _cache[config] = built;
            else CSDebug.LogWarning($"[ModePreview] {prefab.name} generated no points - " +
                                    $"{definition.Mode} shows no idle model.");
            return built;
        }

        void Attach(CellMiniatureBuilder.Miniature miniature)
        {
            _context ??= new ToyContext { GameData = gameData };

            var go = ToyFactory.AddMiniatureBody(_modelHost, miniature, _context, "IdleModel");
            if (!go) return;

            SetLayerRecursive(go.transform, _layer);
            ToyFactory.ScaleInFromZero(go.transform, 0.5f).Forget();
        }

        void ClearModel()
        {
            if (!_modelHost) return;
            for (int i = _modelHost.childCount - 1; i >= 0; i--)
                Destroy(_modelHost.GetChild(i).gameObject);
            _modelHost.localRotation = Quaternion.identity;
        }

        // ── Stage + render texture ───────────────────────────────────────────

        bool EnsureRenderTexture()
        {
            if (_renderTexture) return true;

            int height = Mathf.Max(64, renderHeight);
            float aspect = 16f / 9f;
            if (_surfaceRect && _surfaceRect.rect.height > 1f)
                aspect = Mathf.Clamp(_surfaceRect.rect.width / _surfaceRect.rect.height, 0.25f, 4f);

            _renderTexture = new RenderTexture(Mathf.Max(64, Mathf.RoundToInt(height * aspect)), height, 24)
            {
                name = "ModePreviewRT",
                antiAliasing = 1,
                useMipMap = false,
            };
            return true;
        }

        void ReleaseRenderTexture()
        {
            if (surface) surface.texture = null;
            if (!_renderTexture) return;
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }

        bool EnsureStage()
        {
            if (_stage) return true;

            _layer = LayerMask.NameToLayer(previewLayerName);
            if (_layer < 0)
            {
                CSDebug.LogError($"[ModePreview] Layer '{previewLayerName}' does not exist. Add it in " +
                                 "Project Settings > Tags and Layers - without a private layer the " +
                                 "idle camera would render the whole menu world a second time, which " +
                                 "is the one thing this feature must not do.");
                return false;
            }

            var root = new GameObject("ModePreviewIdleStage") { layer = _layer };
            root.transform.position = Vector3.up * stageDistance;
            _stage = root.transform;

            var host = new GameObject("Model") { layer = _layer };
            host.transform.SetParent(_stage, false);
            _modelHost = host.transform;

            var camGo = new GameObject("IdleCamera") { layer = _layer };
            camGo.transform.SetParent(_stage, false);

            float dist = stageRadius * Mathf.Max(0.5f, cameraDistance);
            var offset = Quaternion.Euler(cameraPitch, 0f, 0f) * Vector3.back * dist;
            camGo.transform.localPosition = offset;
            camGo.transform.localRotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);

            _idleCamera = camGo.AddComponent<Camera>();
            _idleCamera.clearFlags = CameraClearFlags.SolidColor;
            _idleCamera.backgroundColor = idleBackground;
            // The whole perf story in one line: this camera sees the stage and nothing else.
            _idleCamera.cullingMask = 1 << _layer;
            _idleCamera.fieldOfView = fieldOfView;
            _idleCamera.nearClipPlane = 0.1f;
            _idleCamera.farClipPlane = dist + stageRadius * 4f;
            _idleCamera.targetTexture = _renderTexture;
            _idleCamera.allowHDR = false;
            _idleCamera.allowMSAA = false;
            _idleCamera.depth = -100;
            _idleCamera.enabled = false;
            camGo.tag = "Untagged";

            var lightGo = new GameObject("IdleLight") { layer = _layer };
            lightGo.transform.SetParent(_stage, false);
            lightGo.transform.localRotation = Quaternion.Euler(45f, -30f, 0f);
            _idleLight = lightGo.AddComponent<Light>();
            _idleLight.type = LightType.Directional;
            _idleLight.intensity = 1.1f;
            // Lights ignore layers unless told to: without this the stage light would fall on the
            // whole menu world.
            _idleLight.cullingMask = 1 << _layer;
            _idleLight.shadows = LightShadows.None;
            _idleLight.enabled = false;

            return true;
        }

        void ReleaseStage()
        {
            if (_idleCamera) _idleCamera.targetTexture = null;
            if (_stage) Destroy(_stage.gameObject);
            _stage = null;
            _modelHost = null;
            _idleCamera = null;
            _idleLight = null;
        }

        void CancelBuild()
        {
            _buildCts?.Cancel();
            _buildCts?.Dispose();
            _buildCts = null;
        }

        static void SetLayerRecursive(Transform target, int layer)
        {
            target.gameObject.layer = layer;
            for (int i = 0; i < target.childCount; i++)
                SetLayerRecursive(target.GetChild(i), layer);
        }
    }
}
