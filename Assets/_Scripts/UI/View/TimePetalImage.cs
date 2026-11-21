using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// A <see cref="ScopePetalImage"/> shaped like the TIME petal (<see cref="TimePetalGeometry"/>) —
    /// the Thresher launch sight's zoom window. The Plant is the Thresher's Time ability, so its
    /// eyepiece is the Time petal, as the Serpent's Space scope wears the Charge petal it was traced
    /// from.
    ///
    /// <para>It overrides ONLY the outline, per the rule <see cref="ScopePetalImage"/> records:
    /// when a component renders and only its shape is wrong, subclass it and override the
    /// geometry — so a bad emit reads as a wrong SHAPE, never as an absence. Its rect must have the
    /// petal's aspect (<see cref="TimePetalGeometry.Aspect"/>) and so must the texture it shows.</para>
    /// </summary>
    [AddComponentMenu("UI/Time Petal Image", 17)]
    public class TimePetalImage : ScopePetalImage
    {
        protected override Vector2[] PetalOutline => TimePetalGeometry.Outline;
        protected override Vector2 PetalCentre => TimePetalGeometry.Centre;
        protected override Vector2 PetalMiter(int i) => TimePetalGeometry.OutwardMiter(i);
    }
}
