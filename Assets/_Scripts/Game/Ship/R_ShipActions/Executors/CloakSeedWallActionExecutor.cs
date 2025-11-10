using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using CosmicShore.Core;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public sealed class CloakSeedWallActionExecutor : ShipActionExecutorBase
    {
        [Header("Scene Refs")]
        [SerializeField] private SkinnedMeshRenderer shipRenderer;
        [SerializeField] private SeedAssemblerActionExecutor seedAssembler;

        [Header("Events")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        // State
        private IVesselStatus _status;
        private VesselPrismController _controller;

        private bool _isRunning;
        private float _cooldownEndTime;
        private CancellationTokenSource _runCts;

        // SO used during active cloak
        private CloakSeedWallActionSO _activeSo;

        // Ship restore cache
        private Material[] _shipOriginalShared;
        private bool _shipPrevEnabled;
        private GameObject _serpentGhost;

        // Prism tracking so we can fully restore
        
        private static readonly int _ColorId     = Shader.PropertyToID("_Color");
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");
        private sealed class TrackedPrism
        {
            public Prism Prism;
            public Renderer[] Renderers;
            public Material[][] OriginalShared;
            public bool[] WasEnabled;
        }
        private readonly List<TrackedPrism> _spawnedDuringCloak = new();

        private bool IsLocalUser => true; 

        // ---------------- Lifecycle ----------------

        void OnEnable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            End();
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
            if (_controller != null) _controller.OnBlockSpawned -= HandleBlockSpawned;
        }

        void OnTurnEndOfMiniGame() => End();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status     = shipStatus;
            _controller = _status?.VesselPrismController;

            if (_controller != null)
                _controller.OnBlockSpawned += HandleBlockSpawned;

            if (seedAssembler != null)
                seedAssembler.Initialize(_status);
        }

        // ---------------- API ----------------

        public void Toggle(CloakSeedWallActionSO so, IVesselStatus status)
        {
            if (!so || status == null) return;
            if (_isRunning || Time.time < _cooldownEndTime) return;

            _activeSo = so;

            if (!seedAssembler.StartSeed(so.SeedWallSo, status))
                return;

            seedAssembler.BeginBonding();

            BeginCloakVisuals(); // ship + existing prisms (per local/remote rules)

            _runCts?.Cancel();
            _runCts?.Dispose();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            RunAsync(so, _runCts.Token).Forget();
        }

        public void End()
        {
            if (_runCts != null)
            {
                try { _runCts.Cancel(); } catch { }
                _runCts.Dispose();
                _runCts = null;
            }

            // Always restore
            RestoreShipImmediate();
            RestoreAllPrismsImmediate();
            seedAssembler?.StopSeedCompletely();

            _activeSo = null;
            _isRunning = false;
        }

        // ---------------- Run ----------------

        private async UniTaskVoid RunAsync(CloakSeedWallActionSO so, CancellationToken ct)
        {
            _isRunning = true;
            _spawnedDuringCloak.Clear();

            _cooldownEndTime = Time.time + Mathf.Max(0.01f, so.CooldownSeconds);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(so.CooldownSeconds),
                                    DelayType.DeltaTime,
                                    PlayerLoopTiming.Update,
                                    ct);
            }
            catch (OperationCanceledException) { /* normal on End() */ }

            _isRunning = false;

            // Restore visuals
            RestoreShipImmediate();
            RestoreAllPrismsImmediate();
            seedAssembler?.StopSeedCompletely();

            _activeSo = null;

            _runCts?.Dispose();
            _runCts = null;
        }

        // ---------------- Cloak visuals ----------------

        private void BeginCloakVisuals()
        {
            // Ship
            if (shipRenderer)
            {
                _shipOriginalShared = shipRenderer.sharedMaterials;
                _shipPrevEnabled    = shipRenderer.enabled;

                if (IsLocalUser)
                {
                    SpawnSerpentGhost();
                    var ghostMat = _activeSo?.GhostShipMaterial;
                    if (ghostMat) ApplySingleMaterialAcrossRenderer(shipRenderer, ghostMat);
                    shipRenderer.enabled = true;

                }
                else
                {
                    // Remote: ship invisible
                    shipRenderer.enabled = false;
                }
            }
        }

        private void HandleBlockSpawned(Prism block)
        {
            if (!_isRunning || !block) return;

            // Ensure this prism can actually render with alpha (uses your Prism's animator)
            block.SetTransparency(true);

            var rends = block.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return;

            // Apply fade (no r.enabled changes, no swaps)
            foreach (var r in rends)
            {
                if (!r) continue;
                ApplyAlphaToRenderer(r, 0f); // 10% visible
            }

            // Track for precise restore on cooldown end
            _spawnedDuringCloak.Add(new TrackedPrism
            {
                Prism     = block,
                Renderers = rends
            });
        }


        private void CloakExistingPrisms(bool localView)
        {
            void CloakTrail(Trail trail)
            {
                if (trail?.TrailList == null) return;
                foreach (var p in trail.TrailList)
                    if (p) CloakOnePrism(p, localView);
            }

            CloakTrail(_controller?.Trail);

            // If you have a second trail privately stored, cover it too
            var trail2Field = typeof(VesselPrismController)
                .GetField("Trail2", BindingFlags.Instance | BindingFlags.NonPublic);
            if (trail2Field?.GetValue(_controller) is Trail t2)
                CloakTrail(t2);
        }

        private void CloakOnePrism(Prism prism, bool localView)
        {
            var rends = prism.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return;

            var tracked = new TrackedPrism
            {
                Prism          = prism,
                Renderers      = rends,
                OriginalShared = new Material[rends.Length][],
                WasEnabled     = new bool[rends.Length]
            };

            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (!r) continue;

                // Snapshot original materials & enabled state (clone!)
                var original = r.sharedMaterials;
                tracked.OriginalShared[i] = (Material[])(original?.Clone() ?? Array.Empty<Material>());
                tracked.WasEnabled[i]     = r.enabled;

                if (localView)
                {
                    // LOCAL OWNER VIEW: swap to the PrismLocalCloakMaterial
                    var mat = _activeSo?.PrismLocalCloakMaterial ;
                    if (mat) ApplySingleMaterialAcrossRenderer(r, mat);
                    r.enabled = true;
                }
                else
                {
                    // REMOTE VIEW: simply hide it
                    r.enabled = false;
                }
            }

            _spawnedDuringCloak.Add(tracked);
        }


        private void SpawnSerpentGhost()
        {
            if (!shipRenderer) return;

            // Remove previous ghost if any
            if (_serpentGhost) { Destroy(_serpentGhost); _serpentGhost = null; }

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

            _serpentGhost.transform.SetPositionAndRotation(
                shipRenderer.transform.position,
                shipRenderer.transform.rotation
            );
            _serpentGhost.transform.localScale = shipRenderer.transform.lossyScale;
            _serpentGhost.SetActive(true);
        }

        // ---------------- Restore ----------------

        private void RestoreShipImmediate()
        {
            // Destroy ghost first
            if (_serpentGhost) { Destroy(_serpentGhost); _serpentGhost = null; }

            if (!shipRenderer) return;

            shipRenderer.enabled = _shipPrevEnabled;

            if (_shipOriginalShared != null)
                shipRenderer.sharedMaterials = _shipOriginalShared;

            _shipOriginalShared = null;
        }

        private void RestoreAllPrismsImmediate()
        {
            foreach (var t in _spawnedDuringCloak)
            {
                if (t == null) continue;

                // Return prism to its normal (opaque) state
                t.Prism?.SetTransparency(false);

                if (t.Renderers != null)
                {
                    foreach (var r in t.Renderers)
                    {
                        if (!r) continue;
                        ClearAlphaOverrides(r); // removes the 10% alpha override
                    }
                }
            }

            _spawnedDuringCloak.Clear();
        }
        
        private static void ApplyAlphaToRenderer(Renderer r, float alpha)
        {
            if (!r) return;

            var mats  = r.sharedMaterials;
            int count = mats?.Length ?? 0;
            if (count == 0) count = 1; // still set a block on index 0

            for (int i = 0; i < count; i++)
            {
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block, i);

                Color baseColor = Color.white;
                if (mats != null && i < mats.Length && mats[i] != null)
                {
                    var mat = mats[i];

                    if (mat.HasProperty(_BaseColorId))
                    {
                        baseColor = mat.GetColor(_BaseColorId);
                        baseColor.a = alpha;
                        block.SetColor(_BaseColorId, baseColor);
                    }
                    else if (mat.HasProperty(_ColorId))
                    {
                        baseColor = mat.GetColor(_ColorId);
                        baseColor.a = alpha;
                        block.SetColor(_ColorId, baseColor);
                    }
                    else
                    {
                        baseColor.a = alpha;
                        block.SetColor(_ColorId, baseColor);
                    }
                }
                else
                {
                    baseColor.a = alpha;
                    block.SetColor(_ColorId, baseColor);
                }

                r.SetPropertyBlock(block, i);
            }
        }

        private static void ClearAlphaOverrides(Renderer r)
        {
            if (!r) return;

            var mats  = r.sharedMaterials;
            int count = mats?.Length ?? 0;
            if (count == 0) count = 1;

            for (int i = 0; i < count; i++)
            {
                // Passing null clears overrides for that submesh index
                r.SetPropertyBlock(null, i);
            }
        }

        private static void ApplySingleMaterialAcrossRenderer(Renderer r, Material mat)
        {
            if (!r || !mat) return;
            var count = (r.sharedMaterials != null && r.sharedMaterials.Length > 0)
                ? r.sharedMaterials.Length : 1;
            var arr = new Material[count];
            for (int i = 0; i < count; i++) arr[i] = mat;
            r.sharedMaterials = arr;
        }

    }
}



