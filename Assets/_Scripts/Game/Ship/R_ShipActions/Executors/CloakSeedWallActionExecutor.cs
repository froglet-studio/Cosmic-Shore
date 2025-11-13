using System;
using System.Collections.Generic;
using System.Linq;
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

        // Prism alpha override (same pattern as old code)
        private static readonly int _ColorId     = Shader.PropertyToID("_Color");
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");

        private sealed class TrackedPrism
        {
            public Prism Prism;
            public MaterialPropertyAnimator Animator;
        }

        private readonly List<TrackedPrism> _cloakedPrisms = new();


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

            BeginCloakVisuals();      // ship ghost
            CloakExistingPrisms();    // all current trail blocks

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

            _activeSo  = null;
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
            catch (OperationCanceledException)
            {
                // normal on End()
            }

            _isRunning = false;

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
            if (!shipRenderer) return;
            _shipOriginalShared = shipRenderer.sharedMaterials;
            _shipPrevEnabled    = shipRenderer.enabled;
            SpawnSerpentGhost();

            if (IsLocalUser)
            {
                var ghostMat = _activeSo?.GhostShipMaterial;
                if (ghostMat) ApplySingleMaterialAcrossRenderer(shipRenderer, ghostMat);
                shipRenderer.enabled = true;
            }
            else
            {
                shipRenderer.enabled = false;
            }
        }

        private void HandleBlockSpawned(Prism block)
        {
            if (!_isRunning || !block) return;

            // Optional: keep these blocks alive for entire cooldown
            var remaining = _cooldownEndTime - Time.time;
            if (remaining > 0f)
            {
                var original = block.waitTime;
                var target   = Mathf.Max(original, remaining);
                if (!Mathf.Approximately(original, target))
                    block.waitTime = target;
            }

            CloakOnePrism(block);
        }


        private void SpawnSerpentGhost()
        {
            if (!shipRenderer) return;

            if (_serpentGhost)
            {
                Destroy(_serpentGhost);
                _serpentGhost = null;
            }

            var baked = new Mesh();
            shipRenderer.BakeMesh(baked, true);

            _serpentGhost = new GameObject("SerpentGhost");
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
        
        private void CloakExistingPrisms()
        {
            if (_controller == null) return;

            void CloakTrail(Trail trail)
            {
                if (trail?.TrailList == null) return;
                foreach (var p in trail.TrailList)
                    if (p != null)
                        CloakOnePrism(p);
            }

            CloakTrail(_controller.Trail);

            // Optional second trail (like your old code)
            var trail2Field = typeof(VesselPrismController)
                .GetField("Trail2", BindingFlags.Instance | BindingFlags.NonPublic);
            if (trail2Field?.GetValue(_controller) is Trail t2)
                CloakTrail(t2);
        }


        // ---------------- Restore ----------------

        private void RestoreShipImmediate()
        {
            if (_serpentGhost)
            {
                Destroy(_serpentGhost);
                _serpentGhost = null;
            }

            if (!shipRenderer) return;

            shipRenderer.enabled = _shipPrevEnabled;

            if (_shipOriginalShared != null)
                shipRenderer.sharedMaterials = _shipOriginalShared;

            _shipOriginalShared = null;
        }

        private void RestoreAllPrismsImmediate()
        {
            foreach (var t in _cloakedPrisms)
            {
                if (t?.Prism == null || t.Animator == null) continue;

                // Next ValidateMaterials() should rebuild the team materials from ThemeManager
                t.Animator.MarkMaterialsDirty();

                if (t.Prism.prismProperties != null)
                    t.Prism.prismProperties.IsTransparent = false;

                // This will call ValidateMaterials(), pull team block materials again,
                // and then choose the opaque one
                t.Prism.SetTransparency(false);
            }

            _cloakedPrisms.Clear();
        }


        // ---------------- Alpha override helpers (copied from old working code) ----------------

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
                if (mats != null && i < mats.Length && mats[i])
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

        // ---------------- Ship helper ----------------
        
        private void CloakOnePrism(Prism prism)
        {
            if (prism == null || _activeSo == null) return;

            var anim = prism.GetComponent<MaterialPropertyAnimator>();
            if (anim == null) return;

            var transparent = _activeSo.PrismCloakTransparent;
            var opaque      = _activeSo.PrismCloakOpaque;

            // If SO not set up yet, just fall back to normal transparency toggle
            if (transparent == null || opaque == null)
            {
                prism.SetTransparency(true);
                _cloakedPrisms.Add(new TrackedPrism { Prism = prism, Animator = anim });
                return;
            }

            // Blend from current team material into the cloak pair, then turn transparent
            anim.UpdateMaterial(
                transparent,
                opaque,
                0.15f,                    // fade duration, tweak as you like
                () =>
                {
                    // Mark the prism logically as transparent if you use this flag
                    if (prism.prismProperties != null)
                        prism.prismProperties.IsTransparent = true;

                    // Now actually switch to the transparent cloak material
                    prism.SetTransparency(true);
                });

            _cloakedPrisms.Add(new TrackedPrism
            {
                Prism    = prism,
                Animator = anim
            });
        }


        private static void ApplySingleMaterialAcrossRenderer(Renderer r, Material mat)
        {
            if (!r || !mat) return;
            var count = r.sharedMaterials is { Length: > 0 }
                ? r.sharedMaterials.Length
                : 1;
            var arr = new Material[count];
            for (int i = 0; i < count; i++) arr[i] = mat;
            r.sharedMaterials = arr;
        }
    }
}
