using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// SILENCE: every SCORED HIT on a pilot silences them for a time proportional to what the hit is
    /// worth (Docs/ElementalAbilitySystem/SILENCE.md, Garrett 2026-10-20). Taking a rival's elements
    /// was a deterrent but not protection: a pilot with fewer elements could still strip you back,
    /// steal your prisms or break the health prisms of your ark. Now the hit also buys a window in
    /// which the victim cannot retaliate.
    ///
    /// <para><b>Worth, not petals settled (Garrett's call after the first Dog Fight run).</b> The
    /// first cut silenced for the petals that actually came loose, and every mode's pot starts empty,
    /// so a fight between fresh pilots silenced nobody: the AI scored 20+ points and nothing happened.
    /// A hit is now priced the way <see cref="CombatHitDrain"/> prices its take - ten points to the
    /// petal, on each of the four elements - whether or not the victim had petals to give.</para>
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
    /// <para><b>Where it is decided.</b> In <see cref="CombatHitDrain.TryAdmit"/>, the moment a hit is
    /// admitted - which is on the machine that DECIDES the hit (the attacker's owner; every reporter
    /// asks <see cref="ElementalTransfer.IsDecidedHere"/> first), so a hit silences once however many
    /// peers replay it. A warded victim is not admitted and is not silenced. The decider extends the
    /// victim's window here and sends the SAME extension to every other peer through the victim
    /// hull's <see cref="ISilenceRelay"/>, because prism damage is simulated on every peer and each
    /// one has to refuse the same hits.</para>
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

        /// <summary>The elements a scored hit is priced on: all four, the fleet price's convention
        /// (<see cref="CombatHitDrain"/>: "ten points is one petal, on every element the hit
        /// touches").</summary>
        public const int ElementsAHitIsPricedOn = 4;

        /// <summary>
        /// Seconds of silence a hit worth <paramref name="points"/> buys: its worth in petals (ten
        /// points to the petal, on each of the four elements) times <paramref name="secondsPerPetal"/>.
        /// A bullet (1 point) is 0.4 petals, a Strike (8) 3.2, a missile shockwave (10) 4 - exactly
        /// what the same hit would take from a pilot who held enough.
        /// </summary>
        public static float SecondsForPoints(int points, float secondsPerPetal) =>
            points <= 0 || secondsPerPetal <= 0f
                ? 0f
                : points / CombatHitDrain.PointsPerLevel * ElementsAHitIsPricedOn * secondsPerPetal;

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
        /// A scored hit worth <paramref name="points"/> was just ADMITTED against
        /// <paramref name="victim"/> under <paramref name="source"/> (<see cref="CombatHitDrain.TryAdmit"/>,
        /// on the machine that decides the hit). Extends the victim's silence by the hit's worth
        /// (<see cref="SecondsForPoints"/>) here and sends the same extension to every other peer.
        /// Returns the seconds added.
        /// </summary>
        public static float OnHitAdmitted(IVesselStatus victim, int points, ElementalDebuffSources source)
        {
            if (victim == null || points <= 0) return 0f;

            var config = Config;
            if (!Silences(source, config.SilencingSources)) return 0f;

            float add = SecondsForPoints(points, config.SecondsPerPetal);
            if (add <= 0f) return 0f;

            NoteHit(victim.PlayerName);
            ExtendBy(victim, add);
            var relay = RelayOf(victim);
            if (relay != null && relay.IsNetworked) relay.PublishSilence(add);
            return add;
        }

        /// <summary>
        /// Adds <paramref name="addSeconds"/> to the silence <paramref name="status"/>'s pilot has
        /// left, up to the ceiling (<see cref="Extend"/>). The decider calls it for its own hit, and
        /// every other peer calls it when that hit's publish arrives, so each machine extends its own
        /// copy of the window by the same amount.
        /// </summary>
        public static void ExtendBy(IVesselStatus status, float addSeconds)
        {
            if (status == null || addSeconds <= 0f) return;
            float left = RemainingSeconds(status.PlayerName);
            float total = Extend(left, addSeconds, Config.MaxSeconds);
            if (total <= 0f) return;
            if (left <= 0f) NoteOnset(status.PlayerName);
            Record(status, total);
        }

        /// <summary>
        /// Counts what a take settled on <paramref name="victim"/>, for the <c>silence</c> console
        /// command. It silences nobody - a hit's silence is decided when it is admitted, whatever it
        /// manages to take - and counts only on the copy whose take is real (offline, or the victim's
        /// owner). "Hit, took nothing" is what a fight between fresh pilots looks like.
        /// </summary>
        public static void OnPetalsTaken(IVesselStatus victim, int petals, ElementalDebuffSources source)
        {
            if (victim == null) return;
            var relay = RelayOf(victim);
            if (relay != null && relay.IsNetworked && !relay.IsOwnedHere) return;   // not this copy's take
            NoteTake(victim.PlayerName, petals);
        }

        /// <summary>Records <paramref name="seconds"/> of silence for <paramref name="playerName"/>
        /// from now, replacing whatever window was there. <see cref="ExtendBy"/> is the gameplay
        /// path; this is its last step, and the <c>silence test</c> console command.</summary>
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
        /// turn, and a window opened in the last one does not carry into it; or an ally's dust
        /// cleansed them (<see cref="ElementalCleanse"/>). True when there was a window to lift.</summary>
        public static bool Lift(string playerName) =>
            !string.IsNullOrEmpty(playerName) && s_byPlayer.Remove(playerName);

        /// <summary>Lifts every silence and forgets the counts (the tests).</summary>
        public static void ClearAll()
        {
            s_byPlayer.Clear();
            s_tally.Clear();
        }

        // ── Diagnostics (the `silence` console command, SilenceScreenFeedback) ─────

        /// <summary>Per pilot, as this machine saw it: scored hits it decided against them, silences
        /// that began, and (on the copy whose takes are real) takes settled, how many took nothing,
        /// and the petals lost.</summary>
        public struct Tally
        {
            public int Hits, Silences, Takes, EmptyTakes, Petals;
        }

        static readonly Dictionary<string, Tally> s_tally = new();

        /// <summary>Every victim this machine has settled a take for, with its counts.</summary>
        public static IReadOnlyDictionary<string, Tally> Tallies => s_tally;

        static void NoteHit(string playerName) => Bump(playerName, hits: 1, silences: 0, petals: -1);
        static void NoteOnset(string playerName) => Bump(playerName, hits: 0, silences: 1, petals: -1);
        static void NoteTake(string playerName, int petals) => Bump(playerName, 0, 0, petals < 0 ? 0 : petals);

        /// <summary>One edit to a pilot's tally. <paramref name="petals"/> -1 means "not a take";
        /// 0 or more counts one settled take of that many petals.</summary>
        static void Bump(string playerName, int hits, int silences, int petals)
        {
            if (string.IsNullOrEmpty(playerName)) return;
            s_tally.TryGetValue(playerName, out var t);
            t.Hits += hits;
            t.Silences += silences;
            if (petals >= 0)
            {
                t.Takes++;
                if (petals == 0) t.EmptyTakes++;
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

        /// <summary>Tells every OTHER peer to extend this hull's pilot's silence by
        /// <paramref name="addSeconds"/> (<see cref="VesselSilence.ExtendBy"/>). Called by whichever
        /// machine decided the hit - the attacker's owner, which usually does not own this hull.</summary>
        void PublishSilence(float addSeconds);
    }
}
