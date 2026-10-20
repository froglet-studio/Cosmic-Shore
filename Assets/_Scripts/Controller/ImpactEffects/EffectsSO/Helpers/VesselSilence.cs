using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// SILENCE: a pilot who has petals STRIPPED or STOLEN is silenced for a time proportional to the
    /// petals they lost (Docs/ElementalAbilitySystem/SILENCE.md, Garrett 2026-10-20). Taking a
    /// rival's elements was a deterrent but not protection: a pilot with fewer elements could still
    /// strip you back, steal your prisms or break the health prisms of your ark. Now the take also
    /// buys a window in which the victim cannot retaliate.
    ///
    /// <para><b>What silence blocks, and what it leaves alone.</b> A silenced pilot keeps EVERY
    /// ability and every control: the Sparrow still fires, the Thresher still works its flail with
    /// both triggers, everybody still flies, boosts, lays their trail and builds their own mass.
    /// What it removes is HARM: nothing the pilot does can hurt another pilot's vessel, fauna or
    /// prisms, and nothing they lay comes out dangerous. Danger prisms that are already standing
    /// keep their danger, because a danger prism is the arena, not a weapon
    /// (Docs/ELEMENTAL_ECONOMY.md §9).</para>
    ///
    /// <para><b>The gates are the existing choke points, not the abilities.</b> Every way a pilot
    /// harms something already funnels through a handful of methods, so silence is enforced there
    /// once rather than in each of the fleet's ~60 ability call sites:</para>
    /// <list type="bullet">
    /// <item>a vessel: <see cref="CombatHitDrain.TryAdmit"/> (the one gate every scored hit passes)
    /// and <see cref="ElementalTransfer"/> (every take);</item>
    /// <item>a prism: <c>Prism.Damage</c>, <c>Consume</c>, <c>Vanish</c> and <c>Steal</c>, which
    /// deflect a silenced pilot's hit on any prism that is not their own domain's, plus the few
    /// sites that break a rival's shield directly;</item>
    /// <item>fauna and flora: <c>LifeForm.Jousted</c> and <c>Fauna.Predated</c>;</item>
    /// <item>danger: <c>Prism.Initialize</c> and <c>Prism.MakeDangerous</c>.</item>
    /// </list>
    ///
    /// <para><b>Where it is decided.</b> On the machine that OWNS the victim, at the moment its petals
    /// settle - the same place <see cref="ElementalTransfer"/> already settles every take, so the
    /// silence is exactly as long as the petals that truly came loose (a warded or empty pilot loses
    /// nothing and is not silenced). The owner then publishes the remaining seconds through the
    /// hull's <see cref="ISilenceRelay"/>, and every peer records the same window, because prism
    /// damage is simulated on every peer and each one has to refuse the same hits.</para>
    ///
    /// <para><b>Keyed by player name</b>, because that is the identity every harm site already
    /// carries: <c>Prism.Damage</c> knows only the attacker's name and domain. Scoring is keyed the
    /// same way (<c>StatsManager</c>, <see cref="VesselCombatHitLatch"/>).</para>
    /// </summary>
    public static class VesselSilence
    {
        const string ConfigResourcePath = "VesselSilenceConfig";

        struct Entry
        {
            public double Until;
            public Domains Domain;
            public float Total;
        }

        static readonly Dictionary<string, Entry> s_byPlayer = new();
        static VesselSilenceConfigSO s_config;
        static bool s_configResolved;

        /// <summary>The clock silence is measured on. Local on purpose: the wire carries REMAINING
        /// seconds, so peers never have to agree on an absolute time (a few milliseconds of latency
        /// is noise against a window of seconds).</summary>
        internal static Func<double> Clock = () => Time.timeAsDouble;

        /// <summary>Raised on every machine when a pilot's silence is recorded or extended, with
        /// their player name. A HUD or an autopilot polls <see cref="RemainingSeconds"/> for the
        /// rest; there is no "lifted" event because a window simply runs out.</summary>
        public static event Action<string> Changed;

        /// <summary>The same moment as <see cref="Changed"/>, with the pilot's vessel, raised only
        /// where the silence is recorded for a vessel this machine holds (its owner's settle, or the
        /// owner's publish arriving). The silenced pilot's own screen listens here
        /// (<c>SilenceScreenFeedback</c>), because only a vessel can say whether it is the local
        /// pilot's.</summary>
        public static event Action<IVesselStatus> VesselSilenced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_byPlayer.Clear();
            s_tally.Clear();
            s_config = null;
            s_configResolved = false;
            Changed = null;
            VesselSilenced = null;
        }

        /// <summary>The live tuning (the Resources asset, or its defaults when it is missing).</summary>
        public static VesselSilenceConfigSO Settings => Config;

        static VesselSilenceConfigSO Config
        {
            get
            {
                if (!s_configResolved)
                {
                    s_config = Resources.Load<VesselSilenceConfigSO>(ConfigResourcePath);
                    if (s_config == null)
                    {
                        CSDebug.LogWarning($"[VesselSilence] No Resources/{ConfigResourcePath} asset - " +
                                           "silence runs on the VesselSilenceConfigSO defaults. Create " +
                                           "the asset (ScriptableObjects > Vessel > Vessel Silence Config).");
                        s_config = ScriptableObject.CreateInstance<VesselSilenceConfigSO>();
                    }
                    s_configResolved = true;
                }
                return s_config;
            }
        }

        // ── Pure rules (tested directly) ────────────────────────────────────────────

        /// <summary>Seconds of silence a take of <paramref name="petals"/> buys.</summary>
        public static float SecondsFor(int petals, float secondsPerPetal) =>
            petals <= 0 || secondsPerPetal <= 0f ? 0f : petals * secondsPerPetal;

        /// <summary>
        /// The silence left after a new take of <paramref name="addSeconds"/> lands on a pilot who
        /// still had <paramref name="remainingSeconds"/>: the two ADD, up to
        /// <paramref name="maxSeconds"/>. Additive so that the window stays proportional to everything
        /// taken; capped so that a pilot is never silenced for longer than one ceiling, however many
        /// hits land.
        /// </summary>
        public static float Extend(float remainingSeconds, float addSeconds, float maxSeconds)
        {
            if (maxSeconds <= 0f) return 0f;
            float left = remainingSeconds > 0f ? remainingSeconds : 0f;
            float add = addSeconds > 0f ? addSeconds : 0f;
            float total = left + add;
            return total > maxSeconds ? maxSeconds : total;
        }

        /// <summary>True when a take of class <paramref name="source"/> silences, under
        /// <paramref name="silencingSources"/>.</summary>
        public static bool Silences(ElementalDebuffSources source, ElementalDebuffSources silencingSources) =>
            source != ElementalDebuffSources.None && (silencingSources & source) != 0;

        // ── Queries (every peer) ────────────────────────────────────────────────────

        /// <summary>True while <paramref name="playerName"/> is silenced.</summary>
        public static bool IsSilenced(string playerName) => RemainingSeconds(playerName) > 0f;

        /// <summary>True while the pilot of <paramref name="status"/> is silenced.</summary>
        public static bool IsSilenced(IVesselStatus status) => status != null && IsSilenced(status.PlayerName);

        /// <summary>Seconds of silence <paramref name="playerName"/> has left; 0 when not silenced.</summary>
        public static float RemainingSeconds(string playerName)
        {
            if (string.IsNullOrEmpty(playerName) || s_byPlayer.Count == 0) return 0f;
            if (!s_byPlayer.TryGetValue(playerName, out var e)) return 0f;
            double left = e.Until - Clock();
            if (left > 0d) return (float)left;
            s_byPlayer.Remove(playerName);
            return 0f;
        }

        /// <summary>The silence left as a fraction of the window it was last set to (1 the moment
        /// it lands, 0 when it is over) - the shape a HUD readout wants.</summary>
        public static float Remaining01(string playerName)
        {
            float left = RemainingSeconds(playerName);
            if (left <= 0f || !s_byPlayer.TryGetValue(playerName, out var e) || e.Total <= 0f) return 0f;
            return Mathf.Clamp01(left / e.Total);
        }

        /// <summary>
        /// True when a hit by <paramref name="attackerName"/> on something of
        /// <paramref name="targetDomain"/> must do nothing because the attacker is silenced. A
        /// silenced pilot can still touch their OWN domain's mass (their own trail, their own
        /// shields); everything else - a rival's prisms, neutral flora, the health prisms of an ark -
        /// is protected.
        /// </summary>
        public static bool Protects(string attackerName, Domains targetDomain)
        {
            if (string.IsNullOrEmpty(attackerName) || s_byPlayer.Count == 0) return false;
            if (!IsSilenced(attackerName)) return false;
            return s_byPlayer.TryGetValue(attackerName, out var e) && targetDomain != e.Domain;
        }

        /// <summary>True when a prism laid by <paramref name="layerName"/> must come out plain rather
        /// than dangerous: its layer is silenced and the config suppresses danger production.</summary>
        public static bool SuppressesDanger(string layerName) =>
            s_byPlayer.Count > 0 && IsSilenced(layerName) && Config.SuppressDangerProduction;

        // ── Writes ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// The VICTIM's half: <paramref name="petals"/> just came loose from
        /// <paramref name="victim"/> under <paramref name="source"/> (a strip or a steal, never a
        /// burn). Called from <see cref="ElementalTransfer"/> where the take settles. Acts only on the
        /// copy whose take counts - offline, or the victim's owner - and publishes the result so every
        /// other peer records the same window. Returns the seconds of silence the victim now has.
        /// </summary>
        public static float OnPetalsTaken(IVesselStatus victim, int petals, ElementalDebuffSources source)
        {
            if (victim == null) return 0f;

            var relay = RelayOf(victim);
            if (relay != null && relay.IsNetworked && !relay.IsOwnedHere) return 0f;   // not this copy's take

            // Counted before anything can refuse it: "hit, but nothing came loose" is the commonest
            // reason a playtester sees no silence (every mode's pot starts empty), and the `silence`
            // console command has to be able to say so.
            Note(victim.PlayerName, petals, silenced: false);
            if (petals <= 0) return 0f;

            var config = Config;
            if (!Silences(source, config.SilencingSources)) return 0f;

            float seconds = Extend(RemainingSeconds(victim.PlayerName),
                                   SecondsFor(petals, config.SecondsPerPetal), config.MaxSeconds);
            if (seconds <= 0f) return 0f;

            Note(victim.PlayerName, 0, silenced: true);
            Record(victim, seconds);
            if (relay != null && relay.IsNetworked) relay.PublishSilence(seconds);
            return seconds;
        }

        /// <summary>Records <paramref name="seconds"/> of silence for <paramref name="playerName"/>
        /// from now, replacing whatever window was there (the owner's figure already includes it).
        /// Called by the owner and by every peer that receives the owner's publish.</summary>
        public static void Record(string playerName, Domains domain, float seconds)
        {
            if (string.IsNullOrEmpty(playerName)) return;
            if (seconds <= 0f)
            {
                s_byPlayer.Remove(playerName);
                return;
            }
            s_byPlayer[playerName] = new Entry { Until = Clock() + seconds, Domain = domain, Total = seconds };
            Changed?.Invoke(playerName);
        }

        /// <summary><see cref="Record(string, Domains, float)"/> for a vessel this machine holds, and
        /// <see cref="VesselSilenced"/> after it.</summary>
        public static void Record(IVesselStatus status, float seconds)
        {
            if (status == null) return;
            Record(status.PlayerName, status.Domain, seconds);
            if (seconds > 0f) VesselSilenced?.Invoke(status);
        }

        /// <summary>Lifts <paramref name="playerName"/>'s silence - their vessel was reset for a new
        /// turn, and a window opened in the last one does not carry into it.</summary>
        public static void Lift(string playerName)
        {
            if (!string.IsNullOrEmpty(playerName)) s_byPlayer.Remove(playerName);
        }

        /// <summary>Lifts every silence and forgets the counts (the tests).</summary>
        public static void ClearAll()
        {
            s_byPlayer.Clear();
            s_tally.Clear();
        }

        // ── Diagnostics (the `silence` console command, SilenceScreenFeedback) ─────

        /// <summary>Per victim, on the copy whose takes count: settled takes, how many of them took
        /// nothing, the petals lost and the silences that began.</summary>
        public struct Tally
        {
            public int Takes, EmptyTakes, Petals, Silences;
        }

        static readonly Dictionary<string, Tally> s_tally = new();

        /// <summary>Every victim this machine has settled a take for, with its counts.</summary>
        public static IReadOnlyDictionary<string, Tally> Tallies => s_tally;

        static void Note(string playerName, int petals, bool silenced)
        {
            if (string.IsNullOrEmpty(playerName)) return;
            s_tally.TryGetValue(playerName, out var t);
            if (silenced) t.Silences++;
            else
            {
                t.Takes++;
                if (petals <= 0) t.EmptyTakes++;
                else t.Petals += petals;
            }
            s_tally[playerName] = t;
        }

        static ISilenceRelay RelayOf(IVesselStatus status)
        {
            // NOT status.Transform: see ElementalCrystalEjector. That property throws rather than
            // answering null on a status whose vessel is gone.
            var hull = status?.Vessel?.Transform;
            return hull && hull.TryGetComponent(out ISilenceRelay relay) ? relay : null;
        }
    }

    /// <summary>
    /// The network half of <see cref="VesselSilence"/>, implemented by <c>NetworkVesselImpactor</c>
    /// on every vessel hull. Declared here so the silence rules stay free of Netcode types and keep
    /// compiling in <c>Tools/Build/elemental_transfer_harness</c>.
    /// </summary>
    public interface ISilenceRelay
    {
        /// <summary>This hull is a spawned network object.</summary>
        bool IsNetworked { get; }

        /// <summary>This machine owns this hull.</summary>
        bool IsOwnedHere { get; }

        /// <summary>Owner only: tells every other peer this hull's pilot is silenced for
        /// <paramref name="seconds"/> from now.</summary>
        void PublishSilence(float seconds);
    }
}
