using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The game's payout numbers, in ONE asset (<c>Resources/RewardTable</c>).
    ///
    /// These used to live as a serialized <c>List&lt;int&gt;</c> on the <c>Scoreboard</c>
    /// component, which put the economy inside a UI prefab and duplicated it across nine
    /// gameplay scenes - retuning meant editing nine scenes, and five more surfaces had already
    /// drifted onto a retired field and were paying out of the C# field initializer instead.
    /// A payout is config, so it lives in config.
    ///
    /// Source of truth for the values: <c>Docs/ECONOMY_TABLES.md</c> Table 2.
    /// </summary>
    [CreateAssetMenu(fileName = "RewardTable", menuName = "ScriptableObjects/Economy/Reward Table")]
    public class RewardTableSO : ScriptableObject
    {
        public const string ResourcePath = "RewardTable";

        [Header("Match placement payout")]
        [Tooltip("Crystals by finishing place, best first: index 0 = 1st, index 1 = 2nd, and so " +
                 "on. Places past the end of the list earn 0. Applies to every mode, tournament " +
                 "included - see Docs/ECONOMY_TABLES.md Table 2.")]
        [SerializeField] List<int> placementCrystals = new() { 200, 50, 0 };

        [Tooltip("When on, the LAST place in the field always earns 0 regardless of what the " +
                 "table would pay it. With two domains that makes the runner-up a loser rather " +
                 "than a silver medallist, which is the intended read.")]
        [SerializeField] bool lastPlaceAlwaysEarnsNothing = true;

        static RewardTableSO s_instance;
        static bool s_loadAttempted;

        // Domain reload with "Reload Domain" off keeps statics alive between play sessions. If
        // s_instance ever goes null after the first attempt, the latch would otherwise skip
        // Resources.Load forever and silently serve CreateInstance code defaults.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_loadAttempted = false;
        }

        /// <summary>
        /// The one table. Falls back to an in-memory instance carrying the authored defaults
        /// above, so a missing asset degrades to the shipped numbers rather than to a silent
        /// economy of zero.
        /// </summary>
        public static RewardTableSO Instance
        {
            get
            {
                if (s_instance) return s_instance;
                if (!s_loadAttempted)
                {
                    s_loadAttempted = true;
                    s_instance = Resources.Load<RewardTableSO>(ResourcePath);
                }
                if (!s_instance)
                    s_instance = CreateInstance<RewardTableSO>();
                return s_instance;
            }
        }

        /// <summary>
        /// Crystals earned by finishing <paramref name="placeIndex"/> (0 = 1st) in a field of
        /// <paramref name="fieldSize"/>. The whole payout policy is here rather than at the call
        /// site, so a second producer cannot implement "last place earns nothing" differently.
        /// </summary>
        public int CrystalsForPlace(int placeIndex, int fieldSize)
        {
            if (placeIndex < 0) return 0;
            if (placementCrystals == null || placementCrystals.Count == 0) return 0;

            if (lastPlaceAlwaysEarnsNothing && fieldSize > 1 && placeIndex == fieldSize - 1)
                return 0;

            return placeIndex < placementCrystals.Count
                ? Mathf.Max(0, placementCrystals[placeIndex])
                : 0;
        }

        /// <summary>
        /// The placement payout as a ready-to-grant <see cref="RewardGrant"/>. Repeatable by
        /// design - a placement is earned every game.
        /// </summary>
        public RewardGrant PlacementGrant(int placeIndex, int fieldSize, string source)
            => RewardGrant.Crystals(CrystalsForPlace(placeIndex, fieldSize), source);

        /// <summary>Read-only view of the authored table, for tests and editor tooling.</summary>
        public IReadOnlyList<int> PlacementCrystals => placementCrystals;
    }
}
