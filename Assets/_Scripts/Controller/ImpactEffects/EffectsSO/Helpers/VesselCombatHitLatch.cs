using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Deduplicates landed vessel-vs-vessel hits, keyed by (shooter, victim, weapon class).
    ///
    /// TWO distinct sources of double-counting make this necessary, and they are why the latch
    /// is SHARED between the projectile effect and the explosion effect rather than each
    /// carrying its own:
    ///
    ///   1. <b>A rocket scores through two code paths for one shot.</b> A skyburst that hits a
    ///      vessel directly detonates on impact (<c>VesselSpinBySkyBurstProjectileEffectSO</c>),
    ///      so the direct hit fires from <c>ProjectileImpactor</c> and the blast fires again
    ///      from <c>ExplosionImpactor</c> a fraction of a second later - one missile, two
    ///      events, and at fifty points each that is not a rounding error.
    ///   2. <b>A hull is more than one collider.</b> The Squirrel carries two box colliders and
    ///      the Manta a body per wing, so a single blast sphere raises <c>OnTriggerEnter</c>
    ///      once per pair. <c>VesselImpactor</c> already latches crystals for exactly this
    ///      reason; this is the same trick for the combat path.
    ///
    /// The window is therefore also an anti-spam floor: two genuinely different rockets landing
    /// on the same pilot inside the window score once. That is deliberate - a dogfight should
    /// reward two hits a second apart, not a shotgun of simultaneous detonations.
    ///
    /// Keyed by NAME rather than by object reference because the pair must stay identified
    /// across a pooled projectile reissue and a respawned explosion instance, neither of which
    /// preserves a reference. Entries are pruned lazily so the dictionary cannot grow with a
    /// long match, and cleared wholesale on a scene change via <see cref="Clear"/>.
    /// </summary>
    public static class VesselCombatHitLatch
    {
        readonly struct Key : System.IEquatable<Key>
        {
            readonly string _shooter;
            readonly string _victim;
            readonly CombatHitClass _hitClass;

            public Key(string shooter, string victim, CombatHitClass hitClass)
            {
                _shooter = shooter;
                _victim = victim;
                // THE THREE MISSILE CLASSES SHARE ONE KEY. They are tiers of a single event -
                // one rocket's shockwave, blast and direct hit all reaching the same pilot -
                // so they must contend for one window; keying them apart would let a
                // centre-punch pay three times, which is the double-count this latch exists
                // to prevent. Bullet and Debuff keep their own keys.
                _hitClass = CombatHitClasses.IsMissile(hitClass) ? CombatHitClass.MissileDirect : hitClass;
            }

            public bool Equals(Key other) =>
                _hitClass == other._hitClass &&
                string.Equals(_shooter, other._shooter, System.StringComparison.Ordinal) &&
                string.Equals(_victim, other._victim, System.StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is Key k && Equals(k);

            public override int GetHashCode() =>
                System.HashCode.Combine(_shooter, _victim, (int)_hitClass);
        }

        readonly struct Entry
        {
            public readonly float Time;
            public readonly int Rank;
            public Entry(float time, int rank) { Time = time; Rank = rank; }
        }

        static readonly Dictionary<Key, Entry> _lastHit = new();

        // Keys are player-name strings that recur across sessions while Time.time restarts at 0,
        // so a stale stamp makes the FIRST hit of the next session read as inside the cooldown.
        // Only Bends/DogFight call Clear() — this covers every other mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();

        // Lazy prune: sweeping every N admissions keeps the dictionary bounded without paying
        // for a scan on the hot path. Sized well above any plausible simultaneous-pair count.
        const int PruneEvery = 128;
        static int _sincePrune;

        /// <summary>
        /// True exactly once per (shooter, victim, class) per <paramref name="cooldownSeconds"/>
        /// window - and, when true, claims the window. A non-positive cooldown disables the
        /// latch entirely (every contact admits), which is how a weapon opts out.
        /// </summary>
        public static bool TryAdmit(string shooterName, string victimName, CombatHitClass hitClass, float cooldownSeconds)
            => TryAdmit(shooterName, victimName, hitClass, cooldownSeconds, out _);

        /// <summary>
        /// Latch admission, with the UPGRADE rule the three missile tiers need.
        ///
        /// <para><paramref name="supersededRank"/> reports what this admission REPLACES: 0 for a
        /// fresh hit, and the previously-paid missile rank when a closer tier lands inside an
        /// open window. A caller that awards points uses it to credit only the DIFFERENCE, so
        /// one rocket pays its best tier against a victim exactly once.</para>
        ///
        /// <para><b>Why an upgrade and not first-wins.</b> A rocket's three radii arrive in an
        /// order set by geometry, not by value: the warhead is both the largest and the fastest
        /// to expand, so on an ordinary proximity kill the CHEAPEST tier lands first. Under
        /// first-wins it would claim the window and a victim who was also inside the blast - or
        /// took the round on the nose - would be paid as if they had merely been clipped. The
        /// tiers exist to price how close the rocket got, so the latch has to be able to
        /// revise upward. It never revises DOWN: a shockwave arriving after a direct hit is
        /// the same rocket's outer edge and is refused.</para>
        /// </summary>
        public static bool TryAdmit(string shooterName, string victimName, CombatHitClass hitClass,
                                    float cooldownSeconds, out int supersededRank)
        {
            supersededRank = 0;
            if (string.IsNullOrEmpty(shooterName) || string.IsNullOrEmpty(victimName)) return false;
            if (cooldownSeconds <= 0f) return true;

            var key = new Key(shooterName, victimName, hitClass);
            float now = Time.time;
            int rank = CombatHitClasses.MissileProximityRank(hitClass);

            if (_lastHit.TryGetValue(key, out Entry last) && now - last.Time < cooldownSeconds)
            {
                // Inside an open window. Only a strictly CLOSER missile tier may re-admit, and
                // it reports what it is replacing so the caller pays the difference rather
                // than the whole tier again.
                if (rank <= last.Rank) return false;
                supersededRank = last.Rank;
            }

            _lastHit[key] = new Entry(now, rank);

            if (++_sincePrune >= PruneEvery)
            {
                _sincePrune = 0;
                Prune(now, cooldownSeconds);
            }
            return true;
        }

        static void Prune(float now, float cooldownSeconds)
        {
            // Iterating to a scratch list rather than mutating during enumeration; the map is
            // small by construction (one entry per live shooter/victim/class triple).
            var stale = new List<Key>();
            foreach (var kvp in _lastHit)
                if (now - kvp.Value.Time >= cooldownSeconds) stale.Add(kvp.Key);

            for (int i = 0; i < stale.Count; i++)
                _lastHit.Remove(stale[i]);
        }

        /// <summary>
        /// Drops every claimed window. Called on turn start so a replay cannot inherit a latch
        /// from the previous match (<see cref="Time.time"/> keeps running across a scene load,
        /// so a stale entry would otherwise expire on its own but a REPLAY within the window
        /// would silently eat the first hit of the new match).
        /// </summary>
        public static void Clear()
        {
            _lastHit.Clear();
            _sincePrune = 0;
        }
    }
}
