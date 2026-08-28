using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class ElementalBarsController : MonoBehaviour
    {
        [Header("Element Bars")]
        [SerializeField] private ElementalBarsView elementBars;

        private ResourceSystem _resources;

        void OnEnable()
        {
            TrySubscribeElementBars();
        }

        void OnDisable()
        {
            TryUnsubscribeElementBars();
        }

        public void Initialize(IVesselStatus status)
        {
            _resources = status?.ResourceSystem;

            InitializeElementBars();
        }

        // --- Element Bars ---
        void TryUnsubscribeElementBars()
        {
            if (_resources != null)
                _resources.OnElementLevelChange -= HandleElementLevelChanged;
        }

        // OnDisable detaches the element-level handler, so OnEnable must re-attach it
        // (mirroring TrySubscribeResources) — otherwise a disable/enable cycle leaves
        // the petal flowers frozen while the energy UI keeps updating. Re-seeds levels
        // because they may have changed while disabled (SetLevel early-outs on no-change).
        void TrySubscribeElementBars()
        {
            if (_resources == null || !elementBars) return;
            TryUnsubscribeElementBars();
            _resources.OnElementLevelChange += HandleElementLevelChanged;
            SeedElementBarLevels();
        }

        void SeedElementBarLevels()
        {
            elementBars.SetLevel(Element.Charge, _resources.GetLevel(Element.Charge));
            elementBars.SetLevel(Element.Mass, _resources.GetLevel(Element.Mass));
            elementBars.SetLevel(Element.Space, _resources.GetLevel(Element.Space));
            elementBars.SetLevel(Element.Time, _resources.GetLevel(Element.Time));
        }

        void InitializeElementBars()
        {
            // The element flower display is a REQUIRED system on every vessel: when the
            // prefab doesn't author an ElementalBarsView, create one on the vessel's HUD
            // canvas. The view self-populates its four default bindings and the shared
            // ElementalBarsConfig stamps the fleet-standard placement, so no per-vessel
            // wiring is needed. Vessels with an authored view (Squirrel, Sparrow) keep it.
            // Adopt a view already present on this vessel's HUD before making one; creating a
            // second would draw the fleet-standard row on top of it with nothing to say why.
            //
            // The ability lockup is asked FIRST and is BUILT here rather than waited on: it docks
            // the flowers into the four ability cards, and its own Awake has not necessarily run
            // (a HUD that starts inactive doesn't Awake until it is shown, which is after this).
            // Build() is idempotent, so asking early costs nothing and removes the ordering
            // dependence entirely.
            if (!elementBars)
            {
                var lockups = GetComponentInChildren<AbilityLockupView>(true);
                if (lockups)
                {
                    lockups.Build();
                    elementBars = lockups.ElementBars;
                }
            }
            if (!elementBars)
                elementBars = GetComponentInChildren<ElementalBarsView>(true);
            if (!elementBars)
                elementBars = CreateDefaultElementBars();
            if (!elementBars) return;

            elementBars.Build();

            TrySubscribeElementBars();
        }

        ElementalBarsView CreateDefaultElementBars()
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            if (!canvas) return null; // no HUD surface on this vessel — nothing to show on

            CSDebug.LogWarning($"[ElementalBarsController] '{name}' has no authored ElementalBarsView - " +
                               "creating one at RUNTIME so the fleet-required display still shows. " +
                               "Author it into the HUD prefab: FrogletTools > Vessels > Bake Elemental " +
                               "Petal Bars Into All Vessel HUDs, then wire it to elementBars.");
            var go = new GameObject("ElementalBars (auto)", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            return go.AddComponent<ElementalBarsView>();
        }

        void HandleElementLevelChanged(Element element, int level)
        {
            elementBars?.SetLevel(element, level);
        }

        /// <summary>
        /// Exposes the ElementalBarsView for the HUD controller to apply juice effects.
        /// </summary>
        public ElementalBarsView ElementBars => elementBars;

    }
}
