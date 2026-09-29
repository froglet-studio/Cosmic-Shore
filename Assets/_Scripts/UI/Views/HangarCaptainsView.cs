using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using Reflex.Attributes;

namespace CosmicShore.UI
{
    public class HangarCaptainsView : View
    {
        [Inject] CaptainManager _captainManager;
        [Header("Captain Details")]
        [SerializeField] TMP_Text SelectedCaptainName;
        [SerializeField] TMP_Text SelectedCaptainElementLabel;
        [SerializeField] TMP_Text SelectedCaptainQuote;
        [SerializeField] Image SelectedCaptainImage;
        [SerializeField] Image SelectedCaptainShipImage;

        [Header("Captains - Upgrades UI")]
        [SerializeField] Transform UnencounteredCaptainRequirementsContainer;
        [SerializeField] Transform LockedCaptainRequirementsContainer;
        [SerializeField] Transform UpgradeCaptainRequirementsContainer;
        [SerializeField] Transform CaptainSelectionContainer; // TODO: convert to a list of cards
        [SerializeField] TMP_Text SelectedUpgradeDescription; 
        [SerializeField] TMP_Text SelectedUpgradeXPRequirement;
        [SerializeField] TMP_Text SelectedUpgradeCrystalRequirement;
        [SerializeField] Image SelectedUpgradeCrystalRequirementImage;
        [SerializeField] Button UpgradeButton;
        [SerializeField] Button GoToStoreButton;
        [SerializeField] Sprite UpgradeButtonLockedSprite;
        [SerializeField] Sprite UpgradeButtonUnlockedSprite;
        [SerializeField] MenuAudio UpgradeMenuAudio;
        [SerializeField] MenuAudio DeniedMenuAudio;

        [SerializeField] public PurchaseConfirmationModal ConfirmationModal;

        [SerializeField] Button EncounterButton;

        bool crystalRequirementSatisfied = false;
        bool xpRequirementSatisfied = false;
        bool initialized;
        VirtualItem upgrade;
        Captain captain;

        const string SatisfiedMarkdownColor = "FFF"; 
        const string UnsatisfiedMarkdownColor = "888"; 

        const string CrystalRequirementTemplate = "<color=#{2}>{0}</color> / {1}";
        const string XPRequirementTemplate = "<color=#{2}>{0}</color> / {1} XP";

        void Start()
        {
            CaptainManager.OnLoadCaptainData += NewCaptainData;
            MarkDeScopedCommerceAffordances();
        }

        /// <summary>
        /// Gives the two commerce affordances on this view the app shell's shared locked look while
        /// the de-scope holds (<c>Docs/STEAM_RELEASE_TASKS.md</c> R4) — the upgrade button, which is
        /// the Hangar's path into the purchase confirmation modal, and the Go To Store button, which
        /// points at a screen that is no longer navigable.
        ///
        /// <para>Structural rather than authored, like <c>ScreenSwitcher.MarkDisabledNavLinks</c>, so
        /// the conversion needs no scene edit. Both buttons are <c>SetActive</c>-toggled by
        /// <see cref="UpdateView"/> and the view re-applies on every enable, so marking once here is
        /// enough.</para>
        ///
        /// <para><b>They deliberately take DIFFERENT surfaces, and the reading differs with them.</b>
        /// The upgrade button is a purchase, so it is <c>Locked</c> — pressable, refusing with a
        /// reason, because there is something to say. Go To Store points at a screen that does not
        /// exist (<c>MenuScreens.STORE</c> has no entry in <c>ScreenSwitcher.screens</c> at all), so it
        /// takes the store's own <c>Unavailable</c> and reads as inert: <i>this is not built</i> has
        /// nothing to add, and the dimming is the whole message. Do not "improve" it into a Locked
        /// button with a toast — that would claim the store is coming, which is a promise this build
        /// is not making.</para>
        ///
        /// <para>It is only the CAPTAIN UPGRADE that is locked. Captain upgrades are priced in the
        /// PlayFab catalog (<c>CatalogManager</c>), which the UGS auth migration left inert — so the
        /// upgrade was already refusing, with a bare denied sting and no explanation, which is the
        /// "reads as broken rather than unfinished" state this de-scope exists to fix. The live
        /// soft-currency loop is elsewhere and untouched: crystals are earned in <c>Scoreboard</c>
        /// and spent on vessels by <c>VesselUnlockSystem</c>, neither of which passes through here.</para>
        /// </summary>
        void MarkDeScopedCommerceAffordances()
        {
            if (UpgradeButton)
                SO_CommerceAvailability.Mark(UpgradeButton.gameObject, CommerceSurface.CatalogPurchase);

            if (GoToStoreButton)
                SO_CommerceAvailability.Mark(GoToStoreButton.gameObject, CommerceSurface.StoreScreen);
        }

        void OnDisable()
        {
            CaptainManager.OnLoadCaptainData -= NewCaptainData;
        }

        void NewCaptainData()
        {
            if (initialized)
                UpdateView();
        }

        public override void AssignModels(List<ScriptableObject> Models)
        {
            base.AssignModels(Models);
            PopulateCaptainSelectionList();
            initialized = true;
        }
        public override void UpdateView()
        {
            var model = SelectedModel as SO_Captain;
            captain = _captainManager.GetCaptainByName(model.Name);

            EncounterButton.gameObject.SetActive(false);
            xpRequirementSatisfied = false;
            crystalRequirementSatisfied = false;

            // Populate Captain Details
            SelectedCaptainName.text = captain.Name;
            SelectedCaptainElementLabel.text = "The " + captain.PrimaryElement.ToString() + " " + captain.Vessel.Name;
            SelectedUpgradeDescription.text = captain.Description;
            SelectedCaptainQuote.text = captain.Flavor;
            SelectedCaptainImage.sprite = captain.Image;
            SelectedCaptainImage.color = Color.white;
            SelectedCaptainShipImage.sprite = captain.Vessel.IconActive;

            //
            // Populate Requirements Box
            //
            UnencounteredCaptainRequirementsContainer.gameObject.SetActive(false);
            LockedCaptainRequirementsContainer.gameObject.SetActive(false);
            UpgradeCaptainRequirementsContainer.gameObject.SetActive(false);

            if (!captain.Encountered)
            {
                UnencounteredCaptainRequirementsContainer.gameObject.SetActive(true);

                SelectedCaptainImage.color = Color.black;

                // TODO: remove once testing is complete
                EncounterButton.gameObject.SetActive(true);
                EncounterButton.onClick.RemoveAllListeners();
                EncounterButton.onClick.AddListener(() => _captainManager.EncounterCaptain(captain.Name));

                GoToStoreButton.gameObject.SetActive(false);
                UpgradeButton.gameObject.SetActive(true);
                UpgradeButton.GetComponent<Image>().sprite = UpgradeButtonLockedSprite;
            }
            else if (!captain.Unlocked)
            {
                LockedCaptainRequirementsContainer.gameObject.SetActive(true);

                GoToStoreButton.gameObject.SetActive(true);
                UpgradeButton.gameObject.SetActive(false);
            }
            else
            {
                UpgradeCaptainRequirementsContainer.gameObject.SetActive(true);

                // Load upgrade from catalog
                upgrade = CatalogManager.Instance.GetCaptainUpgrade(captain);

                if (upgrade != null)
                {
                    // XP Requirement
                    var xpNeeded = _captainManager.GetCaptainUpgradeXPRequirement(captain);
                    xpRequirementSatisfied = captain.XP >= xpNeeded;
                    SelectedUpgradeXPRequirement.text = string.Format(XPRequirementTemplate, captain.XP, xpNeeded, xpRequirementSatisfied ? SatisfiedMarkdownColor : UnsatisfiedMarkdownColor);

                    // Crystal Requirement
                    var crystalsNeeded = upgrade.Price[0].Amount;
                    var crystalBalance = CatalogManager.Instance.GetCrystalBalance(captain.PrimaryElement);
                    crystalRequirementSatisfied = crystalBalance >= crystalsNeeded;
                    SelectedUpgradeCrystalRequirement.text = string.Format(CrystalRequirementTemplate, crystalBalance, crystalsNeeded, crystalRequirementSatisfied ? SatisfiedMarkdownColor : UnsatisfiedMarkdownColor);
                    SelectedUpgradeCrystalRequirementImage.sprite = CosmicShore.Gameplay.Elements.Get(captain.PrimaryElement).GetFullIcon(crystalRequirementSatisfied);
                }

                GoToStoreButton.gameObject.SetActive(false);
                // Upgrade Button
                UpgradeButton.gameObject.SetActive(true);
                if (xpRequirementSatisfied && crystalRequirementSatisfied)
                    UpgradeButton.GetComponent<Image>().sprite = UpgradeButtonUnlockedSprite;
                else
                    UpgradeButton.GetComponent<Image>().sprite = UpgradeButtonLockedSprite;
            }


        }

        void PopulateCaptainSelectionList()
        {
            if (CaptainSelectionContainer == null) return;

            // Assign captains
            for (var i = 0; i < CaptainSelectionContainer.transform.childCount; i++)
                CaptainSelectionContainer.GetChild(i).GetComponent<CaptainUpgradeSelectionCard>().AssignCaptain(Models[i] as SO_Captain);

            SelectCaptain(shipClassTypeVariable.Value);
        }

        /// <summary>
        /// The second of the two paths that can open <see cref="PurchaseConfirmationModal"/> (the
        /// other is <c>PurchaseCard.OnClickBuy</c>), so the de-scope has to hold here too. The
        /// commerce gate is asked FIRST and presents its own refusal: below it sits the bare
        /// <c>DeniedMenuAudio</c>, which says "no" without saying why, and a de-scoped surface that
        /// answers with an unexplained sting is what reads as broken.
        /// </summary>
        public virtual void OnClickBuy()
        {
            if (!SO_CommerceAvailability.TryPress(
                    UpgradeButton ? UpgradeButton.gameObject : gameObject,
                    CommerceSurface.CatalogPurchase))
                return;

            if (crystalRequirementSatisfied && xpRequirementSatisfied)
            {
                ConfirmationModal.SetVirtualItem(upgrade, PurchaseUpgrade);
                ConfirmationModal.ModalWindowIn();
            }
            else
            {
                DeniedMenuAudio.PlayAudio();
            }
        }

        /// <summary>
        /// The modal's confirm callback. Gated as well as <see cref="OnClickBuy"/> because it is
        /// public and a scene could wire a button straight to it — the same defence-in-depth split
        /// <c>IAPManager.OpenCheckout</c> uses: gate the UI so the player is never offered it, and
        /// never rely on the UI alone to enforce it. It cannot double-sting, because the only other
        /// way in is a modal the gate above keeps closed.
        /// </summary>
        public void PurchaseUpgrade()
        {
            if (!SO_CommerceAvailability.Instance.IsAvailable(CommerceSurface.CatalogPurchase))
                return;

            if (crystalRequirementSatisfied && xpRequirementSatisfied)
            {
                CatalogManager.Instance.PurchaseCaptainUpgrade(captain, OnCaptainUpgraded);
                ConfirmationModal.ModalWindowOut();
            }
            else
            {
                DeniedMenuAudio.PlayAudio();
            }
        }

        public void OnCaptainUpgraded()
        {
            _captainManager.LoadCaptainData(captain);
            UpgradeMenuAudio.PlayAudio();
            UpdateView();

            // refresh captains
            for (var i = 0; i < CaptainSelectionContainer.transform.childCount; i++)
                CaptainSelectionContainer.GetChild(i).GetComponent<CaptainUpgradeSelectionCard>().RefreshCaptainData();
        }

        /* Selects the Captain in the UI for display */
        /// <summary>
        /// Select a Captain in the UI to display its meta data
        /// </summary>
        /// <param name="index">Index of the displayed Captain list</param>
        public void SelectCaptain(int index)
        {
            try
            {
                for (var i = 0; i < 4; i++)
                    CaptainSelectionContainer.GetChild(i).GetComponent<CaptainUpgradeSelectionCard>().ToggleSelected(i == index);

                Select(index);
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                CSDebug.LogWarningFormat("{0} - {1} - The vessel lacks captain assets. Please add them. {2}", nameof(HangarScreen),
                    nameof(SelectCaptain), argumentOutOfRangeException.Message);
            }
            catch (NullReferenceException nullReferenceException)
            {
                CSDebug.LogWarningFormat("{0} - {1} - The vessel lacks captain assets. Please add them. {2}", nameof(HangarScreen),
                    nameof(SelectCaptain), nullReferenceException.Message);
            }
        }
    }
}