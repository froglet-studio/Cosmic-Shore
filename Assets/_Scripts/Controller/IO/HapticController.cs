using System.Text;
using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;
using Lofelt.NiceVibrations;
using LofeltHaptics = Lofelt.NiceVibrations.HapticController;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Haptic Type
    /// Legacy abstract haptic categories. Retained only so existing call sites keep
    /// compiling — every one of them now routes through the silenced <see cref="PlayHaptic"/>
    /// / <see cref="PlayConstant"/> below (see the two-feel policy).
    /// </summary>
    public enum HapticType
    {
        None = 0,
        ButtonPress = 1,
        PrismCollision = 2,
        ShipCollision = 3,
        CrystalCollision = 4,
        MineCollision = 5,
    }

    /// <summary>
    /// The whole game's haptic policy in one place.
    ///
    /// Cosmic Shore ships exactly TWO feels, both local-human-pilot-only:
    ///   • <see cref="PlaySkim"/>   — the reward: a bright, sharp, proximity-scaled pulse; many in
    ///     sequence read as a rapid, continuously rewarding pulse train (Squirrel skim).
    ///   • <see cref="PlayPunish"/> — the mistake: one heavy, low-frequency thud when the vessel
    ///     body slams a prism.
    ///
    /// Everything else is deliberately silent — the legacy <see cref="PlayHaptic"/> /
    /// <see cref="PlayConstant"/> entry points (UI, drift, boost, jousts, explosions, overtake,
    /// elemental debuffs …) are no-ops.
    ///
    /// NiceVibrations keeps only ONE loaded clip — every <c>Load()</c> evicts whatever is playing —
    /// so a tiny priority/rate-limit gate arbitrates the two feels: punish outranks skim (punish
    /// always interrupts the skim train; the skim train never interrupts a thud).
    /// </summary>
    public class HapticController : MonoBehaviour
    {
        [Inject] GameSetting injectedGameSetting;
        static GameSetting s_injectedGameSetting;

        void Awake() => s_injectedGameSetting = injectedGameSetting;

        // The HapticController component isn't guaranteed to live in every gameplay scene, so
        // fall back to the persistent GameSetting singleton for the enabled/level read.
        static GameSetting Settings =>
            s_injectedGameSetting != null ? s_injectedGameSetting : GameSetting.Instance;

        // ---------------------------------------------------------------- silenced legacy API

        /// <summary>Silenced — see the two-feel policy on <see cref="HapticController"/>.</summary>
        public static void PlayHaptic(HapticType type) { }

        /// <summary>Silenced — see the two-feel policy on <see cref="HapticController"/>.</summary>
        public static void PlayConstant(float amplitude, float frequency, float duration) { }

        // ---------------------------------------------------------------- the two feels

        // Skim pulses may fire as fast as prisms enter the skimmer; rate-limit to a rapid train.
        const float SkimMinIntervalSec = 0.030f;   // ≥30 ms between skim pulses
        // The punish thud is a punctuation mark, not a texture.
        const float PunishMinIntervalSec = 0.250f; // ≥250 ms between thuds
        const float PunishDurationSec = 0.200f;    // ~200 ms clip — skim is suppressed for this long

        static float s_lastSkimTime = -999f;
        static float s_lastPunishTime = -999f;
        static float s_punishBusyUntil = -999f;    // skim is suppressed until here (punish owns the motor)

        /// <summary>
        /// The reward pulse. <paramref name="strength01"/> (0..1) is how close the prism passed to
        /// the skimmer centre — a dead-centre skim hits hardest. Rate-limited into a pulse train and
        /// never allowed to interrupt an in-progress punish thud.
        /// </summary>
        public static void PlaySkim(float strength01)
        {
            if (!TryBeginPlayback(out var level)) return;

            float now = Time.unscaledTime;
            if (now < s_punishBusyUntil) return;                 // punish outranks skim — don't interrupt it
            if (now - s_lastSkimTime < SkimMinIntervalSec) return;
            s_lastSkimTime = now;

            EnsureClips();
            LofeltHaptics.Load(s_skimJson, s_skimRumble);
            LofeltHaptics.outputLevel = level;
            LofeltHaptics.clipLevel = Mathf.Clamp01(strength01); // scales both the iOS clip and gamepad motors
            LofeltHaptics.Play();
        }

        /// <summary>
        /// The mistake thud. Always interrupts the skim train (loads over it) and, once fired,
        /// suppresses skim pulses for the clip's duration so the train can never cut it short.
        /// </summary>
        public static void PlayPunish()
        {
            if (!TryBeginPlayback(out var level)) return;

            float now = Time.unscaledTime;
            if (now - s_lastPunishTime < PunishMinIntervalSec) return;
            s_lastPunishTime = now;
            s_punishBusyUntil = now + PunishDurationSec;

            EnsureClips();
            LofeltHaptics.Load(s_punishJson, s_punishRumble); // evicts any skim clip mid-train
            LofeltHaptics.outputLevel = level;
            LofeltHaptics.clipLevel = 1f;
            LofeltHaptics.Play();
        }

        // Shared gate on the player's setting. Returns the output level (haptics "volume") to use.
        static bool TryBeginPlayback(out float level)
        {
            level = 0f;
            var settings = Settings;
            if (settings == null || !settings.HapticsEnabled || settings.HapticsLevel <= 0f)
                return false;
            level = settings.HapticsLevel;
            return true;
        }

        // ---------------------------------------------------------------- runtime clip generation

        // Both clips are generated once as .haptic JSON (iOS/Android) + a GamepadRumble (gamepads),
        // then reloaded per pulse — exactly how NiceVibrations' own HapticPatterns work. The JSON
        // matches the plugin's nv-*-template.txt schema; decimal points are hard-coded so the strings
        // are locale-independent.
        static byte[] s_skimJson;
        static byte[] s_punishJson;
        static GamepadRumble s_skimRumble;
        static GamepadRumble s_punishRumble;
        static bool s_clipsBuilt;

        static void EnsureClips()
        {
            if (s_clipsBuilt) return;
            s_clipsBuilt = true;

            // Skim — bright & sharp: a transient at t=0 (sharp attack) plus a brief high-frequency
            // body, ~70 ms. High frequency = crisp/bright on iOS; the high-frequency motor carries it
            // on gamepads.
            s_skimJson = ClipJson(
                "{\"time\":0.0,\"amplitude\":0.0,\"emphasis\":{\"amplitude\":1.0,\"frequency\":0.95}}," +
                "{\"time\":0.02,\"amplitude\":0.7}," +
                "{\"time\":0.07,\"amplitude\":0.0}",
                frequency: "0.95", durationSec: "0.07");
            s_skimRumble = Rumble(
                new[] { 70 },
                low: new[] { 0.15f },
                high: new[] { 1.0f });

            // Punish — heavy & dull: full amplitude held then decaying over ~200 ms, zero frequency
            // (the deepest/heaviest character, the opposite of the skim). No transient.
            s_punishJson = ClipJson(
                "{\"time\":0.0,\"amplitude\":1.0}," +
                "{\"time\":0.12,\"amplitude\":0.85}," +
                "{\"time\":0.20,\"amplitude\":0.0}",
                frequency: "0.0", durationSec: "0.20");
            s_punishRumble = Rumble(
                new[] { 120, 80 },
                low: new[] { 1.0f, 0.6f },   // low-frequency motor = the heavy rumble
                high: new[] { 0.15f, 0.1f });
        }

        // Builds a continuous .haptic clip: the caller supplies the amplitude breakpoints; frequency
        // is held constant across the clip.
        static byte[] ClipJson(string amplitudeBreakpoints, string frequency, string durationSec)
        {
            string json =
                "{\"version\":{\"major\":1,\"minor\":0,\"patch\":0}," +
                "\"signals\":{\"continuous\":{\"envelopes\":{" +
                "\"amplitude\":[" + amplitudeBreakpoints + "]," +
                "\"frequency\":[" +
                "{\"time\":0.0,\"frequency\":" + frequency + "}," +
                "{\"time\":" + durationSec + ",\"frequency\":" + frequency + "}]" +
                "}}}}";
            return Encoding.UTF8.GetBytes(json);
        }

        static GamepadRumble Rumble(int[] durationsMs, float[] low, float[] high)
        {
            var rumble = new GamepadRumble
            {
                durationsMs = durationsMs,
                lowFrequencyMotorSpeeds = low,
                highFrequencyMotorSpeeds = high,
                totalDurationMs = 0
            };
            for (int i = 0; i < durationsMs.Length; i++)
                rumble.totalDurationMs += durationsMs[i];
            return rumble;
        }
    }
}
