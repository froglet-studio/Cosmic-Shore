using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Anything that sails an Ark the HUD can follow: the Ark escort and the job voyages.</summary>
    public interface IArkStatusSource
    {
        /// <summary>The run as it stands now (a snapshot; poll it every frame if you like, it never walks the fauna).</summary>
        ArkEscortStatus Status { get; }
    }

    /// <summary>
    /// The one place the HUD hooks to follow an Ark voyage (Milestone 1's toasts and objective box), whichever run sails
    /// it: <see cref="Current"/> is the run sailing on this machine, <see cref="Signal"/> fires for every moment worth a
    /// toast. Raised on the machine that runs the voyage.
    /// </summary>
    public static class ArkVoyageSignals
    {
        /// <summary>The Ark voyage sailing on this machine, or null.</summary>
        public static IArkStatusSource Current { get; private set; }

        /// <summary>Every moment of an Ark voyage the HUD may want to show.</summary>
        public static event System.Action<ArkEscortSignal> Signal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            Signal = null;
        }

        internal static void Begin(IArkStatusSource source) => Current = source;

        internal static void End(IArkStatusSource source)
        {
            if (ReferenceEquals(Current, source)) Current = null;
        }

        internal static void Emit(ArkEscortSignalKind kind, IArkStatusSource source)
        {
            var handlers = Signal;
            if (handlers == null || source == null) return;
            handlers(new ArkEscortSignal(kind, source.Status));
        }
    }

    /// <summary>
    /// The fauna's assaults on an Ark, for whichever run sails it (<see cref="ArkEscortRun"/>, <see cref="VoyageRun"/>):
    /// the <see cref="ArkAssaultClock"/>, the hull's "under attack" flag, the banners (authored on
    /// <see cref="ArkEscortSO"/>) and the signals. The run decides WHEN its Ark's world can be assaulted and feeds the
    /// clock its tick, kills and plate losses; the lure the run sets on that world follows <see cref="Lure"/>.
    /// </summary>
    public sealed class ArkAssaultDriver
    {
        /// <summary>The hull counts as under attack for this long after its last lost plate.</summary>
        public const float UnderAttackSeconds = 4f;

        readonly ArkAssaultClock _clock = new();
        readonly IArkStatusSource _source;
        readonly System.Action<string> _banner;
        readonly System.Action<string, ArkwayVoyageHud.Mood> _toast;

        ArkEscortSO _words;
        ArkAssaultClock.Settings _settings;
        float _lastPlateLostAt = -1f;
        bool _underAttack;
        int _lastHullAlive = -1;
        int _enteredLeg = -1;
        string _world = string.Empty;

        public ArkAssaultDriver(IArkStatusSource source, System.Action<string> banner)
        {
            _source = source;
            _banner = banner;
        }

        /// <summary>A driver whose lines are toasts with a mood: a warning and an assault are threats, a repelled
        /// assault is good, one that passed is an event.</summary>
        public ArkAssaultDriver(IArkStatusSource source, System.Action<string, ArkwayVoyageHud.Mood> toast)
        {
            _source = source;
            _toast = toast;
        }

        public bool UnderAttack => _underAttack;
        public ArkAssaultClock Clock => _clock;

        /// <summary>A new voyage: <paramref name="words"/> carries the assault lines and timings, <paramref name="tier"/>
        /// the assault's strength (null or a zero AssaultLureShare: no assaults).</summary>
        public void Begin(ArkEscortSO words, ArkEscortSO.Tier tier)
        {
            _clock.Reset();
            _words = words;
            _settings = SettingsFor(words, tier);
            _lastPlateLostAt = -1f;
            _underAttack = false;
            _lastHullAlive = -1;
            _enteredLeg = -1;
            _world = string.Empty;
            ArkVoyageSignals.Begin(_source);
        }

        /// <summary>A new leg of the same voyage may sail at another strength (a job voyage's next job).</summary>
        public void Retune(ArkEscortSO words, ArkEscortSO.Tier tier)
        {
            _words = words;
            _settings = SettingsFor(words, tier);
        }

        public static ArkAssaultClock.Settings SettingsFor(ArkEscortSO words, ArkEscortSO.Tier tier)
        {
            if (!words || tier == null) return default;
            return new ArkAssaultClock.Settings
            {
                FirstDelay = words.AssaultFirstDelay,
                Warning = words.AssaultWarningSeconds,
                Duration = tier.AssaultSeconds,
                Gap = tier.AssaultGapSeconds,
                RepelKills = tier.AssaultRepelKills,
                LureShare = tier.AssaultLureShare,
            };
        }

        /// <summary>Signal that the Ark is under way.</summary>
        public void Underway() => ArkVoyageSignals.Emit(ArkEscortSignalKind.Underway, _source);

        /// <summary>The Ark is bound for a new world named <paramref name="world"/>.</summary>
        public void NewWorld(string world)
        {
            Apply(_clock.NewWorld());
            _world = world ?? string.Empty;
            ArkVoyageSignals.Emit(ArkEscortSignalKind.WorldAhead, _source);
        }

        /// <summary>
        /// Once a second. <paramref name="leg"/> is the world the Ark is bound for, <paramref name="inside"/> whether it
        /// is inside that world's membrane, <paramref name="canAssault"/> whether that world's fauna may turn on it now
        /// (hostile, not pacified, the voyage not decided).
        /// </summary>
        public void Tick(Ark ark, int leg, bool inside, bool canAssault)
        {
            if (!ark) return;
            float now = Time.time;
            if (inside && leg >= 0 && _enteredLeg != leg)
            {
                _enteredLeg = leg;
                ArkVoyageSignals.Emit(ArkEscortSignalKind.WorldEntered, _source);
            }
            Apply(_clock.Tick(now, inside && canAssault, _settings));
            if (_underAttack && now - _lastPlateLostAt > UnderAttackSeconds)
            {
                _underAttack = false;
                ArkVoyageSignals.Emit(ArkEscortSignalKind.ArkAttackEased, _source);
            }
            if (ark.AliveCount != _lastHullAlive)
            {
                _lastHullAlive = ark.AliveCount;
                ArkVoyageSignals.Emit(ArkEscortSignalKind.HullChanged, _source);
            }
        }

        /// <summary>The lure share on the Ark's own world now: the assault's while one is on, else
        /// <paramref name="baseShare"/>.</summary>
        public float Lure(float baseShare) => _clock.LureShare(baseShare, _settings);

        /// <summary>A hull plate was lost.</summary>
        public void PlateLost()
        {
            _clock.NotePlateLost();
            _lastPlateLostAt = Time.time;
            if (_underAttack) return;
            _underAttack = true;
            ArkVoyageSignals.Emit(ArkEscortSignalKind.ArkUnderAttack, _source);
        }

        /// <summary>A creature of the Ark's own world died near the Ark.</summary>
        public void Kill() => Apply(_clock.NoteKill(Time.time, _settings));

        public void Pacified() => ArkVoyageSignals.Emit(ArkEscortSignalKind.WorldPacified, _source);

        /// <summary>The voyage is over (the source's Status should carry the outcome by now).</summary>
        public void End()
        {
            _clock.NewWorld();
            _underAttack = false;
            ArkVoyageSignals.Emit(ArkEscortSignalKind.Ended, _source);
            ArkVoyageSignals.End(_source);
        }

        /// <summary>Copy the assault's state into <paramref name="s"/>.</summary>
        public void Fill(ref ArkEscortStatus s)
        {
            float now = Time.time;
            s.UnderAttack = _underAttack;
            s.Assault = _clock.Phase;
            s.AssaultSecondsLeft = _clock.SecondsLeft(now);
            s.NextAssaultIn = _clock.NextIn(now);
            s.AssaultKills = _clock.Kills;
            s.AssaultKillsNeeded = _clock.KillsNeeded;
            s.AssaultPlatesLost = _clock.PlatesLost;
            s.AssaultsRepelled = _clock.Repelled;
            s.AssaultsPassed = _clock.Passed;
        }

        void Apply(ArkAssaultClock.Change change)
        {
            if (change == ArkAssaultClock.Change.None || !_words) return;
            switch (change)
            {
                case ArkAssaultClock.Change.Incoming:
                    Say(TemplateText.Fill(_words.AssaultIncomingLine, "name", _world,
                        "seconds", Mathf.CeilToInt(_words.AssaultWarningSeconds).ToString()), ArkwayVoyageHud.Mood.Threat);
                    ArkVoyageSignals.Emit(ArkEscortSignalKind.AssaultIncoming, _source);
                    break;
                case ArkAssaultClock.Change.Started:
                    Say(TemplateText.Fill(_words.AssaultLine, "name", _world, "kills", _clock.KillsNeeded.ToString()),
                        ArkwayVoyageHud.Mood.Threat);
                    ArkVoyageSignals.Emit(ArkEscortSignalKind.AssaultStarted, _source);
                    break;
                case ArkAssaultClock.Change.Repelled:
                    Say(TemplateText.Fill(_words.AssaultRepelledLine, "name", _world, "lost", _clock.PlatesLost.ToString()),
                        ArkwayVoyageHud.Mood.Good);
                    ArkVoyageSignals.Emit(ArkEscortSignalKind.AssaultRepelled, _source);
                    break;
                case ArkAssaultClock.Change.Passed:
                    // A warning dropped before it started (the Ark left the world, it was pacified) passes quietly.
                    if (_clock.PlatesLost > 0 || _clock.Kills > 0)
                        Say(TemplateText.Fill(_words.AssaultPassedLine, "name", _world, "lost", _clock.PlatesLost.ToString()),
                            ArkwayVoyageHud.Mood.Event);
                    ArkVoyageSignals.Emit(ArkEscortSignalKind.AssaultPassed, _source);
                    break;
            }
            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[Ark] Assault {change} in {_world} " +
                $"(kills {_clock.Kills}/{_clock.KillsNeeded}, plates lost {_clock.PlatesLost}).");
        }

        void Say(string line, ArkwayVoyageHud.Mood mood)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (_toast != null) _toast(line, mood);
            else _banner?.Invoke(line);
        }
    }
}
