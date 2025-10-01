using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Cloak + Seed Wall executor (material-swap based, persistent until cooldown ends).
    /// </summary>
    public class CloakSeedWallActionExecutor : ShipActionExecutorBase
    {
        public event Action OnCloakStarted;
        public event Action OnCloakEnded;

        [Header("Scene Refs")]
        [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
        [SerializeField] private Transform modelRoot;
        [Tooltip("If your seed is a separate action/executor, we call into it via the registry.")]
        [SerializeField] private SeedAssemblerActionExecutor seedAssemblerExecutor;
        [SerializeField] private SeedWallActionSO seedWallConfig;

        // Runtime refs
        private IVesselStatus _status;
        private PrismSpawner _spawner;
        private ActionExecutorRegistry _registry;

        // State
        private CloakSeedWallActionSO _so;
        private Coroutine _runRoutine;
        private bool _cloakActive;
        private float _cooldownEndTime;

        // Ghost
        private GameObject _ghostGo;
        private Coroutine _ghostFollowRoutine;
        private Vector3 _ghostAnchorPos;
        private Transform _followTf;

        // Persistence caches
        private Material[] _originalShipMats; 
        private readonly Dictionary<Renderer, Material[]> _cloakedPrismRenderers = new(128);
        private readonly HashSet<int> _protectedBlockIds = new();

        [SerializeField] private bool enableWatchdog = false;
        [SerializeField] private float watchdogInterval = 0.25f;
        private float _watchdogTimer;

        private readonly Dictionary<int, Material[]> _cloakArrayBySlots = new();

        private readonly Dictionary<Prism, Renderer[]> _prismRenderers = new();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _spawner = shipStatus?.PrismSpawner;
            _registry = GetComponent<ActionExecutorRegistry>();

            if (_spawner != null)
                _spawner.OnBlockSpawned += HandleBlockSpawned;

            if (seedAssemblerExecutor == null && _registry != null)
                seedAssemblerExecutor = _registry.Get<SeedAssemblerActionExecutor>();
        }

        private void OnDestroy()
        {
            if (_spawner != null)
                _spawner.OnBlockSpawned -= HandleBlockSpawned;
        }

        private void Update()
        {
            if (!_cloakActive || !enableWatchdog) return;

            _watchdogTimer += Time.deltaTime;
            if (!(_watchdogTimer >= watchdogInterval)) return;
            _watchdogTimer = 0f;
            ReapplyCloakIfNeeded();
        }

        private Material[] GetCloakArray(int slots, Material cloakMat)
        {
            if (slots <= 0) slots = 1;
            if (_cloakArrayBySlots.TryGetValue(slots, out var mats)) return mats;

            var arr = new Material[slots];
            for (int i = 0; i < slots; i++) arr[i] = cloakMat;
            _cloakArrayBySlots[slots] = arr;
            return arr;
        }

        
        public void Begin(CloakSeedWallActionSO so, IVesselStatus status)
        {
            if (_runRoutine != null) return;

            _so = so;

            if (_so.RequireExistingTrailBlock && !GetLatestBlock())
            {
                Debug.LogWarning("[CloakSeedWall] No trail block found to plant seed on.");
                return;
            }

            _runRoutine = StartCoroutine(Run());
        }

        public void End() { }

        // ===== Core routine =====
        private IEnumerator Run()
        {
            if (seedAssemblerExecutor && seedAssemblerExecutor.StartSeed(seedWallConfig, _status))
            {
                var seed = seedAssemblerExecutor.ActiveSeedBlock;
                if (seed) _protectedBlockIds.Add(seed.GetInstanceID());
                seedAssemblerExecutor.BeginBonding();
            }

            var seedBlock = seedAssemblerExecutor?.ActiveSeedBlock ?? GetLatestBlock();
            if (seedBlock && skinnedMeshRenderer)
                CreateGhostAt(seedBlock.transform.position);

            ApplyShipCloakMaterials();
            CloakExistingPrisms();

            var wait = Mathf.Max(0.01f, _so.CooldownSeconds);
            var lifetime = _so.GhostLifetime > 0f ? _so.GhostLifetime : wait;

            _cloakActive = true;
            _cooldownEndTime = Time.time + wait;
            OnCloakStarted?.Invoke();

            if (lifetime > 0f && _ghostGo)
                StartCoroutine(DestroyAfter(_ghostGo, lifetime));

            while (Time.time < _cooldownEndTime)
                yield return null;

            _cloakActive = false;
            OnCloakEnded?.Invoke();

            CleanupGhost();
            RestoreShipMaterials();
            RestoreAllPrismMaterials();

            if (seedAssemblerExecutor)
                seedAssemblerExecutor.StopSeedCompletely();

            _runRoutine = null;
        }

        // ===== Prism spawn hook =====
        private void HandleBlockSpawned(Prism block)
        {
            if (!_cloakActive || !block) return;
            if (_protectedBlockIds.Contains(block.GetInstanceID())) return;
            if (block.GetComponent<Assembler>()) return;

            var remaining = _cooldownEndTime - Time.time;
            if (remaining > 0f)
            {
                var original = block.waitTime;
                var target = Mathf.Max(original, remaining);
                if (!Mathf.Approximately(original, target)) block.waitTime = target;
            }

            StartCoroutine(ApplyPrismCloakNextFrame(block));
        }

        private IEnumerator ApplyPrismCloakNextFrame(Prism p)
        {
            yield return null; // let all children/renderers appear
            if (_cloakActive) ApplyPrismCloakTo(p);
        }


        // ===== Ghost =====
        private void CreateGhostAt(Vector3 anchorPos)
        {
            if (!skinnedMeshRenderer) return;

            var baked = new Mesh();
            skinnedMeshRenderer.BakeMesh(baked, true);

            _ghostGo = new GameObject("ShipGhost");
            var mf = _ghostGo.AddComponent<MeshFilter>();
            var mr = _ghostGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = baked;

            if (_so.GhostMaterialOverride)
            {
                mr.material = new Material(_so.GhostMaterialOverride);
            }
            else
            {
                var live = skinnedMeshRenderer.materials;
                var ghost = new Material[live.Length];
                for (int i = 0; i < live.Length; i++) ghost[i] = new Material(live[i]);
                mr.materials = ghost;
            }

            _ghostAnchorPos = anchorPos;
            _followTf = _status?.ShipTransform;

            // Make ghost look at the live vessel at spawn time
            var shipTf = _followTf;
            var shipUp = shipTf ? shipTf.up : Vector3.up;
            var toShip = (shipTf ? shipTf.position : (anchorPos + Vector3.forward)) - anchorPos;
            var baseRot = toShip.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(toShip.normalized, shipUp)
                : (shipTf ? shipTf.rotation : Quaternion.identity);

            var finalRot = baseRot * Quaternion.Euler(_so.GhostEulerOffset);
            _ghostGo.transform.SetPositionAndRotation(_ghostAnchorPos, finalRot);

            var baseScale = modelRoot ? modelRoot.lossyScale : Vector3.one;
            var s = Mathf.Max(0.0001f, _so.GhostScaleMultiplier);
            _ghostGo.transform.localScale = baseScale * s;

            if (_ghostFollowRoutine != null) StopCoroutine(_ghostFollowRoutine);
            _ghostFollowRoutine = StartCoroutine(GhostFollow());
        }

        private void CleanupGhost()
        {
            if (_ghostFollowRoutine != null)
            {
                StopCoroutine(_ghostFollowRoutine);
                _ghostFollowRoutine = null;
            }

            if (_ghostGo)
            {
                Destroy(_ghostGo);
                _ghostGo = null;
            }
        }

        private IEnumerator GhostFollow()
        {
            float t = 0f;
            var frozenRot = _ghostGo ? _ghostGo.transform.rotation : Quaternion.identity;

            while (_ghostGo)
            {
                var pos = _ghostAnchorPos;
                if (_so.GhostIdleMotion)
                {
                    t += Time.deltaTime;
                    pos += new Vector3(0, Mathf.Sin(t * _so.GhostBobSpeed) * _so.GhostBobAmplitude, 0);
                }

                _ghostGo.transform.SetPositionAndRotation(pos, frozenRot);
                _ghostGo.transform.Rotate(Vector3.up, _so.GhostYawSpeed * Time.deltaTime, Space.World);

                yield return null;
            }
        }

        private IEnumerator DestroyAfter(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go) Destroy(go);
        }
        
        private void ApplyShipCloakMaterials()
        {
            if (!_so || !_so.ShipCloakMaterial || !skinnedMeshRenderer) return;

            if (_originalShipMats == null || _originalShipMats.Length == 0)
                _originalShipMats = skinnedMeshRenderer.sharedMaterials; // cache shared refs (no instancing)

            int slots = _originalShipMats.Length;
            skinnedMeshRenderer.sharedMaterials = GetCloakArray(slots, _so.ShipCloakMaterial);
        }

        private void RestoreShipMaterials()
        {
            if (!skinnedMeshRenderer || _originalShipMats == null) return;
            skinnedMeshRenderer.sharedMaterials = _originalShipMats;
            _originalShipMats = null;
        }

        private void CloakExistingPrisms()
        {
            if (!_spawner || !_so || !_so.PrismCloakMaterial) return;

            void CloakList(List<Prism> list)
            {
                if (list == null) return;
                for (int i = 0; i < list.Count; i++)
                    ApplyPrismCloakTo(list[i]);
            }

            CloakList(_spawner.Trail?.TrailList);

            // compat private Trail2
            var trail2Field = typeof(PrismSpawner).GetField("Trail2", BindingFlags.Instance | BindingFlags.NonPublic);
            var trail2 = trail2Field?.GetValue(_spawner) as Trail;
            if (trail2 != null) CloakList(trail2.TrailList);
        }

        private void ApplyPrismCloakTo(Prism prism)
        {
            if (!_so || !_so.PrismCloakMaterial || !prism) return;

            if (!_prismRenderers.TryGetValue(prism, out var renderers) || renderers == null || renderers.Length == 0)
            {
                renderers = prism.GetComponentsInChildren<Renderer>(true);
                _prismRenderers[prism] = renderers;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;

                if (!_cloakedPrismRenderers.ContainsKey(r))
                    _cloakedPrismRenderers[r] = r.sharedMaterials;

                int slots = r.sharedMaterials?.Length ?? 1;

                r.sharedMaterials = GetCloakArray(slots, _so.PrismCloakMaterial);
            }
        }

        private void RestoreAllPrismMaterials()
        {
            if (_cloakedPrismRenderers.Count > 0)
            {
                foreach (var kv in _cloakedPrismRenderers)
                {
                    var r = kv.Key;
                    if (!r) continue;
                    var originals = kv.Value;
                    if (originals != null) r.sharedMaterials = originals;
                }
                _cloakedPrismRenderers.Clear();
            }

            _prismRenderers.Clear();
            _protectedBlockIds.Clear();
        }
        
        private void ReapplyCloakIfNeeded()
        {
            if (!_so || !_so.PrismCloakMaterial) return;

            // Ship
            if (skinnedMeshRenderer && _originalShipMats != null)
            {
                var mats = skinnedMeshRenderer.sharedMaterials;
                if (!AllAreCloak(mats, _so.ShipCloakMaterial))
                    skinnedMeshRenderer.sharedMaterials = GetCloakArray(_originalShipMats.Length, _so.ShipCloakMaterial);
            }

            // Prisms
            foreach (var kv in _cloakedPrismRenderers)
            {
                var r = kv.Key;
                if (!r) continue;
                var mats = r.sharedMaterials;
                if (!AllAreCloak(mats, _so.PrismCloakMaterial))
                {
                    int slots = mats?.Length ?? 1;
                    r.sharedMaterials = GetCloakArray(slots, _so.PrismCloakMaterial);
                }
            }
        }

        private static bool AllAreCloak(Material[] mats, Material cloak)
        {
            if (mats == null || mats.Length == 0) return false;
            for (int i = 0; i < mats.Length; i++) if (mats[i] != cloak) return false;
            return true;
        }
        
        // ===== Utils =====
        private Prism GetLatestBlock()
        {
            var listA = _spawner?.Trail?.TrailList;
            if (listA != null && listA.Count > 0) return listA[^1];

            var trail2Field = typeof(Game.PrismSpawner).GetField("Trail2", BindingFlags.Instance | BindingFlags.NonPublic);
            var trail2 = trail2Field?.GetValue(_spawner) as Trail;
            if (trail2 != null && trail2.TrailList.Count > 0) return trail2.TrailList[^1];

            return null;
        }
    }
}
