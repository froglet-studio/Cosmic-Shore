using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// A <see cref="ScopePetalImage"/> cut to any element's petal (<see cref="ElementPetalGeometry"/>). The Serpent's
    /// component, subclassed so the only thing that changes is the outline — the surface that renders is the one that
    /// already renders (<c>SniperScopeOverlay</c>'s round-7 rule).
    /// </summary>
    public class ElementPetalImage : ScopePetalImage
    {
        Element _element = Element.Charge;

        /// <summary>Which petal to draw. Re-tessellates only on a change.</summary>
        public Element Element
        {
            get => _element;
            set
            {
                if (_element == value) return;
                _element = value;
                SetVerticesDirty();
            }
        }

        protected override Vector2[] PetalOutline => ElementPetalGeometry.Outline(_element);
        protected override Vector2 PetalCentre => ElementPetalGeometry.Centre;
        protected override Vector2 PetalMiter(int i) => ElementPetalGeometry.OutwardMiter(_element, i);
    }
}
