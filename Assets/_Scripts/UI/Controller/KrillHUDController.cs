using System.Globalization;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Krill's phase cycle and hand to <see cref="KrillHUDView"/> (design: <c>R_VesselActions/KRILL.md</c> §5).
    ///
    /// <para>Every signal is POLLED per frame from this vessel's OWN executor - a serialized reference on its prefab, never
    /// reached for by type through the hierarchy (vessel rule 14), so an unwired HUD shows in the inspector. Every word is
    /// the view's authored template filled by <see cref="TemplateText.Fill"/>; the card names are the deck's.</para>
    ///
    /// <para>The pilot gate runs every <see cref="Initialize"/>, above anything that depends on it, so a re-init that hands
    /// this vessel to an AI or a remote owner stops driving the HUD (rule 5).</para>
    /// </summary>
    public class KrillHUDController : VesselHUDController
    {
        [Header("Krill")]
        [SerializeField] KrillHUDView view;
        [Tooltip("This vessel's swarm executor: the phase cycle, the hand and the swarm.")]
        [SerializeField] KrillSwarmExecutor krill;

        bool _isLocalPilotHud;
        int _secondsShown = -1, _countShown = -1, _capShown = -1;
        KrillPhase _phaseShown = (KrillPhase)(-1);

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = GetComponentInChildren<KrillHUDView>(true);

            // The fleet's pilot gate (AnglerHUDController / StoatHUDController / BrittleStarHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
            _secondsShown = _countShown = _capShown = -1;
            _phaseShown = (KrillPhase)(-1);
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !krill) return;
            var rules = krill.Rules;
            if (rules == null) return;

            int seconds = Mathf.CeilToInt(Mathf.Max(0f, rules.PhaseLength(rules.Phase) - rules.PhaseTime));
            if (seconds != _secondsShown || rules.Phase != _phaseShown)   // the line is rebuilt only on a change
            {
                _secondsShown = seconds;
                _phaseShown = rules.Phase;
                view.SetPhase(TemplateText.Fill(view.PhaseTemplate,
                    "phase", view.PhaseName((int)rules.Phase),
                    "seconds", seconds.ToString(CultureInfo.InvariantCulture)));
            }

            int count = krill.MemberCount, cap = rules.CapNow;
            if (count != _countShown || cap != _capShown)
            {
                _countShown = count;
                _capShown = cap;
                view.SetSwarm(TemplateText.Fill(view.SwarmTemplate,
                    "count", count.ToString(CultureInfo.InvariantCulture),
                    "cap", cap.ToString(CultureInfo.InvariantCulture)));
            }

            for (int slot = 0; slot < KrillRules.HandOrder.Length; slot++)
            {
                var element = KrillRules.HandOrder[slot];
                int held = Mathf.Min(KrillRules.MaxLevel, rules.HeldInElement(element));
                var card = rules.HandCard(slot);
                if (!card.IsEmpty)
                    view.SetCard(slot,
                        TemplateText.Fill(view.CardNameTemplate, "name", card.Card.DisplayName),
                        TemplateText.Fill(view.CardLevelTemplate, "level", card.Level.ToString(CultureInfo.InvariantCulture)),
                        true, held / (float)KrillRules.MaxLevel);
                else
                    view.SetCard(slot, string.Empty,
                        held > 0 ? TemplateText.Fill(view.HeldTemplate, "level", held.ToString(CultureInfo.InvariantCulture)) : string.Empty,
                        false, held / (float)KrillRules.MaxLevel);
            }
        }
    }
}
