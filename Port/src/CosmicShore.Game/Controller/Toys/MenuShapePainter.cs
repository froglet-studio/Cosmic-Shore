using System;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Engine.Tasks;
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Self-contained "fly by numbers" runner. Reads a <see cref="ShapeDefinition"/>'s waypoints,
    /// draws a ghost outline + a guide line + a lit marker at the next point, and advances as the
    /// vessel flies near each point in order — the vessel's own trail does the painting. Deliberately
    /// minimal: no Cell, no crystal manager, no scoring, no HUD, so it runs anywhere the freestyle
    /// vessel flies (including a menu with no ecology). Toy-faithful — completes when the last point is
    /// reached, then fades out; there is no fail state.
    /// </summary>
    public class MenuShapePainter : MonoBehaviour
    {
        public event Action Completed;

        IVesselStatus _vessel;
        Vector3[] _worldWaypoints;
        int _index;
        float _reachThreshold;
        LineRenderer _ghost;
        LineRenderer _guide;
        GameObject _marker;
        Color _color;
        bool _running;

        public void Begin(ShapeDefinition shape, Vector3 origin, Quaternion rotation, IVesselStatus vessel,
            float scale, float reachThreshold, Color color)
        {
            if (shape == null || vessel == null) { Finish(); return; }

            shape.EnsureWaypoints();
            var local = shape.GetAllWorldWaypoints(Vector3.zero, scale); // origin 0 → pure local*scale
            if (local.Length < 2) { Finish(); return; }

            _worldWaypoints = new Vector3[local.Length];
            for (int i = 0; i < local.Length; i++)
                _worldWaypoints[i] = origin + rotation * local[i];

            _vessel = vessel;
            _reachThreshold = reachThreshold;
            _color = color;
            _index = 0;

            _ghost = MakeLine("Ghost", new Color(color.r, color.g, color.b, 0.18f), 1.0f, transform);
            _ghost.positionCount = _worldWaypoints.Length;
            _ghost.SetPositions(_worldWaypoints);

            _guide = MakeLine("Guide", color, 1.6f, transform);
            _guide.positionCount = 2;

            SpawnMarker(_worldWaypoints[0]);
            _running = true;
        }

        void Update()
        {
            if (!_running) return;
            // Bail if the painted-on vessel was destroyed (e.g. the player swapped ships mid-run).
            if (_vessel == null || (_vessel is CosmicShore.Engine.Object uo && !uo) || _vessel.Vessel == null)
            {
                Finish();
                return;
            }

            Vector3 shipPos = _vessel.Transform.position;
            Vector3 target = _worldWaypoints[_index];

            if (_guide)
            {
                _guide.SetPosition(0, shipPos);
                _guide.SetPosition(1, target);
            }

            if ((shipPos - target).sqrMagnitude <= _reachThreshold * _reachThreshold)
            {
                _index++;
                if (_index >= _worldWaypoints.Length) { Finish(); return; }
                SpawnMarker(_worldWaypoints[_index]);
            }
        }

        void SpawnMarker(Vector3 pos)
        {
            if (_marker) Destroy(_marker);
            _marker = new GameObject("Waypoint");
            _marker.transform.SetParent(transform, false);
            _marker.transform.position = pos;
            ToyFactory.AddSphereBody(_marker.transform, Mathf.Max(4f, _reachThreshold * 0.35f), _color);
        }

        void Finish()
        {
            _running = false;
            if (_marker) Destroy(_marker);
            if (_guide) _guide.enabled = false;
            Completed?.Invoke();
            FadeAndDestroy(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async Task FadeAndDestroy(CancellationToken ct)
        {
            // Leave the finished outline up briefly, then remove the guides (the painted trail stays).
            await GameTask.Delay(2000 / 1000f, unscaledTime: true, cancellationToken: ct);
            if (this) Destroy(gameObject);
        }

        static LineRenderer MakeLine(string name, Color color, float width, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 0;
            lr.startWidth = lr.endWidth = width;
            lr.startColor = lr.endColor = color;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = CosmicShore.Engine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader) lr.material = new Material(shader) { color = color };
            return lr;
        }
    }
}
