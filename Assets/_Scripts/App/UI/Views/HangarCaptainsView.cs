using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
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

        [SerializeField] TMP_Text SelectedUpgradeDescription;   // TODO:
        [SerializeField] TMP_Text SelectedUpgradeXPRequirement;
        [SerializeField] TMP_Text SelectedUpgradeCrystalRequirement;
        [SerializeField] Image SelectedUpgradeCrystalRequirementImage;
        [SerializeField] Button UpgradeButton;
        [SerializeField] Sprite UpgradeButtonLockedSprite;
        [SerializeField] Sprite UpgradeButtonUnlockedSprite;

        bool crystalRequirementSatisfied = false;
        bool xpRequirementSatisfied = false;
        VirtualItem upgrade;
        Captain captain;

        const string SatisfiedMarkdownColor = "FFF"; 
        const string UnsatisfiedMarkdownColor = "888"; 

        const string CrystalRequirementTemplate = "<color=#{2}>{0}</color> / {1}";
        const string XPRequirementTemplate = "<color=#{2}>{0}</color> / {1} XP";

        public override void UpdateView()
        {
            var model = SelectedModel as SO_Captain;

            captain = CaptainManager.Instance.GetCaptainByName(model.Name);

            // Load upgrade from catalog
            upgrade = CatalogManager.Inventory.GetCaptainUpgrade(captain.Ship.Class.ToString(), captain.PrimaryElement.ToString(), captain.Level);

            // Lock Logic - Lock Icon

            // XP Requirement
            var xpNeeded = CaptainManager.Instance.GetCaptainUpgradeXPRequirement(captain);
            xpRequirementSatisfied = captain.XP >= xpNeeded;
            SelectedUpgradeXPRequirement.text = string.Format(XPRequirementTemplate, captain.XP, xpNeeded, xpRequirementSatisfied ? SatisfiedMarkdownColor : UnsatisfiedMarkdownColor);

            // Crystal Requirement
            var crystalsNeeded = 100;
            var crystalBalance = CatalogManager.Instance.GetCrystalBalance(captain.PrimaryElement);
            crystalRequirementSatisfied = crystalBalance >= crystalsNeeded;
            SelectedUpgradeCrystalRequirement.text = string.Format(CrystalRequirementTemplate, crystalBalance, crystalsNeeded, crystalRequirementSatisfied ? SatisfiedMarkdownColor : UnsatisfiedMarkdownColor);

            SelectedUpgradeCrystalRequirementImage.sprite = Elements.Get(captain.PrimaryElement).GetFullIcon(crystalRequirementSatisfied);

            // Upgrade Button
            if (xpRequirementSatisfied && crystalRequirementSatisfied)
            {
                UpgradeButton.GetComponent<Image>().sprite = UpgradeButtonUnlockedSprite;
                UpgradeButton.enabled = true;
            }
            else
            {
                UpgradeButton.GetComponent<Image>().sprite = UpgradeButtonLockedSprite;
                UpgradeButton.enabled = false;
            }

            // Populate Captain Details
            Debug.Log($"Populating Captain Details List: {captain.Name}");
            Debug.Log($"Populating Captain Details List: {captain.Description}");
            Debug.Log($"Populating Captain Details List: {captain.Icon}");
            Debug.Log($"Populating Captain Details List: {captain.Image}");
            if (SelectedCaptainName != null) SelectedCaptainName.text = captain.Name;
            if (SelectedCaptainElementLabel != null) SelectedCaptainElementLabel.text = "The " + captain.PrimaryElement.ToString() + " " + captain.Ship.Name;
            if (SelectedUpgradeDescription != null) SelectedUpgradeDescription.text = captain.Description;
            if (SelectedCaptainQuote != null) SelectedCaptainQuote.text = captain.Flavor;
            if (SelectedCaptainImage != null) SelectedCaptainImage.sprite = captain.Image;
            if (SelectedCaptainShipImage != null) SelectedCaptainShipImage.sprite = captain.Ship.Icon;
        }

        public void PurchaseUpgrade()
        {
            Debug.Log("PurchaseUpgrade");
            if (crystalRequirementSatisfied && xpRequirementSatisfied)
            {
                Debug.Log("PurchaseUpgrade - Requirements satisfied");

                CatalogManager.Instance.PurchaseCaptainUpgrade(captain, OnCaptainUpgraded);
            }
        }

        public void OnCaptainUpgraded()
        {
            CaptainManager.Instance.ReloadCaptain(captain);
            UpdateView();
        }
    }
}