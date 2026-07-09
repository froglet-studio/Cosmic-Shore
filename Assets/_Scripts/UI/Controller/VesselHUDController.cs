using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
namespace CosmicShore.UI
{
    public class VesselHUDController : MonoBehaviour, IVesselHUDController
    {
        [Header("Base View (fallback)")]
        [SerializeField] private VesselHUDView baseView;

        [Header("Ability Bar (4-icon contract)")]
        [Tooltip("Optional. When present, guarantees exactly four ability icons (placeholders for " +
                 "unfilled slots). Resolved from children if left empty; a HUD without one is flagged " +
                 "by Tools > Cosmic Shore > Validate Vessel Ability Icons.")]
        [SerializeField] private VesselAbilityBar abilityBar;

        [Header("Legacy Silhouette")]
        [SerializeField] private SilhouetteController silhouette;

        protected R_VesselActionHandler Actions { get; private set; }
        protected VesselHUDView View => baseView;

        private void OnDestroy() => UnsubscribeFromEvents();

        public virtual void Initialize(IVesselStatus vesselStatus)
        {
            Actions = vesselStatus.ActionHandler;

            if (!baseView)
                baseView = GetComponentInChildren<VesselHUDView>(true);

            baseView?.Initialize();

            // Four-icon contract: initialize the ability bar if this HUD has one. No-op when absent,
            // so existing HUDs are untouched until they adopt a VesselAbilityBar.
            if (!abilityBar)
                abilityBar = GetComponentInChildren<VesselAbilityBar>(true);
            abilityBar?.Initialize(vesselStatus);
        }

        public void SubscribeToEvents()
        {
            if (!Actions || !baseView) return;
            Actions.OnInputEventStarted += HandleStart;
            Actions.OnInputEventStopped += HandleStop;
        }

        public void UnsubscribeFromEvents()
        {
            if (!Actions) return;
            Actions.OnInputEventStarted -= HandleStart;
            Actions.OnInputEventStopped -= HandleStop;
        }

        public void ShowHUD() => baseView?.Show();
        public void HideHUD() => baseView?.Hide();

        private void HandleStart(InputEvents ev) => Toggle(ev, true);
        private void HandleStop(InputEvents ev)  => Toggle(ev, false);

        private void Toggle(InputEvents ev, bool on)
        {
            if (!baseView) return;

            foreach (var h in baseView.highlights)
            {
                if (h.input == ev && h.image)
                    h.image.enabled = on;
            }
        }

        public void SetBlockPrefab(GameObject prefab)
        {
            if (baseView != null)
                baseView.TrailBlockPrefab = prefab;

            if (silhouette != null)
                silhouette.SetBlockPrefab(prefab);
        }
    }
}
