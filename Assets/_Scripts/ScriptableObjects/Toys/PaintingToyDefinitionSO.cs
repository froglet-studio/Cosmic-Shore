using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The "fly by numbers" painting toy — fly through it to start a shape-painting sequence
    /// where you trace a pattern with your trail. Raises the existing decoupled
    /// <c>ShapeSignEvents.OnShapeSelected</c>; a <see cref="FreestylePaintingDirector"/>
    /// (with a wired <c>ShapeDrawingManager</c>) runs the preview → draw flow.
    ///
    /// The painted trail is conserved mass like any other trail — no caps, no TTL, no culler.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_Painting", menuName = "ScriptableObjects/Toys/Painting Toy")]
    public class PaintingToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Painting (Fly-by-Numbers)")]
        [SerializeField, Tooltip("Shape this toy starts. Requires a FreestylePaintingDirector + " +
                                 "ShapeDrawingManager wired in the scene to actually draw.")]
        ShapeDefinition shape;

        public override Toy CreateToy(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor, DisplayName);
            var toy = go.AddComponent<PaintingToy>();
            toy.SetShape(shape);
            toy.Initialize(this, context, placement);
            return toy;
        }
    }
}
