using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
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

            // Four-icon contract: every vessel presents exactly four ability icons, counting the
            // icons the view ALREADY shows first (bound via VesselHUDView.GetAbilitySlotImage —
            // pure refactor, no visible change). Prefer a bar authored in the HUD prefab; otherwise
            // auto-adopt one ONLY if the view leaves slots genuinely un-iconed (same zero-wire
            // Resources pattern as ElementalBarsView's config fallback).
            if (!abilityBar)
                abilityBar = GetComponentInChildren<VesselAbilityBar>(true);
            if (!abilityBar)
                abilityBar = TryAutoAdoptAbilityBar(vesselStatus);
            abilityBar?.Initialize(vesselStatus, baseView);
        }

        /// <summary>
        /// Zero-wire adoption of the four-icon contract, for the local human pilot only. Creates a
        /// runtime bar ONLY when (a) a <c>Resources/VesselAbilitySets/{VesselClassType}</c> set
        /// exists and (b) the view leaves at least one slot without an existing icon — a view that
        /// already presents all four (e.g. Squirrel) gets nothing: zero new objects, zero visible
        /// change. The bar then renders placeholders for just the missing slots.
        /// </summary>
        VesselAbilityBar TryAutoAdoptAbilityBar(IVesselStatus vesselStatus)
        {
            if (vesselStatus == null || vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser)
                return null;
            if (!baseView) return null;

            bool anyMissing = false;
            for (int i = 0; i < VesselAbilitySetSO.SlotCount; i++)
            {
                if (baseView.GetAbilitySlotImage(i)) continue;
                anyMissing = true;
                break;
            }
            if (!anyMissing) return null; // the view already presents all four icons

            var set = Resources.Load<VesselAbilitySetSO>($"VesselAbilitySets/{vesselStatus.VesselType}");
            if (!set) return null;

            var go = new GameObject("VesselAbilityBar (auto)", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(baseView.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bar = go.AddComponent<VesselAbilityBar>();
            bar.SetAbilitySet(set);
            return bar;
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
