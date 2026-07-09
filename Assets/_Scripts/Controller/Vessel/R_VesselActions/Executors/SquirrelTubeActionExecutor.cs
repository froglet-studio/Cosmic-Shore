using System.Collections;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runtime executor for <see cref="SquirrelTubeActionSO"/> — the Squirrel's "Oak Trunk" tube.
    ///
    /// Begin (trigger press): raises a translucent preview built from the EXACT ghost geometry the
    /// tube will form (a ring field of prism-sized boxes), parented to and following the vessel's
    /// orientation — never the camera — so as the player turns or drifts the preview swings with the
    /// nose and shows the true radius/length/thickness of the final wall.
    /// Commit (trigger release): freezes the vessel's pose, destroys the preview, and lays the wall
    /// of thick danger prisms along that forward axis through the canonical
    /// <see cref="PrismTrailBuilder"/> path (batched a few per frame). The wall is real conserved
    /// mass — it blooms in, registers with the spatial index, and is only removed by an active
    /// force. A long cooldown gates re-use (surfaced to the HUD via <see cref="CooldownRemaining01"/>).
    /// </summary>
    public sealed class SquirrelTubeActionExecutor : ShipActionExecutorBase
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        // Unity's built-in unit cube, shared across every executor (never destroyed — a built-in).
        static Mesh _unitCubeMesh;

        [Header("Events")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        float _cooldownEndTime;
        float _activeCooldown;

        GameObject _preview;
        Material _previewMaterialInstance;
        Coroutine _previewFollow;

        // Ghost preview mesh, cached per SO geometry so repeated presses reuse the built mesh.
        Mesh _ghostMesh;
        int _ghostSignature;

        CancellationTokenSource _spawnCts;
        readonly List<GameObject> _tubes = new();

        /// <summary>
        /// Cooldown remaining as a 0-1 fraction: 1 right after a deploy (full cooldown left),
        /// 0 when ready again. Read by the Squirrel HUD to drive the tube cooldown icon fill.
        /// </summary>
        public float CooldownRemaining01 =>
            _activeCooldown <= 0f ? 0f : Mathf.Clamp01((_cooldownEndTime - Time.time) / _activeCooldown);

        /// <summary>True when the tube can be deployed again (off cooldown).</summary>
        public bool TubeReady => Time.time >= _cooldownEndTime;

        void OnEnable()
        {
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
            Cleanup();
        }

        void OnDestroy()
        {
            if (_ghostMesh) Destroy(_ghostMesh);
            _ghostMesh = null;
        }

        void OnTurnEndOfMiniGame() => Cleanup();

        // Stateless: vessel context is passed into Begin/Commit each call (matches ShipActionSO).

        // ---------------- API ----------------

        public void Begin(SquirrelTubeActionSO so, IVesselStatus status)
        {
            if (!so || status?.Vessel?.Transform == null) return;
            if (Time.time < _cooldownEndTime) return;   // on cooldown → no preview, no-op
            if (_preview) return;                        // already previewing

            BuildPreview(so, status);
        }

        public void Commit(SquirrelTubeActionSO so, IVesselStatus status)
        {
            // A release with no live preview (e.g. pressed while on cooldown) forms nothing.
            if (!_preview || !so || status?.Vessel?.Transform == null)
            {
                DestroyPreview();
                return;
            }

            DestroyPreview();

            var vessel = status.Vessel.Transform;
            // The tube mouth forms ahead of the vessel, axis aligned to the release-frame forward
            // (vessel orientation, not the camera), so flying straight carries the vessel through
            // the hollow centre.
            Vector3 origin = vessel.position + vessel.forward * so.ForwardOffset;
            Quaternion rotation = vessel.rotation;

            SpawnTube(so, status, origin, rotation);

            _activeCooldown = so.Cooldown;
            _cooldownEndTime = Time.time + so.Cooldown;
        }

        // ---------------- Tube spawn ----------------

        void SpawnTube(SquirrelTubeActionSO so, IVesselStatus status, Vector3 origin, Quaternion rotation)
        {
            if (!so.Prism) return;

            var container = new GameObject($"SquirrelTube_{status.PlayerName}");
            container.transform.SetPositionAndRotation(origin, rotation);
            _tubes.Add(container);

            var lays = BuildLays(so, status.Domain);
            var trail = new Trail(false);

            _spawnCts?.Cancel();
            _spawnCts?.Dispose();
            _spawnCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            PrismTrailBuilder.LayBatched(
                so.Prism, lays, container.transform, trail,
                $"{status.PlayerName}::tube", so.SpawnPerFrame, _spawnCts.Token).Forget();
        }

        /// <summary>
        /// Rings of prisms around the local +z axis. Positions are container-local; the container
        /// carries the world pose. Every ring is centred on the axis so a vessel down the middle
        /// passes through the hollow centre of each one. Shared by the real spawn and the ghost
        /// preview so the two are geometrically identical.
        /// </summary>
        List<PrismLay> BuildLays(SquirrelTubeActionSO so, Domains domain)
        {
            int rings = so.Rings;
            int segments = so.Segments;
            float radius = so.Radius;
            float spacing = so.RingSpacing;
            var scale = Vector3.one * so.PrismScale;
            var kind = so.Danger ? PrismKind.Danger : PrismKind.Plain;

            var lays = new List<PrismLay>(rings * segments);

            for (int z = 0; z < rings; z++)
            {
                float depth = z * spacing;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * (2f * Mathf.PI / segments);
                    Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    Vector3 position = radial * radius + Vector3.forward * depth;
                    // Long side runs along the tube axis; block "up" points outward radially.
                    var rotation = Quaternion.LookRotation(Vector3.forward, radial);
                    lays.Add(new PrismLay(new SpawnPoint(position, rotation, scale), domain, kind));
                }
            }

            return lays;
        }

        // ---------------- Preview ----------------

        void BuildPreview(SquirrelTubeActionSO so, IVesselStatus status)
        {
            _preview = new GameObject("SquirrelTubePreview");
            var mf = _preview.AddComponent<MeshFilter>();
            var mr = _preview.AddComponent<MeshRenderer>();
            mf.sharedMesh = GetGhostMesh(so);

            _previewMaterialInstance = BuildPreviewMaterial(so);
            mr.sharedMaterial = _previewMaterialInstance;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _previewFollow = StartCoroutine(PreviewFollowRoutine(so, status));
        }

        IEnumerator PreviewFollowRoutine(SquirrelTubeActionSO so, IVesselStatus status)
        {
            float fade = so.PreviewFadeInSeconds;
            float elapsed = 0f;

            Color baseColor = ReadColor(_previewMaterialInstance, so.PreviewColor);
            float targetAlpha = baseColor.a;

            while (_preview && status?.Vessel?.Transform != null)
            {
                var vessel = status.Vessel.Transform;
                // Container origin = the tube mouth (forwardOffset ahead); the ghost mesh spans
                // 0..Length in local +z, so the preview is the exact final geometry and swings with
                // the vessel's orientation as the player turns or drifts.
                _preview.transform.SetPositionAndRotation(
                    vessel.position + vessel.forward * so.ForwardOffset,
                    vessel.rotation);

                if (fade > 0f && elapsed < fade)
                {
                    elapsed += Time.deltaTime;
                    float a = Mathf.Lerp(0f, targetAlpha, Mathf.Clamp01(elapsed / fade));
                    SetColorAlpha(_previewMaterialInstance, baseColor, a);
                }

                yield return null;
            }
        }

        /// <summary>
        /// The ghost preview mesh: one prism-sized box per ring position, combined into a single
        /// mesh (one draw call). Built from the same <see cref="BuildLays"/> geometry as the real
        /// tube, so the preview is a faithful stand-in for the final wall's radius, length and
        /// thickness. Cached and rebuilt only when the SO geometry changes.
        /// </summary>
        Mesh GetGhostMesh(SquirrelTubeActionSO so)
        {
            int sig = System.HashCode.Combine(so.Rings, so.Segments, so.Radius, so.RingSpacing, so.PrismScale);
            if (_ghostMesh && sig == _ghostSignature)
                return _ghostMesh;

            if (_ghostMesh) Destroy(_ghostMesh);

            var lays = BuildLays(so, Domains.Blue); // domain irrelevant for geometry
            var cube = GetUnitCubeMesh();
            var instances = new CombineInstance[lays.Count];
            for (int i = 0; i < lays.Count; i++)
                instances[i] = new CombineInstance
                {
                    mesh = cube,
                    transform = Matrix4x4.TRS(lays[i].Point.Position, lays[i].Point.Rotation, lays[i].Point.Scale)
                };

            _ghostMesh = new Mesh { name = "SquirrelTubePreviewGhost", indexFormat = IndexFormat.UInt32 };
            _ghostMesh.CombineMeshes(instances, true, true);
            _ghostMesh.RecalculateBounds();
            _ghostSignature = sig;
            return _ghostMesh;
        }

        static Mesh GetUnitCubeMesh()
        {
            if (_unitCubeMesh) return _unitCubeMesh;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _unitCubeMesh = temp.GetComponent<MeshFilter>().sharedMesh; // built-in shared mesh; survives GO destroy
            if (Application.isPlaying) Destroy(temp);
            else DestroyImmediate(temp);
            return _unitCubeMesh;
        }

        Material BuildPreviewMaterial(SquirrelTubeActionSO so)
        {
            if (so.PreviewMaterial)
                return new Material(so.PreviewMaterial);

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);

            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;

            var c = so.PreviewColor;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, c);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, c);
            return mat;
        }

        static Color ReadColor(Material mat, Color fallback)
        {
            if (!mat) return fallback;
            if (mat.HasProperty(BaseColorId)) return mat.GetColor(BaseColorId);
            if (mat.HasProperty(ColorId)) return mat.GetColor(ColorId);
            return fallback;
        }

        static void SetColorAlpha(Material mat, Color baseColor, float alpha)
        {
            if (!mat) return;
            var c = baseColor; c.a = alpha;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, c);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, c);
        }

        void DestroyPreview()
        {
            if (_previewFollow != null)
            {
                StopCoroutine(_previewFollow);
                _previewFollow = null;
            }
            if (_preview) Destroy(_preview);
            _preview = null;
            if (_previewMaterialInstance) Destroy(_previewMaterialInstance);
            _previewMaterialInstance = null;
        }

        // ---------------- Cleanup ----------------

        void Cleanup()
        {
            _spawnCts?.Cancel();
            _spawnCts?.Dispose();
            _spawnCts = null;

            DestroyPreview();

            for (int i = 0; i < _tubes.Count; i++)
                if (_tubes[i]) Destroy(_tubes[i]);
            _tubes.Clear();
        }
    }
}
