using FMOD.Studio;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay.Audio
{
    /// <summary>
    /// Helpers for playing FMOD events as one-shots with a caller-supplied
    /// volume applied. Use these in place of
    /// <see cref="RuntimeManager.PlayOneShot(EventReference, Vector3)"/> /
    /// <see cref="RuntimeManager.PlayOneShotAttached(EventReference, GameObject)"/>
    /// whenever the one-shot must respect <c>GameSetting.SFXLevel</c> and
    /// <c>GameSetting.SFXEnabled</c>.
    ///
    /// Why this exists: FMOD's PlayOneShot family has no overload that
    /// accepts per-instance volume — the call internally creates the
    /// instance, starts it, and releases it before user code can call
    /// <c>setVolume()</c>. The result is one-shots that ignore the in-game
    /// SFX slider unless the FMOD project routes the event through a bus
    /// or VCA whose volume is driven from the slider. Cosmic Shore's FMOD
    /// project doesn't have an SFX bus / VCA wired up, so all SFX volume
    /// is applied per-instance via <c>setVolume()</c>. These helpers
    /// reproduce the create / start / release sequence with a
    /// <c>setVolume()</c> in between so the slider is honoured.
    ///
    /// Volume semantics:
    ///   - <c>volume &lt;= 0f</c> short-circuits without creating any FMOD
    ///     instance, so a muted SFX slider produces zero allocations and
    ///     zero FMOD voices.
    ///   - <c>volume &gt; 0f</c> creates an instance, applies the volume,
    ///     starts it, and immediately calls <c>release()</c> so FMOD frees
    ///     the instance once playback finishes (same lifetime contract as
    ///     the built-in PlayOneShot family).
    /// </summary>
    public static class FMODOneShotVolumeHelper
    {

        // ── The fire-and-forget contract, and the one way it leaks ──────────────
        //
        // start() + release() is only safe for an event that STOPS BY ITSELF: FMOD frees a
        // released instance when it stops, so an event with a loop region (or a sustain point)
        // is never freed and never stops - it accumulates one immortal, forever-playing
        // instance PER CALL, for the whole session.
        //
        // That cost is invisible where it is caused: every live instance is re-processed by
        // studioSystem.update() every frame, so it lands as SELF time inside
        // RuntimeManager.Update() with zero managed children and zero GC alloc, growing until
        // the game is unplayable. It shipped exactly once, on
        // 'event:/SFX/Oneshots/Gameplay sfx/Boost Activate' - a LOOPING event under a folder
        // called Oneshots, fired by five boost call sites on every vessel and every AI.
        //
        // FMOD can answer this itself (EventDescription.isOneshot), so the check is dynamic:
        // remove the loop region in FMOD Studio and this starts playing again with no code
        // change. Cached per event - one native query per event, not per call.
        static readonly Dictionary<(int, int, int, int), bool> _fireAndForgetSafe = new();
        static readonly HashSet<(int, int, int, int)> _warned = new();

        static (int, int, int, int) KeyOf(EventReference reference)
            => (reference.Guid.Data1, reference.Guid.Data2, reference.Guid.Data3, reference.Guid.Data4);

        /// <summary>
        /// True when <paramref name="instance"/>'s event stops on its own and may therefore be
        /// released fire-and-forget. False for a looping / sustaining event, which would leak.
        /// Undeterminable (banks still loading) counts as SAFE, so a query failure can never
        /// silently mute audio - only a definite "this loops" refuses.
        /// </summary>
        static bool IsFireAndForgetSafe(EventReference reference, EventInstance instance)
        {
            var key = KeyOf(reference);
            if (_fireAndForgetSafe.TryGetValue(key, out bool cached)) return cached;

            bool safe = true;
            if (instance.getDescription(out FMOD.Studio.EventDescription desc) == FMOD.RESULT.OK
                && desc.isValid())
            {
                if (desc.isOneshot(out bool oneshot) == FMOD.RESULT.OK) safe = oneshot;
            }

            _fireAndForgetSafe[key] = safe;
            return safe;
        }

        /// <summary>
        /// Refuses the call and reports it ONCE per event. Fail-loud by policy: a mis-authored
        /// looping event is an authoring bug that must be visible, and playing it anyway is what
        /// makes the game unplayable. Guarding for silence, never substituting another event.
        /// </summary>
        static void RejectLoopingOneShot(EventReference reference, EventInstance instance)
        {
            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.release();

            if (!_warned.Add(KeyOf(reference))) return;
            Debug.LogError(
                $"[FMODOneShotVolumeHelper] '{reference}' is a LOOPING (or sustaining) FMOD event " +
                "but is being played as a fire-and-forget one-shot. A looping instance never " +
                "stops, so release() never frees it and one immortal voice leaks PER CALL - " +
                "which shows up as RuntimeManager.Update() eating the frame. NOT PLAYED. " +
                "Fix by removing the loop region in FMOD Studio, or give the ability its own " +
                "EventReference and own the instance (start on begin, stop on end).");
        }

        /// <summary>
        /// Plays <paramref name="reference"/> as a one-shot at
        /// <paramref name="worldPosition"/> with <paramref name="volume"/>
        /// applied (linear, 0..2 typical). Equivalent to
        /// <see cref="RuntimeManager.PlayOneShot(EventReference, Vector3)"/>
        /// plus per-instance <c>setVolume()</c>.
        /// </summary>
        public static void PlaySFXOneShot(EventReference reference, Vector3 worldPosition, float volume)
        {
            if (reference.IsNull) return;
            if (volume <= 0f) return;

            // FmodSafe: no throw on a missing event / dead system, reported once per event, and
            // nothing is created while the application is quitting.
            if (!FmodSafe.TryCreateInstance(reference, out EventInstance instance)) return;

            if (!IsFireAndForgetSafe(reference, instance)) { RejectLoopingOneShot(reference, instance); return; }

            instance.setVolume(volume);
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(worldPosition));
            instance.start();
            instance.release();
        }

        /// <summary>
        /// Plays <paramref name="reference"/> attached to
        /// <paramref name="attachTo"/> with <paramref name="volume"/>
        /// applied (linear, 0..2 typical). Equivalent to
        /// <see cref="RuntimeManager.PlayOneShotAttached(EventReference, GameObject)"/>
        /// plus per-instance <c>setVolume()</c>. The instance follows
        /// <paramref name="attachTo"/> for the duration of playback.
        /// </summary>
        public static void PlaySFXOneShotAttached(EventReference reference, GameObject attachTo, float volume)
        {
            if (reference.IsNull) return;
            if (attachTo == null) return;
            if (volume <= 0f) return;

            if (!FmodSafe.TryCreateInstance(reference, out EventInstance instance)) return;

            // Doubly important on the attached path: a leaked instance ALSO sits in
            // RuntimeManager's attachedInstances list, which it walks every frame.
            if (!IsFireAndForgetSafe(reference, instance)) { RejectLoopingOneShot(reference, instance); return; }

            instance.setVolume(volume);
            RuntimeManager.AttachInstanceToGameObject(instance, attachTo);
            instance.start();
            instance.release();
        }
    }
}
