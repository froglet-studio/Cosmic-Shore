using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore.Game.Projectiles
{
    public class AOEFlowerCreation : AOEBlockCreation
    {
        [SerializeField] private int TunnelAmount = 3;

        private enum Branch
        {
            first = -1,
            both = 0,
            second = 1,
        }

        public override void BeginExplosion()
        {
            // [Optimization] Standardize token usage. Cancel old ones, create new one.
            CancelExplosion();
            explosionCts = new CancellationTokenSource();
            
            // Start the flower pattern task
            FlowerAsync(explosionCts.Token).Forget();
        }

        private async UniTaskVoid FlowerAsync(CancellationToken ct)
        {
            try
            {
                if (ExplosionDelay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(ExplosionDelay), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);

                float count = 0f;
                // [Safety] Ensure Vessel/PrismController exists before accessing
                if (Vessel == null || Vessel.VesselStatus == null || Vessel.VesselStatus.VesselPrismController == null) 
                    return;

                int currentPosition = Vessel.VesselStatus.VesselPrismController.TrailLength - 1;

                while (count < TunnelAmount)
                {
                    // [Performance] Checks token from AOEExplosion base. Throws immediately if TurnEnd happened.
                    ct.ThrowIfCancellationRequested();

                    if (Vessel == null) return; // Safety check if vessel died mid-loop

                    if (currentPosition < Vessel.VesselStatus.VesselPrismController.TrailLength)
                    {
                        count++;
                        currentPosition++;

                        SetBlockDimensions(Vessel.VesselStatus.VesselPrismController.TargetScale);

                        var lastTwoBlocks = Vessel.VesselStatus.VesselPrismController.GetLastTwoBlocks();
                        if (lastTwoBlocks != null)
                            SeedBlocks(lastTwoBlocks);

                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }
                    else
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }
                }
            }
            catch (OperationCanceledException) { /* Expected */ }
        }

        // ... [Helper methods below use inherited logic and add to 'trails', so Cleanup works automatically] ...

        public void SetBlockDimensions(Vector3 innerDimensions)
        {
            blockScale = innerDimensions;
        }

        public void SeedBlocks(List<Prism> lastTwoBlocks)
        {
            trails.Add(new Trail());

            var origin = (lastTwoBlocks[0].transform.position + lastTwoBlocks[1].transform.position) * 0.5f;
            var maxGap = Mathf.Abs(lastTwoBlocks[0].transform.localScale.x - (blockScale.x * 0.5f));

            const int stepAngle = 30;
            for (int i = stepAngle; i < 180; i += stepAngle)
            {
                var block1 = CreateBlock(
                    lastTwoBlocks[0].transform.position,
                    lastTwoBlocks[0].transform.forward,
                    lastTwoBlocks[0].transform.up,
                    OwnerIdSuffix(1, i),
                    trails[^1]
                );

                var block2 = CreateBlock(
                    lastTwoBlocks[1].transform.position,
                    lastTwoBlocks[1].transform.forward,
                    -lastTwoBlocks[1].transform.up,
                    OwnerIdSuffix(2, i),
                    trails[^1]
                );

                if (block1)
                {
                    block1.transform.RotateAround(origin, block1.transform.forward, i);
                    CreateBranches(block1, maxGap, stepAngle * 0.5f);
                }

                if (block2)
                {
                    block2.transform.RotateAround(origin, block2.transform.forward, i);
                    CreateBranches(block2, maxGap, stepAngle * 0.5f);
                }
            }

            CreateBranches(lastTwoBlocks[0], maxGap, stepAngle * 0.5f);
            lastTwoBlocks[1].transform.Rotate(0f, 0f, 180f);
            CreateBranches(lastTwoBlocks[1], maxGap, stepAngle * 0.5f);
        }

        private string OwnerIdSuffix(int seedIndex, int angle) => $"::F::{seedIndex}::{angle}";

        private void CreateBranches(Prism sourcePrism, float gap, float angle, int handedness = 1, int depth = 0, Branch branch = Branch.both)
        {
            depth--;

            if (branch == Branch.both)
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    var block = CreateBlock(
                        sourcePrism.transform.position,
                        sourcePrism.transform.forward,
                        sourcePrism.transform.up,
                        OwnerIdSuffix(i, Mathf.RoundToInt(angle)),
                        trails[^1]
                    );

                    if (!block) continue;

                    var origin = block.transform.position + (block.transform.right * (blockScale.x / 2f + gap)) * handedness;
                    block.transform.RotateAround(origin, block.transform.forward, 180f);

                    origin = block.transform.position - (block.transform.right * (blockScale.x / 2f + gap)) * handedness;
                    block.transform.RotateAround(origin, block.transform.forward, i * angle);
                    block.transform.RotateAround(origin, (block.transform.position - origin).normalized, 90f * i);

                    if (depth > 0)
                    {
                        if (depth == 1) CreateBranches(block, gap, angle, -handedness, depth, (Branch)i);
                        else            CreateBranches(block, gap, angle, -handedness, depth);
                    }
                }
            }
            else
            {
                float signedAngle = (branch == Branch.first) ? -angle : angle;

                var block = CreateBlock(
                    sourcePrism.transform.position,
                    sourcePrism.transform.forward,
                    sourcePrism.transform.up,
                    OwnerIdSuffix((int)signedAngle, Mathf.RoundToInt(angle)),
                    trails[^1]
                );

                if (!block) return;

                var origin = block.transform.position + (block.transform.right * (blockScale.x / 2f + gap)) * handedness;
                block.transform.RotateAround(origin, block.transform.forward, 180f + signedAngle * 4f);
            }
        }
    }
}