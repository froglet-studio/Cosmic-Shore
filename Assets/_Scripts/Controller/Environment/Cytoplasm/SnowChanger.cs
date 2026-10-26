using System.Collections.Generic;
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

        [Header("Limbs Into Shards")]
        [Tooltip("Most shards the cell's dying spindles may add to the field. A withering or dying " +
                 "limb condenses into a new shard where it stood (Spindle.StampEvaporate -> " +
                 "AddMorphedShard), so the field fills with the ecology's history. Scaled by the " +
                 "cytoplasm density the device tier / quality preset allows, like the field itself. " +
                 "Past it a limb evaporates exactly as it always did. A performance backstop, not a " +
                 "look knob: each shard is one more instance to draw and reorient.")]
        [SerializeField] int maxMorphedShards = 800;

        [Tooltip("How many dying limbs it takes to leave one shard: 1 = every limb, 5 = every fifth " +
                 "(Garrett, 2026-10-21: a fifth as many as one per limb). The other limbs evaporate as " +
                 "they always did. Counted per cell, so a plant's ordered wither still leaves its " +
                 "shards in order.")]
        [SerializeField, Min(1)] int limbsPerMorphedShard = 5;

        // A List, not an array: dying limbs append to it (AddMorphedShard).
        List<GameObject> shards;

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
        // Live shards in the arrays above. The arrays are CAPACITY and grow by doubling as limbs
        // become shards, so _count, never Length, is the shard count.
        int _count;
        Mesh _mesh;
        Material _instancedMaterial; // a runtime copy with GPU instancing on; the asset is untouched
        RenderParams _renderParams;
        static readonly ProfilerMarker s_submit = new("SnowChanger.RenderMeshInstanced");
        readonly float nodeScaler = 10;
        readonly float nodeSize = .25f;
        Vector3 origin = Vector3.zero;
        Vector3 targetAxis;

        // Reorientation cursor: index of the next shard to process, or >= ShardCount
        // when idle. OnCellItemsUpdated only rewinds the cursor; Update does the work.
        int _reorientCursor = int.MaxValue;

        // A mode-side tint (CellVisualTint): ONE material clone for this cell's shards, made once, so recolouring the
        // whole cytoplasm is one SetColor a frame however many shards there are (a property block per shard would be
        // thousands of writes a frame). On the instanced path the clone IS _instancedMaterial (already this cell's own
        // copy). The snow prefab's own material is never written.
        static readonly int ColorId = Shader.PropertyToID("_Color");
        Material _tintMaterial;
        Color _baseColour = Color.white;

        // ── Shards condensing out of dying limbs ──────────────────────────────────────────
        // A shard a limb became grows from nothing to its law scale while the limb's own fade
        // runs, turning from the limb's heading to the crystal (continuity of existence: the
        // limb never pops out, the shard never pops in). Only these few shards are touched per
        // frame; a settled one is an ordinary shard and leaves the list.
        struct Condensing
        {
            public int Index;
            public float StartTime;
            public float Duration;
            public Quaternion From;
        }

        readonly List<Condensing> _condensing = new();
        int _morphedShards;
        int _dyingLimbs;

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
            if (!cellData.TryGetLocalCrystal(out Crystal crystal))
                return;

            _condensing.Clear();
            _morphedShards = 0;
            _dyingLimbs = 0;

            if (cellData.Cell != null)
            {
                nucleusRadius = cellData.Cell.NucleusRadius;
                membraneRadius = cellData.Cell.MembraneRadius;
            }

            float innerR = nucleusRadius;
            float outerR = membraneRadius;
            // a cell config may space its motes wider than the prefab does (a big arena: the count is a CUBE of the radius)
            var config = cellData.Cell != null ? cellData.Cell.Config : null;
            float spacing = config != null && config.CytoplasmShardDistance > 0 ? config.CytoplasmShardDistance : shardDistance;
            float cellVolume = spacing * spacing * spacing;

            // Uniform density throughout the full sphere (0 → membrane)
            float sphereVolume = (4f / 3f) * Mathf.PI * (outerR * outerR * outerR);
            // A device tier or quality preset may THIN the field (GraphicsCuts.CytoplasmDensity:
            // MobileLow, the Low preset): fewer transparent shards to draw and reorient. Never to
            // zero - the shards point at the crystal, so they are how a pilot finds it.
            // Docs/PLATFORM_UNIFICATION.md §3.6, §3.10.
            int shardCount = Mathf.Max(1, Mathf.RoundToInt(sphereVolume / cellVolume * GraphicsCuts.CytoplasmDensity));
            float outerR3 = outerR * outerR * outerR;

            _instanced = TryBeginInstanced(shardCount, outerR);
            if (!_instanced) shards = new List<GameObject>(shardCount);
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
                    _count++;
                    continue;
                }

                GameObject tempSnow = Instantiate(snow, transform, true);
                tempSnow.transform.localScale = Vector3.one * nodeScaler;
                tempSnow.transform.position = origin + new Vector3(
                    r * sinTheta * Mathf.Cos(phi),
                    r * sinTheta * Mathf.Sin(phi),
                    r * cosTheta);

                shards.Add(tempSnow);
                if (_tintMaterial) AssignTint(tempSnow);
            }

            ChangeSnowOrientation();
        }

        /// <summary>The cytoplasm's own colour (SnowGraph's <c>_Color</c> on the shard prefab's material).</summary>
        public bool TryGetMaterialColour(out Color colour)
        {
            colour = Color.white;
            var r = snow ? snow.GetComponentInChildren<Renderer>() : null;
            var m = r ? r.sharedMaterial : null;
            if (!m || !m.HasProperty(ColorId)) return false;
            colour = m.GetColor(ColorId);
            return true;
        }

        /// <summary>
        /// Make this cell's shards tintable: clone the shard material ONCE and point every shard at the clone. A loop over
        /// every shard, so call it before the moment that needs it (a mode's ready screen), not inside a transition.
        /// </summary>
        public void PrepareColourOverride()
        {
            if (_tintMaterial) return;
            if (_instanced)
            {
                // the instanced path already draws from this cell's own runtime copy (_instancedMaterial): tint that
                // one - no second clone, nothing to assign
                _tintMaterial = _instancedMaterial;
                if (_tintMaterial && _tintMaterial.HasProperty(ColorId)) _baseColour = _tintMaterial.GetColor(ColorId);
                return;
            }
            var r = snow ? snow.GetComponentInChildren<Renderer>() : null;
            var baseMat = r ? r.sharedMaterial : null;
            if (!baseMat) return;
            _tintMaterial = new Material(baseMat) { name = baseMat.name + " (cell tint)" };
            if (baseMat.HasProperty(ColorId)) _baseColour = baseMat.GetColor(ColorId);
            if (shards == null) return;
            for (int i = 0; i < shards.Count; i++) if (shards[i]) AssignTint(shards[i]);
        }

        /// <summary>Recolour every shard of this cell (the clone's colour; prepared on first use).</summary>
        public void SetColourOverride(Color colour)
        {
            PrepareColourOverride();
            if (_tintMaterial) _tintMaterial.SetColor(ColorId, colour);
        }

        /// <summary>Back to the material's own colour (the clone stays assigned; it now matches the original).</summary>
        public void ClearColourOverride()
        {
            if (_tintMaterial) _tintMaterial.SetColor(ColorId, _baseColour);
        }

        void AssignTint(GameObject shard)
        {
            var renderers = shard.GetComponentsInChildren<Renderer>();
            for (int k = 0; k < renderers.Length; k++) renderers[k].sharedMaterial = _tintMaterial;
        }

        /// <summary>Requests a reorientation pass. The work is time-sliced in Update
        /// (shardsPerFrame per frame) so SOAP raisers — e.g. the crystal-pickup chain,
        /// which raises OnCellItemsUpdated from a physics callback — never pay the
        /// O(shardCount) loop inline.</summary>
        public void ChangeSnowOrientation()
        {
            _reorientCursor = 0;
        }

        int ShardCount => _instanced ? _count : shards?.Count ?? 0;

        /// <summary>Most shards dying limbs may still add: <see cref="maxMorphedShards"/> thinned by the
        /// same cytoplasm density as the field (Docs/PLATFORM_UNIFICATION.md §3.10).</summary>
        int MorphedShardCap => Mathf.FloorToInt(maxMorphedShards * GraphicsCuts.CytoplasmDensity + 0.5f);

        /// <summary>
        /// A dying spindle becomes a shard of this cytoplasm (Docs/ECOSYSTEM.md, "A dying limb
        /// becomes a cytoplasm shard"). The shard joins the shard list at <paramref name="position"/>,
        /// starts at zero scale facing <paramref name="limbRotation"/>, and condenses to an ordinary
        /// shard (its law scale, facing the crystal) over <paramref name="duration"/> seconds from
        /// <paramref name="startTime"/> (a <see cref="PrismClock"/> time: the limb's own fade start,
        /// so an ordered wither stays ordered). Only every <see cref="limbsPerMorphedShard"/>-th dying
        /// limb leaves one. Returns false, adding nothing, for the limbs in between, once the field holds
        /// <see cref="maxMorphedShards"/> limb shards, or before <see cref="Initialize"/> built it.
        /// </summary>
        public bool AddMorphedShard(Vector3 position, Quaternion limbRotation, float startTime, float duration)
        {
            if (_morphedShards >= MorphedShardCap) return false;
            if (!_instanced && shards == null) return false;
            if (_dyingLimbs++ % Mathf.Max(1, limbsPerMorphedShard) != 0) return false;

            int index;
            if (_instanced)
            {
                index = _count;
                if (index == _matrices.Length) GrowInstancedCapacity();
                _shardPos[index] = position;
                _shardRot[index] = limbRotation;
                _shardScale[index] = Vector3.zero;
                WriteMatrix(index);
                _count++;
            }
            else
            {
                GameObject shard = Instantiate(snow, transform, true);
                shard.transform.SetPositionAndRotation(position, limbRotation);
                shard.transform.localScale = Vector3.zero;
                index = shards.Count;
                shards.Add(shard);
                if (_tintMaterial) AssignTint(shard);   // a tinted cell's new shard wears the tint too
            }

            _morphedShards++;
            _condensing.Add(new Condensing { Index = index, StartTime = startTime, Duration = Mathf.Max(0.01f, duration), From = limbRotation });
            return true;
        }

        void GrowInstancedCapacity()
        {
            int capacity = Mathf.Max(16, _matrices.Length * 2);
            System.Array.Resize(ref _shardPos, capacity);
            System.Array.Resize(ref _shardRot, capacity);
            System.Array.Resize(ref _shardScale, capacity);
            System.Array.Resize(ref _matrices, capacity);
        }

        void Update()
        {
            if (ShardCount > 0 && _reorientCursor < ShardCount) ReorientSlice();
            // After the slice: a condensing shard the slice just set to its full law scale is
            // brought back to where its condense has reached, in the same frame.
            if (_condensing.Count > 0) CondenseTick();
            if (_instanced)
                using (s_submit.Auto())
                    // In calls of 1023, the per-call instance limit NcaCreatureFauna's fallback draw keeps to.
                    for (int start = 0; start < _count; start += 1023)
                        Graphics.RenderMeshInstanced(_renderParams, _mesh, 0, _matrices, Mathf.Min(1023, _count - start), start);
        }

        void CondenseTick()
        {
            cellData.TryGetLocalCrystal(out Crystal crystal);
            float now = PrismClock.Now;
            for (int c = _condensing.Count - 1; c >= 0; c--)
            {
                var condensing = _condensing[c];
                float t = Mathf.Clamp01((now - condensing.StartTime) / condensing.Duration);
                float k = t * t * (3f - 2f * t);

                Vector3 position;
                Transform shard = null;
                if (_instanced) position = _shardPos[condensing.Index];
                else
                {
                    var go = shards[condensing.Index];
                    if (!go) { _condensing.RemoveAt(c); continue; }
                    shard = go.transform;
                    position = shard.position;
                }

                ShardLaw(position, crystal, condensing.From, out Quaternion lawRotation, out Vector3 lawScale);
                Quaternion rotation = Quaternion.Slerp(condensing.From, lawRotation, k);
                Vector3 scale = lawScale * k;
                if (_instanced)
                {
                    _shardRot[condensing.Index] = rotation;
                    _shardScale[condensing.Index] = scale;
                    WriteMatrix(condensing.Index);
                }
                else
                {
                    shard.rotation = rotation;
                    shard.localScale = scale;
                }

                if (t >= 1f) _condensing.RemoveAt(c);
            }
        }

        void OnDestroy()
        {
            if (_instancedMaterial) Destroy(_instancedMaterial);
            if (_tintMaterial && _tintMaterial != _instancedMaterial) Destroy(_tintMaterial);   // the GameObject path's clone
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
            _count = 0;
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
            for (int i = start; i < end; i++)
            {
                ShardLaw(_shardPos[i], crystal, _shardRot[i], out _shardRot[i], out _shardScale[i]);
                WriteMatrix(i);
            }
        }

        /// <summary>Where a shard at <paramref name="p"/> points and how big it is: LookAt the crystal (else
        /// face the axis), stretched by its distance. <paramref name="rotation"/> keeps
        /// <paramref name="current"/> where no heading can be derived.</summary>
        void ShardLaw(Vector3 p, Crystal crystal, Quaternion current, out Quaternion rotation, out Vector3 scale)
        {
            rotation = current;
            float normalizedDistance;
            if (crystal)
            {
                Vector3 to = crystal.transform.position - p;
                normalizedDistance = Mathf.Clamp(to.magnitude, 0, membraneRadius) / membraneRadius;
                if (SafeLookRotation.TryGet(to, out var rot, this, logError: false)) rotation = rot;
            }
            else
            {
                Vector3 axisN = targetAxis.normalized;
                var reject = p - (Vector3.Dot(p, axisN) * axisN);
                normalizedDistance = Mathf.Clamp(reject.magnitude, 0, membraneRadius) / membraneRadius;
                Vector3 dir = lookAt ? -reject.normalized : targetAxis;
                if (SafeLookRotation.TryGet(dir, out var rot, this, logError: false)) rotation = rot;
            }
            float nodeScalerOverThree = nodeScaler / 3;
            scale = Vector3.forward * (normalizedDistance * nodeScaler + nodeSize) +
                    Vector3.one * (normalizedDistance * nodeScalerOverThree + nodeSize);
        }

        public void SetOrigin(Vector3 o) => origin = o;
    }
}
