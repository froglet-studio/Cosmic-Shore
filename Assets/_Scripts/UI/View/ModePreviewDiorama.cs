using System.Collections.Generic;
using System.Threading;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The arcade modal's preview window: a slowly turning <b>scale model of the arena the mode
    /// actually builds</b>, in place of the pre-rendered video clip.
    ///
    /// <para>It is the same model the Cell Selector toy shows — <see cref="CellMiniatureBuilder"/>
    /// strides the environment generator's own output into ONE mesh with a submesh per domain and
    /// <b>spawns no prisms</b>. So the preview cannot go stale when a generator changes, it shows
    /// the mode's true silhouette and domain composition, and it costs a few draw calls rather
    /// than a video decode.</para>
    ///
    /// <para><b>How it stays off the frame budget.</b> The model sits on a private stage
    /// <see cref="stageDistance"/> units away — beyond every gameplay camera's far clip (8000 in
    /// Menu_Main) — on its own <c>ModePreview</c> layer, and the one camera that renders it is
    /// culled to that layer alone. So this camera never renders the menu world, and the menu
    /// cameras never render this: no second pass over the ~42k-prism Lattice cell. The camera and
    /// its render texture are live only while the modal is open.</para>
    ///
    /// <para>Models are built one frame after the modal opens (so opening is never gated on a
    /// generation) and cached per cell config, with the generator's lay data released immediately
    /// after sampling — holding several 34k-entry lay lists so the menu can show thumbnails is
    /// the wrong trade on mobile.</para>
    /// </summary>
    public class ModePreviewDiorama : MonoBehaviour
    {
        [Header("Surface")]
        [SerializeField, Tooltip("The RawImage inside the modal's preview window that the stage " +
                                 "renders into.")]
        RawImage surface;

        [SerializeField, Tooltip("Render texture size. Small on purpose - this is a thumbnail, and " +
                                 "the silhouette is what reads at this size.")]
        Vector2Int resolution = new(384, 216);

        [SerializeField, Tooltip("Colour behind the model.")]
        Color background = new(0.02f, 0.03f, 0.06f, 1f);

        [Header("Stage")]
        [SerializeField, Tooltip("Layer the stage lives on. The preview camera is culled to this " +
                                 "layer and nothing else, which is what keeps it off the world.")]
        string previewLayerName = "ModePreview";

        [SerializeField, Tooltip("How far from the origin the private stage sits. Must stay well " +
                                 "beyond every gameplay camera's far clip (8000 in Menu_Main) so no " +
                                 "game camera can ever see it, and well inside float precision.")]
        float stageDistance = 50000f;

        [SerializeField, Tooltip("World radius the model is fitted into on the stage. Larger keeps " +
                                 "the shards above the float-precision floor at stage distance.")]
        float stageRadius = 50f;

        [Header("Framing")]
        [SerializeField, Tooltip("Camera distance as a multiple of the model radius.")]
        float cameraDistance = 2.4f;

        [SerializeField, Tooltip("Degrees the camera looks down on the model from.")]
        float cameraPitch = 18f;

        [SerializeField, Tooltip("Vertical field of view.")]
        float fieldOfView = 40f;

        [Inject] GameDataSO gameData;

        readonly Dictionary<CellConfigDataSO, CellMiniatureBuilder.Miniature> _cache = new();

        Transform _stage;
        Transform _modelHost;
        Camera _camera;
        RenderTexture _renderTexture;
        Light _light;
        int _layer = -1;
        float _spinRate;
        CancellationTokenSource _buildCts;
        ToyContext _context;

        /// <summary>True while a model is being shown.</summary>
        public bool IsShowing { get; private set; }

        void OnDestroy()
        {
            CancelBuild();
            ReleaseStage();

            // The builder mints a Mesh per config; nothing else owns them.
            foreach (var entry in _cache)
                if (entry.Value.Mesh) Destroy(entry.Value.Mesh);
            _cache.Clear();
        }

        void Update()
        {
            if (!IsShowing || !_modelHost) return;

            // The world turns in place - a thing you can watch. Unscaled: the modal is free to
            // sit over a paused menu.
            _modelHost.Rotate(Vector3.up, _spinRate * Time.unscaledDeltaTime, Space.Self);
        }

        /// <summary>
        /// Show <paramref name="definition"/>'s arena. A null definition, or one whose cell has no
        /// authored environment, hides the window instead - the card's own artwork is the
        /// fallback, and an empty black rectangle would read as a broken preview.
        /// </summary>
        public void Show(ModePreviewDefinitionSO definition)
        {
            CancelBuild();

            if (!definition || !definition.PreviewCell || !definition.PreviewCell.EnvironmentPrefab)
            {
                Hide();
                return;
            }

            if (!EnsureStage())
            {
                Hide();
                return;
            }

            _spinRate = definition.DioramaSpinRate;
            ClearModel();

            IsShowing = true;
            _camera.enabled = true;
            if (_light) _light.enabled = true;
            if (surface)
            {
                surface.texture = _renderTexture;
                surface.enabled = true;
            }

            _buildCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            BuildAsync(definition, _buildCts.Token).Forget();
        }

        /// <summary>Stop rendering and release the camera. Safe to call when already hidden.</summary>
        public void Hide()
        {
            CancelBuild();
            IsShowing = false;

            if (_camera) _camera.enabled = false;
            if (_light) _light.enabled = false;
            if (surface) surface.enabled = false;

            ClearModel();
        }

        // ── Model ────────────────────────────────────────────────────────────

        async UniTaskVoid BuildAsync(ModePreviewDefinitionSO definition, CancellationToken ct)
        {
            // One frame, so opening the modal is never gated on a generation pass.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            var environment = ResolveMiniature(definition.PreviewCell.EnvironmentPrefab,
                                               definition.PreviewCell, definition);
            if (environment.IsValid) Attach(environment);

            // A mode whose gameplay structure is built by its controller rather than by its cell
            // (hoops, goals, a track) previews as an empty arena unless its structure prop is
            // modelled too. Only generator-driven props can be: anything else is authored
            // geometry and is not ours to sample.
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

            // Release the generator's point data immediately: a 34k-lay list retained so the menu
            // can show a thumbnail is the wrong trade, and re-generating is a small fraction of
            // the cost of an actual build.
            if (prefab is CellEnvironmentSpawnableBase env) env.ReleaseGeneratedData();

            if (built.IsValid) _cache[config] = built;
            else CSDebug.LogWarning($"[ModePreview] {prefab.name} generated no points - " +
                                    $"{definition.Mode} shows no diorama.");
            return built;
        }

        void Attach(CellMiniatureBuilder.Miniature miniature)
        {
            _context ??= new ToyContext { GameData = gameData };

            var go = ToyFactory.AddMiniatureBody(_modelHost, miniature, _context, "DioramaModel");
            if (!go) return;

            SetLayerRecursive(go.transform, _layer);

            // Continuity of existence: the model grows in rather than appearing.
            ToyFactory.ScaleInFromZero(go.transform, 0.5f).Forget();
        }

        void ClearModel()
        {
            if (!_modelHost) return;

            for (int i = _modelHost.childCount - 1; i >= 0; i--)
                Destroy(_modelHost.GetChild(i).gameObject);

            _modelHost.localRotation = Quaternion.identity;
        }

        // ── Stage ────────────────────────────────────────────────────────────

        bool EnsureStage()
        {
            if (_stage) return true;

            _layer = LayerMask.NameToLayer(previewLayerName);
            if (_layer < 0)
            {
                CSDebug.LogError($"[ModePreview] Layer '{previewLayerName}' does not exist. Add it in " +
                                 "Project Settings > Tags and Layers - without a private layer the " +
                                 "preview camera would render the whole menu world a second time, " +
                                 "which is the one thing this feature must not do.");
                return false;
            }

            var root = new GameObject("ModePreviewStage");
            root.transform.position = Vector3.up * stageDistance;
            _stage = root.transform;
            root.layer = _layer;

            var host = new GameObject("Model");
            host.transform.SetParent(_stage, false);
            host.layer = _layer;
            _modelHost = host.transform;

            _renderTexture = new RenderTexture(
                Mathf.Max(64, resolution.x), Mathf.Max(64, resolution.y), 16)
            {
                name = "ModePreviewRT",
                antiAliasing = 1,
                useMipMap = false,
            };

            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(_stage, false);
            camGo.layer = _layer;

            float dist = stageRadius * Mathf.Max(0.5f, cameraDistance);
            var offset = Quaternion.Euler(cameraPitch, 0f, 0f) * Vector3.back * dist;
            camGo.transform.localPosition = offset;
            camGo.transform.localRotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);

            _camera = camGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = background;
            // The whole perf story in one line: this camera sees the stage and nothing else.
            _camera.cullingMask = 1 << _layer;
            _camera.fieldOfView = fieldOfView;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = dist + stageRadius * 4f;
            _camera.targetTexture = _renderTexture;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            // Never let this camera join the gameplay stack or be picked up as Camera.main.
            _camera.depth = -100;
            _camera.enabled = false;
            camGo.tag = "Untagged";

            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(_stage, false);
            lightGo.layer = _layer;
            lightGo.transform.localRotation = Quaternion.Euler(45f, -30f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.intensity = 1.1f;
            // Lights ignore layers unless told to: without this the stage light would fall on the
            // whole menu world.
            _light.cullingMask = 1 << _layer;
            _light.shadows = LightShadows.None;
            _light.enabled = false;

            return true;
        }

        void ReleaseStage()
        {
            if (_camera) _camera.targetTexture = null;
            if (surface) surface.texture = null;

            if (_renderTexture)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (_stage) Destroy(_stage.gameObject);
            _stage = null;
            _modelHost = null;
            _camera = null;
            _light = null;
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
