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
                _hitClass = hitClass;
            }

            public bool Equals(Key other) =>
                _hitClass == other._hitClass &&
                string.Equals(_shooter, other._shooter, System.StringComparison.Ordinal) &&
                string.Equals(_victim, other._victim, System.StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is Key k && Equals(k);

            public override int GetHashCode() =>
                System.HashCode.Combine(_shooter, _victim, (int)_hitClass);
        }

        static readonly Dictionary<Key, float> _lastHitTime = new();

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
        {
            if (string.IsNullOrEmpty(shooterName) || string.IsNullOrEmpty(victimName)) return false;
            if (cooldownSeconds <= 0f) return true;

            var key = new Key(shooterName, victimName, hitClass);
            float now = Time.time;

            if (_lastHitTime.TryGetValue(key, out float last) && now - last < cooldownSeconds)
                return false;

            _lastHitTime[key] = now;

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
            foreach (var kvp in _lastHitTime)
                if (now - kvp.Value >= cooldownSeconds) stale.Add(kvp.Key);

            for (int i = 0; i < stale.Count; i++)
                _lastHitTime.Remove(stale[i]);
        }

        /// <summary>
        /// Drops every claimed window. Called on turn start so a replay cannot inherit a latch
        /// from the previous match (<see cref="Time.time"/> keeps running across a scene load,
        /// so a stale entry would otherwise expire on its own but a REPLAY within the window
        /// would silently eat the first hit of the new match).
        /// </summary>
        public static void Clear()
        {
            _lastHitTime.Clear();
            _sincePrune = 0;
        }
    }
}
