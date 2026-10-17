using CosmicShore.Core;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    public class SnowChanger : MonoBehaviour
    {
        [SerializeField]
        CellRuntimeDataSO cellData;

        [SerializeField] GameObject snow;

        float nucleusRadius;
        float membraneRadius;

        [Header("Shard Density")]
        [Tooltip("Approximate spacing between shards - lower values produce more shards")]
        [SerializeField] int shardDistance = 100;

        [Header("Optional Fields")]
        [SerializeField] bool lookAt;

        [Tooltip("Shards reoriented per frame. Reorientation used to run as one O(shardCount) " +
                 "LookAt loop inline in the OnCellItemsUpdated raise — i.e. inside the crystal-" +
                 "pickup physics callback, a multi-ms self-time spike at ~4k shards. Slicing " +
                 "spreads it invisibly over a few frames (shards drift slowly by design).")]
        [SerializeField] int shardsPerFrame = 128;

        GameObject[] shards;

        // ── Instanced path (2026-10-16, lever L6) ──────────────────────────────────────
        // A shard prefab that is ONE mesh on ONE renderer with no children (shard.prefab) is drawn as one
        // instanced call from these arrays instead of ~4k GameObjects: the same shards, the same positions,
        // the same reorientation and scale law, minus ~4k renderers in the culling population (4,189 enabled
        // SnowMaterial renderers were the largest family in the first shared Unity capture) and ~4k
        // Instantiates at cell boot. Any other prefab keeps the GameObject path, unchanged.
        bool _instanced;
        Vector3[] _shardPos;
        Quaternion[] _shardRot;
        Vector3[] _shardScale;     // the shard's localScale, as the GameObject path would set it
        Matrix4x4[] _matrices;
        Mesh _mesh;
        Material _instancedMaterial; // a runtime copy with GPU instancing on; the asset is untouched
        RenderParams _renderParams;
        static readonly ProfilerMarker s_submit = new("SnowChanger.RenderMeshInstanced");
        readonly float nodeScaler = 10;
        readonly float nodeSize = .25f;
        Vector3 origin = Vector3.zero;
        Vector3 targetAxis;

        // Reorientation cursor: index of the next shard to process, or >= shards.Length
        // when idle. OnCellItemsUpdated only rewinds the cursor; Update does the work.
        int _reorientCursor = int.MaxValue;

        void OnEnable()
        {
            cellData.OnCellItemsUpdated.OnRaised += ChangeSnowOrientation;
        }

        void OnDisable()
        {
            cellData.OnCellItemsUpdated.OnRaised -= ChangeSnowOrientation;
        }

        public void Initialize()
        {
            // A device tier may go without the mote field (PlatformProfileSO.DisableCytoplasm,
            // MobileLow): ~300 transparent objects per cell and the reorient sweep over all of
            // them. Cosmetic only - not mass, not lifeforms. Docs/PLATFORM_UNIFICATION.md §3.6.
            var profile = PlatformProfile.Current;
            if (profile && profile.DisableCytoplasm)
                return;

            if (!cellData.TryGetLocalCrystal(out Crystal crystal))
                return;

            if (cellData.Cell != null)
            {
                nucleusRadius = cellData.Cell.NucleusRadius;
                membraneRadius = cellData.Cell.MembraneRadius;
            }

            float innerR = nucleusRadius;
            float outerR = membraneRadius;
            float cellVolume = shardDistance * shardDistance * shardDistance;

            // Uniform density throughout the full sphere (0 → membrane)
            float sphereVolume = (4f / 3f) * Mathf.PI * (outerR * outerR * outerR);
            int shardCount = Mathf.Max(1, Mathf.RoundToInt(sphereVolume / cellVolume));
            float outerR3 = outerR * outerR * outerR;

            _instanced = TryBeginInstanced(shardCount, outerR);
            if (!_instanced) shards = new GameObject[shardCount];
            for (int i = 0; i < shardCount; i++)
            {
                float r = Mathf.Pow(Random.Range(0f, outerR3), 1f / 3f);
                float cosTheta = Random.Range(-1f, 1f);
                float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);
                float phi = Random.Range(0f, 2f * Mathf.PI);
                if (_instanced)
                {
                    _shardPos[i] = origin + new Vector3(r * sinTheta * Mathf.Cos(phi), r * sinTheta * Mathf.Sin(phi), r * cosTheta);
                    _shardRot[i] = snow.transform.rotation;
                    _shardScale[i] = Vector3.one * nodeScaler;
                    WriteMatrix(i);
                    continue;
                }

                GameObject tempSnow = Instantiate(snow, transform, true);
                tempSnow.transform.localScale = Vector3.one * nodeScaler;
                tempSnow.transform.position = origin + new Vector3(
                    r * sinTheta * Mathf.Cos(phi),
                    r * sinTheta * Mathf.Sin(phi),
                    r * cosTheta);

                shards[i] = tempSnow;
            }

            ChangeSnowOrientation();
        }

        /// <summary>Requests a reorientation pass. The work is time-sliced in Update
        /// (shardsPerFrame per frame) so SOAP raisers — e.g. the crystal-pickup chain,
        /// which raises OnCellItemsUpdated from a physics callback — never pay the
        /// O(shardCount) loop inline.</summary>
        public void ChangeSnowOrientation()
        {
            _reorientCursor = 0;
        }

        int ShardCount => _instanced ? _shardPos.Length : shards?.Length ?? 0;

        void Update()
        {
            if (ShardCount > 0 && _reorientCursor < ShardCount) ReorientSlice();
            if (_instanced)
                using (s_submit.Auto())
                    // In calls of 1023, the per-call instance limit NcaCreatureFauna's fallback draw keeps to.
                    for (int start = 0; start < _matrices.Length; start += 1023)
                        Graphics.RenderMeshInstanced(_renderParams, _mesh, 0, _matrices, Mathf.Min(1023, _matrices.Length - start), start);
        }

        void OnDestroy()
        {
            if (_instancedMaterial) Destroy(_instancedMaterial);
        }

        /// <summary>Sets up the instanced path when the shard prefab is a single mesh with a single material
        /// and no children; false keeps the GameObject path.</summary>
        bool TryBeginInstanced(int count, float outerRadius)
        {
            if (!SystemInfo.supportsInstancing) return false;
            if (!snow || snow.transform.childCount > 0) return false;
            if (!snow.TryGetComponent(out MeshFilter filter) || !filter.sharedMesh) return false;
            if (!snow.TryGetComponent(out MeshRenderer renderer) || renderer.sharedMaterials.Length != 1 || !renderer.sharedMaterial) return false;
            // Only Transform + MeshFilter + MeshRenderer: anything else on the shard is behaviour the
            // GameObject path runs and this one would silently drop.
            if (snow.GetComponents<Component>().Length != 3) return false;

            _mesh = filter.sharedMesh;
            _instancedMaterial = new Material(renderer.sharedMaterial) { name = renderer.sharedMaterial.name + " (instanced)", enableInstancing = true };
            _renderParams = new RenderParams(_instancedMaterial)
            {
                layer = snow.layer,
                worldBounds = new Bounds(origin, Vector3.one * (Mathf.Max(1f, outerRadius) * 2.2f)),
                shadowCastingMode = renderer.shadowCastingMode,
                receiveShadows = renderer.receiveShadows,
                renderingLayerMask = renderer.renderingLayerMask,
            };
            _shardPos = new Vector3[count];
            _shardRot = new Quaternion[count];
            _shardScale = new Vector3[count];
            _matrices = new Matrix4x4[count];
            return true;
        }

        void WriteMatrix(int i)
        {
            // The GameObject path parents each shard under this transform with worldPositionStays, so its
            // world scale is this object's lossy scale times the localScale it is given.
            _matrices[i] = Matrix4x4.TRS(_shardPos[i], _shardRot[i], Vector3.Scale(transform.lossyScale, _shardScale[i]));
        }

        void ReorientSlice()
        {
            if (cellData == null || membraneRadius <= 0f ||
                !cellData.TryGetLocalCrystal(out Crystal crystal))
            {
                _reorientCursor = int.MaxValue;
                return;
            }

            int end = Mathf.Min(_reorientCursor + Mathf.Max(1, shardsPerFrame), ShardCount);
            if (_instanced)
            {
                ReorientInstanced(crystal, _reorientCursor, end);
                _reorientCursor = end;
                return;
            }
            float nodeScalerOverThree = nodeScaler / 3;
            Vector3 axisN = targetAxis.normalized; // hoisted: targetAxis is invariant across the shard loop
            for (int i = _reorientCursor; i < end; i++)
            {
                var shard = shards[i];
                float normalizedDistance;
                if (crystal)
                {
                    float clampedDistance =
                        Mathf.Clamp((shard.transform.position - crystal.transform.position).magnitude, 0,
                            membraneRadius);
                    normalizedDistance = clampedDistance / membraneRadius;
                    shard.transform.LookAt(crystal.transform);
                }
                else
                {
                    var reject = shard.transform.position -
                                 (Vector3.Dot(shard.transform.position, axisN) * axisN);
                    float clampedDistance = Mathf.Clamp(reject.magnitude, 0, membraneRadius);
                    normalizedDistance = clampedDistance / membraneRadius;
                    if (lookAt)
                        SafeLookRotation.TrySet(shard.transform, -reject.normalized, shard);
                    else
                        SafeLookRotation.TrySet(shard.transform, targetAxis, shard);
                }

                shard.transform.localScale = Vector3.forward * (normalizedDistance * nodeScaler + nodeSize) +
                                             Vector3.one * (normalizedDistance * nodeScalerOverThree +
                                                            nodeSize);
            }

            _reorientCursor = end;
        }

        /// <summary>The GameObject path's per-shard law (LookAt the crystal, else face the axis), on arrays.</summary>
        void ReorientInstanced(Crystal crystal, int start, int end)
        {
            float nodeScalerOverThree = nodeScaler / 3;
            Vector3 axisN = targetAxis.normalized;
            for (int i = start; i < end; i++)
            {
                Vector3 p = _shardPos[i];
                float normalizedDistance;
                if (crystal)
                {
                    Vector3 to = crystal.transform.position - p;
                    normalizedDistance = Mathf.Clamp(to.magnitude, 0, membraneRadius) / membraneRadius;
                    if (SafeLookRotation.TryGet(to, out var rot, this, logError: false)) _shardRot[i] = rot;
                }
                else
                {
                    var reject = p - (Vector3.Dot(p, axisN) * axisN);
                    normalizedDistance = Mathf.Clamp(reject.magnitude, 0, membraneRadius) / membraneRadius;
                    Vector3 dir = lookAt ? -reject.normalized : targetAxis;
                    if (SafeLookRotation.TryGet(dir, out var rot, this, logError: false)) _shardRot[i] = rot;
                }
                _shardScale[i] = Vector3.forward * (normalizedDistance * nodeScaler + nodeSize) +
                                 Vector3.one * (normalizedDistance * nodeScalerOverThree + nodeSize);
                WriteMatrix(i);
            }
        }

        public void SetOrigin(Vector3 o) => origin = o;
    }
}
