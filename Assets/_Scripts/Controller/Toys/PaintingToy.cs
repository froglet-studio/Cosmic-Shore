using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The "fly by numbers" painting toy: fly through it to start a shape-painting run. It drives a
    /// self-contained <see cref="MenuShapePainter"/> that guides you through the shape's waypoints
    /// while your own trail paints the pattern — no Cell, crystal manager, scoring, or HUD required, so
    /// it works in the menu. The painted trail is conserved mass like any other trail (no caps/TTLs).
    /// </summary>
    public class PaintingToy : Toy
    {
        ShapeDefinition _shape;
        float _scale = 1f;
        float _reachThreshold = 30f;
        float _originForwardOffset = 120f;

        MenuShapePainter _activePainter;

        public void Configure(ShapeDefinition shape, float scale, float reachThreshold, float originForwardOffset)
        {
            _shape = shape;
            if (scale > 0f) _scale = scale;
            if (reachThreshold > 0f) _reachThreshold = reachThreshold;
            if (originForwardOffset > 0f) _originForwardOffset = originForwardOffset;
        }

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (_shape == null)
            {
                CSDebug.LogWarning("[PaintingToy] No ShapeDefinition assigned — nothing to paint.");
                return;
            }
            if (_activePainter) return; // a run is already in progress
            if (localVessel?.Vessel == null) return;

            var shipT = localVessel.Vessel.Transform;
            Vector3 origin = shipT.position + shipT.forward * _originForwardOffset;

            // Orient the shape plane to face the vessel, so you fly across a front-on pattern.
            Vector3 normal = shipT.position - origin;
            Quaternion rot = normal.sqrMagnitude > 1e-4f
                ? Quaternion.LookRotation(normal.normalized, Vector3.up)
                : Quaternion.identity;

            var go = new GameObject("MenuShapePainter");
            _activePainter = go.AddComponent<MenuShapePainter>();
            _activePainter.Completed += () => _activePainter = null;
            Color color = Definition ? Definition.AccentColor : new Color(0.2f, 0.9f, 1f);
            _activePainter.Begin(_shape, origin, rot, localVessel, _scale, _reachThreshold, color);
        }
    }
}
