using System;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Three-button team selector (Jade, Ruby, Gold).
    /// Each owner picks their own team. The selected domain is written
    /// to the local player's NetDomain NetworkVariable (owner-writable).
    /// </summary>
    public class TeamSelectionPanel : MonoBehaviour
    {
        [Header("Team Buttons")]
        [SerializeField] Button jadeButton;
        [SerializeField] Button rubyButton;
        [SerializeField] Button goldButton;

        [Header("Selection Indicator")]
        [Tooltip("Optional outline or highlight images toggled per selection.")]
        [SerializeField] GameObject jadeSelectedIndicator;
        [SerializeField] GameObject rubySelectedIndicator;
        [SerializeField] GameObject goldSelectedIndicator;

        Domains _selectedDomain = Domains.Jade;

        public Domains SelectedDomain => _selectedDomain;

        public event Action<Domains> OnTeamSelected;

        void OnEnable()
        {
            jadeButton.onClick.AddListener(SelectJade);
            rubyButton.onClick.AddListener(SelectRuby);
            goldButton.onClick.AddListener(SelectGold);
        }

        void OnDisable()
        {
            jadeButton.onClick.RemoveListener(SelectJade);
            rubyButton.onClick.RemoveListener(SelectRuby);
            goldButton.onClick.RemoveListener(SelectGold);
        }

        public void SetSelection(Domains domain)
        {
            _selectedDomain = domain;
            RefreshIndicators();
        }

        void SelectJade() => Select(Domains.Jade);
        void SelectRuby() => Select(Domains.Ruby);
        void SelectGold() => Select(Domains.Gold);

        void Select(Domains domain)
        {
            _selectedDomain = domain;
            RefreshIndicators();
            OnTeamSelected?.Invoke(domain);
        }

        void RefreshIndicators()
        {
            if (jadeSelectedIndicator) jadeSelectedIndicator.SetActive(_selectedDomain == Domains.Jade);
            if (rubySelectedIndicator) rubySelectedIndicator.SetActive(_selectedDomain == Domains.Ruby);
            if (goldSelectedIndicator) goldSelectedIndicator.SetActive(_selectedDomain == Domains.Gold);
        }
    }
}
