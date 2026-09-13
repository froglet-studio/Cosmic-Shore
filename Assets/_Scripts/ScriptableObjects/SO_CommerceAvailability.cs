using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// A surface that would take the player's money, or spend a balance through the PlayFab
    /// catalog. Named here rather than per-screen so the posture below can be read by one lookup.
    /// </summary>
    public enum CommerceSurface
    {
        /// <summary>The Store screen and its nav-bar entry (<c>MenuScreens.STORE</c>).</summary>
        StoreScreen = 0,

        /// <summary>
        /// Episodes: the panel, its Support Us button, and any episode card whose
        /// <c>priceUsd</c> makes it a real-money buy button.
        /// </summary>
        Episodes = 1,

        /// <summary>
        /// Anything that opens <see cref="PurchaseConfirmationModal"/> — the Store's purchase
        /// cards and the Hangar's captain upgrade. Deliberately ONE surface, because that modal
        /// is what they have in common and it is the thing that must become unreachable.
        /// </summary>
        CatalogPurchase = 2,
    }

    /// <summary>
    /// What the commerce surfaces currently do — the ONE authority for the de-scope that ships in
    /// the invite build, so the paid-EA conversion is one asset edit rather than a hunt across two
    /// scenes and three prefabs (<c>Docs/STEAM_RELEASE_TASKS.md</c> R4).
    ///
    /// <para><b>It says WHICH state each surface is in; it never says what that state looks
    /// like.</b> The look and the refusal belong to <see cref="MenuAvailabilityView"/>, which is
    /// the one place a <see cref="MenuAvailability"/> becomes pixels and a response — so this
    /// asset cannot become a second locked look. <see cref="Mark"/> and <see cref="TryPress"/>
    /// both route through it.</para>
    ///
    /// <para><b>The defaults are the DE-SCOPED state, which inverts the usual fallback rule.</b>
    /// Every other config in the project falls back to the behaviour that shipped before it
    /// existed; this one must not. A missing or unassigned asset here would otherwise re-open a
    /// screen that takes money, and a money surface that comes back because an asset failed to
    /// load is the one failure nobody would see until a player hit it. So an absent asset fails
    /// CLOSED, and the conversion is a deliberate edit.</para>
    ///
    /// <para><b>Two layers, on purpose.</b> The presentation layer marks an entry so the player
    /// can see it is not open (<see cref="Mark"/>); the action layer refuses the press even if
    /// nothing marked it (<see cref="TryPress"/>, and the service-side gates in
    /// <c>IAPManager.OpenCheckout</c>). This is the same split <c>OfflineUIGate</c> records: gate
    /// the UI so a player is never offered something that cannot work, and never rely on the UI
    /// alone to enforce it.</para>
    ///
    /// <para><b>Not the soft-currency loop.</b> Crystals earned from match placement
    /// (<c>Scoreboard</c> → <c>PlayerDataService.AddCrystals</c>) and spent on vessel unlocks
    /// (<c>VesselUnlockSystem.TryPurchaseVessel</c> → <c>TrySpendCrystals</c>) run through UGS and
    /// touch none of the surfaces above. Nothing here reaches them, and nothing here should grow
    /// a field that does.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "CommerceAvailability",
                     menuName = "ScriptableObjects/Commerce Availability", order = 0)]
    public class SO_CommerceAvailability : ScriptableObject
    {
        [Header("Store")]
        [SerializeField, Tooltip("The Store screen and its nav-bar entry. Unavailable while " +
                                 "nothing in it is purchasable: 'this is not built' is the honest " +
                                 "state, and it takes the entry out of navigation rather than " +
                                 "teasing a screen of catalog cards that cannot be bought.")]
        MenuAvailability storeScreen = MenuAvailability.Unavailable;

        [SerializeField, Tooltip("Reason the Store nav entry gives when pressed. Empty by default " +
                                 "because Unavailable has nothing to say - it reads as inert, and " +
                                 "the dimming is the whole message.")]
        string storeScreenMessage = "";

        [Header("Episodes")]
        [SerializeField, Tooltip("The episode panel, its Support Us button and any priced episode " +
                                 "card. Locked rather than Unavailable: the episodes exist as " +
                                 "content and the entitlement is real, so 'you cannot buy one YET' " +
                                 "is exactly true.")]
        MenuAvailability episodes = MenuAvailability.Locked;

        [SerializeField, Tooltip("Reason an episode or Support Us press gives.")]
        string episodesMessage = "Episodes arrive with the full release.";

        [Header("Catalog purchases")]
        [SerializeField, Tooltip("Every path that opens the purchase confirmation modal - the " +
                                 "Store's purchase cards and the Hangar's captain upgrade.")]
        MenuAvailability catalogPurchase = MenuAvailability.Locked;

        [SerializeField, Tooltip("Reason a purchase press gives.")]
        string catalogPurchaseMessage = "Purchases open with the full release.";

        [Header("Real-money checkout (service gate)")]
        [SerializeField, Tooltip("Off stands IAPManager's hosted web checkout down at the choke " +
                                 "point BOTH of its entry points share, so an un-gated screen " +
                                 "still cannot open a payment page. This is the action layer, not " +
                                 "a look - leave it off until a backend verifies orders " +
                                 "(Docs/MENU_PROGRESSION_AND_IAP.md, section 5).")]
        bool allowRealMoneyCheckout = false;

        /// <summary>What <paramref name="surface"/> currently does when pressed.</summary>
        public MenuAvailability AvailabilityFor(CommerceSurface surface) => surface switch
        {
            CommerceSurface.StoreScreen => storeScreen,
            CommerceSurface.Episodes => episodes,
            _ => catalogPurchase,
        };

        /// <summary>The reason <paramref name="surface"/> gives when a Locked press refuses.</summary>
        public string MessageFor(CommerceSurface surface) => surface switch
        {
            CommerceSurface.StoreScreen => storeScreenMessage,
            CommerceSurface.Episodes => episodesMessage,
            _ => catalogPurchaseMessage,
        };

        /// <summary>True while <paramref name="surface"/> does its job.</summary>
        public bool IsAvailable(CommerceSurface surface)
            => AvailabilityFor(surface) == MenuAvailability.Available;

        /// <summary>
        /// True while <c>IAPManager</c> may open a hosted checkout page. The service gate, read
        /// where both purchase entry points meet rather than at either of them.
        /// </summary>
        public bool AllowRealMoneyCheckout => allowRealMoneyCheckout;

        // ------------------------------------------------------------------
        // Instance

        const string ResourcePath = "CommerceAvailability";
        static SO_CommerceAvailability s_instance;
        static bool s_loadAttempted;

        // If s_instance ever goes null after the first attempt, the latch would otherwise skip
        // Resources.Load forever and serve the code defaults. Those defaults are the de-scoped
        // state, so that is safe rather than dangerous - but it would also make an authored
        // conversion stop taking effect, which is the same surprise in the other direction.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_loadAttempted = false;
        }

        /// <summary>
        /// The shipped posture. Falls back to an in-memory instance carrying the de-scoped
        /// defaults above, so a missing asset closes the money surfaces rather than opening them.
        /// </summary>
        public static SO_CommerceAvailability Instance
        {
            get
            {
                if (s_instance) return s_instance;
                if (!s_loadAttempted)
                {
                    s_loadAttempted = true;
                    s_instance = Resources.Load<SO_CommerceAvailability>(ResourcePath);
                }
                if (!s_instance)
                    s_instance = CreateInstance<SO_CommerceAvailability>();
                return s_instance;
            }
        }

        // ------------------------------------------------------------------
        // The two seams every de-scoped surface uses

        /// <summary>
        /// Gives <paramref name="host"/> the shared locked look for <paramref name="surface"/> and
        /// returns its view. Structural rather than authored — like
        /// <c>ScreenSwitcher.MarkDisabledNavLinks</c>, so a surface nobody has opened in the editor
        /// still gets its state and the conversion needs no scene edit. Idempotent.
        /// </summary>
        public static MenuAvailabilityView Mark(GameObject host, CommerceSurface surface)
        {
            var view = MenuAvailabilityView.Ensure(host);
            if (!view) return null;

            var config = Instance;
            view.SetLockedMessage(config.MessageFor(surface));
            view.SetAvailability(config.AvailabilityFor(surface));
            return view;
        }

        /// <summary>
        /// Ask before acting. Returns true when the caller should go ahead; returns false having
        /// ALREADY presented the refusal, so no caller repeats the sting or the wording. Marks
        /// <paramref name="host"/> on the way through, so a press is refused correctly even if
        /// nothing marked it at Start.
        /// </summary>
        public static bool TryPress(GameObject host, CommerceSurface surface)
        {
            var view = Mark(host, surface);

            // No host to hang a view on is not permission: answer from the posture itself. A
            // refusal with no sting is still a refusal, and silently proceeding would be the one
            // outcome this class exists to prevent.
            return view ? view.TryPress() : Instance.IsAvailable(surface);
        }
    }
}
