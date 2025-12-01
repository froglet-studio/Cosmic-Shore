using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace CosmicShore.Utility.ClassExtensions
{
    public static class TransformExtensions
    {
        /// <summary>
        /// Set game object position, rotation and local scale.
        /// </summary>
        public static void SetFullProperties(this Transform transform, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = scale;
        }

        /// <summary>
        /// Converts a local position (relative to a transform) into a global world position.
        /// </summary>
        public static Vector3 ToGlobal(this Transform transform, Vector3 localPosition)
        {
            return localPosition.x * transform.right
                 + localPosition.y * transform.up
                 + localPosition.z * transform.forward
                 + transform.position;
        }

        // ─────────────────────────────────────────────────────────────
        // NEW: ResizeForSeconds EXTENSION (UniTask + CancellationToken)
        // ─────────────────────────────────────────────────────────────

        // Holds per-Transform cancellation tokens
        private static readonly Dictionary<Transform, CancellationTokenSource> resizeTokens =
            new Dictionary<Transform, CancellationTokenSource>();

        public static async UniTask ResizeForSeconds(
            this Transform transform,
            float multiplier,
            float holdDuration,
            CancellationToken externalToken = default)
        {
            if (transform == null)
                return;

            // Cancel any previous scale operation on this transform
            if (resizeTokens.TryGetValue(transform, out var oldCts))
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            // New CTS for this operation
            var cts = new CancellationTokenSource();
            resizeTokens[transform] = cts;

            // Combine with external cancellation source (like Destroy token)
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cts.Token,
                externalToken
            );

            Vector3 original = transform.localScale;
            Vector3 target = original * multiplier;

            float transitionTime = 1f;
            float t = 0f;

            try
            {
                // ────────────────────────────────
                // 1) ORIGINAL → TARGET (1 second)
                // ────────────────────────────────
                t = 0f;
                while (t < transitionTime)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    float lerp = t / transitionTime;
                    transform.localScale = Vector3.Lerp(original, target, lerp);

                    t += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, linkedCts.Token);
                }

                transform.localScale = target;

                // ────────────────────────────────
                // 2) HOLD at TARGET
                // ────────────────────────────────
                float hold = 0f;
                while (hold < holdDuration)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    hold += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, linkedCts.Token);
                }

                // ────────────────────────────────
                // 3) TARGET → ORIGINAL (1 second)
                // ────────────────────────────────
                t = 0f;
                while (t < transitionTime)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    float lerp = t / transitionTime;
                    transform.localScale = Vector3.Lerp(target, original, lerp);

                    t += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, linkedCts.Token);
                }

                transform.localScale = original;
            }
            catch (OperationCanceledException)
            {
                // On cancel, always restore original scale
                transform.localScale = original;
            }
            finally
            {
                // Remove token and dispose
                resizeTokens.Remove(transform);
            }
        }


        /// <summary>
        /// Manually cancel any ongoing ResizeForSeconds on this transform.
        /// </summary>
        public static void CancelResize(this Transform transform)
        {
            if (resizeTokens.TryGetValue(transform, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                resizeTokens.Remove(transform);
            }
        }
    }
}
