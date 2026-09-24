using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Toy Box's own two numbers: what a day's activity pays, and how the two big buttons at
    /// the top of the window are captioned.
    ///
    /// <para><b>There is deliberately no POOL here.</b> The day's activity is drawn from the LIVE
    /// toys' own activity options, never from a list authored on an asset - the same rule the whole
    /// Toy Box is built on (<c>Docs/HomeHub/ARCHITECTURE.md</c> §4.1.1: the menu never gets its own
    /// copy of what a toy does). An authored list of "Connect the Dots &gt; Rainbow" rows would be a
    /// second authority on what activities exist, and it would go out of step the first time a
    /// painting was renamed or added.</para>
    ///
    /// <para><b>Every field has a code default and the asset is OPTIONAL.</b> A missing
    /// <c>Resources/ToyboxDailyActivityConfig</c> costs the feature nothing - the reward still pays
    /// and the buttons still read correctly - because a config that can make a shipped surface
    /// blank is worse than no config. Same shape as <c>ElementalBarsConfigSO</c> and
    /// <c>CrystalCaptureConfigSO</c>.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "ToyboxDailyActivityConfig",
        menuName = "ScriptableObjects/Toys/Toybox Daily Activity Config")]
    public class ToyboxDailyActivityConfigSO : ScriptableObject
    {
        /// <summary>Where <see cref="Instance"/> looks. A path, not a folder scan.</summary>
        public const string ResourcePath = "ToyboxDailyActivityConfig";

        public const int DefaultRewardCrystals = 10;

        [Header("Reward")]
        [SerializeField, Min(0), Tooltip(
            "Crystals paid the first time the player STARTS a day's activity. Paid for TRYING it, " +
            "not for finishing it - a painting has no pass mark and a wander has no end, so " +
            "there is nothing here to complete. 0 makes the button a prompt with no reward.")]
        int rewardCrystals = DefaultRewardCrystals;

        [Header("Copy")]
        [SerializeField, Tooltip("Heading on the daily-activity button.")]
        string activityTitle = "TODAY'S ACTIVITY";

        [SerializeField, Tooltip("Heading on the shuffle button.")]
        string shuffleTitle = "SHUFFLE THE TOY BOX";

        [SerializeField, Tooltip("Second line on the shuffle button - what a press will do.")]
        string shuffleDetail = "NEW WORLD, NEW COLOURS, NEW HULL";

        /// <summary>Crystals a day's first start pays. Never negative.</summary>
        public int RewardCrystals => Mathf.Max(0, rewardCrystals);

        public string ActivityTitle => Fallback(activityTitle, "TODAY'S ACTIVITY");
        public string ShuffleTitle => Fallback(shuffleTitle, "SHUFFLE THE TOY BOX");
        public string ShuffleDetail => Fallback(shuffleDetail, "NEW WORLD, NEW COLOURS, NEW HULL");

        static string Fallback(string value, string ifBlank) =>
            string.IsNullOrWhiteSpace(value) ? ifBlank : value;

        static ToyboxDailyActivityConfigSO _instance;

        /// <summary>
        /// The asset, or null when none is authored. Callers must survive null - see the class
        /// note. Cached, and cleared on a domain reload so Enter Play Mode Options cannot hand the
        /// next session a destroyed asset.
        /// </summary>
        public static ToyboxDailyActivityConfigSO Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<ToyboxDailyActivityConfigSO>(ResourcePath);
                return _instance;
            }
        }

        /// <summary>Reward for a day's first start, with the code default applied.</summary>
        public static int EffectiveRewardCrystals =>
            Instance != null ? Instance.RewardCrystals : DefaultRewardCrystals;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;
    }
}
