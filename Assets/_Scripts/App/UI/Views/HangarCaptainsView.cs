using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Models;
using CosmicShore.App.UI.Modals;
using CosmicShore.App.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace CosmicShore.App.UI.Views
{
    public class HangarCaptainsView : View
    {
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

        void OnEnable()
        {
            CaptainManager.OnLoadCaptainData += NewCaptainData;
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
            captain = CaptainManager.Instance.GetCaptainByName(model.Name);

            Debug.Log($"Populating Captain Details List: {captain.Name}");
            Debug.Log($"Populating Captain Details List: {captain.Description}");
            Debug.Log($"Populating Captain Details List: {captain.Icon}");
            Debug.Log($"Populating Captain Details List: {captain.Image}");

            EncounterButton.gameObject.SetActive(false);
            xpRequirementSatisfied = false;
            crystalRequirementSatisfied = false;

            // Populate Captain Details
            SelectedCaptainName.text = captain.Name;
            SelectedCaptainElementLabel.text = "The " + captain.PrimaryElement.ToString() + " " + captain.Ship.Name;
            SelectedUpgradeDescription.text = captain.Description;
            SelectedCaptainQuote.text = captain.Flavor;
            SelectedCaptainImage.sprite = captain.Image;
            SelectedCaptainImage.color = Color.white;
            SelectedCaptainShipImage.sprite = captain.Ship.Icon;

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
                EncounterButton.onClick.AddListener(() => CaptainManager.Instance.EncounterCaptain(captain.Name));

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
                    var xpNeeded = CaptainManager.Instance.GetCaptainUpgradeXPRequirement(captain);
                    xpRequirementSatisfied = captain.XP >= xpNeeded;
                    SelectedUpgradeXPRequirement.text = string.Format(XPRequirementTemplate, captain.XP, xpNeeded, xpRequirementSatisfied ? SatisfiedMarkdownColor : UnsatisfiedMarkdownColor);

                    // Crystal Requirement
                    var crystalsNeeded = upgrade.Price[0].Amount;
                    var crystalBalance = CatalogManager.Instance.GetCrystalBalance(captain.PrimaryElement);
                    crystalRequirementSatisfied = crystalBalance >= crystalsNeeded;
                    SelectedUpgradeCrystalRequirement.text = string.Format(CrystalRequirementTemplate, crystalBalance, crystalsNeeded, crystalRequirementSatisfied ? SatisfiedMarkdownColor : UnsatisfiedMarkdownColor);
                    SelectedUpgradeCrystalRequirementImage.sprite = CosmicShore.Elements.Get(captain.PrimaryElement).GetFullIcon(crystalRequirementSatisfied);
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

            SelectCaptain(SelectedIndex);
        }

        public virtual void OnClickBuy()
        {
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

        public void PurchaseUpgrade()
        {
            Debug.Log("PurchaseUpgrade");
            if (crystalRequirementSatisfied && xpRequirementSatisfied)
            {
                Debug.Log("PurchaseUpgrade - Requirements satisfied");

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
            CaptainManager.Instance.LoadCaptainData(captain);
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
            Debug.Log($"SelectCaptain: {index}");

            try
            {
                for (var i = 0; i < 4; i++)
                    CaptainSelectionContainer.GetChild(i).GetComponent<CaptainUpgradeSelectionCard>().ToggleSelected(i == index);

                Select(index);
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                Debug.LogWarningFormat("{0} - {1} - The ship lacks captain assets. Please add them. {2}", nameof(HangarScreen),
                    nameof(SelectCaptain), argumentOutOfRangeException.Message);
            }
            catch (NullReferenceException nullReferenceException)
            {
                Debug.LogWarningFormat("{0} - {1} - The ship lacks captain assets. Please add them. {2}", nameof(HangarScreen),
                    nameof(SelectCaptain), nullReferenceException.Message);
            }
        }
    }
}