using CosmicShore.Gameplay;
using CosmicShore.Engine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The "fly by numbers" painting toy — fly through it to start a shape-painting run where you
    /// trace a pattern with your trail. Drives a self-contained <see cref="MenuShapePainter"/> (guide
    /// line + ghost outline + waypoint markers) that needs no Cell / crystal manager / scoring, so it
    /// works in the menu. The painted trail is conserved mass like any other trail — no caps/TTL/culler.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_Painting", menuName = "ScriptableObjects/Toys/Painting Toy")]
    public class PaintingToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Painting (Fly-by-Numbers)")]
        [SerializeField, Tooltip("Shape this toy paints. Any ShapeDefinition asset (or one with an " +
                                 "autoGeneratePreset) works — the toy draws it with a self-contained runner.")]
        ShapeDefinition shape;

        [SerializeField, Tooltip("Scale applied to the shape's local waypoints (~±100 units authored).")]
        float shapeScale = 1f;

        [SerializeField, Tooltip("How close the vessel must get to a waypoint to advance, world units.")]
        float reachThreshold = 30f;

        [SerializeField, Tooltip("How far ahead of the vessel the shape appears when the toy starts, world units.")]
        float originForwardOffset = 120f;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor, DisplayName);
            var toy = go.AddComponent<PaintingToy>();
            toy.Configure(shape, shapeScale, reachThreshold, originForwardOffset);
            toy.Initialize(this, context, placement);
        }

        /// <summary>Assigns a shape on a runtime-synthesised definition (the zero-config default toybox).</summary>
        internal void SetRuntimeShape(ShapeDefinition runtimeShape) => shape = runtimeShape;
    }
}
