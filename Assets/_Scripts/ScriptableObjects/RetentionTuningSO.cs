using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The numbers of the retention build (Garrett, 2026-11-21: "Build everything proposed. Get all personas retained"):
    /// what a party, a voyage, a painting, a daily job and a returning player are paid. One asset at
    /// <c>Assets/Resources/RetentionTuning.asset</c>, loaded without wiring like the other fleet-wide configs in that
    /// folder. Every number names the Loop Atlas proposal it serves (Docs/LoopAtlas/model/proposals.yaml) and the
    /// simulation that sized it (Docs/LoopAtlas/sim/economy.yaml), so a tuning pass starts from the reason.
    /// </summary>
    [CreateAssetMenu(fileName = "RetentionTuning", menuName = "ScriptableObjects/Retention Tuning")]
    public class RetentionTuningSO : ScriptableObject
    {
        public const string ResourcePath = "RetentionTuning";

        [Header("Party pay (arc.social hole)")]
        [Tooltip("A party match pays every party member by the party's best placing, so the friend who wins least still " +
                 "grows their fleet at the group's pace. Off: each player is paid by their own domain's place.")]
        [SerializeField] bool payPartyByBestPlacing = true;

        [Header("Voyage pay (P10, one currency)")]
        [Tooltip("Wallet crystals (the ones that buy vessels) a docking banks for every job finished since the last dock. " +
                 "The sim's 'a clean voyage is about three arcade wins' is 600.")]
        [SerializeField, Min(0)] int crystalsPerVoyageJob = 600;

        [Tooltip("The most wallet crystals one docking can bank (0 = no limit). Matches the server's per-call limit.")]
        [SerializeField, Min(0)] int voyageDockCap = 1800;

        [Header("Painting pay (P16, making pays)")]
        [Tooltip("Wallet crystals a finished Toybox painting pays, so making feeds the long game. 0 pays nothing.")]
        [SerializeField, Min(0)] int crystalsPerPainting = 150;

        [Tooltip("How many finished paintings a day are paid (a painter is paid for making, not farming).")]
        [SerializeField, Min(0)] int paintingsPaidPerDay = 2;

        public bool PayPartyByBestPlacing => payPartyByBestPlacing;
        public int CrystalsPerPainting => crystalsPerPainting;
        public int PaintingsPaidPerDay => paintingsPaidPerDay;
        public int CrystalsPerVoyageJob => crystalsPerVoyageJob;
        public int VoyageDockCap => voyageDockCap;

        static RetentionTuningSO _cached;

        /// <summary>The project's asset, cached; the built-in defaults (the shipped values) when it is missing.</summary>
        public static RetentionTuningSO Load()
        {
            if (_cached) return _cached;
            _cached = Resources.Load<RetentionTuningSO>(ResourcePath);
            if (!_cached)
            {
                CSDebug.LogWarning($"[Retention] Resources/{ResourcePath} is missing - the retention economy runs on its " +
                                   "built-in defaults and edits to the asset will not apply.");
                _cached = CreateInstance<RetentionTuningSO>();
            }
            return _cached;
        }
    }
}
