using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Posts a menu toast when a reward lands outside a match, so a reward earned in the menu
    /// is not silent.
    ///
    /// Deliberately does NOT catch up on a grant announced while it was inactive - that is
    /// <see cref="RewardPayoutPanel"/>'s job, and the payout for a match the player just
    /// finished has already been shown to them on the end-game screen. This driver reports
    /// only what happens while the menu is up.
    ///
    /// STATUS: correct and currently silent. The only producer in the game today is the
    /// end-game placement payout, which raises in a gameplay scene. This is the surface a
    /// daily-challenge, quest or milestone payout posts through the day one is wired - see
    /// Docs/REWARD_SYSTEM.md for the paths that are designed and not yet granting.
    /// </summary>
    public class RewardToastDriver : MonoBehaviour
    {
        [Header("Channels")]
        [Tooltip("Resources/Channels/RewardGrantedChannel. Raised by RewardService whenever a " +
                 "reward actually lands.")]
        [SerializeField] ScriptableEventRewardGranted rewardChannel;
        [Tooltip("The menu's ToastChannel asset - the same one every other menu toast posts on.")]
        [SerializeField] ToastChannel toastChannel;

        [Header("Copy")]
        [Tooltip("Leading line. The amount is appended as the postfix.")]
        [SerializeField] string crystalPrefix = "Reward earned";
        [Tooltip("Format for the amount, {0} = crystals earned.")]
        [SerializeField] string crystalPostfixFormat = "+{0} crystals";
        [SerializeField] float duration = 3.5f;
        [Tooltip("Optional crystal sprite shown on the toast.")]
        [SerializeField] Sprite crystalIcon;

        void OnEnable() => rewardChannel.OnRaised += HandleRewardGranted;

        void OnDisable() => rewardChannel.OnRaised -= HandleRewardGranted;

        void HandleRewardGranted(RewardGranted granted)
        {
            if (!toastChannel) return;

            // Crystals only for now. An entitlement (a skin, a toy) is a real reward and wants
            // its own copy and its own art rather than being announced as a number - adding it
            // means a branch here, not a change to the channel.
            if (granted.Grant.Kind != RewardKind.Crystals || granted.CrystalDelta <= 0)
                return;

            toastChannel.ShowPrefixPostfix(
                crystalPrefix,
                string.Format(crystalPostfixFormat, granted.CrystalDelta),
                duration,
                ToastAnimation.ChatSubtleSlide,
                crystalIcon);
        }
    }
}
