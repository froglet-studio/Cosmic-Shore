// Ported verbatim from Assets/_Scripts/Controller/FX/FMODOneShotVolumeHelper.cs
// (AudioSystem unit 2026-07-10). Mechanical substitutions: using FMOD.Studio; /
// using FMODUnity; → using CosmicShore.Engine.Audio.Fmod; (the engine's FMOD
// placeholder surface); UnityEngine → CosmicShore.Engine.
using CosmicShore.Engine.Audio.Fmod;
using CosmicShore.Engine;

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

            EventInstance instance;
            try
            {
                instance = RuntimeManager.CreateInstance(reference);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FMODOneShotVolumeHelper] CreateInstance failed for '{reference}': {ex.Message}");
                return;
            }
            if (!instance.isValid()) return;

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

            EventInstance instance;
            try
            {
                instance = RuntimeManager.CreateInstance(reference);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FMODOneShotVolumeHelper] CreateInstance failed for '{reference}': {ex.Message}");
                return;
            }
            if (!instance.isValid()) return;

            instance.setVolume(volume);
            RuntimeManager.AttachInstanceToGameObject(instance, attachTo.transform);
            instance.start();
            instance.release();
        }
    }
}
