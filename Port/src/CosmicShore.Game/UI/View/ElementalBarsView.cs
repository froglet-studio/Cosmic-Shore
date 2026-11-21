// PORT Deviation #13 — type-preserving SHELL of the UI/DOTween-bound ElementalBarsView
// (original: Assets/_Scripts/UI/View/ElementalBarsView.cs, 544 lines). The original
// renders one 5-petal "flower" per element via UnityEngine.UI Images recoloured/juiced
// with DOTween against the shared ElementalBarsConfigSO — all presentation-phase
// concerns. Only the public surface used by the vessel-layer closure is present
// (SilhouetteController.InitializeElementBars drives Build/SetLevel and exposes the
// type through its ElementBars property); bodies keep the trivially correct
// bookkeeping (built flag, per-element level store with the original's [-5, 15] clamp
// and no-change early-out) and no-op the rendering. The real port arrives with the
// phase-5 UI/render backend. Precedent: AudioSystem shell (Deviation #11),
// SilhouetteView shell (Deviation #13).
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Data;

namespace CosmicShore.UI
{
    public class ElementalBarsView : MonoBehaviour
    {
        public bool IsBuilt => _built;

        // Original constants live on ElementalBarsConfigSO (PetalCount=5, MinLevel=-5, MaxLevel=15);
        // the level bounds are kept here until that config SO ports.
        private const int MinLevel = -5;
        private const int MaxLevel = 15;

        bool _built;
        readonly Dictionary<Element, int> _currentLevels = new();

        public void Build()
        {
            if (_built) return;
            _built = true;
        }

        /// <param name="domainColor">
        /// Retained for API compatibility. Petal colours follow the fixed element-tick spec, not the
        /// team domain colour, so this argument is ignored.
        /// </param>
        public void SetLevel(Element element, int level, Color domainColor) => SetLevel(element, level);

        public void SetLevel(Element element, int level)
        {
            if (!_built) return;

            int clamped = Mathf.Clamp(level, MinLevel, MaxLevel);
            if (_currentLevels.TryGetValue(element, out int prev) && clamped == prev) return; // nothing to redraw

            _currentLevels[element] = clamped;
        }
    }
}
