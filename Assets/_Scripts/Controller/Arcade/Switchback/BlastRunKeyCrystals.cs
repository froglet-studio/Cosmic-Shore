using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Where Blast Run's KEY crystals are (<see cref="SwitchbackBlastRun"/>), so the Dolphin's crystal blast can tell
    /// a key from any other crystal at the moment it fires. A key's blast is wider: its core is scaled by
    /// <see cref="KeyCoreMultiplier"/> through <see cref="VesselExplosionByCrystalEffectSO"/> (a runtime multiplier on
    /// the effect, never an edit to the shared DolphinVesselExplosionByCrystalEffect asset), so one key clears the
    /// door's full port and a worthwhile tunnel through the world. Everywhere else, and whenever no Blast Run is
    /// live, <see cref="CoreMultiplierFor"/> is exactly 1.
    ///
    /// <para>Static state: registered by <see cref="SwitchbackController"/> on every peer when the course is raised,
    /// cleared when it is torn down, and on domain reload (<see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>)
    /// so an Editor play session without a domain reload never inherits stale keys.</para>
    /// </summary>
    public static class BlastRunKeyCrystals
    {
        /// <summary>The key blast's core multiplier. The blast's length is untouched (it already reaches 400 u at its
        /// weakest, past every <see cref="SwitchbackBlastRun.KeyLeadMax"/>); only its width grows.</summary>
        public const float KeyCoreMultiplier = 1.25f;

        /// <summary>u within which an explosion origin counts as a key's. A crystal is collected where it floats
        /// (its respawn nudge is 1.5 u), and the blast's origin is the crystal's position.</summary>
        public const float MatchRadius = 4f;

        static readonly List<Vector3> Keys = new(32);

        public static int Count => Keys.Count;

        public static void Register(IEnumerable<Vector3> keys)
        {
            Keys.Clear();
            if (keys == null) return;
            foreach (var k in keys) Keys.Add(k);
        }

        public static void Clear() => Keys.Clear();

        /// <summary>1, or <see cref="KeyCoreMultiplier"/> when <paramref name="origin"/> is a registered key's.</summary>
        public static float CoreMultiplierFor(Vector3 origin)
        {
            if (Keys.Count == 0) return 1f;
            const float r2 = MatchRadius * MatchRadius;
            for (int i = 0; i < Keys.Count; i++)
                if ((Keys[i] - origin).sqrMagnitude <= r2)
                    return KeyCoreMultiplier;
            return 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Keys.Clear();
    }
}
