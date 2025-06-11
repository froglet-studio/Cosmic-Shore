using CosmicShore.Core;
using CosmicShore.Utilities;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class GameCanvas : MonoBehaviour
    {
        [Header("HUD and Controls")]
        [SerializeField] public MiniGameHUD MiniGameHUD;
        [SerializeField] public ShipButtonPanel ShipButtonPanel;
        [SerializeField] public GameObject EndGameScreen;

        [Header("Scoring UI")]
        [SerializeField] public Scoreboard scoreboard;

        [Header("Goodies and Awards")]
        [SerializeField] public GameObject AwardsContainer;
        [SerializeField] public Image XPEarnedImage;
        [SerializeField] public TMP_Text XPEarnedText;
        [SerializeField] public Image CrystalsEarnedImage;
        [SerializeField] public TMP_Text CrystalsEarnedText;
        [SerializeField] public List<Image> EncounteredCaptainImages;
        [SerializeField] public TMP_Text EncounteredCaptainText;

        [SerializeField]
        ShipHUDEventChannelSO onShipHUDInitialized;

        private void OnEnable()
        {
            onShipHUDInitialized.OnEventRaised += OnShipHUDInitialized;
        }


        private void OnDisable()
        {
            onShipHUDInitialized.OnEventRaised -= OnShipHUDInitialized;
        }
        private void OnShipHUDInitialized(ShipHUDData data)
        {
            MiniGameHUD.Hide();
            MiniGameHUD = data.ShipHUD;
            MiniGameHUD.gameObject.SetActive(true);

            foreach (var child in MiniGameHUD.GetComponentsInChildren<Transform>(false))
            {
                child.SetParent(transform, false);
                child.SetSiblingIndex(0);   // Don't draw on top of modal screens
            }
        }
    }
}