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
    /// Begin (trigger press): raises a translucent hollow-cylinder preview that tracks the vessel,
    /// telegraphing where the tube will form (the Dolphin ghost-crystal shape, scaled to a tube).
    /// Commit (trigger release): freezes the vessel's pose, destroys the preview, and lays a long
    /// wall of thick danger prisms along that forward axis through the canonical
    /// <see cref="PrismTrailBuilder"/> path (batched a few per frame). The wall is real conserved
    /// mass — it blooms in, registers with the spatial index, and is only removed by an active
    /// force. A long cooldown gates re-use; while the ability is cooling down Begin is a no-op so
    /// no preview appears.
    /// </summary>
    public sealed class SquirrelTubeActionExecutor : ShipActionExecutorBase
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        // One shared unit tube mesh (radius 1, length 1, centred on origin along +z), scaled per use.
        static Mesh _unitTubeMesh;

        [Header("Events")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        float _cooldownEndTime;

        GameObject _preview;
        Material _previewMaterialInstance;
        Coroutine _previewFollow;

        CancellationTokenSource _spawnCts;
        readonly List<GameObject> _tubes = new();

        void OnEnable()
        {
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
            Cleanup();
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
            // The tube mouth forms ahead of the vessel, axis aligned to the release-frame forward,
            // so flying straight carries the vessel through the hollow centre.
            Vector3 origin = vessel.position + vessel.forward * so.ForwardOffset;
            Quaternion rotation = vessel.rotation;

            SpawnTube(so, status, origin, rotation);

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
        /// passes through the hollow centre of each one.
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
            mf.sharedMesh = GetUnitTubeMesh();

            _previewMaterialInstance = BuildPreviewMaterial(so);
            mr.sharedMaterial = _previewMaterialInstance;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _previewFollow = StartCoroutine(PreviewFollowRoutine(so, status));
        }

        IEnumerator PreviewFollowRoutine(SquirrelTubeActionSO so, IVesselStatus status)
        {
            float length = Mathf.Max(so.Length, so.PrismScale);
            float half = length * 0.5f;
            float fade = so.PreviewFadeInSeconds;
            float elapsed = 0f;

            Color baseColor = ReadColor(_previewMaterialInstance, so.PreviewColor);
            float targetAlpha = baseColor.a;

            while (_preview && status?.Vessel?.Transform != null)
            {
                var vessel = status.Vessel.Transform;
                _preview.transform.SetPositionAndRotation(
                    vessel.position + vessel.forward * (so.ForwardOffset + half),
                    vessel.rotation);
                _preview.transform.localScale = new Vector3(so.Radius, so.Radius, length);

                if (fade > 0f && elapsed < fade)
                {
                    elapsed += Time.deltaTime;
                    float a = Mathf.Lerp(0f, targetAlpha, Mathf.Clamp01(elapsed / fade));
                    SetColorAlpha(_previewMaterialInstance, baseColor, a);
                }

                yield return null;
            }
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

        // ---------------- Mesh ----------------

        /// <summary>
        /// A hollow, double-sided open cylinder: radius 1, spanning z ∈ [-0.5, 0.5]. Double-sided
        /// so the preview reads correctly from inside (flying through it) and outside.
        /// </summary>
        static Mesh GetUnitTubeMesh()
        {
            if (_unitTubeMesh) return _unitTubeMesh;

            const int seg = 24;
            var verts = new Vector3[(seg + 1) * 2];
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                float a = t * 2f * Mathf.PI;
                float x = Mathf.Cos(a);
                float y = Mathf.Sin(a);
                verts[i] = new Vector3(x, y, -0.5f);
                verts[i + seg + 1] = new Vector3(x, y, 0.5f);
                uvs[i] = new Vector2(t, 0f);
                uvs[i + seg + 1] = new Vector2(t, 1f);
            }

            // Two triangles per quad, emitted with both windings for double-sided rendering.
            var tris = new int[seg * 12];
            int ti = 0;
            for (int i = 0; i < seg; i++)
            {
                int a = i;
                int b = i + 1;
                int c = i + seg + 1;
                int d = i + seg + 2;

                // front
                tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                // back
                tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                tris[ti++] = b; tris[ti++] = d; tris[ti++] = c;
            }

            _unitTubeMesh = new Mesh { name = "SquirrelTubePreviewUnitMesh" };
            _unitTubeMesh.vertices = verts;
            _unitTubeMesh.uv = uvs;
            _unitTubeMesh.triangles = tris;
            _unitTubeMesh.RecalculateNormals();
            _unitTubeMesh.RecalculateBounds();
            return _unitTubeMesh;
        }
    }
}
