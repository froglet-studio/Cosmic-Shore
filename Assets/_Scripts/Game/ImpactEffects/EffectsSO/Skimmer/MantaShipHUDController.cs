﻿using UnityEngine;

namespace CosmicShore.Game
{ 
    public class MantaShipHUDController : ShipHUDController
    {
        [Header("View")]
        [SerializeField] private MantaShipHUDView view;

        [Header("Effect Source (SO)")]
        [SerializeField] private SkimmerOverchargeCollectPrismEffectSO overchargeSO;

        [Header("Skimmer binding")]
        [SerializeField] private SkimmerImpactor skimmer;

        private int _max = 1;

        public override void Initialize(IShipStatus shipStatus, ShipHUDView baseView)
        {
            base.Initialize(shipStatus, baseView);
            view = view != null ? view : baseView as MantaShipHUDView;

            if (overchargeSO == null || skimmer == null)
            {
                Debug.LogWarning("[MantaShipHUDController] Missing SO or Skimmer reference.");
                return;
            }

            // subscribe
            overchargeSO.OnCountChanged    += HandleCountChanged;
            overchargeSO.OnOvercharge      += HandleOvercharge;
            overchargeSO.OnCooldownStarted += HandleCooldownStarted;

            SetCounter(0, overchargeSO.MaxBlockHits);
        }

        private void OnDestroy()
        {
            if (overchargeSO != null)
            {
                overchargeSO.OnCountChanged    -= HandleCountChanged;
                overchargeSO.OnOvercharge      -= HandleOvercharge;
                overchargeSO.OnCooldownStarted -= HandleCooldownStarted;
            }
        }


        void HandleCountChanged(SkimmerImpactor who, int count, int max)
        {
            if (who != skimmer) return;     
            _max = Mathf.Max(1, max);
            SetCounter(count, _max);
        }

        void HandleOvercharge(SkimmerImpactor who)
        {
            if (who != skimmer) return;
     
            SetCounter(_max, _max);
        }

        void HandleCooldownStarted(SkimmerImpactor who, float seconds)
        {
            
        }


        void SetCounter(int count, int max)
        {
            if (view == null) return;

            if (view.countText != null)
                view.countText.text = $"{count}/{max}";

        }
    }
}