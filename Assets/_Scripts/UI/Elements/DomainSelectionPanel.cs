using System;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Three-button domain selector (Jade, Ruby, Gold).
    /// Each owner picks their own domain. The selection is forwarded via
    /// <see cref="OnDomainSelected"/> and applied to the player's NetDomain
    /// NetworkVariable through the server (see Player.RequestSetDomain_ServerRpc).
    /// </summary>
    public class DomainSelectionPanel : MonoBehaviour
    {
        [Header("Domain Buttons")]
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

        public event Action<Domains> OnDomainSelected;

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
            OnDomainSelected?.Invoke(domain);
        }

        void RefreshIndicators()
        {
            if (jadeSelectedIndicator) jadeSelectedIndicator.SetActive(_selectedDomain == Domains.Jade);
            if (rubySelectedIndicator) rubySelectedIndicator.SetActive(_selectedDomain == Domains.Ruby);
            if (goldSelectedIndicator) goldSelectedIndicator.SetActive(_selectedDomain == Domains.Gold);
        }
    }
}
