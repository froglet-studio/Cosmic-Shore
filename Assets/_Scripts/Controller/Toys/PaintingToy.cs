using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The "fly by numbers" painting toy: fly through it to start a shape-painting sequence
    /// (trace a pattern with your trail). Raises the existing decoupled
    /// <see cref="ShapeSignEvents.OnShapeSelected"/>; a <see cref="FreestylePaintingDirector"/>
    /// (with a wired <c>ShapeDrawingManager</c>) runs the preview → draw flow. Keeping the toy
    /// decoupled from the drawing engine means the heavy shape infrastructure can be wired
    /// independently without changing the toy.
    /// </summary>
    public class PaintingToy : Toy
    {
        ShapeDefinition _shape;

        public void SetShape(ShapeDefinition shape) => _shape = shape;

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (_shape == null)
            {
                CSDebug.LogWarning("[PaintingToy] No ShapeDefinition assigned — nothing to paint.");
                return;
            }

            // A FreestylePaintingDirector listens for this and drives ShapeDrawingManager.
            // If no director is wired the event is a harmless no-op and the toy simply re-arms.
            ShapeSignEvents.RaiseShapeSelected(_shape, transform.position);
        }
    }
}
