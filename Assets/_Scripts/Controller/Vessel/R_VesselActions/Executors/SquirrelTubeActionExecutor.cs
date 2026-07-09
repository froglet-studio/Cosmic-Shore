using System.Collections;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runtime executor for <see cref="SquirrelTubeActionSO"/> — the Squirrel's "Oak Trunk" tube.
    /// See <c>SQUIRREL_TUBE.md</c> for the full design (orientation, pooling, cooldown HUD).
    ///
    /// Begin (trigger press): raises a translucent preview built from the EXACT ghost geometry the
    /// tube will form (a ring field of prism-sized boxes). It projects from the VESSEL MODEL's
    /// orientation (<see cref="ResolveModelTransform"/>) — the puppeteered ship transform, NOT the
    /// camera-followed root — so as the player turns and drifts the preview banks and swings with
    /// the ship model.
    /// Commit (trigger release): freezes that model pose, destroys the preview, and lays a wall of
    /// thick danger prisms along the axis. The prisms are POOLED — pulled from the shared prism pool
    /// via <see cref="PrismEventChannelWithReturnSO"/> (the same path the trail and AOE danger
    /// blocks use) and returned to the pool on teardown — never Instantiate/Destroy. Each blooms in,
    /// registers with the spatial index, and is removed only by an active force. A long cooldown
    /// gates re-use (surfaced to the HUD via <see cref="CooldownRemaining01"/>).
    /// </summary>
    public sealed class SquirrelTubeActionExecutor : ShipActionExecutorBase
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        // Unity's built-in unit cube, shared across every executor (never destroyed — a built-in).
        static Mesh _unitCubeMesh;

        [Header("Scene Refs")]
        [Tooltip("Shared pooled-prism spawn channel (EventOnSpawnPrismAndReturn) — same asset the " +
                 "vessel trail uses. The tube's prisms are pooled, never Instantiated.")]
        [SerializeField] private PrismEventChannelWithReturnSO prismSpawnChannel;

        [Tooltip("Optional explicit transform the tube + preview project from — the ship's visual " +
                 "model transform that banks with flight/drift. If unset, resolves to " +
                 "ShipGeometries[0] → OrientationHandle → vessel root. See SQUIRREL_TUBE.md.")]
        [SerializeField] private Transform orientationSource;

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

        // Pooled prisms laid by this executor, tracked so they can be returned to the pool on
        // teardown (turn end / vessel despawn). ReturnToPool self-unsubscribes, so it is safe to
        // call on an already-returned prism.
        readonly List<Prism> _tubePrisms = new();

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

            var pose = ResolveTubePose(so, status);
            SpawnTube(so, status, pose);

            _activeCooldown = so.Cooldown;
            _cooldownEndTime = Time.time + so.Cooldown;
        }

        // ---------------- Orientation ----------------

        /// <summary>
        /// The tube's world pose: origin ahead of the vessel centre, axis aligned to the VESSEL
        /// MODEL's orientation (which banks/leans with flight and drift), not the camera.
        /// </summary>
        Pose ResolveTubePose(SquirrelTubeActionSO so, IVesselStatus status)
        {
            var model = ResolveModelTransform(status);
            Quaternion rot = model.rotation;
            Vector3 origin = status.Vessel.Transform.position + rot * Vector3.forward * so.ForwardOffset;
            return new Pose(origin, rot);
        }

        /// <summary>
        /// The visual ship model transform, whose world rotation carries the flight/drift
        /// puppeteering. Prefers an explicitly-wired <see cref="orientationSource"/>, then the
        /// customization-collected ship geometry, then the orientation handle, then the
        /// (camera-followed) root as a last resort.
        /// </summary>
        Transform ResolveModelTransform(IVesselStatus status)
        {
            if (orientationSource) return orientationSource;

            var geos = status.ShipGeometries;
            if (geos != null)
                for (int i = 0; i < geos.Count; i++)
                    if (geos[i]) return geos[i].transform;

            if (status.OrientationHandle) return status.OrientationHandle.transform;
            return status.Vessel.Transform;
        }

        // ---------------- Tube spawn (pooled) ----------------

        void SpawnTube(SquirrelTubeActionSO so, IVesselStatus status, Pose pose)
        {
            if (!prismSpawnChannel)
            {
                CSDebug.LogWarning("[SquirrelTube] prismSpawnChannel not wired — cannot spawn tube.");
                return;
            }

            var points = BuildRingPoints(so);

            _spawnCts?.Cancel();
            _spawnCts?.Dispose();
            _spawnCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            SpawnTubeAsync(so, status, pose, points, _spawnCts.Token).Forget();
        }

        async UniTaskVoid SpawnTubeAsync(SquirrelTubeActionSO so, IVesselStatus status, Pose pose,
            IReadOnlyList<SpawnPoint> points, CancellationToken ct)
        {
            int perFrame = so.SpawnPerFrame;
            string playerName = status.PlayerName;
            Domains domain = status.Domain;

            for (int i = 0; i < points.Count; i++)
            {
                if (ct.IsCancellationRequested) return;

                var p = points[i];
                Vector3 worldPos = pose.position + pose.rotation * p.Position;
                Quaternion worldRot = pose.rotation * p.Rotation;
                SpawnOnePooledPrism(so, playerName, domain, worldPos, worldRot, p.Scale);

                if ((i + 1) % perFrame == 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        /// <summary>
        /// Pulls one prism from the shared pool and configures it like the trail does (team, danger,
        /// grow-in). IsDangerous is set BEFORE Initialize so Initialize's MakeDangerous repaints it
        /// to the team's dangerous material.
        /// </summary>
        void SpawnOnePooledPrism(SquirrelTubeActionSO so, string playerName, Domains domain,
            Vector3 worldPos, Quaternion worldRot, Vector3 scale)
        {
            var ret = prismSpawnChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = domain,
                Rotation = worldRot,
                SpawnPosition = worldPos,
                Scale = scale,
                PrismType = so.PrismType
            });

            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism))
                return;

            prism.ownerID = playerName;
            prism.TargetScale = scale;
            prism.ChangeTeam(domain);
            if (so.Danger && prism.prismProperties != null)
                prism.prismProperties.IsDangerous = true; // Initialize → MakeDangerous repaints it

            prism.Initialize(playerName);
            _tubePrisms.Add(prism);
        }

        /// <summary>
        /// Ring positions around the local +z axis (container-local). Every ring is centred on the
        /// axis so a vessel down the middle passes through the hollow centre. Shared by the pooled
        /// spawn and the ghost preview so the two are geometrically identical.
        /// </summary>
        List<SpawnPoint> BuildRingPoints(SquirrelTubeActionSO so)
        {
            int rings = so.Rings;
            int segments = so.Segments;
            float radius = so.Radius;
            float spacing = so.RingSpacing;
            var scale = Vector3.one * so.PrismScale;

            var points = new List<SpawnPoint>(rings * segments);

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
                    points.Add(new SpawnPoint(position, rotation, scale));
                }
            }

            return points;
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
                // Project from the vessel MODEL each frame so the preview banks and swings with the
                // ship's flight/drift puppeteering, not the camera. The ghost mesh spans 0..Length
                // in local +z from this pose, matching the final tube exactly.
                var pose = ResolveTubePose(so, status);
                _preview.transform.SetPositionAndRotation(pose.position, pose.rotation);

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
        /// mesh (one draw call) from the same <see cref="BuildRingPoints"/> geometry as the real
        /// tube. Cached and rebuilt only when the SO geometry changes.
        /// </summary>
        Mesh GetGhostMesh(SquirrelTubeActionSO so)
        {
            int sig = System.HashCode.Combine(so.Rings, so.Segments, so.Radius, so.RingSpacing, so.PrismScale);
            if (_ghostMesh && sig == _ghostSignature)
                return _ghostMesh;

            if (_ghostMesh) Destroy(_ghostMesh);

            var points = BuildRingPoints(so);
            var cube = GetUnitCubeMesh();
            var instances = new CombineInstance[points.Count];
            for (int i = 0; i < points.Count; i++)
                instances[i] = new CombineInstance
                {
                    mesh = cube,
                    transform = Matrix4x4.TRS(points[i].Position, points[i].Rotation, points[i].Scale)
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

            // Return pooled prisms rather than destroying them. ReturnToPool self-unsubscribes, so
            // an already-returned (e.g. skimmed-and-exploded) prism is a safe no-op; only live tube
            // prisms are recycled here. Clear the danger state FIRST so a recycled prism can't carry
            // its danger flag into a plain trail block the shared pool later hands out.
            for (int i = 0; i < _tubePrisms.Count; i++)
            {
                var p = _tubePrisms[i];
                if (!p || p.destroyed) continue;
                PrismKinds.Clear(p);
                p.ReturnToPool();
            }
            _tubePrisms.Clear();
        }
    }
}
