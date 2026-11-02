using System;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The one place a vessel says "an omni crystal I collected was just born as this creature" —
    /// the Angler's omni morph (<see cref="AnglerCommandExecutor.OmniMorph"/>) and the Jacks' worm
    /// (<see cref="VesselSpawnFaunaByCrystalEffectSO"/>). The fauna-birth crystal morph
    /// (<see cref="FaunaBirthCrystalMorph"/>, <c>R_VesselActions/FAUNA_BIRTH_CRYSTAL_MORPH.md</c>)
    /// listens here so the crystal folds into the newborn's heart instead of shattering.
    ///
    /// It lives on the VESSEL side on purpose: the creature is born through the ecology's one spawn
    /// seam (<see cref="CellLifeSpawnerBase.SpawnFaunaWithDomain"/>) exactly as before, and nothing in
    /// the ecology knows the morph exists. A listener that throws is isolated and logged, so a
    /// visual can never cost the pickup its creature.
    /// </summary>
    public static class CrystalFaunaBirth
    {
        /// <summary>Raised on the peer that spawned the creature, right after the spawn seam returned
        /// it (lineage, heart and colour already assigned).</summary>
        public static event Action<IVesselStatus, Fauna> Born;

        /// <summary>Raised when an omni pickup that could have produced a creature did not, for a
        /// reason that is ordinary play (the Angler's school is full, or a replicated species is
        /// born on another peer). The morph then fades its crystal quietly instead of warning.</summary>
        public static event Action<IVesselStatus, string> Declined;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Born = null;
            Declined = null;
        }

        public static void Decline(IVesselStatus status, string reason)
        {
            if (status == null) return;
            var declined = Declined;
            if (declined == null) return;
            foreach (Action<IVesselStatus, string> listener in declined.GetInvocationList())
            {
                try { listener(status, reason); }
                catch (Exception e) { CSDebug.LogError($"[CrystalFaunaBirth] a decline listener threw: {e}"); }
            }
        }

        public static void Announce(IVesselStatus status, Fauna creature)
        {
            if (status == null || !creature) return;
            var born = Born;
            if (born == null) return;
            foreach (Action<IVesselStatus, Fauna> listener in born.GetInvocationList())
            {
                try { listener(status, creature); }
                catch (Exception e)
                {
                    CSDebug.LogError($"[CrystalFaunaBirth] a listener threw on '{creature.name}': {e}. " +
                                     "The creature is unaffected.");
                }
            }
        }
    }
}
