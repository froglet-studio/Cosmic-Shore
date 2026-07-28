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

        [Header("Legacy Silhouette")]
        [SerializeField] private SilhouetteController silhouette;

        protected R_VesselActionHandler Actions { get; private set; }
        protected VesselHUDView View => baseView;

        R_VesselElementalAbilityHandler _abilityHandler;

        // One ordering contract for the fleet - the ability row, the element flowers and this
        // seeding loop all read the same array.
        static Element[] AllElements => VesselHUDView.AbilityDisplayOrder;

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (_abilityHandler)
                _abilityHandler.OnUpgradeStateChanged -= HandleUpgradeStateChanged;
        }

        public virtual void Initialize(IVesselStatus vesselStatus)
        {
            Actions = vesselStatus.ActionHandler;

            if (!baseView)
                baseView = GetComponentInChildren<VesselHUDView>(true);

            baseView?.Initialize();

            // Elemental upgrade highlight - shared across all vessel HUDs: the view binds each
            // ability icon to the element that upgrades it (per the vessel's ElementalAbilityMapSO)
            // and the icon glows while that upgrade is active. Idempotent across re-inits.
            if (_abilityHandler)
                _abilityHandler.OnUpgradeStateChanged -= HandleUpgradeStateChanged;
            _abilityHandler = vesselStatus.ElementalAbilityHandler;
            if (_abilityHandler && baseView)
            {
                _abilityHandler.OnUpgradeStateChanged += HandleUpgradeStateChanged;
                foreach (var element in AllElements) // seed already-active upgrades
                    baseView.SetAbilityUpgraded(element, _abilityHandler.IsUpgradeActive(element));
            }

#if UNITY_EDITOR
            // Structural contract: four ability icons, charge/mass/space/time, left to right.
            baseView?.ValidateAbilityIconRow();
#endif
        }

        private void HandleUpgradeStateChanged(Element element, bool active)
            => baseView?.SetAbilityUpgraded(element, active);

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
