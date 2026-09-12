using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Helper to safely build or apply look rotations, guarding against zero-length forward vectors.
    /// Leaves rotation unchanged on failure and returns false so callers can skip the action.
    /// </summary>
    public static class SafeLookRotation
    {
        private const float EPSILON = 0.000001f;

        // Callers (boid steering, worm slither) can hit a zero forward every frame while a
        // creature is stationary, so the warning is one-shot per context object.
        static readonly System.Collections.Generic.HashSet<int> _warnedContexts = new();
        static bool _warnedNullContext;

        public static bool TryGet(Vector3 forward, Vector3 up, out Quaternion rotation, Object context = null, bool logError = true)
        {
            if (forward.sqrMagnitude > EPSILON)
            {
                rotation = Quaternion.LookRotation(forward, up);
                return true;
            }

            rotation = Quaternion.identity;

            if (logError && ShouldWarn(context))
                CSDebug.LogWarning($"[SafeLookRotation] ZERO LOOK ROTATION detected on {(context ? context.name : "unknown object")}", context);

            return false;
        }

        static bool ShouldWarn(Object context)
        {
            if (!context)
            {
                if (_warnedNullContext) return false;
                _warnedNullContext = true;
                return true;
            }
            return _warnedContexts.Add(context.GetInstanceID());
        }

        public static bool TryGet(Vector3 forward, out Quaternion rotation, Object context = null, bool logError = true) =>
            TryGet(forward, Vector3.up, out rotation, context, logError);

        public static bool TrySet(Transform target, Vector3 forward, Vector3 up, Object context = null, bool logError = true)
        {
            if (TryGet(forward, up, out var rotation, context, logError))
            {
                target.rotation = rotation;
                return true;
            }

            return false;
        }

        public static bool TrySet(Transform target, Vector3 forward, Object context = null, bool logError = true) =>
            TrySet(target, forward, Vector3.up, context, logError);
    }
}
