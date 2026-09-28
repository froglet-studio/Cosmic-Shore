using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The part of a ribbon that was laid BEFORE a teleport, left standing where it was laid and
    /// drained away from its oldest end at the rate it would have aged out had the vessel stayed.
    /// It ends at the point the vessel left from (a fold gate's near mouth, for a transit), so the
    /// ribbon reads as having gone somewhere rather than stopping in mid-air.
    ///
    /// <para><b>Why the drain is simulated rather than left to the renderer.</b> A
    /// <see cref="TrailRenderer"/> keeps each point's age internally and exposes none of it, so
    /// a copy made with <c>AddPositions</c> stamps every point with the same birth time and the
    /// whole ribbon would stand at full length and then vanish in one frame — a pop, which is the
    /// one thing this exists to prevent. Each point's age is instead estimated from its distance
    /// back along the ribbon at the vessel's speed (exact at constant speed, which is what a
    /// trail at rest length is), and a drained point is collapsed onto the oldest surviving one.
    /// Collapsed points make zero-length segments, which draw nothing, so the point count never
    /// changes and nothing is reallocated per frame.</para>
    ///
    /// <para>It is presentation only: a TrailRenderer is not mass, has no collider and is read by
    /// nothing, so a timed fade here is not the timed culler the mass law forbids.</para>
    /// </summary>
    public sealed class TeleportRibbonGhost : MonoBehaviour
    {
        TrailRenderer _trail;
        Vector3[] _points;
        float[] _age;
        float _life;
        float _elapsed;
        int _drained;

        /// <summary>Hand <paramref name="source"/>'s current ribbon to a ghost ending at
        /// <paramref name="endAt"/>. The source is left untouched.</summary>
        public static void Spawn(TrailRenderer source, Vector3 endAt, float speed)
        {
            int n = source.positionCount;
            if (n <= 0) return;

            var points = new Vector3[n + 1];
            source.GetPositions(points);
            points[n] = endAt;                 // index 0 is the OLDEST point, the last the newest

            float life = Mathf.Max(0.01f, source.time);

            // Arc length back from the head, then age = distance / speed. A vessel that is barely
            // moving would give every point an enormous age; the ribbon's own mean speed (its
            // length over its life) is the floor, since a trail can be no older than its time.
            var age = new float[n + 1];
            float arc = 0f;
            for (int i = n - 1; i >= 0; i--)
                arc += Vector3.Distance(points[i], points[i + 1]);
            float effectiveSpeed = Mathf.Max(speed, arc / life, 0.01f);
            float back = 0f;
            age[n] = 0f;
            for (int i = n - 1; i >= 0; i--)
            {
                back += Vector3.Distance(points[i], points[i + 1]);
                age[i] = Mathf.Min(life, back / effectiveSpeed);
            }

            var go = new GameObject($"{source.name} (ribbon ghost)");
            var ghost = go.AddComponent<TeleportRibbonGhost>();
            ghost._trail = CopyRenderer(source, go, life);
            ghost._points = points;
            ghost._age = age;
            ghost._life = life;
            ghost._trail.AddPositions(points);
        }

        static TrailRenderer CopyRenderer(TrailRenderer source, GameObject host, float life)
        {
            var t = host.AddComponent<TrailRenderer>();
            t.emitting = false;
            t.autodestruct = false;
            // The ghost ages its points itself; the renderer's own clock is set past the ghost's
            // life so it never removes a point first.
            t.time = life + 1f;
            t.sharedMaterials = source.sharedMaterials;
            t.colorGradient = source.colorGradient;
            t.widthCurve = source.widthCurve;
            t.widthMultiplier = source.widthMultiplier;
            t.minVertexDistance = source.minVertexDistance;
            t.textureMode = source.textureMode;
            t.alignment = source.alignment;
            t.numCornerVertices = source.numCornerVertices;
            t.numCapVertices = source.numCapVertices;
            t.shadowCastingMode = source.shadowCastingMode;
            t.receiveShadows = source.receiveShadows;
            t.generateLightingData = source.generateLightingData;
            t.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            t.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            t.sortingLayerID = source.sortingLayerID;
            t.sortingOrder = source.sortingOrder;
            t.forceRenderingOff = source.forceRenderingOff;

            // Anything painted per renderer (a domain tint, an energy state) travels too, so the
            // ghost is the ribbon the player was looking at rather than its prefab default.
            var block = new MaterialPropertyBlock();
            source.GetPropertyBlock(block);
            if (!block.isEmpty) t.SetPropertyBlock(block);
            return t;
        }

        void Update()
        {
            if (!_trail) { Destroy(gameObject); return; }
            _elapsed += Time.deltaTime;

            // Advance the drained prefix. Ages decrease toward the head, so the points that have
            // outlived the ribbon's life are always a prefix of the array.
            int n = _points.Length;
            int drained = _drained;
            while (drained < n && _age[drained] + _elapsed >= _life) drained++;

            if (drained >= n - 1)
            {
                Destroy(gameObject);
                return;
            }

            if (drained == _drained) return;
            _drained = drained;
            Vector3 anchor = _points[drained];
            for (int i = 0; i < drained; i++) _trail.SetPosition(i, anchor);
        }
    }
}
