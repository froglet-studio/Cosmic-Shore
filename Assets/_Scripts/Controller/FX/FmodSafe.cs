using System;
using System.Collections.Generic;
using CosmicShore.Core;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay.Audio
{
    /// <summary>
    /// The guarded seams every code-created FMOD instance goes through. FMOD's Unity integration
    /// THROWS in the two places gameplay code touches it most, and both throws used to escape:
    ///
    /// <list type="bullet">
    ///   <item><see cref="RuntimeManager.CreateInstance(EventReference)"/> throws
    ///   <c>EventNotFoundException</c> for a GUID no loaded bank knows (a renamed/deleted event, a
    ///   bank that has not finished loading, a stale reference) and <c>SystemNotInitializedException</c>
    ///   when FMOD itself failed to start (no audio device). A controller that retried creation every
    ///   frame turned one bad reference into an exception per frame for the life of the vessel.</item>
    ///   <item><see cref="RuntimeManager.DetachInstanceFromGameObject"/> / <c>AttachInstanceToGameObject</c>
    ///   go through <c>RuntimeManager.Instance</c>, which RE-CREATES the manager and re-initialises the
    ///   whole FMOD system if it has already been torn down - which is exactly the state during
    ///   application quit, where <c>RuntimeManager.OnDestroy</c> may run before a vessel's. FMOD's
    ///   own <c>StudioEventEmitter</c> guards this with an <c>isQuitting</c> flag; our controllers did
    ///   not.</item>
    /// </list>
    ///
    /// Policy per CLAUDE.md audio rules: guard for SILENCE, report ONCE with the offender's address,
    /// never substitute another event.
    /// </summary>
    public static class FmodSafe
    {
        static readonly HashSet<(int, int, int, int)> _reportedCreateFailures = new();

        static (int, int, int, int) KeyOf(EventReference reference)
            => (reference.Guid.Data1, reference.Guid.Data2, reference.Guid.Data3, reference.Guid.Data4);

        /// <summary>
        /// True while it is safe to talk to the FMOD runtime: the studio system is up and the
        /// application is not tearing down. Reads <c>RuntimeManager.IsInitialized</c>, which inspects
        /// the existing manager WITHOUT creating one.
        /// </summary>
        public static bool RuntimeAlive =>
            !ApplicationLifecycleManager.IsQuitting && RuntimeManager.IsInitialized;

        /// <summary>
        /// Creates an instance of <paramref name="reference"/>, returning false (and an invalid
        /// handle) instead of throwing. The first failure per event is reported as an error naming
        /// the event and the caller; repeats are silent so a broken reference costs one log line,
        /// not one per frame.
        /// </summary>
        public static bool TryCreateInstance(EventReference reference, out EventInstance instance, UnityEngine.Object context = null)
        {
            instance = default;
            if (reference.IsNull) return false;
            if (ApplicationLifecycleManager.IsQuitting) return false;

            try
            {
                instance = RuntimeManager.CreateInstance(reference);
            }
            catch (Exception ex)
            {
                instance = default;
                if (_reportedCreateFailures.Add(KeyOf(reference)))
                {
                    Debug.LogError(
                        $"[FmodSafe] CreateInstance failed for '{reference}' ({ex.GetType().Name}: {ex.Message}). " +
                        "The event is not in any loaded bank (renamed / deleted in FMOD Studio, or a stale " +
                        "EventReference), or the FMOD system did not initialise. NOT PLAYED; reported once.",
                        context);
                }
                return false;
            }

            return instance.isValid();
        }

        /// <summary>
        /// Attaches <paramref name="instance"/> to <paramref name="target"/> only while the runtime is
        /// alive (an attach during teardown would resurrect the RuntimeManager).
        /// </summary>
        public static void Attach(EventInstance instance, GameObject target)
        {
            if (!instance.isValid() || target == null || !RuntimeAlive) return;
            RuntimeManager.AttachInstanceToGameObject(instance, target);
        }

        /// <summary>
        /// Detaches <paramref name="instance"/> only while the runtime is alive. A detach after the
        /// RuntimeManager is gone is a no-op in effect (the manager's attached list died with it) but
        /// would otherwise re-create the manager mid-quit.
        /// </summary>
        public static void Detach(EventInstance instance)
        {
            if (!instance.isValid() || !RuntimeAlive) return;
            RuntimeManager.DetachInstanceFromGameObject(instance);
        }

        /// <summary>
        /// Full teardown of an owned instance: detach, stop (if it was ever started), release, and
        /// clear the handle so <c>isValid()</c> reads false afterwards. Safe to call on a default
        /// handle and safe during quit - stop/release on an already-released system return an FMOD
        /// error code, they do not throw.
        /// </summary>
        public static void StopAndRelease(ref EventInstance instance, bool started, FMOD.Studio.STOP_MODE stopMode)
        {
            if (instance.isValid())
            {
                Detach(instance);
                if (started) instance.stop(stopMode);
                instance.release();
                instance.clearHandle();
            }
            instance = default;
        }
    }
}
