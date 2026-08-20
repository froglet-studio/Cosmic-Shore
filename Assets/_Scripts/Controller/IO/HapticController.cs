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
    /// Cosmic Shore ships TWO everyday feels, both local-human-pilot-only:
    ///   • <see cref="PlaySkim"/>   — the reward: a bright, sharp, proximity-scaled pulse; many in
    ///     sequence read as a rapid, continuously rewarding pulse train (Squirrel skim).
    ///   • <see cref="PlayPunish"/> — the mistake: one heavy, low-frequency thud when the vessel
    ///     body slams a prism.
    ///
    /// …plus two fenced additions, each a deliberate exercise of the "adding a feel" clause:
    ///   • <see cref="PlayAlert"/>  — a long rattle for RARE match-changing events.
    ///   • <see cref="PlaySpray"/>  — a rising buzz while a full-auto trigger is held.
    ///
    /// Everything else is deliberately silent — the legacy <see cref="PlayHaptic"/> /
    /// <see cref="PlayConstant"/> entry points (UI, drift, boost, jousts, explosions, overtake,
    /// elemental debuffs …) are no-ops.
    ///
    /// NiceVibrations keeps only ONE loaded clip — every <c>Load()</c> evicts whatever is playing —
    /// so a tiny priority/rate-limit gate arbitrates them. Priority, top to bottom:
    /// <b>alert &gt; punish &gt; skim &gt; spray</b>. Punish always interrupts the skim train and
    /// the skim train never interrupts a thud; the spray is a texture, so it yields to all three
    /// and interrupts none of them.
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

        // The alert is the loudest event the game has: a long, rattling shake that says
        // "something just changed". It outranks BOTH other feels for its whole duration.
        const float AlertMinIntervalSec = 1.500f;  // can't stack or retrigger into a drone
        const float AlertDurationSec = 1.200f;     // ~1.2 s of shaking

        // The spray buzz is a TEXTURE, not an event: it repeats for as long as a trigger is
        // held, so it sits at the BOTTOM of the priority order and never suppresses anything.
        const float SprayMinIntervalSec = 0.035f;  // backstop floor; the caller sets the real cadence
        const float SprayDurationSec = 0.050f;     // one short buzz per pulse
        const float SkimDurationSec = 0.070f;      // the skim clip's length — read only by spray

        static float s_lastSkimTime = -999f;
        static float s_skimBusyUntil = -999f;      // spray is suppressed until here (skim outranks it)
        static float s_lastPunishTime = -999f;
        static float s_punishBusyUntil = -999f;    // skim is suppressed until here (punish owns the motor)
        static float s_lastAlertTime = -999f;
        static float s_alertBusyUntil = -999f;     // skim AND punish are suppressed until here
        static float s_lastSprayTime = -999f;

        /// <summary>
        /// The reward pulse. <paramref name="strength01"/> (0..1) is how close the prism passed to
        /// the skimmer centre — a dead-centre skim hits hardest. Rate-limited into a pulse train and
        /// never allowed to interrupt an in-progress punish thud.
        /// </summary>
        public static void PlaySkim(float strength01)
        {
            if (!TryBeginPlayback(out var level)) return;

            float now = Time.unscaledTime;
            if (now < s_alertBusyUntil) return;                  // alert outranks everything
            if (now < s_punishBusyUntil) return;                 // punish outranks skim — don't interrupt it
            if (now - s_lastSkimTime < SkimMinIntervalSec) return;
            s_lastSkimTime = now;
            s_skimBusyUntil = now + SkimDurationSec;   // spray must not cut the reward short

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
            if (now < s_alertBusyUntil) return;                  // alert outranks the thud too
            if (now - s_lastPunishTime < PunishMinIntervalSec) return;
            s_lastPunishTime = now;
            s_punishBusyUntil = now + PunishDurationSec;

            EnsureClips();
            LofeltHaptics.Load(s_punishJson, s_punishRumble); // evicts any skim clip mid-train
            LofeltHaptics.outputLevel = level;
            LofeltHaptics.clipLevel = 1f;
            LofeltHaptics.Play();
        }

        /// <summary>
        /// The ALERT shake — the third feel, added deliberately (see Docs/HAPTICS.md ▸ "Adding /
        /// changing a feel", which requires a dedicated method and an extended gate rather than
        /// the silenced legacy API). ~1.2 s of hard, rattling alternation between the heavy and
        /// bright characters: unmistakably not a skim and not a thud, and long enough to read as
        /// "something just happened" rather than "you hit something".
        ///
        /// Reserved for RARE, match-changing state changes — currently only Ribcage's fauna
        /// release rungs. It outranks both other feels for its whole duration and is rate-limited
        /// so it can never stack into a drone. Do NOT hang it on anything frequent; the two-feel
        /// policy exists because haptics stop meaning anything once they are common.
        /// </summary>
        public static void PlayAlert()
        {
            if (!TryBeginPlayback(out var level)) return;

            float now = Time.unscaledTime;
            if (now - s_lastAlertTime < AlertMinIntervalSec) return;
            s_lastAlertTime = now;
            s_alertBusyUntil = now + AlertDurationSec;

            EnsureClips();
            LofeltHaptics.Load(s_alertJson, s_alertRumble);  // evicts whatever was playing
            LofeltHaptics.outputLevel = level;
            LofeltHaptics.clipLevel = 1f;
            LofeltHaptics.Play();
        }

        /// <summary>
        /// The SPRAY buzz — the fourth feel, added deliberately (Docs/HAPTICS.md ▸ "Adding /
        /// changing a feel", which requires a dedicated method and an extended gate rather than
        /// the silenced legacy API). A short mid-frequency buzz with no transient: neither the
        /// bright skim, the dull thud, nor the long rattle.
        ///
        /// It is the game's only CONTINUOUS feel, and that is precisely why it sits at the
        /// BOTTOM of the priority order — alert, punish and skim all suppress it, and it
        /// suppresses nothing. A texture that could cut off an event would make the two feels
        /// the policy is built around less legible, not more; being interruptible costs the
        /// spray nothing because the very next pulse is milliseconds away.
        ///
        /// <paramref name="strength01"/> is how far the gun's accuracy has decayed. The CALLER
        /// owns the cadence (it tightens with the same quantity, so the buzz climbs in rate as
        /// well as strength); the interval floor here is only a backstop against a second caller.
        ///
        /// Fenced to a held full-auto trigger on the LOCAL HUMAN pilot's own vessel. Do not hang
        /// it on anything else: a fourth feel is defensible only while it means exactly one thing.
        /// </summary>
        public static void PlaySpray(float strength01)
        {
            if (!TryBeginPlayback(out var level)) return;

            float now = Time.unscaledTime;
            if (now < s_alertBusyUntil) return;                  // alert outranks everything
            if (now < s_punishBusyUntil) return;                 // a thud must land intact
            if (now < s_skimBusyUntil) return;                   // so must the reward pulse
            if (now - s_lastSprayTime < SprayMinIntervalSec) return;
            s_lastSprayTime = now;
            // Deliberately sets NO busy window: the spray never suppresses another feel.

            EnsureClips();
            LofeltHaptics.Load(s_sprayJson, s_sprayRumble);
            LofeltHaptics.outputLevel = level;
            LofeltHaptics.clipLevel = Mathf.Clamp01(strength01);
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
        static byte[] s_alertJson;
        static byte[] s_sprayJson;
        static GamepadRumble s_skimRumble;
        static GamepadRumble s_punishRumble;
        static GamepadRumble s_alertRumble;
        static GamepadRumble s_sprayRumble;
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

            // Alert — a long RATTLE: full-amplitude sawtooth so the motors slam on and off
            // repeatedly instead of holding, plus a mid frequency so it reads as neither the
            // bright skim nor the dull thud. Both gamepad motors run hot and out of phase, which
            // is what makes a controller genuinely shake rather than hum.
            s_alertJson = ClipJson(
                "{\"time\":0.0,\"amplitude\":1.0,\"emphasis\":{\"amplitude\":1.0,\"frequency\":0.8}}," +
                "{\"time\":0.10,\"amplitude\":0.25}," +
                "{\"time\":0.20,\"amplitude\":1.0,\"emphasis\":{\"amplitude\":1.0,\"frequency\":0.5}}," +
                "{\"time\":0.32,\"amplitude\":0.25}," +
                "{\"time\":0.44,\"amplitude\":1.0,\"emphasis\":{\"amplitude\":1.0,\"frequency\":0.8}}," +
                "{\"time\":0.58,\"amplitude\":0.3}," +
                "{\"time\":0.72,\"amplitude\":1.0,\"emphasis\":{\"amplitude\":1.0,\"frequency\":0.5}}," +
                "{\"time\":0.90,\"amplitude\":0.35}," +
                "{\"time\":1.05,\"amplitude\":0.8}," +
                "{\"time\":1.20,\"amplitude\":0.0}",
                frequency: "0.55", durationSec: "1.20");
            s_alertRumble = Rumble(
                new[] { 90, 70, 90, 70, 90, 70, 90, 70, 100, 90, 80, 60, 60, 30 },
                low:  new[] { 1.0f, 0.2f, 1.0f, 0.25f, 1.0f, 0.3f, 1.0f, 0.3f, 0.9f, 0.35f, 0.8f, 0.3f, 0.6f, 0.0f },
                high: new[] { 0.9f, 0.3f, 1.0f, 0.2f,  0.9f, 0.35f, 1.0f, 0.25f, 0.8f, 0.3f, 0.7f, 0.25f, 0.5f, 0.0f });

            // Spray — a short mid-frequency BUZZ, ~50 ms. Deliberately NO transient (that is the
            // skim's signature) and a mid frequency (punish is 0.0, skim 0.95), so a gun being
            // held down is instantly separable from a reward or a mistake. Both motors run
            // together rather than one-or-the-other, which is what reads as a buzz instead of a
            // tick or a rumble. The clip is authored HOT and scaled down by clipLevel: the quiet
            // end of the ramp is the caller's 0.15 floor, the loud end is this.
            s_sprayJson = ClipJson(
                "{\"time\":0.0,\"amplitude\":0.9}," +
                "{\"time\":0.03,\"amplitude\":1.0}," +
                "{\"time\":0.05,\"amplitude\":0.0}",
                frequency: "0.45", durationSec: "0.05");
            s_sprayRumble = Rumble(
                new[] { 30, 20 },
                low:  new[] { 0.85f, 0.55f },
                high: new[] { 0.70f, 0.45f });
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
