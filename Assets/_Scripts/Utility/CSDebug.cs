using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

// NOTE: deliberately NO `using System;` here. Every Log overload below takes a
// UnityEngine.Object context by its short name, and importing System makes bare `Object`
// ambiguous with System.Object (CS0104) on all seven of them. [System.Flags] is spelled out
// for the same reason.

namespace CosmicShore.Utility
{
    /// <summary>
    /// Controls which log levels are active at runtime.
    /// </summary>
    public enum CSLogLevel
    {
        /// <summary>All logs enabled (Log, Warning, Error).</summary>
        All = 0,
        /// <summary>Only warnings and errors are logged. Debug.Log calls are suppressed.</summary>
        WarningsAndErrors = 1,
        /// <summary>All logging is disabled.</summary>
        Off = 2
    }

    /// <summary>
    /// Opt-in diagnostic channels for BRING-UP TELEMETRY — the dense per-step traces a system
    /// needs while it is being built, which are noise for everyone else once it works.
    ///
    /// <para>The rule: a trace that answers "why did this system not do the thing" belongs on a
    /// channel (<see cref="CSDebug.LogVerbose"/>), not on <see cref="CSDebug.Log"/>. Channels
    /// default to <see cref="None"/>, so a finished system is silent until someone turns its
    /// channel back on in <c>FrogletTools &gt; Toolbox &gt; Logging</c>. That is what keeps a
    /// past development cycle's instrumentation from being either console spam or deleted
    /// knowledge.</para>
    ///
    /// <para>Only add a member when you are converting real call sites onto it — an unused
    /// channel is a promise the toolbox cannot keep.</para>
    /// </summary>
    /// <summary>
    /// Human label for a <see cref="CSLogChannel"/> member, read by FrogletTools &gt; Toolbox &gt;
    /// Logging so a new channel is toggleable the moment it is declared — the toolbox reflects
    /// over the enum rather than carrying a hand-maintained row table that drifts behind it.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false)]
    public sealed class CSLogChannelLabelAttribute : System.Attribute
    {
        public string Label { get; }
        public CSLogChannelLabelAttribute(string label) => Label = label;
    }

    [System.Flags]
    public enum CSLogChannel
    {
        None = 0,
        /// <summary>
        /// <c>[FLOW-n]</c> — the numbered player/vessel spawn + session bring-up trace across
        /// MultiplayerSetup, the vessel initializers, SceneLoader and the minigame controllers.
        /// </summary>
        [CSLogChannelLabel("[FLOW-n] / [NetTrace] spawn, session and scene flow")]
        NetworkFlow = 1 << 0,
        /// <summary>
        /// <c>[GyroidColony]</c> — octagon-colony lattice telemetry (founder claims, the 5s
        /// population heartbeat). Coherence DEFECTS stay on the warning channel and are
        /// unaffected by this flag.
        /// </summary>
        [CSLogChannelLabel("[GyroidColony] lattice telemetry")]
        GyroidColony = 1 << 1,
        /// <summary>
        /// <c>[QuasicrystalColony]</c> — star-colony lattice telemetry (founder claims, plant
        /// completions, births). Defect warnings (blocked reseed mints) stay on the warning
        /// channel and are unaffected by this flag.
        /// </summary>
        [CSLogChannelLabel("[QuasicrystalColony] lattice telemetry")]
        QuasicrystalColony = 1 << 2,
        /// <c>[ScarabNucleusField]</c> — the Scarab nucleus-seeding ability: seeds planted, balls
        /// knocked in or out, and the overload detonation. Off by default like every channel; a
        /// real fault here is still a warning and is unaffected by this flag.
        /// </summary>
        [CSLogChannelLabel("[ScarabNucleusField] Scarab nucleus seeding")]
        ScarabNucleus = 1 << 3,
        /// <summary>
        /// <c>[ScarabSwitch]</c> / <c>[PlaceSwitch]</c> — the Scarab's switch: placements,
        /// refusals, and the wing dais a strike pays out. Off by default like every channel;
        /// a real fault here is still a warning and is unaffected by this flag.
        /// </summary>
        [CSLogChannelLabel("[ScarabSwitch] switch placements and payouts")]
        ScarabSwitch = 1 << 4,
        /// <summary>
        /// <c>[ScarabJuke]</c> / <c>[ScarabCavitation]</c> — the right-stick dash and the swept
        /// cavitation plate that rides it (fire direction, plate radius, cooldown). Both used to
        /// log unconditionally on every dash, which is per-input console spam for a finished
        /// system. Off by default like every channel; a real fault here is still an error and is
        /// unaffected by this flag.
        /// </summary>
        [CSLogChannelLabel("[ScarabJuke] / [ScarabCavitation] dash and plate")]
        ScarabDash = 1 << 5,
        /// <summary>
        /// <c>[ShieldShatter]</c> — one line per shield disengage: whether the batched
        /// overlay was accepted at all, and the breaking impulse it carries before and
        /// after the shield's own speed clamp. The velocity terms are the identity at
        /// zero (Docs/PRISM_ANIMATION.md §4.8.1), so "the shatter looks unchanged" has
        /// two very different causes — no overlay, or an overlay with no impulse — and
        /// this is what tells them apart. Off by default like every channel.
        /// </summary>
        [CSLogChannelLabel("[ShieldShatter] shield disengage impulse")]
        PrismShieldShatter = 1 << 6,
        /// <summary>
        /// <c>[BarrelRoll]</c> — the Sparrow's strafing roll: direction, the stick vector that
        /// triggered it, and the nudge direction (plus whether it fired stopped, as the turret
        /// stance's dodge). Logged unconditionally on every roll until 2026-08-25, which is the
        /// same per-input console spam <see cref="ScarabDash"/> records for the sibling ability.
        /// Off by default like every channel; a real fault here is still a warning or an error
        /// and is unaffected by this flag.
        /// </summary>
        [CSLogChannelLabel("[BarrelRoll] Sparrow strafing roll")]
        SparrowStrafingRoll = 1 << 7,
        /// <summary>
        /// <c>[MouseFlight]</c> — one line the first time the desktop one-thumb mouse scheme
        /// takes over the input. Off by default like every channel; it exists so a playtest can
        /// tell "engaged" from "a pad is in use" (both are silent otherwise). The scheme's
        /// REFUSALS are warnings on <c>MouseFlightDiagnostics</c> and are unaffected by this flag —
        /// a system whose failure mode is silence has to stay loud when it fails.
        /// </summary>
        [CSLogChannelLabel("[MouseFlight] one-thumb mouse controls engaged")]
        MouseFlight = 1 << 8,
        /// <summary>
        /// <c>[ArcadeLaunch]</c> — the arcade launch panel: which panel a card routed to, how the
        /// controls rows resolved their icons and chips, and the Maelstrom pool a chosen intensity
        /// unlocks. Off by default like every channel; a real fault here is still a warning.
        /// </summary>
        [CSLogChannelLabel("[ArcadeLaunch] launch panel routing and controls rows")]
        ArcadeLaunch = 1 << 9,
        /// <summary>
        /// <c>[WeeklyChallenge]</c> — the weekly challenge: which challenge the UTC week resolved
        /// to, an armed attempt, and what an attempt recorded against the cloud record. Off by
        /// default like every channel; a missing catalog asset is still a warning and is
        /// unaffected by this flag.
        /// </summary>
        [CSLogChannelLabel("[WeeklyChallenge] weekly challenge resolution and attempts")]
        WeeklyChallenge = 1 << 10,
        /// <summary>
        /// <c>[CrystalMorph]</c> — a vessel's bespoke omni-crystal retirement, step by step: the
        /// retirement firing, the shells it adopted, the target it resolved, the stamp, and the
        /// hand-off to the real object.
        ///
        /// It exists because a morph's dependencies are invisible to it — the thing it lands on
        /// is minted by somebody else — and every way that can fail produces the SAME symptom on
        /// screen: the target appears normally and the crystal fades. This channel separates
        /// "the retirement never ran" from "the target never arrived" from "the target arrived
        /// and was rejected". Rejections are WARNINGS and fire whether or not this flag is on.
        /// </summary>
        [CSLogChannelLabel("[CrystalMorph] omni-crystal retirement steps")]
        CrystalMorph = 1 << 11,
        /// <summary>
        /// <c>[GunVesselTransformer]</c> — the Urchin's prismscape ride: which dimension a
        /// contact resolved to and therefore whether the vessel is grinding a ribbon or rolling
        /// a surface.
        ///
        /// It logged unconditionally on every surface attach, which was tolerable while nothing
        /// was built to be ridden and is per-contact console spam now that Hijack's arena is:
        /// rolling a burr is that mode's main verb and every touch re-logged. Off by default
        /// like every channel; a ride that fails to begin is still an error and is unaffected
        /// by this flag.
        /// </summary>
        [CSLogChannelLabel("[GunVesselTransformer] ride dimension (grind vs roll)")]
        PrismscapeRide = 1 << 12,
        /// <summary>
        /// <c>[ToyBox]</c> — the app shell's Toy Box: which toy a card bound, and the Navigate
        /// handoff into freestyle (press, transition wait, arrival pose). Off by default like every
        /// channel; every real fault on that path is a warning and is unaffected by this flag.
        /// </summary>
        [CSLogChannelLabel("[ToyBox] toy binding and freestyle handoff")]
        ToyBox = 1 << 13,
        /// <summary>
        /// <c>[Skein]</c> — one line per Skein cable build: the seed it landed on, and the rails,
        /// prisms and rings it laid. It exists because that mode's whole claim is that the runtime
        /// generator and <c>Tools/Build/skein_budget.py</c> agree, and this is the reading you
        /// compare against the model's own table in one glance. A cable that cannot be laid at all
        /// is an ERROR and is unaffected by this flag. Off by default like every channel.
        /// </summary>
        [CSLogChannelLabel("[Skein] cable build (seed, rails, prisms, rings)")]
        SkeinCable = 1 << 14,
        /// <summary>
        /// <c>[Boot]</c> — the application boot chain: AppManager, the application state
        /// machine, splash routing, the authentication scene, UGS sign-in, scene transitions,
        /// the main-menu state machine, offline mode and reconnect. Every step used to log
        /// unconditionally, which is ~40 console lines before the menu is interactive on a
        /// machine where nothing is wrong. A boot step that FAILS is still a warning or an
        /// error and is unaffected by this flag.
        /// </summary>
        [CSLogChannelLabel("[Boot] app boot, auth, scene transitions, menu state")]
        Boot = 1 << 15,
        /// <summary>
        /// <c>[Party]</c> — the presence lobby, party session, invite and friends layer:
        /// lobby joins, property writes, invite lifecycle, party membership, the SOAP party
        /// event bus and the host/client Netcode transitions. The presence lobby refreshes
        /// every 3 s, so an unconditional trace here grows for as long as the menu is open.
        /// Failures (rate limits, join errors, refresh errors) stay warnings.
        /// </summary>
        [CSLogChannelLabel("[Party] presence lobby, party session, invites, friends")]
        Party = 1 << 16,
        /// <summary>
        /// <c>[CloudData]</c> — cloud save, the local snapshot cache, progression and quest
        /// unlocks, player economy (crystals, episode tokens, IAP), roaming settings and
        /// analytics dispatch. Off by default like every channel; a failed load, a failed
        /// purchase and a rejected receipt are still warnings or errors.
        /// </summary>
        [CSLogChannelLabel("[CloudData] cloud save, progression, economy, settings, analytics")]
        CloudData = 1 << 17,
        /// <summary>
        /// <c>[Audio]</c> — FMOD emitter lifecycle on vessels, flora and drift/boost loops,
        /// parameter discovery, attach mode, and music track changes. Every vessel spawn used
        /// to print its whole audio bring-up, and a flora ambient loop logged per PLANT. A
        /// missing bank, an unresolvable parameter or a failed instance is still a warning.
        /// </summary>
        [CSLogChannelLabel("[Audio] FMOD emitters, vessel audio layers, music")]
        Audio = 1 << 18,
        /// <summary>
        /// <c>[SchwarzPColony]</c> — the third lattice colony's telemetry (founder claims, plant
        /// completions, births), the sibling of <see cref="GyroidColony"/> and
        /// <see cref="QuasicrystalColony"/>. One line per plant event in the boot world's
        /// forest. Coherence defects stay warnings.
        /// </summary>
        [CSLogChannelLabel("[SchwarzPColony] lattice telemetry")]
        SchwarzPColony = 1 << 19,
        /// <summary>
        /// <c>[Cell]</c> / <c>[Ecology]</c> — cell lifecycle and the food web's bookkeeping:
        /// spawner start/stop, cell swaps, satellite builds, runtime-data resets, the domain
        /// fauna buff and lifeform releases. A cell that cannot initialize is still a warning.
        /// </summary>
        [CSLogChannelLabel("[Ecology] cell lifecycle, spawners, swaps, fauna buff")]
        Ecology = 1 << 20,
        /// <summary>
        /// <c>[Arcade]</c> — match flow inside a mode: the server setting a turn target,
        /// ready-up counts, score calculation and sync, the comeback system's buff decisions,
        /// race course generation, and end-of-match stat reports. Distinct from
        /// <see cref="ArcadeLaunch"/>, which is the launch PANEL. A mode that cannot start is
        /// still a warning or an error.
        /// </summary>
        [CSLogChannelLabel("[Arcade] match flow: targets, ready-up, scoring, comeback")]
        ArcadeMatch = 1 << 21,
        /// <summary>
        /// <c>[Menu]</c> — app-shell navigation and selection: screen slides, hangar / store /
        /// arcade card population, vessel and captain selection, profile edits. Population
        /// used to log one line PER CARD on every screen build. A screen that cannot be built
        /// is still a warning.
        /// </summary>
        [CSLogChannelLabel("[Menu] screen navigation, card population, selection")]
        MenuUI = 1 << 22,
        /// <summary>
        /// <c>[Input]</c> — input device detection (gamepad connect / disconnect, controller
        /// family), device orientation and phone-flip state, and invert-setting sync. Off by
        /// default like every channel; an unavailable sensor is not a fault and is silent too.
        /// </summary>
        [CSLogChannelLabel("[Input] device detection, orientation, invert toggles")]
        Input = 1 << 23,
        /// <summary>
        /// <c>[Telemetry]</c> — per-vessel telemetry lifecycle (Awake, turn start / end) and
        /// the stats provider's discovery of what a vessel reports. A vessel with no telemetry
        /// component is still a warning.
        /// </summary>
        [CSLogChannelLabel("[Telemetry] vessel telemetry lifecycle, stat discovery")]
        VesselTelemetry = 1 << 24,
        /// <summary>
        /// <c>[Prism]</c> — prism runtime services: the instanced render service coming up,
        /// pool maintenance and scene-transition cleanup, the effects manager's implosion
        /// census. An unsupported device or a pool that cannot instantiate is still a warning.
        /// </summary>
        [CSLogChannelLabel("[Prism] render service, pools, effect census")]
        PrismRuntime = 1 << 25,
        /// <summary>
        /// <c>[FTUE]</c> — the first-time-user tutorial flow: step advance, skip, outro and
        /// completion.
        /// </summary>
        [CSLogChannelLabel("[FTUE] tutorial step flow")]
        FTUE = 1 << 27,
        All = ~0
    }

    /// <summary>
    /// Centralized debug logger for Cosmic Shore.
    ///
    /// Features:
    /// - Runtime log level control via <see cref="LogLevel"/> property.
    /// - In release builds (non-Editor, non-Development), all <c>Log</c> and <c>LogFormat</c>
    ///   calls are stripped entirely by the compiler via [Conditional] attributes,
    ///   eliminating both the method call and argument evaluation at the call site.
    /// - Warnings and errors are always compiled in but respect the runtime <see cref="LogLevel"/>.
    ///
    /// Usage:
    ///   CSDebug.Log("hello");                       // same as Debug.Log
    ///   CSDebug.LogWarning("careful", this);         // same as Debug.LogWarning with context
    ///   CSDebug.LogLevel = CSLogLevel.WarningsAndErrors;  // suppress info logs
    ///   CSDebug.LogLevel = CSLogLevel.Off;                // silence everything
    /// </summary>
    public static class CSDebug
    {
        /// <summary>
        /// Per-type flags for granular control. Toggle individual log types on/off.
        /// </summary>
        public static bool LogEnabled = true;
        public static bool WarningsEnabled = true;
        public static bool ErrorsEnabled = true;

        /// <summary>
        /// Convenience property for preset log levels.
        /// Getter derives the closest preset from the individual flags.
        /// Setter applies the preset by setting all flags at once.
        /// </summary>
        public static CSLogLevel LogLevel
        {
            get
            {
                if (LogEnabled && WarningsEnabled && ErrorsEnabled) return CSLogLevel.All;
                if (!LogEnabled && WarningsEnabled && ErrorsEnabled) return CSLogLevel.WarningsAndErrors;
                if (!LogEnabled && !WarningsEnabled && !ErrorsEnabled) return CSLogLevel.Off;
                // Custom combination that doesn't map to a preset; treat as All.
                return CSLogLevel.All;
            }
            set
            {
                switch (value)
                {
                    case CSLogLevel.All:
                        LogEnabled = true;
                        WarningsEnabled = true;
                        ErrorsEnabled = true;
                        break;
                    case CSLogLevel.WarningsAndErrors:
                        LogEnabled = false;
                        WarningsEnabled = true;
                        ErrorsEnabled = true;
                        break;
                    case CSLogLevel.Off:
                        LogEnabled = false;
                        WarningsEnabled = false;
                        ErrorsEnabled = false;
                        break;
                }
            }
        }

        // ──────────────────────────────────────────────
        //  Log  (info / debug level)
        //  Stripped entirely in release builds.
        // ──────────────────────────────────────────────

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(object message)
        {
            if (!LogEnabled) return;
            Debug.Log(message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(object message, Object context)
        {
            if (!LogEnabled) return;
            Debug.Log(message, context);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogFormat(string format, params object[] args)
        {
            if (!LogEnabled) return;
            Debug.LogFormat(format, args);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogFormat(Object context, string format, params object[] args)
        {
            if (!LogEnabled) return;
            Debug.LogFormat(context, format, args);
        }

        // ──────────────────────────────────────────────
        //  LogVerbose  (opt-in diagnostic channels)
        //  Stripped entirely in release builds; silent
        //  in the Editor until the channel is enabled.
        // ──────────────────────────────────────────────

        /// <summary>
        /// Which bring-up channels are currently emitting. Defaults to
        /// <see cref="CSLogChannel.None"/> — a finished system stays silent without deleting the
        /// trace that made it work. Toggled from FrogletTools &gt; Toolbox &gt; Logging.
        /// </summary>
        public static CSLogChannel VerboseChannels = CSLogChannel.None;

        /// <summary>
        /// True when <paramref name="channel"/> is emitting. Guard with this — rather than
        /// calling <see cref="LogVerbose"/> directly — anywhere the MESSAGE ITSELF is expensive
        /// to build (string interpolation in a per-frame or per-contact path): the arguments of a
        /// [Conditional] call are still evaluated in the Editor, so an unguarded interpolated
        /// string costs its allocation on every call even while the channel is off.
        /// </summary>
        public static bool IsVerbose(CSLogChannel channel)
            => LogEnabled && (VerboseChannels & channel) != 0;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogVerbose(CSLogChannel channel, object message)
        {
            if (!IsVerbose(channel)) return;
            Debug.Log(message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogVerbose(CSLogChannel channel, object message, Object context)
        {
            if (!IsVerbose(channel)) return;
            Debug.Log(message, context);
        }

        // ──────────────────────────────────────────────
        //  Warning
        //  Always compiled; respects runtime LogLevel.
        // ──────────────────────────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(object message)
        {
            if (!WarningsEnabled) return;
            Debug.LogWarning(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(object message, Object context)
        {
            if (!WarningsEnabled) return;
            Debug.LogWarning(message, context);
        }

        public static void LogWarningFormat(string format, params object[] args)
        {
            if (!WarningsEnabled) return;
            Debug.LogWarningFormat(format, args);
        }

        public static void LogWarningFormat(Object context, string format, params object[] args)
        {
            if (!WarningsEnabled) return;
            Debug.LogWarningFormat(context, format, args);
        }

        // ──────────────────────────────────────────────
        //  Error
        //  Always compiled; respects runtime LogLevel.
        // ──────────────────────────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(object message)
        {
            if (!ErrorsEnabled) return;
            Debug.LogError(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(object message, Object context)
        {
            if (!ErrorsEnabled) return;
            Debug.LogError(message, context);
        }

        public static void LogErrorFormat(string format, params object[] args)
        {
            if (!ErrorsEnabled) return;
            Debug.LogErrorFormat(format, args);
        }

        public static void LogErrorFormat(Object context, string format, params object[] args)
        {
            if (!ErrorsEnabled) return;
            Debug.LogErrorFormat(context, format, args);
        }
    }
}
