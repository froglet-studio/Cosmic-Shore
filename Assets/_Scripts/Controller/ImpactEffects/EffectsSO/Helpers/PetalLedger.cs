using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE PER-ATTACKER PETAL LEDGER: for each pilot, how many petals OPPOSING pilots lost because of
    /// them. It is the balancing plan's score (<c>Docs/ElementalAbilitySystem/SILENCE.md</c> §6):
    /// <b>score(i) = the petals every opposing pilot loses that pilot i caused</b> - what i strips,
    /// what i steals, and what danger prisms i laid burn off opponents who fly into them. One petal is
    /// one point.
    ///
    /// <para><b>Where a credit comes from.</b> Only <see cref="ElementalTransfer.CreditTake"/> calls
    /// <see cref="Record"/>, and it does so with the number of WHOLE petals the victim actually lost
    /// (<c>ResourceSystem.AccrueElementalLoss</c>'s return), so a ward, an empty victim or a fraction
    /// still accruing credits nothing. That call is made once per take, on the one machine whose copy
    /// counts (the victim's owner), and a client forwards it to the server through the hull's relay,
    /// so the server's ledger holds every take of the match. <c>StatsManager</c> listens to
    /// <see cref="Credited"/> on the server and adds it to <c>IRoundStats.PetalsTaken</c>, which the
    /// scoreboard and a scoring rule (<c>ScoringMetric.PetalsTaken</c>) read, and the AI trainer through
    /// any fitness that reads a scoring metric.</para>
    ///
    /// <para><b>What does not count.</b> A take with no named attacker (an anonymous hit, a
    /// creature's own contact), a take of nothing, and a pilot's loss to themselves. A danger burn is
    /// only ever called for an opposing-domain prism (<c>VesselElementalDebuffByDangerPrismEffectSO</c>),
    /// and a strip or a steal only after <c>CombatHitDrain.TryAdmit</c> or the effect's own team check
    /// let the hit through, so the ledger does not re-decide who is an opponent.</para>
    ///
    /// <para>Pure and Netcode-free, so it compiles in <c>Tools/Build/elemental_transfer_harness</c>.</para>
    /// </summary>
    public static class PetalLedger
    {
        static readonly Dictionary<string, int> s_totals = new();

        /// <summary>Raised for every counted credit: the attacker's player name and the petals.</summary>
        public static event Action<string, int> Credited;

        /// <summary>Every attacker's total since the last <see cref="Reset"/>, keyed by player name.</summary>
        public static IReadOnlyDictionary<string, int> Totals => s_totals;

        /// <summary>The petals <paramref name="attackerName"/> has taken from opponents since the last
        /// <see cref="Reset"/>; 0 for a name the ledger has not seen.</summary>
        public static int TotalFor(string attackerName) =>
            !string.IsNullOrEmpty(attackerName) && s_totals.TryGetValue(attackerName, out int n) ? n : 0;

        /// <summary>Whether a take counts: a named attacker, at least one petal, and a victim who is
        /// somebody else.</summary>
        public static bool Counts(string attackerName, string victimName, int petals) =>
            petals > 0 && !string.IsNullOrEmpty(attackerName) && attackerName != victimName;

        /// <summary>Credits <paramref name="petals"/> to <paramref name="attackerName"/> when the take
        /// <see cref="Counts"/>. Returns whether it was credited.</summary>
        public static bool Record(string attackerName, string victimName, int petals)
        {
            if (!Counts(attackerName, victimName, petals)) return false;
            s_totals.TryGetValue(attackerName, out int n);
            s_totals[attackerName] = n + petals;
            Credited?.Invoke(attackerName, petals);
            return true;
        }

        /// <summary>Clears every total. <c>StatsManager</c> calls it when a match's stats are reset,
        /// so the totals are per match like <c>IRoundStats</c>.</summary>
        public static void Reset() => s_totals.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_totals.Clear();
    }
}
