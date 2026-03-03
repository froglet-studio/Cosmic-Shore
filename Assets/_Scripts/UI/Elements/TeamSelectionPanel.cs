using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class TeamSelectionPanel : MonoBehaviour
    {
        [Header("Team Buttons")]
        [SerializeField] Button jadeButton;
        [SerializeField] Button rubyButton;
        [SerializeField] Button goldButton;

        [Header("Selection Indicators")]
        [SerializeField] GameObject jadeSelectedIndicator;
        [SerializeField] GameObject rubySelectedIndicator;
        [SerializeField] GameObject goldSelectedIndicator;

        [Inject] GameDataSO gameData;

        Domains _currentSelection = Domains.Jade;

        void OnEnable()
        {
            if (jadeButton) jadeButton.onClick.AddListener(SelectJade);
            if (rubyButton) rubyButton.onClick.AddListener(SelectRuby);
            if (goldButton) goldButton.onClick.AddListener(SelectGold);

            RefreshFromLocalPlayer();
        }

        void OnDisable()
        {
            if (jadeButton) jadeButton.onClick.RemoveListener(SelectJade);
            if (rubyButton) rubyButton.onClick.RemoveListener(SelectRuby);
            if (goldButton) goldButton.onClick.RemoveListener(SelectGold);
        }

        void SelectJade() => SelectTeam(Domains.Jade);
        void SelectRuby() => SelectTeam(Domains.Ruby);
        void SelectGold() => SelectTeam(Domains.Gold);

        void SelectTeam(Domains domain)
        {
            _currentSelection = domain;
            WriteToLocalPlayer(domain);
            RefreshIndicators();
        }

        public Domains CurrentSelection => _currentSelection;

        void WriteToLocalPlayer(Domains domain)
        {
            if (gameData?.LocalPlayer is not Player player)
                return;

            if (!player.IsSpawned || !player.IsOwner)
                return;

            player.NetPreferredDomain.Value = domain;
        }

        void RefreshFromLocalPlayer()
        {
            if (gameData?.LocalPlayer is Player player && player.IsSpawned)
            {
                var preferred = player.NetPreferredDomain.Value;
                _currentSelection = DomainAssigner.IsPlayableDomain(preferred)
                    ? preferred
                    : Domains.Jade;
            }

            RefreshIndicators();
        }

        void RefreshIndicators()
        {
            if (jadeSelectedIndicator) jadeSelectedIndicator.SetActive(_currentSelection == Domains.Jade);
            if (rubySelectedIndicator) rubySelectedIndicator.SetActive(_currentSelection == Domains.Ruby);
            if (goldSelectedIndicator) goldSelectedIndicator.SetActive(_currentSelection == Domains.Gold);
        }
    }
}
