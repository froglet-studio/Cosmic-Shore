using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Termite queen's deck to <see cref="TermiteHUDView"/> (design:
    /// <c>R_VesselActions/TERMITE.md</c> §6).
    ///
    /// <para>Everything is POLLED from this vessel's OWN deck executor, a serialized reference on
    /// its prefab (never type-searched through the hierarchy — a HUD that hunts a component the
    /// vessel does not carry leaves a dead gauge and no error). The pheromone is continuous with
    /// no event behind it, and a card's affordability is a function of it, so polling is the
    /// honest drive. A played card is detected as its slot's face-up card CHANGING, which is the
    /// same fact the deck records and cannot disagree with it.</para>
    /// </summary>
    public class TermiteHUDController : VesselHUDController
    {
        [Header("Termite")]
        [SerializeField] TermiteHUDView view;

        [Tooltip("This vessel's deck executor. Serialized so an unwired HUD is visible in the inspector.")]
        [SerializeField] TermiteDeckExecutor deck;

        static readonly Element[] s_slots = { Element.Charge, Element.Mass, Element.Space };

        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            if (!view) view = GetComponentInChildren<TermiteHUDView>(true);

            // The fleet's pilot gate, verbatim (ButterflyHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !deck) return;

            float max = Mathf.Max(0.01f, deck.MaxPheromone);
            view.SetPheromone(deck.Pheromone / max);

            for (int i = 0; i < s_slots.Length; i++)
            {
                var element = s_slots[i];
                if (!deck.TryGetFaceUp(element, out var card)) continue;

                if (view.SetFaceUp(element, card.Card)) View?.PlayAbilityFlash(element);

                // The veil IS the elixir wait: it depletes as pheromone fills toward the card's
                // cost, and a card blocked by anything else (a cap, no mound for workers) is
                // fully veiled rather than drawn as ready.
                float remaining;
                if (deck.CanPlayFaceUp(element)) remaining = 0f;
                else if (card.Cost > 0f && deck.Pheromone < card.Cost)
                    remaining = Mathf.Clamp01(1f - deck.Pheromone / card.Cost);
                else remaining = 1f;
                View?.SetAbilityCooldown(element, remaining);
            }
        }
    }
}
