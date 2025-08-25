using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Game;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Single-class workflow:
    /// • On press: ship turns transparent for <cooldown>.
    /// • During cooldown: NEW trail blocks are frozen (no growth) and optionally transparent.
    /// • A seed wall is planted on the latest trail block with a "staging" look.
    /// • When cooldown ends: ship returns to normal, blocks resume growth/appearance, seed swaps to final look and starts bonding.
    /// 
    /// Zero modifications to TrailBlock or TrailSpawner required.
    /// Uses reflection to reach BlockScaleAnimator.GrowthRate.
    /// </summary>
    public class CloakSeedWallAction : ShipAction
    {
        #region Config
        [Header("Cooldown / Flow")]
        [Tooltip("How long the ship stays transparent and trail blocks are suppressed.")]
        [SerializeField] private float cooldownSeconds = 2.5f;

        [Tooltip("If true, block growth is frozen during cooldown by setting GrowthRate = 0 via reflection.")]
        [SerializeField] private bool freezeGrowthDuringCooldown = true;

        [Tooltip("If true, newly spawned blocks are made transparent during cooldown.")]
        [SerializeField] private bool makeBlocksTransparentDuringCooldown = true;

        [Header("Ship Visuals")]
        [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;

        [Header("Seed Wall")]
        [Tooltip("Provide a GameObject that has the desired Assembler component (e.g., WallAssembler). Only the TYPE is used.")]
        [SerializeField] private Assembler assemblerTypeSource;   // we add a component of this exact type to the seed block
        [SerializeField] private int assemblerDepth = 50;

        [Header("Seed Wall Looks")]
        [Tooltip("Material while cooldown is active (staging).")]
        [SerializeField] private Material seedStagingMaterial;
        [Tooltip("Material after cooldown ends (final).")]
        [SerializeField] private Material seedFinalMaterial;

        [Header("Safety")]
        [Tooltip("If true, do nothing if no trail block exists yet when pressed.")]
        [SerializeField] private bool requireExistingTrailBlock = true;
        #endregion

        #region State
        private TrailSpawner _spawner;
        private CancellationTokenSource _cts;
        private bool _running;

        // Counting new blocks without events
        private int _startCountTrail;
        private int _startCountTrail2;

        // Access to internal Trail2
        private static FieldInfo _trail2Field;

        // Reflection to reach BlockScaleAnimator.GrowthRate
        private static Type _blockScaleAnimatorType;
        private static PropertyInfo _growthRateProp;

        // Per-block original GrowthRate cache
        private readonly Dictionary<TrailBlock, float> _savedGrowth = new();

        // Tracked blocks affected during cooldown (for restore)
        private readonly HashSet<TrailBlock> _affectedBlocks = new();

        // Seed refs
        private Assembler _seedAssembler;
        private Renderer  _seedRenderer;
        private Material  _seedOriginalMaterialInstance; // in case you want to restore original (we finalize instead)
        #endregion

        #region Lifecycle
        public override void Initialize(IShip ship)
        {
            base.Initialize(ship);
            _spawner = Ship?.ShipStatus?.TrailSpawner;

            // Cache reflection handles
            _trail2Field ??= typeof(TrailSpawner).GetField("Trail2", BindingFlags.Instance | BindingFlags.NonPublic);

            if (_blockScaleAnimatorType == null)
            {
                // BlockScaleAnimator is on TrailBlock via [RequireComponent]
                _blockScaleAnimatorType = Type.GetType("CosmicShore.Core.BlockScaleAnimator, Assembly-CSharp");
                if (_blockScaleAnimatorType != null)
                    _growthRateProp = _blockScaleAnimatorType.GetProperty("GrowthRate", BindingFlags.Instance | BindingFlags.Public);
            }
        }

        public override void StartAction()
        {
            if (_running) return;
            _running = true;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _ = RunAsync(_cts.Token);
        }

        public override void StopAction()
        {
            if (!_running) return;
            _cts?.Cancel();
            _running = false;
            // best-effort cleanup
            SetShipTransparent(false);
            RestoreAllBlocks();
            FinalizeSeed(startBonding: false); // normalize appearance at least
        }
        #endregion

        #region Main Flow
        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            // 1) Ship → transparent
            SetShipTransparent(true);

            // 2) Snapshot baseline counts for NEW block detection
            _startCountTrail = _spawner.Trail.TrailList.Count;
            var trail2 = GetTrail2();
            _startCountTrail2 = trail2?.TrailList.Count ?? 0;

            // 3) Plant seed on the latest existing block (if any)
            PlantSeedOnLatestBlock();

            // 4) Watch for NEW blocks during cooldown and suppress them
            var watchTask = WatchBlocksDuringCooldown(ct);

            // 5) Wait cooldown
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0.05f, cooldownSeconds)), cancellationToken: ct);
            }
            catch (OperationCanceledException) { /* noop */ }

            // 6) Restore everything
            SetShipTransparent(false);
            RestoreAllBlocks();
            FinalizeSeed(startBonding: true);

            _running = false;
        }
        #endregion

        #region Ship Transparency
        private void SetShipTransparent(bool transparent)
        {
            if (skinnedMeshRenderer == null) return;
            foreach (var m in skinnedMeshRenderer.materials)
            {
                m.color = new Color(m.color.r, m.color.g, m.color.b, transparent ? 1f : 0f);
            }
                
        }
        #endregion

        #region Seed Wall
        private void PlantSeedOnLatestBlock()
        {
            var latest = GetLatestBlock();
            if (latest == null) return;

            var assemblerType = assemblerTypeSource.GetType();
            _seedAssembler = latest.GetComponent(assemblerType) as Assembler;
            if (_seedAssembler == null)
                _seedAssembler = latest.gameObject.AddComponent(assemblerType) as Assembler;

            _seedAssembler.Depth = assemblerDepth;

            // Staging look
            _seedRenderer = latest.GetComponent<Renderer>();
            if (_seedRenderer == null) _seedRenderer = latest.GetComponentInChildren<Renderer>();
            if (_seedRenderer != null && seedStagingMaterial != null)
            {
                _seedOriginalMaterialInstance = _seedRenderer.material; // instance
                _seedRenderer.material = new Material(seedStagingMaterial);
            }
        }

        private void FinalizeSeed(bool startBonding)
        {
            if (_seedRenderer != null && seedFinalMaterial != null)
            {
                _seedRenderer.material = new Material(seedFinalMaterial);
            }
            // (Optional) else: keep whatever it had

            if (startBonding)
                _seedAssembler?.SeedBonding();
        }
        #endregion

        #region New Block Watching & Suppression
        private async UniTaskVoid WatchBlocksDuringCooldown(CancellationToken ct)
        {
            var trail2 = GetTrail2();

            while (!ct.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

                // trail A (primary)
                var listA = _spawner.Trail.TrailList;
                for (int i = _startCountTrail; i < listA.Count; i++)
                    SuppressBlock(listA[i]);
                _startCountTrail = listA.Count;

                // trail B (gap mode)
                if (trail2 != null)
                {
                    var listB = trail2.TrailList;
                    for (int i = _startCountTrail2; i < listB.Count; i++)
                        SuppressBlock(listB[i]);
                    _startCountTrail2 = listB.Count;
                }
            }
        }

        private void SuppressBlock(TrailBlock block)
        {
            if (block == null) return;

            // Freeze growth via reflection into BlockScaleAnimator.GrowthRate
            if (freezeGrowthDuringCooldown)
            {
                var scale = block.GetComponent(_blockScaleAnimatorType);
                if (scale != null && _growthRateProp != null)
                {
                    try
                    {
                        float current = (float)_growthRateProp.GetValue(scale);
                        if (!_savedGrowth.ContainsKey(block))
                            _savedGrowth[block] = current;

                        // set to zero to stop growth
                        _growthRateProp.SetValue(scale, 0f);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"CloakSeedWallAction: Could not set GrowthRate via reflection. {e.Message}");
                    }
                }
            }

            // Make transparent (optional)
            if (makeBlocksTransparentDuringCooldown)
                block.SetTransparency(true);

            _affectedBlocks.Add(block);
        }

        private void RestoreAllBlocks()
        {
            foreach (var block in _affectedBlocks)
            {
                if (block == null) continue;

                // restore transparency
                if (makeBlocksTransparentDuringCooldown)
                    block.SetTransparency(false);

                // restore growth rate
                if (freezeGrowthDuringCooldown)
                {
                    var scale = block.GetComponent(_blockScaleAnimatorType);
                    if (scale != null && _growthRateProp != null)
                    {
                        try
                        {
                            if (_savedGrowth.TryGetValue(block, out float prev))
                                _growthRateProp.SetValue(scale, Mathf.Max(prev, 0.0001f));
                            else
                                _growthRateProp.SetValue(scale, 0.01f); // fallback gentle growth
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"CloakSeedWallAction: Could not restore GrowthRate. {e.Message}");
                        }
                    }

                    // nudge size recompute
                    block.ChangeSize();
                }
            }

            _affectedBlocks.Clear();
            _savedGrowth.Clear();
        }
        #endregion

        #region Utilities
        private Trail GetTrail2()
        {
            return _trail2Field?.GetValue(_spawner) as Trail;
        }

        private TrailBlock GetLatestBlock()
        {
            // prefer primary trail’s latest
            if (_spawner.Trail.TrailList.Count > 0)
                return _spawner.Trail.TrailList[^1];

            // else consider Trail2
            var trail2 = GetTrail2();
            if (trail2 != null && trail2.TrailList.Count > 0)
                return trail2.TrailList[^1];

            return null;
        }
        #endregion
    }
}