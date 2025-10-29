using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using CosmicShore.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game
{
    public sealed class CloakSeedWallActionExecutor : ShipActionExecutorBase
    {
        [SerializeField] private SkinnedMeshRenderer shipRenderer;
        [SerializeField] private SeedAssemblerActionExecutor seedAssembler; 
 
        private GameObject _serpentGhost;

        private IVesselStatus _status;
        private VesselPrismController _controller;

        private bool _isRunning;
        private float _cooldownEndTime;
        private CancellationTokenSource _runCts;

        private Material[] _shipOriginalShared;
        private bool _shipRendererDisabledForCloak;
        private bool _shipPrevEnabled;
        private bool _restoring;
        
        [Header("Ghost Tuning")]
        [SerializeField] private Vector3 ghostEulerOffset = Vector3.zero;
        [SerializeField] private Vector3 ghostPositionOffset = Vector3.zero; 
        [SerializeField] private float   ghostScaleMultiplier = 1f;      
        [SerializeField] private bool    ghostUseShipOrientation = true;   

        private class TrackedPrism
        {
            public Prism Prism;
            public Renderer[] Renderers;
            public Material[][] OriginalShared; 
            public bool[] WasEnabled;          
        }
        private readonly List<TrackedPrism> _spawnedDuringCloak = new();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _controller = _status?.VesselPrismController;

            if (_controller != null)
                _controller.OnBlockSpawned += HandleBlockSpawned;

            if (seedAssembler != null)
                seedAssembler.Initialize(_status);
        }

        private void LateUpdate()
        {
            if (!_isRunning || _restoring) return;

            if (shipRenderer && shipRenderer.enabled)
                shipRenderer.enabled = false;

            foreach (var r in from tp in _spawnedDuringCloak where tp?.Renderers != null from r in tp.Renderers where r && r.enabled select r)
            {
                r.enabled = false;
            }
        }
        
        public void Toggle(CloakSeedWallActionSO so, IVesselStatus status)
        {
            if (!so || status == null) return;

            if (_isRunning || Time.time < _cooldownEndTime) return;

            var ok = seedAssembler.StartSeed(so.SeedWallSo, status);

            seedAssembler.BeginBonding();
            SpawnSerpentGhostAtSeed();

            _runCts = new CancellationTokenSource();
            RunAsync(so, _runCts.Token).Forget();
        }

        private async UniTaskVoid RunAsync(CloakSeedWallActionSO so, CancellationToken ct)
        {
            _isRunning = true;
            _spawnedDuringCloak.Clear();

            if (shipRenderer)
            {
                _shipOriginalShared = shipRenderer.sharedMaterials;
                _shipPrevEnabled = shipRenderer.enabled;
                shipRenderer.enabled = false;                
                _shipRendererDisabledForCloak = true;
            }
            
            _cooldownEndTime = Time.time + Mathf.Max(0.01f, so.CooldownSeconds);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(so.CooldownSeconds), cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
            }

            // Restore everything
            _isRunning = false;
            _restoring = true;  
            RestoreShipImmediate();
            DestroySerpentGhost();
            //RestoreAllPrismsImmediate();
            ForceUncloakAllExistingPrisms();
            
            seedAssembler?.StopSeedCompletely();
            _restoring = false;  

            _runCts?.Dispose(); _runCts = null;
        }

        // =================== PRISMS: make fully invisible ===================
        private void HandleBlockSpawned(Prism block)
        {
            if (!_isRunning || !block) return;

            var rends = block.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return;

            var tracked = new TrackedPrism
            {
                Prism = block,
                Renderers = rends,
                OriginalShared = new Material[rends.Length][], // optional; kept for safety
                WasEnabled = new bool[rends.Length]
            };

            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (!r) continue;

                tracked.OriginalShared[i] = r.sharedMaterials;
                tracked.WasEnabled[i] = r.enabled;

                r.enabled = false;
            }

            _spawnedDuringCloak.Add(tracked);
        }

        private void RestoreAllPrismsImmediate()
        {
            var snapshot = _spawnedDuringCloak.ToArray();

            foreach (var t in snapshot)
            {
                if (t?.Renderers == null) continue;

                for (int i = 0; i < t.Renderers.Length; i++)
                {
                    var r = t.Renderers[i];
                    if (!r) continue;

                    bool shouldEnable = (t.WasEnabled == null || i >= t.WasEnabled.Length) || t.WasEnabled[i];
                    r.enabled = shouldEnable;

                    if (t.OriginalShared != null && i < t.OriginalShared.Length && t.OriginalShared[i] != null)
                        r.sharedMaterials = t.OriginalShared[i];
                }
            }
            _spawnedDuringCloak.Clear();
        }

        private void RestoreShipImmediate()
        {
            if (!shipRenderer) return;

            if (_shipRendererDisabledForCloak)
            {
                shipRenderer.enabled = _shipPrevEnabled;
                _shipRendererDisabledForCloak = false;
            }

            if (_shipOriginalShared != null)
                shipRenderer.sharedMaterials = _shipOriginalShared;
        }
        
        private void SpawnSerpentGhostAtSeed()
        {
            if (_serpentGhost) return;
            if (!shipRenderer)
            {
                Debug.LogWarning("[CloakSeedWall] shipRenderer missing; cannot bake ghost.");
                return;
            }

            Prism seed = seedAssembler ? seedAssembler.ActiveSeedBlock : null;
            if (!seed) seed = GetLatestBlock();
            if (!seed) return;

            var baked = new Mesh();
            shipRenderer.BakeMesh(baked, true); 

            _serpentGhost = new GameObject("SerpentGhost (Baked)");
            var mf = _serpentGhost.AddComponent<MeshFilter>();
            var mr = _serpentGhost.AddComponent<MeshRenderer>();
            mf.sharedMesh = baked;

            var live = shipRenderer.sharedMaterials;
            if (live is { Length: > 0 })
            {
                var clones = new Material[live.Length];
                for (int i = 0; i < live.Length; i++)
                    clones[i] = live[i] ? new Material(live[i]) : null;
                mr.materials = clones;
            }

            var baseRot = ghostUseShipOrientation
                ? shipRenderer.transform.rotation
                : seed.transform.rotation;

            Vector3 basePos = seed.transform.position;

            _serpentGhost.transform.SetParent(null, true);
            _serpentGhost.transform.SetPositionAndRotation(
                basePos + ghostPositionOffset,
                baseRot * Quaternion.Euler(ghostEulerOffset)
            );
            
            _serpentGhost.transform.localScale = Vector3.one * Mathf.Max(0.0001f, ghostScaleMultiplier);
        }


        private void DestroySerpentGhost()
        {
            if (!_serpentGhost) return;
            Destroy(_serpentGhost);
            _serpentGhost = null;
        }

        private Prism GetLatestBlock()
        {
            // Primary trail
            var listA = _controller?.Trail?.TrailList;
            if (listA != null && listA.Count > 0)
                return listA[^1];

            var trail2Field = typeof(VesselPrismController)
                .GetField("Trail2", BindingFlags.Instance | BindingFlags.NonPublic);

            if (trail2Field?.GetValue(_controller) is Trail { TrailList: { Count: > 0 } } trail2)
                return trail2.TrailList[^1];

            return null;
        }
        
        /// <summary>
        /// Global belt-and-suspenders: ensure ALL current prism renderers are enabled,
        /// even if they weren't in our tracked list (assembler spawns, pooling swaps, etc.).
        /// </summary>
        private void ForceUncloakAllExistingPrisms()
        {
            // Primary trail
            var listA = _controller?.Trail?.TrailList;
            if (listA != null)
                foreach (var t in listA)
                    EnableAllUnder(t);

            var trail2Field = typeof(VesselPrismController)
                .GetField("Trail2", BindingFlags.Instance | BindingFlags.NonPublic);

            if (trail2Field?.GetValue(_controller) is not Trail t2 || t2.TrailList == null) return;
            {
                foreach (var t in t2.TrailList)
                    EnableAllUnder(t);
            }
            return;

            void EnableAllUnder(Prism p)
            {
                if (!p) return;
                var rends = p.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    if (!r) continue;
                    r.enabled = true;
                }
            }
        }
    }
}
