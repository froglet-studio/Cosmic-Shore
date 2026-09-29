using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Termite queen's HUD: her deck (design: <c>R_VesselActions/TERMITE.md</c> §6). The row
    /// order IS the element map — <b>Autothysis / Team Crystal (charge) · Queen / Mound Drones
    /// (mass) · Teleport / New Mound (space) · Pheromone (time)</b>.
    ///
    /// <list type="bullet">
    /// <item><b>The three card slots show the card a press will play</b>: the slot's icon IS the
    /// face-up card's front art (the prototype's own card faces), and it turns over when the card
    /// is played. A card is never drawn as "ready" when it is not: the fleet's clockwise
    /// depleting veil sweeps it while the pheromone to pay for it is still filling, so the veil
    /// is literally the elixir wait, and a card blocked by a cap (no room for another drone or
    /// mound) is fully veiled.</item>
    /// <item><b>The Time plate is the pheromone tank</b> — a linear fill of what the queen holds,
    /// the lockup's own gauge shape. It is the one number every card is priced in.</item>
    /// </list>
    ///
    /// <para>The upgrade signal lives on the lockup's card, so this view only re-anchors its
    /// gauge's rest scale in <see cref="SetAbilityUpgraded"/> (the Squirrel's recorded trap).</para>
    /// </summary>
    public class TermiteHUDView : VesselHUDView
    {
        [Serializable]
        public struct CardArt
        {
            public TermiteCard card;
            [Tooltip("The card's FRONT art (_Graphics/VesselButtons/TermiteCard_Front_*).")]
            public Sprite front;
        }

        [Header("Deck")]
        [Tooltip("Front art per card. The slot icon is swapped to the face-up card's front.")]
        [SerializeField] List<CardArt> cardArt = new();

        [Header("Pheromone (Time row)")]
        [Tooltip("Filled Image on the Time plate: the pheromone tank. Bind it as the Time icon's " +
                 "gauge too, so the lockup re-homes and masks it — and keep writing fillAmount here.")]
        [SerializeField] Image pheromoneGauge;

        [Tooltip("Gauge colour while filling.")]
        [SerializeField] Color fillingColor = new(0.85f, 0.7f, 0.35f, 1f);

        [Tooltip("Gauge colour when the tank is full (pheromone is being wasted — play a card).")]
        [SerializeField] Color fullColor = new(1f, 0.95f, 0.6f, 1f);

        readonly Dictionary<Element, TermiteCard> _shown = new();

        public override void Initialize()
        {
            _shown.Clear();
            SetPheromone(0.5f);
            SetAbilityCooldown(Element.Charge, 0f);
            SetAbilityCooldown(Element.Mass, 0f);
            SetAbilityCooldown(Element.Space, 0f);
        }

        /// <summary>The tank as a fraction of its capacity.</summary>
        public void SetPheromone(float fill01)
        {
            if (!pheromoneGauge) return;
            float v = Mathf.Clamp01(fill01);
            pheromoneGauge.fillAmount = v;
            pheromoneGauge.color = v >= 0.999f ? fullColor : fillingColor;
        }

        /// <summary>Turn the slot's icon to <paramref name="card"/>'s front. Returns true when the
        /// card actually changed (i.e. the slot was played), which the controller flashes.</summary>
        public bool SetFaceUp(Element element, TermiteCard card)
        {
            if (_shown.TryGetValue(element, out var current) && current == card) return false;
            bool hadCard = _shown.ContainsKey(element);
            _shown[element] = card;

            if (TryGetAbilityIcon(element, out var icon) && icon && TryGetArt(card, out var art))
            {
                icon.sprite = art;
                icon.preserveAspect = true;
            }
            return hadCard;
        }

        bool TryGetArt(TermiteCard card, out Sprite sprite)
        {
            for (int i = 0; i < cardArt.Count; i++)
                if (cardArt[i].card == card && cardArt[i].front) { sprite = cardArt[i].front; return true; }
            sprite = null;
            return false;
        }

        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            if (element == Element.Time && pheromoneGauge)
                pheromoneGauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
