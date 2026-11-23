using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A vessel ability's HELD gameplay sound - one that lasts as long as the ability does, such as
    /// <see cref="GameplaySFXCategory.BoostActivate"/> (a looping, Speed-driven boost bed). Call
    /// <see cref="Begin"/> when the ability engages and <see cref="End"/> on EVERY way it can end
    /// (natural end, interrupt, turn end, disable). Wraps
    /// <see cref="AudioSystem.StartHeldGameplaySFX"/>, which owns the FMOD instance.
    ///
    /// Who hears it: only the pilot on THIS machine (<see cref="IPlayer.IsLocalPilot"/>), the same
    /// rule as the drift, proximity-boost and engine loops (their <c>onlyAudibleToController</c>).
    /// The boost bed is a 2D event with one instance max, driven by the global Speed parameter:
    /// played for every AI and remote vessel it would steal the local pilot's own boost sound.
    /// </summary>
    public static class HeldVesselSfx
    {
        public static void Begin(AudioSystem audio, GameplaySFXCategory category,
            Object owner, IVesselStatus status)
        {
            if (owner == null || status == null) return;
            var player = status.Player;
            if (player == null || !player.IsLocalPilot) return;

            audio = audio ? audio : AudioSystem.Instance;
            if (!audio) return;

            GameObject attachTo = null;
            var vessel = status.Vessel;
            bool vesselAlive = vessel is Object unityVessel ? unityVessel != null : vessel != null;
            if (vesselAlive)
            {
                var t = status.Transform;
                if (t) attachTo = t.gameObject;
            }
            audio.StartHeldGameplaySFX(category, owner, attachTo);
        }

        /// <summary>Fades out and releases the held sound. Safe to call when nothing is held.</summary>
        public static void End(AudioSystem audio, GameplaySFXCategory category, Object owner)
        {
            audio = audio ? audio : AudioSystem.Instance;
            if (!audio) return;
            audio.StopHeldGameplaySFX(category, owner);
        }
    }
}
