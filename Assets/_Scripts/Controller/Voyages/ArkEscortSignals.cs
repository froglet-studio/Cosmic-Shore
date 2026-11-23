namespace CosmicShore.Gameplay
{
    /// <summary>Where an Ark escort's assault stands (<see cref="ArkAssaultClock"/>).</summary>
    public enum ArkAssaultPhase
    {
        /// <summary>No assault: the lull, or a world that cannot be assaulted (friendly, pacified, the open sea).</summary>
        None = 0,
        /// <summary>Announced: the fauna turn on the Ark when the warning runs out.</summary>
        Incoming = 1,
        /// <summary>The world's fauna are converging on the Ark.</summary>
        Active = 2,
    }

    /// <summary>What just happened in an Ark escort, for the HUD's toasts and objective box
    /// (<see cref="ArkVoyageSignals.Signal"/>).</summary>
    public enum ArkEscortSignalKind
    {
        /// <summary>The Ark has set sail.</summary>
        Underway = 0,
        /// <summary>The Ark left a world's core and is bound for the next world (<see cref="ArkEscortStatus.WorldName"/>).</summary>
        WorldAhead = 1,
        /// <summary>The Ark crossed into the world it is bound for.</summary>
        WorldEntered = 2,
        /// <summary>An assault was announced; it starts in <see cref="ArkEscortStatus.AssaultSecondsLeft"/>.</summary>
        AssaultIncoming = 3,
        /// <summary>The world's fauna turned on the Ark.</summary>
        AssaultStarted = 4,
        /// <summary>The squad killed enough attackers near the Ark to drive the assault off.</summary>
        AssaultRepelled = 5,
        /// <summary>The assault ran out its time, or was called off (the world pacified, the Ark left it).</summary>
        AssaultPassed = 6,
        /// <summary>The hull started losing plates after a quiet spell.</summary>
        ArkUnderAttack = 7,
        /// <summary>The hull has not lost a plate for a few seconds.</summary>
        ArkAttackEased = 8,
        /// <summary>The hull's share changed (at most once a second).</summary>
        HullChanged = 9,
        /// <summary>The squad pacified a world: its fauna stop attacking and the Ark can heal in its nucleus.</summary>
        WorldPacified = 10,
        /// <summary>The escort ended (<see cref="ArkEscortStatus.Outcome"/>).</summary>
        Ended = 11,
    }

    /// <summary>
    /// A snapshot of one Ark voyage for the HUD (<see cref="IArkStatusSource.Status"/>: the Ark escort or a job voyage): where the Ark is, what is coming,
    /// how its hull stands against the breach line, and the assault in progress. Read it every frame, or on each
    /// <see cref="ArkVoyageSignals.Signal"/>. Facts only: every word on screen stays authored on the HUD's side.
    /// </summary>
    public struct ArkEscortStatus
    {
        /// <summary>True while an escort is sailing.</summary>
        public bool Active;
        /// <summary>1-9.</summary>
        public int Difficulty;
        /// <summary>The world the Ark is bound for or in, 0-based; -1 before the first.</summary>
        public int Leg;
        /// <summary>Worlds this voyage sails.</summary>
        public int Legs;
        /// <summary>The name of the world the Ark is bound for or in.</summary>
        public string WorldName;
        /// <summary>The world after it, empty on the last.</summary>
        public string NextWorldName;
        /// <summary>The Ark is inside the current world's membrane.</summary>
        public bool InsideWorld;
        /// <summary>The current world started hostile (its fauna attack the Ark until it is pacified).</summary>
        public bool WorldHostile;
        /// <summary>The squad pacified the current world.</summary>
        public bool WorldPacified;
        /// <summary>Distance from the Ark to the current world's core, -1 when unknown.</summary>
        public float DistanceToCore;
        /// <summary>The Ark may regrow plates now (inside the nucleus of a world the squad holds).</summary>
        public bool Healing;

        /// <summary>The hull's live share, 0-1.</summary>
        public float Hull;
        public int HullAlive;
        public int HullTotal;
        /// <summary>The Ark is overrun below this share; 0 = only the last plate.</summary>
        public float BreachLine;
        /// <summary>The hull lost a plate in the last few seconds.</summary>
        public bool UnderAttack;
        /// <summary>Live hostile creatures inside the escort's guard radius of the Ark.</summary>
        public int ThreatsNear;

        public ArkAssaultPhase Assault;
        /// <summary>Incoming: seconds to the attack. Active: seconds until it runs out.</summary>
        public float AssaultSecondsLeft;
        /// <summary>None, in a world that will be assaulted: seconds to the next warning; -1 otherwise.</summary>
        public float NextAssaultIn;
        public int AssaultKills;
        public int AssaultKillsNeeded;
        /// <summary>Plates the current assault has cost.</summary>
        public int AssaultPlatesLost;
        public int AssaultsRepelled;
        public int AssaultsPassed;

        /// <summary>Set once the escort ended.</summary>
        public ArkEscortReport.Outcome Outcome;
    }

    /// <summary>One event of an Ark escort, with the status at that moment.</summary>
    public readonly struct ArkEscortSignal
    {
        public readonly ArkEscortSignalKind Kind;
        public readonly ArkEscortStatus Status;

        public ArkEscortSignal(ArkEscortSignalKind kind, in ArkEscortStatus status)
        {
            Kind = kind;
            Status = status;
        }
    }

    /// <summary>
    /// The assault rhythm of one escort world, as pure state (no scene): a lull, a warning, then the world's fauna turn on
    /// the Ark until the squad kills enough of them near it or the assault runs out, then the next lull. The run drives it
    /// once a second and when an attacker dies; the lure (<c>Cell.SetFeedingLure</c>) follows <see cref="Phase"/>.
    /// </summary>
    public sealed class ArkAssaultClock
    {
        public struct Settings
        {
            public float FirstDelay;
            public float Warning;
            public float Duration;
            public float Gap;
            public int RepelKills;
            /// <summary>0 = this difficulty has no assaults.</summary>
            public float LureShare;
        }

        /// <summary>What a tick or a kill changed.</summary>
        public enum Change
        {
            None = 0,
            Incoming = 1,
            Started = 2,
            Repelled = 3,
            Passed = 4,
        }

        public ArkAssaultPhase Phase { get; private set; }
        public int Kills { get; private set; }
        public int KillsNeeded { get; private set; }
        public int Repelled { get; private set; }
        public int Passed { get; private set; }
        /// <summary>Plates lost during the current (or last) assault.</summary>
        public int PlatesLost { get; private set; }

        float _nextAt = -1f;   // the next warning, -1 when unscheduled
        float _phaseEndsAt;

        /// <summary>Forget everything (a new escort).</summary>
        public void Reset()
        {
            Phase = ArkAssaultPhase.None;
            Kills = KillsNeeded = Repelled = Passed = PlatesLost = 0;
            _nextAt = -1f;
            _phaseEndsAt = 0f;
        }

        /// <summary>A new world: whatever was going on ends quietly, and its first warning is <see cref="Settings.FirstDelay"/>
        /// after the clock first sees the world assaultable.</summary>
        public Change NewWorld()
        {
            var change = Phase != ArkAssaultPhase.None ? EndAssault(repelled: false) : Change.None;
            _nextAt = -1f;
            return change;
        }

        /// <summary>
        /// Advance to <paramref name="now"/>. <paramref name="canAssault"/>: the Ark is inside a hostile, unpacified world
        /// (an assault in progress is called off, as passed, when it stops being true).
        /// </summary>
        public Change Tick(float now, bool canAssault, in Settings s)
        {
            if (!canAssault || s.LureShare <= 0f)
            {
                _nextAt = -1f;
                return Phase != ArkAssaultPhase.None ? EndAssault(repelled: false) : Change.None;
            }
            switch (Phase)
            {
                case ArkAssaultPhase.None:
                    if (_nextAt < 0f) _nextAt = now + System.Math.Max(0f, s.FirstDelay);
                    if (now < _nextAt) return Change.None;
                    Phase = ArkAssaultPhase.Incoming;
                    _phaseEndsAt = now + System.Math.Max(0f, s.Warning);
                    Kills = 0;
                    PlatesLost = 0;
                    KillsNeeded = System.Math.Max(1, s.RepelKills);
                    return Change.Incoming;
                case ArkAssaultPhase.Incoming:
                    if (now < _phaseEndsAt) return Change.None;
                    Phase = ArkAssaultPhase.Active;
                    _phaseEndsAt = now + System.Math.Max(1f, s.Duration);
                    return Change.Started;
                default:
                    if (now < _phaseEndsAt) return Change.None;
                    _nextAt = now + System.Math.Max(1f, s.Gap);
                    return EndAssault(repelled: false);
            }
        }

        /// <summary>An attacker died near the Ark at <paramref name="now"/> (only counts while the assault is on).</summary>
        public Change NoteKill(float now, in Settings s)
        {
            if (Phase != ArkAssaultPhase.Active) return Change.None;
            Kills++;
            if (Kills < KillsNeeded) return Change.None;
            _nextAt = now + System.Math.Max(1f, s.Gap);
            return EndAssault(repelled: true);
        }

        /// <summary>A hull plate was lost.</summary>
        public void NotePlateLost()
        {
            if (Phase == ArkAssaultPhase.Active) PlatesLost++;
        }

        /// <summary>The lure share on the Ark's world now: the assault's while it is on, else <paramref name="baseShare"/>.</summary>
        public float LureShare(float baseShare, in Settings s) =>
            Phase == ArkAssaultPhase.Active ? System.Math.Max(baseShare, s.LureShare) : baseShare;

        /// <summary>Seconds left in the warning or the assault; 0 in the lull.</summary>
        public float SecondsLeft(float now) => Phase == ArkAssaultPhase.None ? 0f : System.Math.Max(0f, _phaseEndsAt - now);

        /// <summary>Seconds to the next warning in the lull, -1 when none is scheduled.</summary>
        public float NextIn(float now) =>
            Phase == ArkAssaultPhase.None && _nextAt >= 0f ? System.Math.Max(0f, _nextAt - now) : -1f;

        Change EndAssault(bool repelled)
        {
            // An announced assault that never started is dropped: it reports Passed (so a HUD clears its warning) but is not
            // counted as one the squad saw through.
            bool started = Phase == ArkAssaultPhase.Active;
            Phase = ArkAssaultPhase.None;
            if (!started) return Change.Passed;
            if (repelled) { Repelled++; return Change.Repelled; }
            Passed++;
            return Change.Passed;
        }
    }
}
